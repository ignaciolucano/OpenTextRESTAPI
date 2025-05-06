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
    /// <summary>
    /// Service for managing Business Workspaces in OpenText Content Server.
    /// Handles creation, update, and relationship management for workspaces.
    /// </summary>
    public class BusinessWorkspaceService
    {
        private readonly HttpClient _httpClient;
        private readonly OpenTextSettings _settings;
        private readonly string _ticket;
        private readonly MasterData _masterData;
        private readonly CSUtilities _csUtilities;
        private readonly Node _csNode;
        private readonly ILogService _logger;

        /// <summary>
        /// Initializes a new instance of the BusinessWorkspaceService with required dependencies.
        /// </summary>
        /// <param name="httpClient">HTTP client for API calls</param>
        /// <param name="ticket">Authentication ticket (OTCSTICKET)</param>
        /// <param name="masterData">Service for master data operations</param>
        /// <param name="settings">Configuration settings for OpenText API</param>
        /// <param name="csUtilities">Utility methods for Content Server operations</param>
        /// <param name="csNode">Service for node operations in Content Server</param>
        /// <param name="logger">Service for logging operations and errors</param>
        public BusinessWorkspaceService(HttpClient httpClient, string ticket, MasterData masterData, OpenTextSettings settings, CSUtilities csUtilities, Node csNode, ILogService logger)
        {
            _httpClient = httpClient;
            _ticket = ticket;
            _masterData = masterData;
            _settings = settings;
            _csUtilities = csUtilities;
            _csNode = csNode;
            _logger = logger;

            // Log service initialization
            _logger.Log("BusinessWorkspaceService initialized", LogLevel.DEBUG);
        }

        /// <summary>
        /// Adds the OTCSTICKET authentication header to HTTP requests.
        /// </summary>
        /// <param name="req">The HTTP request message to modify</param>
        private void AddTicketHeader(HttpRequestMessage req)
        {
            _logger.Log("Adding authentication ticket to request header", LogLevel.TRACE);
            req.Headers.Remove("OTCSTICKET");
            req.Headers.Add("OTCSTICKET", _ticket);
        }

        /// <summary>
        /// Creates a new Business Workspace using the OpenText API.
        /// </summary>
        /// <param name="boType">Business Object Type</param>
        /// <param name="boId">Business Object ID</param>
        /// <returns>Response containing the created workspace details</returns>
        /// <exception cref="Exception">Thrown when workspace creation fails</exception>
        public async Task<BusinessWorkspaceResponse?> CreateBusinessWorkspaceAsync(string boType, string boId)
        {
            _logger.Log($"Starting CreateBusinessWorkspaceAsync for BO: {boType}/{boId}", LogLevel.INFO);

            var baseUrl = _settings.BaseUrl;
            var extSystemId = _settings.ExtSystemId;
            var url = $"{baseUrl}/api/v2/businessworkspaces/";

            _logger.Log($"Workspace creation URL: {url}", LogLevel.DEBUG);

            // Prepare workspace creation body
            var workspaceCreationBody = new
            {
                parent_id = "1223697",
                template_id = 1226272,
                wksp_type_id = 1782920,
                name = "Prueba BW",
                bo_type = boType,
                bo_id = boId,
                ext_system_id = extSystemId
            };

            string jsonBody = JsonSerializer.Serialize(workspaceCreationBody);
            _logger.Log("Created workspace creation JSON body", LogLevel.DEBUG);

            // Log request details
            _logger.LogRawApi("api_request_create_workspace", jsonBody);

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "body", jsonBody }
            });

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };

            AddTicketHeader(request);
            _logger.Log($"Creating Business Workspace via: {url}", LogLevel.DEBUG);

            // Send request
            HttpResponseMessage response;
            try
            {
                _logger.Log("Sending workspace creation request", LogLevel.DEBUG);
                response = await _httpClient.SendAsync(request);

                // Check for successful response
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    _logger.Log($"Business Workspace creation failed: {response.StatusCode} - {err}", LogLevel.ERROR);

                    // Log error response
                    _logger.LogRawApi("api_response_create_workspace_error", err);

                    throw new Exception($"Business Workspace creation failed with status {response.StatusCode}: {err}");
                }
            }
            catch (Exception ex) when (!(ex is Exception))
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error sending workspace creation request: {ex.Message}", LogLevel.ERROR);
                throw;
            }

            // Process response
            var responseJson = await response.Content.ReadAsStringAsync();

            // Log response (limited to reasonable size)
            _logger.LogRawApi("api_response_create_workspace",
                responseJson.Length > 1000 ? responseJson.Substring(0, 1000) + "..." : responseJson);

            try
            {
                _logger.Log("Deserializing workspace creation response", LogLevel.DEBUG);
                var wsResponse = JsonSerializer.Deserialize<BusinessWorkspaceResponse>(
                    responseJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                _logger.Log($"Successfully created Business Workspace for {boType}/{boId}", LogLevel.INFO);
                return wsResponse;
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Failed to deserialize BusinessWorkspaceResponse: {ex.Message}", LogLevel.ERROR);
                throw;
            }
        }

        /// <summary>
        /// Gets a unique name ID from OpenText Content Server.
        /// </summary>
        /// <param name="ticket">Authentication ticket</param>
        /// <param name="uName">Unique name to retrieve</param>
        /// <returns>Unique name ID as string</returns>
        /// <exception cref="Exception">Thrown when unique name retrieval fails</exception>
        private async Task<string> GetUniqueName(string ticket, string uName)
        {
            _logger.Log($"Getting unique name ID for: {uName}", LogLevel.DEBUG);

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

            _logger.Log($"Unique name request URL: {wsEDUrl}", LogLevel.DEBUG);

            // Log request details
            _logger.LogRawApi("api_request_get_unique_name",
                JsonSerializer.Serialize(new { unique_name = uName, url = wsEDUrl }));

            HttpResponseMessage wsEDResponse;
            try
            {
                _logger.Log("Sending unique name request", LogLevel.TRACE);
                wsEDResponse = await _httpClient.SendAsync(wsEDRequest);

                // Check for successful response
                if (!wsEDResponse.IsSuccessStatusCode)
                {
                    var err = await wsEDResponse.Content.ReadAsStringAsync();
                    _logger.Log($"Unique name search failed: {wsEDResponse.StatusCode} - {err}", LogLevel.ERROR);

                    // Log error response
                    _logger.LogRawApi("api_response_get_unique_name_error", err);

                    throw new Exception($"Business Workspace search failed with status {wsEDResponse.StatusCode}: {err}");
                }
            }
            catch (Exception ex) when (!(ex is Exception))
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error retrieving unique name: {ex.Message}", LogLevel.ERROR);
                throw;
            }

            // Process response
            var wsEDJson = await wsEDResponse.Content.ReadAsStringAsync();

            // Log response
            _logger.LogRawApi("api_response_get_unique_name", wsEDJson);

            string? UniqueNameId = ExtractUniqueNameId(wsEDJson);

            if (string.IsNullOrEmpty(UniqueNameId))
            {
                _logger.Log($"Could not extract unique name ID for {uName}", LogLevel.WARNING);
            }
            else
            {
                _logger.Log($"Extracted unique name ID for {uName}: {UniqueNameId}", LogLevel.DEBUG);
            }

            return UniqueNameId;
        }

        /// <summary>
        /// Extracts the unique name ID from a JSON response.
        /// </summary>
        /// <param name="json">JSON response containing unique name information</param>
        /// <returns>Unique name ID as string or null if not found</returns>
        private string? ExtractUniqueNameId(string json)
        {
            _logger.Log("Extracting unique name ID from response", LogLevel.TRACE);

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
                            string nodeId = idElement.GetRawText().Trim('\"');
                            _logger.Log($"Found unique name NodeId: {nodeId}", LogLevel.TRACE);
                            return nodeId;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Exception in ExtractUniqueNameId: {ex.Message}", LogLevel.ERROR);
            }

            _logger.Log("Could not find unique name ID in response", LogLevel.WARNING);
            return null;
        }

        /// <summary>
        /// Creates a new Change Request Business Workspace using the OpenText API.
        /// </summary>
        /// <param name="boType">Business Object Type</param>
        /// <param name="boId">Business Object ID</param>
        /// <param name="ticket">Authentication ticket</param>
        /// <param name="updateRequest">Request containing CR data</param>
        /// <returns>Response containing the created workspace details</returns>
        /// <exception cref="Exception">Thrown when workspace creation fails</exception>
        public async Task<string?> CreateBusinessWorkspaceCRAsync(string boType, string boId, string ticket, DTOs.ChangeRequestUpdateRequest updateRequest)
        {
            _logger.Log($"Starting CreateBusinessWorkspaceCRAsync for BO: {boType}/{boId}", LogLevel.INFO);

            var baseUrl = _settings.BaseUrl;
            var extSystemId = _settings.ExtSystemId;
            var url = $"{baseUrl}/api/v2/businessworkspaces/";

            _logger.Log($"CR Workspace creation URL: {url}", LogLevel.DEBUG);

            var uNameParentId = "";
            var uNameTemplateId = "";
            var uNameWorkspaceTypeId = "";

            // Get category unique names
            _logger.Log("Getting category unique names", LogLevel.DEBUG);
            var crCategory = await GetUniqueName(ticket, "SMDG_CR_CATEGORY");
            var crBOCategory = await GetUniqueName(ticket, "SMDG_CR_BO_CATEGORY");

            _logger.Log($"CR Category ID: {crCategory}, CR BO Category ID: {crBOCategory}", LogLevel.DEBUG);

            // Format Business Object Number based on type
            _logger.Log($"Formatting Business Object ID for type: {boType}", LogLevel.DEBUG);
            if (boType.Equals("BUS1001006", StringComparison.OrdinalIgnoreCase) ||
                boType.Equals("BUS1001001", StringComparison.OrdinalIgnoreCase))
            {
                boId = boId.PadLeft(18, '0');

                if (boType.Equals("BUS1001006", StringComparison.OrdinalIgnoreCase))
                {
                    uNameParentId = _settings.uNamePIBUS1001006;
                    uNameTemplateId = _settings.uNameTIBUS1001006;
                    uNameWorkspaceTypeId = _settings.uNameWTIBUS1001006;
                }
                else
                {
                    uNameParentId = _settings.uNamePIBUS1001001;
                    uNameTemplateId = _settings.uNameTIBUS1001001;
                    uNameWorkspaceTypeId = _settings.uNameWTIBUS1001001;
                }

                _logger.Log($"Formatted boId for {boType}: {boId}", LogLevel.DEBUG);
            }
            else if (boType.Equals("BUS1006", StringComparison.OrdinalIgnoreCase))
            {
                boId = boId.PadLeft(10, '0');
                uNameParentId = _settings.uNamePIBUS1006;
                uNameTemplateId = _settings.uNameTIBUS1006;
                uNameWorkspaceTypeId = _settings.uNameWTIBUS1006;

                _logger.Log($"Formatted boId for {boType}: {boId}", LogLevel.DEBUG);
            }
            else if (boType.Equals("BUS2250", StringComparison.OrdinalIgnoreCase))
            {
                boId = boId.PadLeft(12, '0');
                uNameParentId = _settings.uNamePIBUS2250;
                uNameTemplateId = _settings.uNameTIBUS2250;
                uNameWorkspaceTypeId = _settings.uNameWTIBUS2250;

                _logger.Log($"Formatted boId for {boType}: {boId}", LogLevel.DEBUG);
            }

            // Get template and workspace type IDs from unique names
            _logger.Log("Getting unique names for parent, template and workspace type", LogLevel.DEBUG);
            var cParentId = await GetUniqueName(ticket, uNameParentId);
            var cTemplateId = await GetUniqueName(ticket, uNameTemplateId);
            var cWorkspaceTypeId = await GetUniqueName(ticket, uNameWorkspaceTypeId);

            _logger.Log($"Parent ID: {cParentId}, Template ID: {cTemplateId}, Workspace Type ID: {cWorkspaceTypeId}", LogLevel.DEBUG);

            // Current date and time for creation date
            string BOcreationDate = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            _logger.Log($"Creation date: {BOcreationDate}", LogLevel.DEBUG);

            // Validate date formats
            _logger.Log("Validating date formats in request", LogLevel.DEBUG);

            // Checks Create At Date
            if (!DateTime.TryParseExact(updateRequest.CreatedAt,
                                        "yyyy-MM-ddTHH:mm:ss",
                                        CultureInfo.InvariantCulture,
                                        DateTimeStyles.None,
                                        out DateTime parsedCreatedDate))
            {
                _logger.Log($"Invalid CreatedAt date format: {updateRequest.CreatedAt}", LogLevel.WARNING);
                updateRequest.CreatedAt = string.Empty;
            }

            // Checks Modified At Date
            if (!DateTime.TryParseExact(updateRequest.ModifiedAt,
                                        "yyyy-MM-ddTHH:mm:ss",
                                        CultureInfo.InvariantCulture,
                                        DateTimeStyles.None,
                                        out DateTime parsedModifiedDate))
            {
                _logger.Log($"Invalid ModifiedAt date format: {updateRequest.ModifiedAt}", LogLevel.WARNING);
                updateRequest.ModifiedAt = string.Empty;
            }

            // Checks End time Date
            if (!DateTime.TryParseExact(updateRequest.EndTime,
                                        "yyyy-MM-ddTHH:mm:ss",
                                        CultureInfo.InvariantCulture,
                                        DateTimeStyles.None,
                                        out DateTime parsedEndTimeDate))
            {
                _logger.Log($"Invalid EndTime date format: {updateRequest.EndTime}", LogLevel.WARNING);
                updateRequest.EndTime = string.Empty;
            }

            // Forms the Creation JSON with category data
            _logger.Log("Creating workspace creation JSON with categories", LogLevel.DEBUG);
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

            // Log request details (limited to reasonable size due to potentially large JSON)
            _logger.LogRawApi("api_request_create_cr_workspace",
                jsonBody.Length > 1000 ? jsonBody.Substring(0, 1000) + "..." : jsonBody);

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "body", jsonBody }
            });

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };

            AddTicketHeader(request);
            _logger.Log($"Sending request to create CR Business Workspace", LogLevel.DEBUG);

            // Send request
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request);

                // Check for successful response
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    _logger.Log($"CR Business Workspace creation failed: {response.StatusCode} - {err}", LogLevel.ERROR);

                    // Log error response
                    _logger.LogRawApi("api_response_create_cr_workspace_error", err);

                    throw new Exception($"Business Workspace creation failed with status {response.StatusCode}: {err}");
                }
            }
            catch (Exception ex) when (!(ex is Exception))
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error sending CR workspace creation request: {ex.Message}", LogLevel.ERROR);
                throw;
            }

            // Process response
            var responseJson = await response.Content.ReadAsStringAsync();

            // Log response (limited to reasonable size)
            _logger.LogRawApi("api_response_create_cr_workspace",
                responseJson.Length > 1000 ? responseJson.Substring(0, 1000) + "..." : responseJson);

            try
            {
                _logger.Log("Parsing workspace creation response", LogLevel.DEBUG);
                var newBoId = ParseBusinessWorkspaceId(responseJson);
                _logger.Log($"Created workspace with ID: {newBoId}", LogLevel.INFO);

                // Create helper service for relationship management
                var workspaceRelService = new BusinessWorkspaceService(_httpClient, ticket, _masterData, _settings, _csUtilities, _csNode, _logger);

                // If the BO has a reference BO we create the relationship
                if (!string.IsNullOrEmpty(updateRequest.MainBOId))
                {
                    try
                    {
                        _logger.Log($"Creating relationship with main BO: {updateRequest.MainBOType}/{updateRequest.MainBOId}", LogLevel.DEBUG);

                        // Create another service instance
                        var workspaceService = new BusinessWorkspaceService(_httpClient, ticket, _masterData, _settings, _csUtilities, _csNode, _logger);

                        // Search the Main Business Workspace
                        var wsMBOResponse = await _masterData.SearchBusinessWorkspaceAsync(updateRequest.MainBOType, updateRequest.MainBOId, ticket);
                        string? workspaceNodeId = null;

                        if (wsMBOResponse != null && wsMBOResponse.results.Count > 0)
                        {
                            // Update the data on the Category
                            var first = wsMBOResponse.results[0].data.properties;
                            workspaceNodeId = first.id.ToString();

                            _logger.Log($"Found main workspace with ID: {workspaceNodeId}", LogLevel.DEBUG);
                            await CreateBORelationAsync(workspaceNodeId, newBoId, ticket);
                            _logger.Log("Relationship created successfully", LogLevel.INFO);
                        }
                        else
                        {
                            _logger.Log($"Main workspace not found for {updateRequest.MainBOType}/{updateRequest.MainBOId}", LogLevel.WARNING);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogException(ex, LogLevel.ERROR);
                        _logger.Log($"Business Workspace relationship could not be created: {ex.Message}", LogLevel.WARNING);
                    }
                }

                _logger.Log($"Successfully created CR Business Workspace for {boType}/{boId}", LogLevel.INFO);
                return newBoId;
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Failed to process workspace creation response: {ex.Message}", LogLevel.ERROR);
                throw;
            }
        }

        /// <summary>
        /// Updates an existing Change Request Business Workspace with new data.
        /// </summary>
        /// <param name="boType">Business Object Type</param>
        /// <param name="boId">Business Object ID</param>
        /// <param name="workspaceNodeId">Node ID of the workspace to update</param>
        /// <param name="ticket">Authentication ticket</param>
        /// <param name="updateRequest">Request containing update data</param>
        /// <param name="newStatus">Optional new status (default: "SUBMITTED")</param>
        /// <returns>True if update was successful</returns>
        /// <exception cref="Exception">Thrown when update fails</exception>
        public async Task<bool> UpdateBusinessWorkspaceCRAsync(string boType, string boId, string workspaceNodeId, string ticket, DTOs.ChangeRequestUpdateRequest updateRequest, string? newStatus = "SUBMITTED")
        {
            _logger.Log($"Starting UpdateBusinessWorkspaceCRAsync for workspace: {workspaceNodeId}, BO: {boType}/{boId}", LogLevel.INFO);

            var baseUrl = _settings.BaseUrl;

            // Get category unique names
            _logger.Log("Getting category unique names", LogLevel.DEBUG);
            var crCategory = await GetUniqueName(ticket, "SMDG_CR_CATEGORY");
            var crBOCategory = await GetUniqueName(ticket, "SMDG_CR_BO_CATEGORY");

            _logger.Log($"CR Category ID: {crCategory}, CR BO Category ID: {crBOCategory}", LogLevel.DEBUG);

            // Create URLs for category updates
            var urlCat = $"{baseUrl}/api/v2/nodes/{workspaceNodeId}/categories/{crCategory}";
            var urlBOCat = $"{baseUrl}/api/v2/nodes/{workspaceNodeId}/categories/{crBOCategory}";

            _logger.Log($"Category update URLs: {urlCat}, {urlBOCat}", LogLevel.DEBUG);

            // Validate date formats
            _logger.Log("Validating date formats in request", LogLevel.DEBUG);

            // Checks Create At Date
            if (!DateTime.TryParseExact(updateRequest.CreatedAt,
                                        "yyyy-MM-ddTHH:mm:ss",
                                        CultureInfo.InvariantCulture,
                                        DateTimeStyles.None,
                                        out DateTime parsedCreatedDate))
            {
                _logger.Log($"Invalid CreatedAt date format: {updateRequest.CreatedAt}", LogLevel.WARNING);
                updateRequest.CreatedAt = string.Empty;
            }

            // Checks Modified At Date
            if (!DateTime.TryParseExact(updateRequest.ModifiedAt,
                                        "yyyy-MM-ddTHH:mm:ss",
                                        CultureInfo.InvariantCulture,
                                        DateTimeStyles.None,
                                        out DateTime parsedModifiedDate))
            {
                _logger.Log($"Invalid ModifiedAt date format: {updateRequest.ModifiedAt}", LogLevel.WARNING);
                updateRequest.ModifiedAt = string.Empty;
            }

            // Checks End time Date
            if (!DateTime.TryParseExact(updateRequest.EndTime,
                                        "yyyy-MM-ddTHH:mm:ss",
                                        CultureInfo.InvariantCulture,
                                        DateTimeStyles.None,
                                        out DateTime parsedEndTimeDate))
            {
                _logger.Log($"Invalid EndTime date format: {updateRequest.EndTime}", LogLevel.WARNING);
                updateRequest.EndTime = string.Empty;
            }

            // Prepare category data for BO
            _logger.Log("Creating category update JSON", LogLevel.DEBUG);
            var crBOCategoryBody = new Dictionary<string, string> // Internal BO Category
            {
                { $"{crBOCategory}_2", updateRequest.MainBOId },
                { $"{crBOCategory}_3", updateRequest.MainBOType },
                { $"{crBOCategory}_5", newStatus}  // updateRequest.Status
            };

            // Prepare category data for CR
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

            // Log request details
            _logger.LogRawApi("api_request_update_cr_category", jsonCatBody);
            _logger.LogRawApi("api_request_update_cr_bo_category", jsonBOCatBody);

            // Prepare content and requests for both categories
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

            // Update CR category
            _logger.Log("Updating CR category", LogLevel.DEBUG);
            HttpResponseMessage responseCat;
            try
            {
                responseCat = await _httpClient.SendAsync(requestCat);

                // Check for successful response
                if (!responseCat.IsSuccessStatusCode)
                {
                    var err = await responseCat.Content.ReadAsStringAsync();
                    _logger.Log($"CR category update failed: {responseCat.StatusCode} - {err}", LogLevel.ERROR);

                    // Log error response
                    _logger.LogRawApi("api_response_update_cr_category_error", err);

                    throw new Exception($"Business Workspace update failed with status {responseCat.StatusCode}: {err}");
                }

                string responseCatJson = await responseCat.Content.ReadAsStringAsync();
                _logger.LogRawApi("api_response_update_cr_category", responseCatJson);
            }
            catch (Exception ex) when (!(ex is Exception))
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error updating CR category: {ex.Message}", LogLevel.ERROR);
                throw;
            }

            // Update BO category
            _logger.Log("Updating BO category", LogLevel.DEBUG);
            HttpResponseMessage responseBOCat;
            try
            {
                responseBOCat = await _httpClient.SendAsync(requestBOCat);

                // Check for successful response
                if (!responseBOCat.IsSuccessStatusCode)
                {
                    var err = await responseBOCat.Content.ReadAsStringAsync();
                    _logger.Log($"BO category update failed: {responseBOCat.StatusCode} - {err}", LogLevel.ERROR);

                    // Log error response
                    _logger.LogRawApi("api_response_update_cr_bo_category_error", err);

                    throw new Exception($"Business Workspace update failed with status {responseBOCat.StatusCode}: {err}");
                }

                string responseBOCatJson = await responseBOCat.Content.ReadAsStringAsync();
                _logger.LogRawApi("api_response_update_cr_bo_category", responseBOCatJson);
            }
            catch (Exception ex) when (!(ex is Exception))
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error updating BO category: {ex.Message}", LogLevel.ERROR);
                throw;
            }

            // Create relationship with main BO workspace if needed
            try
            {
                _logger.Log($"Searching for main BO workspace: {updateRequest.MainBOType}/{updateRequest.MainBOId}", LogLevel.DEBUG);
                var wsMBOResponse = await _masterData.SearchBusinessWorkspaceAsync(updateRequest.MainBOType, updateRequest.MainBOId, ticket);

                if (wsMBOResponse != null && wsMBOResponse.results.Count > 0)
                {
                    // Extract main workspace node ID
                    var first = wsMBOResponse.results[0].data.properties;
                    var workspaceMainNodeId = first.id.ToString();

                    _logger.Log($"Found main workspace with ID: {workspaceMainNodeId}", LogLevel.DEBUG);

                    // Create relationship between workspaces
                    await CreateBORelationAsync(workspaceMainNodeId, workspaceNodeId, ticket);
                    _logger.Log("Relationship created successfully", LogLevel.INFO);
                }
                else
                {
                    _logger.Log($"Main workspace not found for {updateRequest.MainBOType}/{updateRequest.MainBOId}", LogLevel.WARNING);
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.WARNING);
                _logger.Log($"Error creating BO relationship: {ex.Message}", LogLevel.WARNING);
                // Continue with the update even if relationship creation fails
            }

            // Apply RM classification if status is not SUBMITTED
            string rmClassification = "";
            try
            {
                rmClassification = await GetUniqueName(ticket, $"SMDG_RM_{updateRequest.MainBOType}");
                _logger.Log($"RM classification ID retrieved: {rmClassification}", LogLevel.DEBUG);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.WARNING);
                _logger.Log($"Error getting RM classification: {ex.Message}", LogLevel.WARNING);
            }

            if (newStatus != "SUBMITTED" && !string.IsNullOrEmpty(updateRequest.MainBOType))
            {
                _logger.Log("Checking for Records Management classification", LogLevel.DEBUG);

                if (!string.IsNullOrEmpty(rmClassification))
                {
                    _logger.Log($"Applying RM classification: {rmClassification} to workspace {workspaceNodeId}", LogLevel.DEBUG);

                    try
                    {
                        await _csUtilities.ApplyRMClassificationAsync(workspaceNodeId, rmClassification, ticket);
                        _logger.Log("RM classification applied successfully to workspace", LogLevel.INFO);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogException(ex, LogLevel.WARNING);
                        _logger.Log($"Failed to apply RM classification to workspace: {ex.Message}", LogLevel.WARNING);
                        // Continue with the update even if classification fails
                    }
                }
                else
                {
                    _logger.Log($"No RM classification found for {updateRequest.MainBOType}", LogLevel.WARNING);
                }
            }

            // Apply RM classification to all documents in the workspace
            try
            {
                _logger.Log($"Retrieving documents from workspace {workspaceNodeId}", LogLevel.DEBUG);
                List<DocumentInfo> documents = await _csNode.GetNodeSubNodesAsync(workspaceNodeId, ticket, "Master", null);
                _logger.Log($"Found {documents.Count} documents to apply RM classification", LogLevel.DEBUG);

                foreach (DocumentInfo document in documents)
                {
                    if (!string.IsNullOrEmpty(rmClassification))
                    {
                        try
                        {
                            _logger.Log($"Applying RM classification to document: {document.Name} (NodeId: {document.NodeId})", LogLevel.DEBUG);
                            await _csUtilities.ApplyRMClassificationAsync(document.NodeId, rmClassification, ticket);
                            _logger.Log($"RM classification applied successfully to document: {document.Name}", LogLevel.DEBUG);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogException(ex, LogLevel.WARNING);
                            _logger.Log($"Failed to apply RM classification to document {document.Name}: {ex.Message}", LogLevel.WARNING);
                            // Continue with other documents even if one fails
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.WARNING);
                _logger.Log($"Error processing documents for RM classification: {ex.Message}", LogLevel.WARNING);
                // Continue with the update even if document classification fails
            }

            _logger.Log($"Successfully updated CR Business Workspace {workspaceNodeId}", LogLevel.INFO);
            return true;
        }

        /// <summary>
        /// Parses the ID of a newly created Business Workspace from a JSON response.
        /// </summary>
        /// <param name="responseJson">JSON response from workspace creation</param>
        /// <returns>Workspace ID as string</returns>
        public string ParseBusinessWorkspaceId(string responseJson)
        {
            _logger.Log("Parsing Business Workspace ID from response", LogLevel.DEBUG);

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
                            _logger.Log($"Extracted workspace ID: {wsId}", LogLevel.DEBUG);
                            return wsId;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Failed to get Node ID of New Business Workspace: {ex.Message}", LogLevel.ERROR);
            }

            _logger.Log("Could not extract workspace ID, returning default value '0'", LogLevel.WARNING);
            return "0";
        }

        /// <summary>
        /// Creates a relationship between two Business Workspaces.
        /// </summary>
        /// <param name="MainBoId">ID of the main (parent) Business Workspace</param>
        /// <param name="boId">ID of the child Business Workspace</param>
        /// <param name="ticket">Authentication ticket</param>
        /// <returns>Task representing the asynchronous operation</returns>
        /// <exception cref="Exception">Thrown when relationship creation fails</exception>
        private async Task CreateBORelationAsync(string MainBoId, string boId, string ticket)
        {
            _logger.Log($"Creating BO relationship: Main BO ID: {MainBoId}, Child BO ID: {boId}", LogLevel.DEBUG);

            var baseUrl = _settings.BaseUrl;

            void AddTicketHeader(HttpRequestMessage req)
            {
                req.Headers.Remove("OTCSTICKET");
                req.Headers.Add("OTCSTICKET", ticket);
            }

            // Post Relation to main BO
            var wsEDUrl = $"{baseUrl}/api/v2/businessworkspaces/{MainBoId}/relateditems";
            _logger.Log($"Relationship creation URL: {wsEDUrl}", LogLevel.DEBUG);

            // Prepare relationship data
            var formData = new Dictionary<string, string>
            {
                { "rel_bw_id", boId },
                { "rel_type" , "child"}
            };

            // Log request details
            _logger.LogRawApi("api_request_create_bo_relation",
                JsonSerializer.Serialize(formData));

            // Create and configure request
            using var wsEDRequest = new HttpRequestMessage(HttpMethod.Post, wsEDUrl)
            {
                Content = new FormUrlEncodedContent(formData)
            };

            AddTicketHeader(wsEDRequest);

            // Send request
            HttpResponseMessage wsEDResponse;
            try
            {
                _logger.Log("Sending relationship creation request", LogLevel.DEBUG);
                wsEDResponse = await _httpClient.SendAsync(wsEDRequest);

                // Check for successful response
                if (!wsEDResponse.IsSuccessStatusCode)
                {
                    var err = await wsEDResponse.Content.ReadAsStringAsync();
                    _logger.Log($"BO relationship creation failed: {wsEDResponse.StatusCode} - {err}", LogLevel.ERROR);

                    // Log error response
                    _logger.LogRawApi("api_response_create_bo_relation_error", err);

                    throw new Exception($"Business Workspace search failed with status {wsEDResponse.StatusCode}: {err}");
                }

                var wsEDJson = await wsEDResponse.Content.ReadAsStringAsync();
                _logger.LogRawApi("api_response_create_bo_relation", wsEDJson);

                _logger.Log("BO relationship created successfully", LogLevel.INFO);
            }
            catch (Exception ex) when (!(ex is Exception))
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error creating BO relationship: {ex.Message}", LogLevel.ERROR);
                throw;
            }
        }
    }
}