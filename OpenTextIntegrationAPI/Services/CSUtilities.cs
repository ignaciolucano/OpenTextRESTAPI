using System.Net.Http.Headers;
using System.Text.Json;
using System.Diagnostics;
using OpenTextIntegrationAPI.Models;
using OpenTextIntegrationAPI.DTOs;
using OpenTextIntegrationAPI.Controllers;
using Microsoft.Extensions.Options;

namespace OpenTextIntegrationAPI.Services
{
    public class CSUtilities
    {
        private readonly HttpClient _httpClient;
        private readonly OpenTextSettings _settings;

        public CSUtilities(HttpClient httpClient, IOptions<OpenTextSettings> settings)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
        }
        public async Task<string> GetClassifications(string nodeId, string ticket)
        {
            var baseUrl = _settings.BaseUrl;
            void AddTicketHeader(HttpRequestMessage req)
            {
                req.Headers.Remove("OTCSTICKET");
                req.Headers.Add("OTCSTICKET", ticket);
            }

            ///v1/nodes/{id}/classifications
            var wsChildNodesUrl = $"{baseUrl}/api/v1/nodes/{nodeId}/classifications";
            var wsChildNodesRequest = new HttpRequestMessage(HttpMethod.Get, wsChildNodesUrl);
            AddTicketHeader(wsChildNodesRequest);
            Debug.WriteLine($"[DEBUG] Searching for Business Workspace: {wsChildNodesUrl}");
            var wsChildNodesResponse = await _httpClient.SendAsync(wsChildNodesRequest);
            if (!wsChildNodesResponse.IsSuccessStatusCode)
            {
                return null;
            }
            var wsChildNodesJson = await wsChildNodesResponse.Content.ReadAsStringAsync();

            try
            {
                using (JsonDocument doc = JsonDocument.Parse(wsChildNodesJson))
                {
                    // Look for the "results" property
                    if (doc.RootElement.TryGetProperty("data", out JsonElement results) &&
                        results.ValueKind == JsonValueKind.Array &&
                        results.GetArrayLength() > 0)
                    {
                        // Get the first result
                        var firstResult = results[0];

                        // Then into "properties"
                        if (firstResult.TryGetProperty("name", out JsonElement idElement))
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
        public string? GetDocTypeName(string key)
        {
            {
                if (_settings.DocumentTypeMapping.TryGetValue(key, out var value))
                {
                    return value;
                }

                throw new Exception($"Document type '{key}' not found in mapping.");
            }
        }
        public async Task<NodeResponseClassifications> GetNodeClassifications(int nodeId, string ticket)
        {
            var baseUrl = _settings.BaseUrl;
            void AddTicketHeader(HttpRequestMessage req)
            {
                req.Headers.Remove("OTCSTICKET");
                req.Headers.Add("OTCSTICKET", ticket);
            }

            ///v1/nodes/{id}/classifications
            var wsChildNodesUrl = $"{baseUrl}/api/v1/nodes/{nodeId}/classifications";
            var wsChildNodesRequest = new HttpRequestMessage(HttpMethod.Get, wsChildNodesUrl);
            AddTicketHeader(wsChildNodesRequest);
            Debug.WriteLine($"[DEBUG] Searching for Business Workspace: {wsChildNodesUrl}");
            var wsChildNodesResponse = await _httpClient.SendAsync(wsChildNodesRequest);
            if (!wsChildNodesResponse.IsSuccessStatusCode)
            {
                return null;
            }
            var wsChildNodesJson = await wsChildNodesResponse.Content.ReadAsStringAsync();

            try
            {
                var nodeResponse = JsonSerializer.Deserialize<NodeResponseClassifications>(wsChildNodesJson);
                return nodeResponse;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Exception in ExtractWorkspaceNodeId: {ex.Message}");
            }

            return null;
        }
        public async Task<NodeResponseClassifications> GetNodeData(int nodeId, string ticket)
        {
            var baseUrl = _settings.BaseUrl;
            void AddTicketHeader(HttpRequestMessage req)
            {
                req.Headers.Remove("OTCSTICKET");
                req.Headers.Add("OTCSTICKET", ticket);
            }

            ///v1/nodes/{id}/classifications
            var wsChildNodesUrl = $"{baseUrl}/api/v1/nodes/{nodeId}/classifications";
            var wsChildNodesRequest = new HttpRequestMessage(HttpMethod.Get, wsChildNodesUrl);
            AddTicketHeader(wsChildNodesRequest);
            Debug.WriteLine($"[DEBUG] Searching for Business Workspace: {wsChildNodesUrl}");
            var wsChildNodesResponse = await _httpClient.SendAsync(wsChildNodesRequest);
            if (!wsChildNodesResponse.IsSuccessStatusCode)
            {
                return null;
            }
            var wsChildNodesJson = await wsChildNodesResponse.Content.ReadAsStringAsync();

            try
            {
                var nodeResponse = JsonSerializer.Deserialize<NodeResponseClassifications>(wsChildNodesJson);
                return nodeResponse;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Exception in ExtractWorkspaceNodeId: {ex.Message}");
            }

            return null;
        }
        public async Task<DocumentTypeResponse> GetTemplateDocTypes(string nodeId, string ticket)
        {
            var baseUrl = _settings.BaseUrl;
            void AddTicketHeader(HttpRequestMessage req)
            {
                req.Headers.Remove("OTCSTICKET");
                req.Headers.Add("OTCSTICKET", ticket);
            }

            ///v1/nodes/{id}/classifications
            var wsChildNodesUrl = $"{baseUrl}/api/v2/businessworkspaces/{nodeId}/doctypes?document_generation_only=false";
            var wsChildNodesRequest = new HttpRequestMessage(HttpMethod.Get, wsChildNodesUrl);
            AddTicketHeader(wsChildNodesRequest);
            Debug.WriteLine($"[DEBUG] Searching for Business Workspace: {wsChildNodesUrl}");
            var wsChildNodesResponse = await _httpClient.SendAsync(wsChildNodesRequest);
            if (!wsChildNodesResponse.IsSuccessStatusCode)
            {
                return null;
            }
            var wsChildNodesJson = await wsChildNodesResponse.Content.ReadAsStringAsync();

            try
            {
                var nodeResponse = JsonSerializer.Deserialize<DocumentTypeResponse>(wsChildNodesJson);
                return nodeResponse;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Exception in ExtractWorkspaceNodeId: {ex.Message}");
            }

            return null;
        }
        public async Task<string> GetDocTypes(int nodeId, string ticket)
        {
            var baseUrl = _settings.BaseUrl;
            void AddTicketHeader(HttpRequestMessage req)
            {
                req.Headers.Remove("OTCSTICKET");
                req.Headers.Add("OTCSTICKET", ticket);
            }

            ///v1/nodes/{id}/classifications
            var wsChildNodesUrl = $"{baseUrl}/api/v1/nodes/{nodeId}/classifications";
            var wsChildNodesRequest = new HttpRequestMessage(HttpMethod.Get, wsChildNodesUrl);
            AddTicketHeader(wsChildNodesRequest);
            Debug.WriteLine($"[DEBUG] Searching for Business Workspace: {wsChildNodesUrl}");
            var wsChildNodesResponse = await _httpClient.SendAsync(wsChildNodesRequest);
            if (!wsChildNodesResponse.IsSuccessStatusCode)
            {
                return null;
            }
            var wsChildNodesJson = await wsChildNodesResponse.Content.ReadAsStringAsync();

            try
            {
                var nodeResponse = JsonSerializer.Deserialize<NodeResponseClassifications>(wsChildNodesJson);
                //return nodeResponse;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Exception in ExtractWorkspaceNodeId: {ex.Message}");
            }

            return null;
        }
        /// <summary>
        /// Applies a classification to a node by calling the OpenText API.
        /// </summary>
        /// <param name="nodeId">The ID of the node to classify.</param>
        /// <param name="catId">The classification category ID.</param>
        /// <param name="ticket">The authentication ticket.</param>
        /// <returns>A Task that represents the asynchronous operation. The Task result contains a boolean indicating success.</returns>
        public async Task<bool> ApplyClassificationAsync(string nodeId, string catId, string ticket)
        {
            // Retrieve the base URL from configuration.
            var baseUrl = _settings.BaseUrl;
            // Construct the API endpoint URL.
            var url = $"{baseUrl}/api/v1/nodes/{nodeId}/classifications";
            int catIdInt = int.Parse(catId);

            // Create the JSON object for the body with the classification information.
            var classificationBody = new
            {
                class_id = new[] { catIdInt }
            };

            // Serialize the object to JSON.
            string jsonBody = JsonSerializer.Serialize(classificationBody);


            // Build form data content with the key "body" containing the JSON string.
            var formValues = new Dictionary<string, string>
            {
                { "body", jsonBody }
            };

            var content = new FormUrlEncodedContent(formValues);

            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            // Create an HTTP POST request with the constructed URL and content.
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };

            // Remove any existing OTCSTICKET header and add the provided ticket.
            request.Headers.Remove("OTCSTICKET");
            request.Headers.Add("OTCSTICKET", ticket);

            Debug.WriteLine($"[DEBUG] Applying classification via: {url} with body: {jsonBody}");

            try
            {
                // Send the request asynchronously.
                HttpResponseMessage response = await _httpClient.SendAsync(request);

                // Check if the response status is not successful.
                if (!response.IsSuccessStatusCode)
                {
                    string errorResponse = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[ERROR] ApplyClassification failed with status {response.StatusCode}: {errorResponse}");
                    throw new Exception($"ApplyClassification failed with status {response.StatusCode}: {errorResponse}");
                }

                // Optionally read the response content.
                string responseJson = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[DEBUG] ApplyClassification response: {responseJson}");

                // Return true indicating success.
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Exception in ApplyClassification: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> ApplyRMClassificationAsync(string nodeId, string catId, string ticket)
        {
            // Retrieve the base URL from configuration.
            var baseUrl = _settings.BaseUrl;
            // Get the rm_metadataToken from the Node
            var urlToken = $"{baseUrl}/api/v1/nodes/{nodeId}/rmclassifications";

            // Local helper function to add the ticket header.
            void AddTicketHeader(HttpRequestMessage req)
            {
                req.Headers.Remove("OTCSTICKET");
                req.Headers.Add("OTCSTICKET", ticket);
            }

            var wsEDRequest = new HttpRequestMessage(HttpMethod.Get, urlToken);
            AddTicketHeader(wsEDRequest);

            var wsEDResponse = await _httpClient.SendAsync(wsEDRequest);
            if (!wsEDResponse.IsSuccessStatusCode)
            {
                var err = await wsEDResponse.Content.ReadAsStringAsync();
                throw new Exception($"RM Classification apply error on getting token with status {wsEDResponse.StatusCode}: {err}");
            }

            var wsEDJson = await wsEDResponse.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(wsEDJson);

            var rmToken = "";
            if (doc.RootElement.TryGetProperty("rm_metadataToken", out JsonElement rmTokenResults))
            {
                rmToken = rmTokenResults.GetRawText().Trim('\"');
            }

            if (rmToken == "")
            {
                throw new Exception($"RM Classification failed. Other User is using the object");
            }
            
            // Construct the API endpoint URL for the Classification
            var url = $"{baseUrl}/api/v1/nodes/{nodeId}/rmclassifications";
            int catIdInt = int.Parse(catId);

            // Create the JSON object for the body with the classification information.
            var classificationBody = new
            {
                class_id =  catIdInt,
                rm_metadataToken = rmToken
            };

            // Serialize the object to JSON.
            string jsonBody = JsonSerializer.Serialize(classificationBody);


            // Build form data content with the key "body" containing the JSON string.
            var formValues = new Dictionary<string, string>
            {
                { "body", jsonBody }
            };

            var content = new FormUrlEncodedContent(formValues);

            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            // Create an HTTP POST request with the constructed URL and content.
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };

            // Remove any existing OTCSTICKET header and add the provided ticket.
            request.Headers.Remove("OTCSTICKET");
            request.Headers.Add("OTCSTICKET", ticket);

            Debug.WriteLine($"[DEBUG] Applying classification via: {url} with body: {jsonBody}");

            try
            {
                // Send the request asynchronously.
                HttpResponseMessage response = await _httpClient.SendAsync(request);

                // Check if the response status is not successful.
                if (!response.IsSuccessStatusCode)
                {
                    string errorResponse = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[ERROR] ApplyClassification failed with status {response.StatusCode}: {errorResponse}");
                    throw new Exception($"ApplyClassification failed with status {response.StatusCode}: {errorResponse}");
                }

                // Optionally read the response content.
                string responseJson = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[DEBUG] ApplyClassification response: {responseJson}");

                // Return true indicating success.
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Exception in ApplyClassification: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Calls the OpenText API to retrieve the Expiration Date Category ID.
        /// </summary>
        /// <param name="ticket">The authentication ticket.</param>
        /// <returns>The Expiration Date Category ID as a string.</returns>
        public async Task<string?> GetExpirationDateCatIdAsync(string ticket)
        {
            // Retrieve necessary configuration values.

            string baseUrl = _settings.BaseUrl;
            string expDateName = _settings.expDateName;

            // Local helper function to add the ticket header.
            void AddTicketHeader(HttpRequestMessage req)
            {
                req.Headers.Remove("OTCSTICKET");
                req.Headers.Add("OTCSTICKET", ticket);
            }

            // Build the URL to search for the unique name.
            string wsEDUrl = $"{baseUrl}/api/v2/uniquenames?where_names={expDateName}";
            var wsEDRequest = new HttpRequestMessage(HttpMethod.Get, wsEDUrl);
            AddTicketHeader(wsEDRequest);

            Debug.WriteLine($"[DEBUG] Searching for Unique Name: {wsEDUrl}");
            var wsEDResponse = await _httpClient.SendAsync(wsEDRequest);
            if (!wsEDResponse.IsSuccessStatusCode)
            {
                var err = await wsEDResponse.Content.ReadAsStringAsync();
                Debug.WriteLine($"[ERROR] Business Workspace search failed: {err}");
                throw new Exception($"Business Workspace search failed with status {wsEDResponse.StatusCode}: {err}");
            }

            var wsEDJson = await wsEDResponse.Content.ReadAsStringAsync();
            string? expDateCatId = ExtractUniqueNameId(wsEDJson);
            return expDateCatId;
        }
        public async Task<string?> GetUniqueNameAsync(string uName, string ticket)
        {
            // Retrieve necessary configuration values.

            string baseUrl = _settings.BaseUrl;

            // Local helper function to add the ticket header.
            void AddTicketHeader(HttpRequestMessage req)
            {
                req.Headers.Remove("OTCSTICKET");
                req.Headers.Add("OTCSTICKET", ticket);
            }

            // Build the URL to search for the unique name.
            string wsEDUrl = $"{baseUrl}/api/v2/uniquenames?where_names={uName}";
            var wsEDRequest = new HttpRequestMessage(HttpMethod.Get, wsEDUrl);
            AddTicketHeader(wsEDRequest);

            Debug.WriteLine($"[DEBUG] Searching for Unique Name: {wsEDUrl}");
            var wsEDResponse = await _httpClient.SendAsync(wsEDRequest);
            if (!wsEDResponse.IsSuccessStatusCode)
            {
                var err = await wsEDResponse.Content.ReadAsStringAsync();
                throw new Exception($"Unique Name search failed with status {wsEDResponse.StatusCode}: {err}");
            }

            var wsEDJson = await wsEDResponse.Content.ReadAsStringAsync();
            string? expDateCatId = ExtractUniqueNameId(wsEDJson);
            return expDateCatId;
        }

        /// <summary>
        /// Parses the JSON response to extract the Expiration Date Category ID.
        /// </summary>
        /// <param name="json">The JSON response from the API call.</param>
        /// <returns>The Expiration Date Category ID if found; otherwise, null.</returns>
        private string? ExtractUniqueNameId(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                // Look for the "results" property.
                if (doc.RootElement.TryGetProperty("results", out JsonElement results) &&
                    results.ValueKind == JsonValueKind.Array &&
                    results.GetArrayLength() > 0)
                {
                    // Get the first result.
                    var firstResult = results[0];
                    // Then retrieve the "NodeId" property.
                    if (firstResult.TryGetProperty("NodeId", out JsonElement idElement))
                    {
                        return idElement.GetRawText().Trim('\"');
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Exception in ExtractUniqueNameId: {ex.Message}");
            }
            return null;
        }
    }
}
