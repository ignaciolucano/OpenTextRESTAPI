using System;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OpenTextIntegrationAPI.ClassObjects;
using OpenTextIntegrationAPI.Models;

namespace OpenTextIntegrationAPI.Services
{
    public class BusinessWorkspaceService
    {
        private readonly HttpClient _httpClient;
        //private readonly IConfiguration _configuration;
        private readonly OpenTextSettings _settings;
        private readonly string _ticket;
        private readonly MasterData _masterData;
        private readonly CSUtilities _csUtilities;
        private readonly Node _csNode;

        // Constructor: se pasa el token de autenticación (ticket)
        public BusinessWorkspaceService(HttpClient httpClient,  string ticket, MasterData masterData, OpenTextSettings settings, CSUtilities csUtilities, Node csNode) //IConfiguration configuration,
        {
            _httpClient = httpClient;
            //_configuration = configuration;
            _ticket = ticket;
            _masterData = masterData;
            _settings = settings;
            _csUtilities = csUtilities;
            _csNode = csNode;
        }

        // Método para agregar el header OTCSTICKET
        private void AddTicketHeader(HttpRequestMessage req)
        {
            req.Headers.Remove("OTCSTICKET");
            req.Headers.Add("OTCSTICKET", _ticket);
        }
        // Método para crear un nuevo Business Workspace usando PUT /api/v2/businessworkspaces
        public async Task<BusinessWorkspaceResponse?> CreateBusinessWorkspaceAsync(string boType, string boId)
        {
            var baseUrl = _settings.BaseUrl;
            var extSystemId = _settings.ExtSystemId; 
            var url = $"{baseUrl}/api/v2/businessworkspaces/";

            var workspaceCreationBody = new
            {
                parent_id = "1223697",
                template_id = 1226272,
                //bo_type_id = "1845893,
                wksp_type_id = 1782920,
                name = "Prueba BW",
                bo_type = boType,
                bo_id = boId,
                ext_system_id = extSystemId
            };
            string jsonBody = JsonSerializer.Serialize(workspaceCreationBody);
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "body", jsonBody }
            });
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };
            AddTicketHeader(request);
            Debug.WriteLine($"[DEBUG] Creating Business Workspace via: {url} with body: {jsonBody}");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[ERROR] Business Workspace creation failed: {err}");
                throw new Exception($"Business Workspace creation failed with status {response.StatusCode}: {err}");
            }
            var responseJson = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"[DEBUG] Business Workspace creation response: {responseJson.Substring(0, Math.Min(300, responseJson.Length))}");
            try
            {
                var wsResponse = JsonSerializer.Deserialize<BusinessWorkspaceResponse>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return wsResponse;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Failed to deserialize BusinessWorkspaceResponse during creation: {ex.Message}");
                throw;
            }
        }
        private async Task<string> GetUniqueName(string ticket, string uName)
        {
            var baseUrl = _settings.BaseUrl;
            
            void AddTicketHeader(HttpRequestMessage req)
            {
                req.Headers.Remove("OTCSTICKET");
                req.Headers.Add("OTCSTICKET", ticket);
            }

            // Get the Unique Name Id
            var wsEDUrl = $"{baseUrl}/api/v2/uniquenames?where_names={uName}";
            var wsEDRequest = new HttpRequestMessage(HttpMethod.Get, wsEDUrl);
            AddTicketHeader(wsEDRequest);
            Debug.WriteLine($"[DEBUG] Searching for Unique Name: {wsEDUrl}");
            var wsEDResponse = await _httpClient.SendAsync(wsEDRequest);
            if (!wsEDResponse.IsSuccessStatusCode)
            {
                var err = await wsEDResponse.Content.ReadAsStringAsync();
                Debug.WriteLine($"[ERROR] Business Workspace search failed search of Unique Names: {err}");
                throw new Exception($"Business Workspace search failed with status {wsEDResponse.StatusCode}: {err}");
            }
            var wsEDJson = await wsEDResponse.Content.ReadAsStringAsync();
            string? UniqueNameId = ExtractUniqueNameId(wsEDJson);

            return UniqueNameId;

        }
        private string? ExtractUniqueNameId(string json)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    // Look for the "results" property
                    if (doc.RootElement.TryGetProperty("results", out JsonElement results) &&
                        results.ValueKind == JsonValueKind.Array &&
                        results.GetArrayLength() > 0)
                    {
                        // Get the first result
                        var firstResult = results[0];

                        // Then into "properties"
                        if (firstResult.TryGetProperty("NodeId", out JsonElement idElement))
                        {
                            return idElement.GetRawText().Trim('\"');
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Exception in ExtractWorkspaceNodeId: {ex.Message}");
            }
            return null;
        }
        public async Task<BusinessWorkspaceResponse?> CreateBusinessWorkspaceCRAsync(string boType, string boId, string ticket, DTOs.ChangeRequestUpdateRequest updateRequest)
        {
            var baseUrl = _settings.BaseUrl;
            var extSystemId = _settings.ExtSystemId;
            var url = $"{baseUrl}/api/v2/businessworkspaces/";
            var uNameParentId = "";
            var uNameTemplateId = "";
            var uNameWorkspaceTypeId = "";
            var crCategory = await GetUniqueName(ticket, "SMDG_CR_CATEGORY");
            var crBOCategory = await GetUniqueName(ticket, "SMDG_CR_BO_CATEGORY");


            // Format Business Object Number
            if (boType.Equals("BUS1001006", StringComparison.OrdinalIgnoreCase) ||
                boType.Equals("BUS1001001", StringComparison.OrdinalIgnoreCase))
            {
                boId = boId.PadLeft(18, '0');

                if (boType.Equals("BUS1001006", StringComparison.OrdinalIgnoreCase))
                {
                    uNameParentId = _settings.uNamePIBUS1001006;// _configuration["OpenText:uNamePIBUS1001006"];
                    uNameTemplateId = _settings.uNameTIBUS1001006; // _configuration["OpenText:uNameTIBUS1001006"];
                    uNameWorkspaceTypeId = _settings.uNameWTIBUS1001006; // _configuration["OpenText:uNameWTIBUS1001006"];
                } else
                {
                    uNameParentId = _settings.uNamePIBUS1001001; // _configuration["OpenText:uNamePIBUS1001001"];
                    uNameTemplateId = _settings.uNameTIBUS1001001; // _configuration["OpenText:uNameTIBUS1001001"];
                    uNameWorkspaceTypeId = _settings.uNameWTIBUS1001001; // _configuration["OpenText:uNameWTIBUS1001001"];
                }
                    
                Debug.WriteLine($"[DEBUG] Formatted boId for {boType}: {boId}");
            }
            else if (boType.Equals("BUS1006", StringComparison.OrdinalIgnoreCase))
            {
                boId = boId.PadLeft(10, '0');
                uNameParentId = _settings.uNamePIBUS1006; // _configuration["OpenText:uNamePIBUS1006"];
                uNameTemplateId = _settings.uNameTIBUS1006; // _configuration["OpenText:uNameTIBUS1006"];
                uNameWorkspaceTypeId = _settings.uNameWTIBUS1006; // _configuration["OpenText:uNameWTIBUS1006"];
                Debug.WriteLine($"[DEBUG] Formatted boId for {boType}: {boId}");
            }
            else if (boType.Equals("BUS2250", StringComparison.OrdinalIgnoreCase))
            {
                boId = boId.PadLeft(12, '0');
                uNameParentId = _settings.uNamePIBUS2250; // _configuration["OpenText:uNamePIBUS2250"];
                uNameTemplateId = _settings.uNameTIBUS2250; // _configuration["OpenText:uNameTIBUS2250"];
                uNameWorkspaceTypeId = _settings.uNameWTIBUS2250; //  _configuration["OpenText:uNameWTIBUS2250"];
            }

            // Get Parent Id
            var cParentId = await GetUniqueName(ticket, uNameParentId);

            // Get Template Id
            var cTemplateId = await GetUniqueName(ticket, uNameTemplateId);

            // Get WSKP ID
            var cWorkspaceTypeId = await GetUniqueName(ticket, uNameWorkspaceTypeId);

            // Converts Current DateTime
            string BOcreationDate = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");

            // Checks Create At Date
            if (!DateTime.TryParseExact(updateRequest.CreatedAt,
                                        "yyyy-MM-ddTHH:mm:ss",
                                        CultureInfo.InvariantCulture,
                                        DateTimeStyles.None,
                                        out DateTime parsedCreatedDate))
            {
                updateRequest.CreatedAt = string.Empty;
            }

            // Checks Modified At Date
            if (!DateTime.TryParseExact(updateRequest.ModifiedAt,
                                        "yyyy-MM-ddTHH:mm:ss",
                                        CultureInfo.InvariantCulture,
                                        DateTimeStyles.None,
                                        out DateTime parsedModifiedDate))
            {
                updateRequest.ModifiedAt = string.Empty;
            }

            // Checks End time Date
            if (!DateTime.TryParseExact(updateRequest.EndTime,
                                        "yyyy-MM-ddTHH:mm:ss",
                                        CultureInfo.InvariantCulture,
                                        DateTimeStyles.None,
                                        out DateTime parsedEndTimeDate))
            {
                updateRequest.EndTime = string.Empty;
            }

            // Forms the Creation JSON
            var workspaceCreationBody = new
            {
                parent_id = cParentId,
                template_id = cTemplateId,
                wksp_type_id = cWorkspaceTypeId,
                name = boId + " - " + updateRequest.ChangeRequestName,

                roles = new 
                { 
                    categories = new Dictionary<string, object>
                    {
                        {
                            $"{crBOCategory}", new Dictionary<string, string> // Internal BO Category
                            {
                                { $"{crBOCategory}_2", updateRequest.MainBOId },
                                { $"{crBOCategory}_3", updateRequest.MainBOType },
                                { $"{crBOCategory}_4", BOcreationDate },
                                { $"{crBOCategory}_5", "SUBMITTED"}  // updateRequest.Status
                            }
                        },
                        {
                            $"{crCategory}", new Dictionary<string, string> // Change Rq Category
                            {
                                { $"{crCategory}_15", boId }, // CR Id
                                { $"{crCategory}_16", updateRequest.ChangeRequestName }, //CR Name
                                { $"{crCategory}_2", updateRequest.Template }, // Template
                                { $"{crCategory}_3", updateRequest.ObjectID }, // ObjectID
                                { $"{crCategory}_4", updateRequest.ERP }, // ERP
                                { $"{crCategory}_5", updateRequest.Status }, // Status
                                { $"{crCategory}_6", updateRequest.CreatedBy }, // Created By
                                { $"{crCategory}_7", updateRequest.CreatedAt }, // Created At
                                { $"{crCategory}_8", updateRequest.ModifiedBy }, // Modified By
                                { $"{crCategory}_9", updateRequest.ModifiedAt }, // Modified At
                                { $"{crCategory}_10", updateRequest.ApprovalVersion }, // Approval Version
                                { $"{crCategory}_11", updateRequest.EndTime }, // End Time
                                { $"{crCategory}_17", updateRequest.RequestType }, // Request Type
                                { $"{crCategory}_18", updateRequest.ObjectType } // Object Type
                            }
                        }
                    }
                }
            };
            string jsonBody = JsonSerializer.Serialize(workspaceCreationBody);
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "body", jsonBody }
            });
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };
            AddTicketHeader(request);
            Debug.WriteLine($"[DEBUG] Creating Business Workspace via: {url} with body: {jsonBody}");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[ERROR] Business Workspace creation failed: {err}");
                throw new Exception($"Business Workspace creation failed with status {response.StatusCode}: {err}");
            }
            var responseJson = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"[DEBUG] Business Workspace creation response: {responseJson.Substring(0, Math.Min(300, responseJson.Length))}");
            try
            {
                var newBoId = ParseBusinessWorkspaceId(responseJson);
                // If the BO has a reference BO we create the relationship

                // Step 2: Instantiate BusinessWorkspaceService class
                var workspaceRelService = new BusinessWorkspaceService(_httpClient, ticket, _masterData,_settings, _csUtilities, _csNode);
                if (!string.IsNullOrEmpty(updateRequest.MainBOId))
                {
                    try
                    {
                        // Step 2: Instantiate BusinessWorkspaceService class
                        var workspaceService = new BusinessWorkspaceService(_httpClient, ticket, _masterData, _settings, _csUtilities, _csNode);

                        // Step 3: Search the Business Workspace.
                        var wsMBOResponse = await _masterData.SearchBusinessWorkspaceAsync(updateRequest.MainBOType, updateRequest.MainBOId, ticket);
                        string? workspaceNodeId = null;

                        if (wsMBOResponse != null && wsMBOResponse.results.Count > 0)
                        {
                            // TODO Update the Data on the Category
                            var first = wsMBOResponse.results[0].data.properties;
                            workspaceNodeId = first.id.ToString();
                            await CreateBORelationAsync(workspaceNodeId, newBoId, ticket);
                        }
                            
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[DEBUG] Business Workspace relationship could not be created");
                    }
                    
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Failed to deserialize BusinessWorkspaceResponse during creation: {ex.Message}");
                throw;
            }
        }
        public async Task<bool> UpdateBusinessWorkspaceCRAsync(string boType, string boId, string workspaceNodeId, string ticket, DTOs.ChangeRequestUpdateRequest updateRequest, string? newStatus = "SUBMITTED")
        {
            var baseUrl = _settings.BaseUrl;
            var crCategory = await GetUniqueName(ticket, "SMDG_CR_CATEGORY");
            var crBOCategory = await GetUniqueName(ticket, "SMDG_CR_BO_CATEGORY");

            var urlCat = $"{baseUrl}/api/v2/nodes/{workspaceNodeId}/categories/{crCategory}";
            var urlBOCat = $"{baseUrl}/api/v2/nodes/{workspaceNodeId}/categories/{crBOCategory}";

            // Checks Create At Date
            if (!DateTime.TryParseExact(updateRequest.CreatedAt,
                                        "yyyy-MM-ddTHH:mm:ss",
                                        CultureInfo.InvariantCulture,
                                        DateTimeStyles.None,
                                        out DateTime parsedCreatedDate))
            {
                updateRequest.CreatedAt = string.Empty;
            }

            // Checks Modified At Date
            if (!DateTime.TryParseExact(updateRequest.ModifiedAt,
                                        "yyyy-MM-ddTHH:mm:ss",
                                        CultureInfo.InvariantCulture,
                                        DateTimeStyles.None,
                                        out DateTime parsedModifiedDate))
            {
                updateRequest.ModifiedAt = string.Empty;
            }

            // Checks End time Date
            if (!DateTime.TryParseExact(updateRequest.EndTime,
                                        "yyyy-MM-ddTHH:mm:ss",
                                        CultureInfo.InvariantCulture,
                                        DateTimeStyles.None,
                                        out DateTime parsedEndTimeDate))
            {
                updateRequest.EndTime = string.Empty;
            }

            // Forms the Creation JSON
            var crBOCategoryBody = new Dictionary<string, string> // Internal BO Category
            {
                { $"{crBOCategory}_2", updateRequest.MainBOId },
                { $"{crBOCategory}_3", updateRequest.MainBOType },
                { $"{crBOCategory}_5", newStatus}  // updateRequest.Status
            };

            var crCategoryBody = new Dictionary<string, string> // Change Rq Category
            {
                { $"{crCategory}_15", boId }, // CR Id
                { $"{crCategory}_16", updateRequest.ChangeRequestName }, //CR Name
                { $"{crCategory}_2", updateRequest.Template }, // Template
                { $"{crCategory}_3", updateRequest.ObjectID }, // ObjectID
                { $"{crCategory}_4", updateRequest.ERP }, // ERP
                { $"{crCategory}_5", updateRequest.Status }, // Status
                { $"{crCategory}_6", updateRequest.CreatedBy }, // Created By
                { $"{crCategory}_7", updateRequest.CreatedAt }, // Created At
                { $"{crCategory}_8", updateRequest.ModifiedBy }, // Modified By
                { $"{crCategory}_9", updateRequest.ModifiedAt }, // Modified At
                { $"{crCategory}_10", updateRequest.ApprovalVersion }, // Approval Version
                { $"{crCategory}_11", updateRequest.EndTime }, // End Time
                { $"{crCategory}_17", updateRequest.RequestType }, // Request Type
                { $"{crCategory}_18", updateRequest.ObjectType } // Object Type
            };
             
            string jsonCatBody = JsonSerializer.Serialize(crCategoryBody);
            string jsonBOCatBody = JsonSerializer.Serialize(crBOCategoryBody);

            var contentCat = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "body", jsonCatBody }
            });
            var contentBOCat = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "body", jsonBOCatBody }
            });

            var requestCat = new HttpRequestMessage(HttpMethod.Put, urlCat)
            {
                Content = contentCat
            };
            var requestBOCat = new HttpRequestMessage(HttpMethod.Put, urlBOCat)
            {
                Content = contentBOCat
            };

            AddTicketHeader(requestCat);
            AddTicketHeader(requestBOCat);

            var responseCat = await _httpClient.SendAsync(requestCat);
            if (!responseCat.IsSuccessStatusCode)
            {
                var err = await responseCat.Content.ReadAsStringAsync();
                throw new Exception($"Business Workspace update failed with status {responseCat.StatusCode}: {err}");
            }

            var responseBOCat = await _httpClient.SendAsync(requestBOCat);

            if (!responseBOCat.IsSuccessStatusCode)
            {
                var err = await responseBOCat.Content.ReadAsStringAsync();
                throw new Exception($"Business Workspace update failed with status {responseBOCat.StatusCode}: {err}");
            }

            try
            {
                var wsMBOResponse = await _masterData.SearchBusinessWorkspaceAsync(updateRequest.MainBOType, updateRequest.MainBOId, ticket);
                if (wsMBOResponse != null && wsMBOResponse.results.Count > 0)
                {
                    // TODO Update the Data on the Category
                    var first = wsMBOResponse.results[0].data.properties;
                    var workspaceMainNodeId = first.id.ToString();
                    //await CreateBORelationAsync(workspaceNodeId, newBoId, ticket);
                    await CreateBORelationAsync(workspaceMainNodeId, workspaceNodeId, ticket);
                }

               
            }
            catch (Exception ex)
            {
               
            }

            string rmClassification = "";
            rmClassification = GetUniqueName(ticket, $"SMDG_RM_{updateRequest.MainBOType}").Result;

            if (newStatus != "SUMITTED" && updateRequest.MainBOType != "")
            {
                if (rmClassification != "") { 
                    try
                    {
                        await _csUtilities.ApplyRMClassificationAsync(workspaceNodeId, rmClassification, ticket);
                    }
                    catch (Exception ex)
                    {
                        //throw new Exception($"Business Workspace rejected but Records Management Classification not added: {ex.Message}");
                    }
                }
            }
            try
            {
                List<DocumentInfo> documents = await _csNode.GetNodeSubNodesAsync(workspaceNodeId, ticket, "Master", null);

                foreach (DocumentInfo document in documents)
                {
                    // Procesa cada documento según necesites.
                    await _csUtilities.ApplyRMClassificationAsync(document.NodeId, rmClassification, ticket);
                    //Console.WriteLine($"Documento: {document.Name}, ID: {document.Id}");
                    // O cualquier otra acción con "document"
                }
            } catch (Exception ex)
            {

            }

            return true;
        }
        public string ParseBusinessWorkspaceId(string responseJson)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(responseJson))
                { 
                    if (doc.RootElement.TryGetProperty("results", out JsonElement resultElement))
                    {
                        if (resultElement.TryGetProperty("id", out JsonElement idElem))
                        {
                            int wsIdint = idElem.GetInt32();
                            string wsId = wsIdint.ToString();
                            return wsId;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Failed to get Node Id of New Business Workspace: {ex.Message}");
            }
            return "0";
        }
        private async Task CreateBORelationAsync(string MainBoId, string boId, string ticket)
        {
            var baseUrl = _settings.BaseUrl;

            void AddTicketHeader(HttpRequestMessage req)
            {
                req.Headers.Remove("OTCSTICKET");
                req.Headers.Add("OTCSTICKET", ticket);
            }

            // Post Relation to main BO
            var wsEDUrl = $"{baseUrl}/api/v2/businessworkspaces/{MainBoId}/relateditems";

            //var wsEDRequest = new HttpRequestMessage(HttpMethod.Post, wsEDUrl);

            var formData = new Dictionary<string, string>
            {
                { "rel_bw_id", boId },
                { "rel_type" , "child"}
            };

            using var wsEDRequest = new HttpRequestMessage(HttpMethod.Post, wsEDUrl)
            {
                Content = new FormUrlEncodedContent(formData)
            };

            AddTicketHeader(wsEDRequest);

            Debug.WriteLine($"[DEBUG] Post the Relation: {wsEDUrl}");
            var wsEDResponse = await _httpClient.SendAsync(wsEDRequest);

            if (!wsEDResponse.IsSuccessStatusCode)
            {
                var err = await wsEDResponse.Content.ReadAsStringAsync();
                Debug.WriteLine($"[ERROR] Business Workspace search failed search of Unique Names: {err}");
                throw new Exception($"Business Workspace search failed with status {wsEDResponse.StatusCode}: {err}");
            }
            var wsEDJson = await wsEDResponse.Content.ReadAsStringAsync();
            
        }
    }
}
