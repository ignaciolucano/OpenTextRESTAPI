using System.Net.Http.Headers;
using System.Text.Json;
using System.Diagnostics;
using OpenTextIntegrationAPI.Models;
using OpenTextIntegrationAPI.DTOs;
using OpenTextIntegrationAPI.Controllers;
using Microsoft.Extensions.Options;

namespace OpenTextIntegrationAPI.Services
{
    /// <summary>
    /// Provides utility methods for operations with OpenText Content Server.
    /// Handles document classifications, types, metadata, and other common operations.
    /// </summary>
    public class CSUtilities
    {
        private readonly HttpClient _httpClient;
        private readonly OpenTextSettings _settings;
        private readonly ILogService _logger;

        /// <summary>
        /// Initializes a new instance of the CSUtilities class with required dependencies.
        /// </summary>
        /// <param name="httpClient">HTTP client for API calls</param>
        /// <param name="settings">Configuration settings for OpenText API</param>
        /// <param name="logger">Service for logging operations and errors</param>
        public CSUtilities(HttpClient httpClient, IOptions<OpenTextSettings> settings, ILogService logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;

            // Log service initialization
            _logger.Log("CSUtilities service initialized", LogLevel.DEBUG);
        }

        /// <summary>
        /// Gets classification information for a node in OpenText Content Server.
        /// </summary>
        /// <param name="nodeId">ID of the node to retrieve classifications for</param>
        /// <param name="ticket">Authentication ticket (OTCSTICKET)</param>
        /// <returns>Classification name as string or null if not found/error</returns>
        public async Task<string> GetClassifications(string nodeId, string ticket)
        {
            _logger.Log($"Getting classifications for node ID: {nodeId}", LogLevel.DEBUG);

            var baseUrl = _settings.BaseUrl;

            // Build URL for the classifications endpoint
            var wsChildNodesUrl = $"{baseUrl}/api/v1/nodes/{nodeId}/classifications";
            _logger.Log($"Classifications request URL: {wsChildNodesUrl}", LogLevel.DEBUG);

            // Create request with authentication ticket
            using var request = new HttpRequestMessage(HttpMethod.Get, wsChildNodesUrl);
            request.Headers.Add("OTCSTICKET", ticket);

            // Log request details
            _logger.LogRawApi("api_request_get_classifications",
                JsonSerializer.Serialize(new { nodeId, url = wsChildNodesUrl }));

            // Send request
            HttpResponseMessage response;
            try
            {
                _logger.Log("Sending classifications request", LogLevel.TRACE);
                // Usar HttpCompletionOption.ResponseHeadersRead para optimizar la recepción de datos
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                // Check for successful response
                if (!response.IsSuccessStatusCode)
                {
                    _logger.Log($"Classifications request failed with status: {response.StatusCode}", LogLevel.WARNING);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error retrieving classifications: {ex.Message}", LogLevel.ERROR);
                return null;
            }

            // Process response
            try
            {
                // Usar método asíncrono para leer y parsear el contenido como stream
                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var document = await JsonDocument.ParseAsync(contentStream);

                // Look for the "data" property
                if (document.RootElement.TryGetProperty("data", out JsonElement results) &&
                    results.ValueKind == JsonValueKind.Array &&
                    results.GetArrayLength() > 0)
                {
                    // Get the first result
                    var firstResult = results[0];

                    // Extract name property
                    if (firstResult.TryGetProperty("name", out JsonElement nameElement))
                    {
                        string classification = nameElement.GetString();
                        _logger.Log($"Found classification: {classification}", LogLevel.DEBUG);
                        return classification;
                    }
                }

                _logger.Log("No classification found in response", LogLevel.DEBUG);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error parsing classifications response: {ex.Message}", LogLevel.ERROR);
            }

            return null;
        }

        /// <summary>
        /// Maps a document type key to its corresponding name using settings configuration.
        /// </summary>
        /// <param name="key">Document type key to look up</param>
        /// <returns>Document type name</returns>
        /// <exception cref="Exception">Thrown when mapping not found</exception>
        public string? GetDocTypeName(string key)
        {
            _logger.Log($"Looking up document type name for key: {key}", LogLevel.DEBUG);

            if (_settings.DocumentTypeMapping.TryGetValue(key, out var value))
            {
                _logger.Log($"Found document type mapping: {key} -> {value}", LogLevel.DEBUG);
                return value;
            }

            _logger.Log($"Document type '{key}' not found in mapping", LogLevel.WARNING);
            throw new Exception($"Document type '{key}' not found in mapping.");
        }

        /// <summary>
        /// Gets detailed classification information for a node in OpenText Content Server.
        /// </summary>
        /// <param name="nodeId">ID of the node to retrieve classifications for</param>
        /// <param name="ticket">Authentication ticket (OTCSTICKET)</param>
        /// <returns>NodeResponseClassifications object or null if not found/error</returns>
        public async Task<NodeResponseClassifications> GetNodeClassifications(int nodeId, string ticket)
        {
            _logger.Log($"Getting node classifications for node ID: {nodeId}", LogLevel.DEBUG);

            var baseUrl = _settings.BaseUrl;

            void AddTicketHeader(HttpRequestMessage req)
            {
                req.Headers.Remove("OTCSTICKET");
                req.Headers.Add("OTCSTICKET", ticket);
            }

            // Build URL for the classifications endpoint
            var wsChildNodesUrl = $"{baseUrl}/api/v1/nodes/{nodeId}/classifications";
            _logger.Log($"Node classifications request URL: {wsChildNodesUrl}", LogLevel.DEBUG);

            // Create request with authentication ticket
            var wsChildNodesRequest = new HttpRequestMessage(HttpMethod.Get, wsChildNodesUrl);
            AddTicketHeader(wsChildNodesRequest);

            // Log request details
            _logger.LogRawApi("api_request_get_node_classifications",
                JsonSerializer.Serialize(new { nodeId, url = wsChildNodesUrl }));

            // Send request
            HttpResponseMessage wsChildNodesResponse;
            try
            {
                _logger.Log("Sending node classifications request", LogLevel.TRACE);
                wsChildNodesResponse = await _httpClient.SendAsync(wsChildNodesRequest);

                // Check for successful response
                if (!wsChildNodesResponse.IsSuccessStatusCode)
                {
                    _logger.Log($"Node classifications request failed with status: {wsChildNodesResponse.StatusCode}", LogLevel.WARNING);

                    // Log error response if available
                    var errorContent = await wsChildNodesResponse.Content.ReadAsStringAsync();
                    _logger.LogRawApi("api_response_get_node_classifications_error", errorContent);

                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error retrieving node classifications: {ex.Message}", LogLevel.ERROR);
                return null;
            }

            // Process response
            var wsChildNodesJson = await wsChildNodesResponse.Content.ReadAsStringAsync();

            // Log response
            _logger.LogRawApi("api_response_get_node_classifications", wsChildNodesJson);

            // Parse response to NodeResponseClassifications object
            try
            {
                _logger.Log("Deserializing node classifications response", LogLevel.TRACE);
                var nodeResponse = JsonSerializer.Deserialize<NodeResponseClassifications>(wsChildNodesJson);

                if (nodeResponse != null && nodeResponse.Data != null)
                {
                    _logger.Log($"Found {nodeResponse.Data.Count} classifications for node {nodeId}", LogLevel.DEBUG);
                }
                else
                {
                    _logger.Log($"No classifications found for node {nodeId}", LogLevel.DEBUG);
                }

                return nodeResponse;
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error deserializing node classifications: {ex.Message}", LogLevel.ERROR);
            }

            return null;
        }

        /// <summary>
        /// Gets data for a node in OpenText Content Server.
        /// </summary>
        /// <param name="nodeId">ID of the node to retrieve data for</param>
        /// <param name="ticket">Authentication ticket (OTCSTICKET)</param>
        /// <returns>NodeResponseClassifications object or null if not found/error</returns>
        public async Task<NodeResponseClassifications> GetNodeData(int nodeId, string ticket)
        {
            _logger.Log($"Getting node data for node ID: {nodeId}", LogLevel.DEBUG);

            var baseUrl = _settings.BaseUrl;

            void AddTicketHeader(HttpRequestMessage req)
            {
                req.Headers.Remove("OTCSTICKET");
                req.Headers.Add("OTCSTICKET", ticket);
            }

            // Build URL for the classifications endpoint (reusing same endpoint as GetNodeClassifications)
            var wsChildNodesUrl = $"{baseUrl}/api/v1/nodes/{nodeId}/classifications";
            _logger.Log($"Node data request URL: {wsChildNodesUrl}", LogLevel.DEBUG);

            // Create request with authentication ticket
            var wsChildNodesRequest = new HttpRequestMessage(HttpMethod.Get, wsChildNodesUrl);
            AddTicketHeader(wsChildNodesRequest);

            // Log request details
            _logger.LogRawApi("api_request_get_node_data",
                JsonSerializer.Serialize(new { nodeId, url = wsChildNodesUrl }));

            // Send request
            HttpResponseMessage wsChildNodesResponse;
            try
            {
                _logger.Log("Sending node data request", LogLevel.TRACE);
                wsChildNodesResponse = await _httpClient.SendAsync(wsChildNodesRequest);

                // Check for successful response
                if (!wsChildNodesResponse.IsSuccessStatusCode)
                {
                    _logger.Log($"Node data request failed with status: {wsChildNodesResponse.StatusCode}", LogLevel.WARNING);

                    // Log error response if available
                    var errorContent = await wsChildNodesResponse.Content.ReadAsStringAsync();
                    _logger.LogRawApi("api_response_get_node_data_error", errorContent);

                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error retrieving node data: {ex.Message}", LogLevel.ERROR);
                return null;
            }

            // Process response
            var wsChildNodesJson = await wsChildNodesResponse.Content.ReadAsStringAsync();

            // Log response
            _logger.LogRawApi("api_response_get_node_data", wsChildNodesJson);

            // Parse response to NodeResponseClassifications object
            try
            {
                _logger.Log("Deserializing node data response", LogLevel.TRACE);
                var nodeResponse = JsonSerializer.Deserialize<NodeResponseClassifications>(wsChildNodesJson);

                if (nodeResponse != null)
                {
                    _logger.Log($"Successfully retrieved data for node {nodeId}", LogLevel.DEBUG);
                }
                else
                {
                    _logger.Log($"No data found for node {nodeId}", LogLevel.DEBUG);
                }

                return nodeResponse;
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error deserializing node data: {ex.Message}", LogLevel.ERROR);
            }

            return null;
        }

        /// <summary>
        /// Gets document types available for a template in OpenText Content Server.
        /// </summary>
        /// <param name="nodeId">ID of the template node</param>
        /// <param name="ticket">Authentication ticket (OTCSTICKET)</param>
        /// <returns>DocumentTypeResponse object or null if not found/error</returns>
        public async Task<DocumentTypeResponse> GetTemplateDocTypes(string nodeId, string ticket)
        {
            _logger.Log($"Getting template document types for node ID: {nodeId}", LogLevel.DEBUG);

            var baseUrl = _settings.BaseUrl;

            void AddTicketHeader(HttpRequestMessage req)
            {
                req.Headers.Remove("OTCSTICKET");
                req.Headers.Add("OTCSTICKET", ticket);
            }

            // Build URL for the document types endpoint
            var wsChildNodesUrl = $"{baseUrl}/api/v2/businessworkspaces/{nodeId}/doctypes?document_generation_only=false";
            _logger.Log($"Template document types request URL: {wsChildNodesUrl}", LogLevel.DEBUG);

            // Create request with authentication ticket
            var wsChildNodesRequest = new HttpRequestMessage(HttpMethod.Get, wsChildNodesUrl);
            AddTicketHeader(wsChildNodesRequest);

            // Log request details
            _logger.LogRawApi("api_request_get_template_doc_types",
                JsonSerializer.Serialize(new { nodeId, url = wsChildNodesUrl }));

            // Send request
            HttpResponseMessage wsChildNodesResponse;
            try
            {
                _logger.Log("Sending template document types request", LogLevel.TRACE);
                wsChildNodesResponse = await _httpClient.SendAsync(wsChildNodesRequest);

                // Check for successful response
                if (!wsChildNodesResponse.IsSuccessStatusCode)
                {
                    _logger.Log($"Template document types request failed with status: {wsChildNodesResponse.StatusCode}", LogLevel.WARNING);

                    // Log error response if available
                    var errorContent = await wsChildNodesResponse.Content.ReadAsStringAsync();
                    _logger.LogRawApi("api_response_get_template_doc_types_error", errorContent);

                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error retrieving template document types: {ex.Message}", LogLevel.ERROR);
                return null;
            }

            // Process response
            var wsChildNodesJson = await wsChildNodesResponse.Content.ReadAsStringAsync();

            // Log response
            _logger.LogRawApi("api_response_get_template_doc_types", wsChildNodesJson);

            // Parse response to DocumentTypeResponse object
            try
            {
                _logger.Log("Deserializing template document types response", LogLevel.TRACE);
                var nodeResponse = JsonSerializer.Deserialize<DocumentTypeResponse>(wsChildNodesJson);

                if (nodeResponse != null)
                {
                    _logger.Log($"Successfully retrieved document types for template {nodeId}", LogLevel.DEBUG);
                }
                else
                {
                    _logger.Log($"No document types found for template {nodeId}", LogLevel.DEBUG);
                }

                return nodeResponse;
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error deserializing template document types: {ex.Message}", LogLevel.ERROR);
            }

            return null;
        }

        /// <summary>
        /// Gets document types for a node in OpenText Content Server.
        /// </summary>
        /// <param name="nodeId">ID of the node</param>
        /// <param name="ticket">Authentication ticket (OTCSTICKET)</param>
        /// <returns>String or null if not found/error</returns>
        public async Task<string> GetDocTypes(int nodeId, string ticket)
        {
            _logger.Log($"Getting document types for node ID: {nodeId}", LogLevel.DEBUG);

            var baseUrl = _settings.BaseUrl;

            void AddTicketHeader(HttpRequestMessage req)
            {
                req.Headers.Remove("OTCSTICKET");
                req.Headers.Add("OTCSTICKET", ticket);
            }

            // Build URL for the classifications endpoint
            var wsChildNodesUrl = $"{baseUrl}/api/v1/nodes/{nodeId}/classifications";
            _logger.Log($"Document types request URL: {wsChildNodesUrl}", LogLevel.DEBUG);

            // Create request with authentication ticket
            var wsChildNodesRequest = new HttpRequestMessage(HttpMethod.Get, wsChildNodesUrl);
            AddTicketHeader(wsChildNodesRequest);

            // Log request details
            _logger.LogRawApi("api_request_get_doc_types",
                JsonSerializer.Serialize(new { nodeId, url = wsChildNodesUrl }));

            // Send request
            HttpResponseMessage wsChildNodesResponse;
            try
            {
                _logger.Log("Sending document types request", LogLevel.TRACE);
                wsChildNodesResponse = await _httpClient.SendAsync(wsChildNodesRequest);

                // Check for successful response
                if (!wsChildNodesResponse.IsSuccessStatusCode)
                {
                    _logger.Log($"Document types request failed with status: {wsChildNodesResponse.StatusCode}", LogLevel.WARNING);

                    // Log error response if available
                    var errorContent = await wsChildNodesResponse.Content.ReadAsStringAsync();
                    _logger.LogRawApi("api_response_get_doc_types_error", errorContent);

                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error retrieving document types: {ex.Message}", LogLevel.ERROR);
                return null;
            }

            // Process response
            var wsChildNodesJson = await wsChildNodesResponse.Content.ReadAsStringAsync();

            // Log response
            _logger.LogRawApi("api_response_get_doc_types", wsChildNodesJson);

            // Try to deserialize the response (but we return null regardless)
            try
            {
                _logger.Log("Attempting to deserialize document types response", LogLevel.TRACE);
                var nodeResponse = JsonSerializer.Deserialize<NodeResponseClassifications>(wsChildNodesJson);
                _logger.Log("Document types deserialization completed", LogLevel.TRACE);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error deserializing document types: {ex.Message}", LogLevel.ERROR);
            }

            // This method always returns null in the original code
            return null;
        }

        /// <summary>
        /// Applies a classification to a node by calling the OpenText API.
        /// </summary>
        /// <param name="nodeId">The ID of the node to classify</param>
        /// <param name="catId">The classification category ID</param>
        /// <param name="ticket">The authentication ticket</param>
        /// <returns>Boolean indicating success</returns>
        /// <exception cref="Exception">Thrown when classification application fails</exception>
        public async Task<bool> ApplyClassificationAsync(string nodeId, string catId, string ticket)
        {
            _logger.Log($"Applying classification {catId} to node {nodeId}", LogLevel.INFO);

            // Retrieve the base URL from configuration
            var baseUrl = _settings.BaseUrl;

            // Construct the API endpoint URL
            var url = $"{baseUrl}/api/v1/nodes/{nodeId}/classifications";
            _logger.Log($"Classification application URL: {url}", LogLevel.DEBUG);

            int catIdInt;
            try
            {
                catIdInt = int.Parse(catId);
                _logger.Log($"Parsed classification ID {catId} to integer", LogLevel.DEBUG);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Invalid classification ID format: {catId}", LogLevel.ERROR);
                throw new ArgumentException($"Invalid classification ID format: {catId}", ex);
            }

            // Create the JSON object for the body with the classification information
            var classificationBody = new
            {
                class_id = new[] { catIdInt }
            };

            // Serialize the object to JSON
            string jsonBody = JsonSerializer.Serialize(classificationBody);

            // Log request details
            _logger.LogRawApi("api_request_apply_classification", jsonBody);

            // Build form data content with the key "body" containing the JSON string
            var formValues = new Dictionary<string, string>
            {
                { "body", jsonBody }
            };

            var content = new FormUrlEncodedContent(formValues);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            // Create an HTTP POST request with the constructed URL and content
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };

            // Remove any existing OTCSTICKET header and add the provided ticket
            request.Headers.Remove("OTCSTICKET");
            request.Headers.Add("OTCSTICKET", ticket);

            _logger.Log($"Sending classification application request", LogLevel.DEBUG);

            try
            {
                // Send the request asynchronously
                HttpResponseMessage response = await _httpClient.SendAsync(request);

                // Check if the response status is not successful
                if (!response.IsSuccessStatusCode)
                {
                    string errorResponse = await response.Content.ReadAsStringAsync();
                    _logger.Log($"ApplyClassification failed with status {response.StatusCode}: {errorResponse}", LogLevel.ERROR);

                    // Log error response
                    _logger.LogRawApi("api_response_apply_classification_error", errorResponse);

                    throw new Exception($"ApplyClassification failed with status {response.StatusCode}: {errorResponse}");
                }

                // Read the response content
                string responseJson = await response.Content.ReadAsStringAsync();

                // Log response
                _logger.LogRawApi("api_response_apply_classification", responseJson);

                _logger.Log($"Successfully applied classification {catId} to node {nodeId}", LogLevel.INFO);

                // Return true indicating success
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Exception in ApplyClassification: {ex.Message}", LogLevel.ERROR);
                throw;
            }
        }

        /// <summary>
        /// Applies a Records Management classification to a node in OpenText Content Server.
        /// </summary>
        /// <param name="nodeId">ID of the node to classify</param>
        /// <param name="catId">Classification category ID</param>
        /// <param name="ticket">Authentication ticket (OTCSTICKET)</param>
        /// <returns>Boolean indicating success</returns>
        /// <exception cref="Exception">Thrown when RM classification application fails</exception>
        public async Task<bool> ApplyRMClassificationAsync(string nodeId, string catId, string ticket)
        {
            _logger.Log($"Applying RM classification {catId} to node {nodeId}", LogLevel.INFO);

            // Retrieve the base URL from configuration
            var baseUrl = _settings.BaseUrl;

            // Get the rm_metadataToken from the Node
            var urlToken = $"{baseUrl}/api/v1/nodes/{nodeId}/rmclassifications";
            _logger.Log($"RM classification token URL: {urlToken}", LogLevel.DEBUG);

            // Local helper function to add the ticket header
            void AddTicketHeader(HttpRequestMessage req)
            {
                req.Headers.Remove("OTCSTICKET");
                req.Headers.Add("OTCSTICKET", ticket);
            }

            // Request to get RM metadata token
            var wsEDRequest = new HttpRequestMessage(HttpMethod.Get, urlToken);
            AddTicketHeader(wsEDRequest);

            // Log token request details
            _logger.LogRawApi("api_request_get_rm_token",
                JsonSerializer.Serialize(new { nodeId, url = urlToken }));

            // Send request for token
            HttpResponseMessage wsEDResponse;
            try
            {
                _logger.Log("Sending RM token request", LogLevel.DEBUG);
                wsEDResponse = await _httpClient.SendAsync(wsEDRequest);

                // Check for successful response
                if (!wsEDResponse.IsSuccessStatusCode)
                {
                    var err = await wsEDResponse.Content.ReadAsStringAsync();
                    _logger.Log($"RM classification token request failed: {wsEDResponse.StatusCode} - {err}", LogLevel.ERROR);

                    // Log error response
                    _logger.LogRawApi("api_response_get_rm_token_error", err);

                    throw new Exception($"RM Classification apply error on getting token with status {wsEDResponse.StatusCode}: {err}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error getting RM classification token: {ex.Message}", LogLevel.ERROR);
                throw;
            }

            // Process token response
            var wsEDJson = await wsEDResponse.Content.ReadAsStringAsync();

            // Log token response
            _logger.LogRawApi("api_response_get_rm_token", wsEDJson);

            // Extract RM token from response
            var rmToken = "";
            try
            {
                _logger.Log("Parsing RM token from response", LogLevel.DEBUG);
                using var doc = JsonDocument.Parse(wsEDJson);

                if (doc.RootElement.TryGetProperty("rm_metadataToken", out JsonElement rmTokenResults))
                {
                    rmToken = rmTokenResults.GetRawText().Trim('\"');
                    _logger.Log($"Found RM token: {rmToken}", LogLevel.DEBUG);
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error parsing RM token: {ex.Message}", LogLevel.ERROR);
                throw;
            }

            if (rmToken == "")
            {
                _logger.Log("RM token is empty - object may be in use by another user", LogLevel.ERROR);
                throw new Exception($"RM Classification failed. Other User is using the object");
            }

            // Construct the API endpoint URL for the Classification
            var url = $"{baseUrl}/api/v1/nodes/{nodeId}/rmclassifications";
            _logger.Log($"RM classification application URL: {url}", LogLevel.DEBUG);

            int catIdInt;
            try
            {
                catIdInt = int.Parse(catId);
                _logger.Log($"Parsed RM classification ID {catId} to integer", LogLevel.DEBUG);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Invalid RM classification ID format: {catId}", LogLevel.ERROR);
                throw new ArgumentException($"Invalid RM classification ID format: {catId}", ex);
            }

            // Create the JSON object for the body with the classification information
            var classificationBody = new
            {
                class_id = catIdInt,
                rm_metadataToken = rmToken
            };

            // Serialize the object to JSON
            string jsonBody = JsonSerializer.Serialize(classificationBody);

            // Log request details
            _logger.LogRawApi("api_request_apply_rm_classification", jsonBody);

            // Build form data content with the key "body" containing the JSON string
            var formValues = new Dictionary<string, string>
            {
                { "body", jsonBody }
            };

            var content = new FormUrlEncodedContent(formValues);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            // Create an HTTP POST request with the constructed URL and content
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };

            // Remove any existing OTCSTICKET header and add the provided ticket
            request.Headers.Remove("OTCSTICKET");
            request.Headers.Add("OTCSTICKET", ticket);

            _logger.Log($"Sending RM classification application request", LogLevel.DEBUG);

            try
            {
                // Send the request asynchronously
                HttpResponseMessage response = await _httpClient.SendAsync(request);

                // Check if the response status is not successful
                if (!response.IsSuccessStatusCode)
                {
                    string errorResponse = await response.Content.ReadAsStringAsync();
                    _logger.Log($"RM classification failed with status {response.StatusCode}: {errorResponse}", LogLevel.ERROR);

                    // Log error response
                    _logger.LogRawApi("api_response_apply_rm_classification_error", errorResponse);

                    throw new Exception($"ApplyClassification failed with status {response.StatusCode}: {errorResponse}");
                }

                // Read the response content
                string responseJson = await response.Content.ReadAsStringAsync();

                // Log response
                _logger.LogRawApi("api_response_apply_rm_classification", responseJson);

                _logger.Log($"Successfully applied RM classification {catId} to node {nodeId}", LogLevel.INFO);

                // Return true indicating success
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Exception in applying RM classification: {ex.Message}", LogLevel.ERROR);
                throw;
            }
        }

        /// <summary>
        /// Calls the OpenText API to retrieve the Expiration Date Category ID.
        /// </summary>
        /// <param name="ticket">The authentication ticket</param>
        /// <returns>The Expiration Date Category ID as a string</returns>
        /// <exception cref="Exception">Thrown when retrieval fails</exception>
        public async Task<string?> GetExpirationDateCatIdAsync(string ticket)
        {
            _logger.Log("Getting Expiration Date Category ID", LogLevel.DEBUG);

            // Retrieve necessary configuration values
            string baseUrl = _settings.BaseUrl;
            string expDateName = _settings.expDateName;

            _logger.Log($"Using expiration date name: {expDateName}", LogLevel.DEBUG);

            // Local helper function to add the ticket header
            void AddTicketHeader(HttpRequestMessage req)
            {
                req.Headers.Remove("OTCSTICKET");
                req.Headers.Add("OTCSTICKET", ticket);
            }

            // Build the URL to search for the unique name
            string wsEDUrl = $"{baseUrl}/api/v2/uniquenames?where_names={expDateName}";
            _logger.Log($"Expiration date category request URL: {wsEDUrl}", LogLevel.DEBUG);

            var wsEDRequest = new HttpRequestMessage(HttpMethod.Get, wsEDUrl);
            AddTicketHeader(wsEDRequest);

            // Log request details
            _logger.LogRawApi("api_request_get_exp_date_category",
                JsonSerializer.Serialize(new { name = expDateName, url = wsEDUrl }));

            // Send request
            HttpResponseMessage wsEDResponse;
            try
            {
                _logger.Log("Sending expiration date category request", LogLevel.DEBUG);
                wsEDResponse = await _httpClient.SendAsync(wsEDRequest);

                // Check for successful response
                if (!wsEDResponse.IsSuccessStatusCode)
                {
                    var err = await wsEDResponse.Content.ReadAsStringAsync();
                    _logger.Log($"Expiration date category request failed: {wsEDResponse.StatusCode} - {err}", LogLevel.ERROR);

                    // Log error response
                    _logger.LogRawApi("api_response_get_exp_date_category_error", err);

                    throw new Exception($"Business Workspace search failed with status {wsEDResponse.StatusCode}: {err}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error getting expiration date category: {ex.Message}", LogLevel.ERROR);
                throw;
            }

            // Process response
            var wsEDJson = await wsEDResponse.Content.ReadAsStringAsync();

            // Log response
            _logger.LogRawApi("api_response_get_exp_date_category", wsEDJson);

            // Extract unique name ID from response
            string? expDateCatId = ExtractUniqueNameId(wsEDJson);

            if (string.IsNullOrEmpty(expDateCatId))
            {
                _logger.Log("Could not extract expiration date category ID", LogLevel.WARNING);
            }
            else
            {
                _logger.Log($"Found expiration date category ID: {expDateCatId}", LogLevel.DEBUG);
            }

            return expDateCatId;
        }

        /// <summary>
        /// Gets a unique name ID from OpenText Content Server by its name.
        /// </summary>
        /// <param name="uName">Unique name to retrieve</param>
        /// <param name="ticket">Authentication ticket</param>
        /// <returns>Unique name ID as string or null if not found/error</returns>
        /// <exception cref="Exception">Thrown when unique name retrieval fails</exception>
        public async Task<string?> GetUniqueNameAsync(string uName, string ticket)
        {
            _logger.Log($"Getting unique name ID for: {uName}", LogLevel.DEBUG);

            // Retrieve necessary configuration values
            string baseUrl = _settings.BaseUrl;

            // Local helper function to add the ticket header
            void AddTicketHeader(HttpRequestMessage req)
            {
                req.Headers.Remove("OTCSTICKET");
                req.Headers.Add("OTCSTICKET", ticket);
            }

            // Build the URL to search for the unique name
            string wsEDUrl = $"{baseUrl}/api/v2/uniquenames?where_names={uName}";
            _logger.Log($"Unique name request URL: {wsEDUrl}", LogLevel.DEBUG);

            var wsEDRequest = new HttpRequestMessage(HttpMethod.Get, wsEDUrl);
            AddTicketHeader(wsEDRequest);

            // Log request details
            _logger.LogRawApi("api_request_get_unique_name",
                JsonSerializer.Serialize(new { name = uName, url = wsEDUrl }));

            // Send request
            HttpResponseMessage wsEDResponse;
            try
            {
                _logger.Log("Sending unique name request", LogLevel.DEBUG);
                wsEDResponse = await _httpClient.SendAsync(wsEDRequest);

                // Check for successful response
                if (!wsEDResponse.IsSuccessStatusCode)
                {
                    var err = await wsEDResponse.Content.ReadAsStringAsync();
                    _logger.Log($"Unique name request failed: {wsEDResponse.StatusCode} - {err}", LogLevel.ERROR);

                    // Log error response
                    _logger.LogRawApi("api_response_get_unique_name_error", err);

                    throw new Exception($"Unique Name search failed with status {wsEDResponse.StatusCode}: {err}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error getting unique name: {ex.Message}", LogLevel.ERROR);
                throw;
            }

            // Process response
            var wsEDJson = await wsEDResponse.Content.ReadAsStringAsync();

            // Log response
            _logger.LogRawApi("api_response_get_unique_name", wsEDJson);

            // Extract unique name ID from response
            string? uniqueNameId = ExtractUniqueNameId(wsEDJson);

            if (string.IsNullOrEmpty(uniqueNameId))
            {
                _logger.Log($"Could not extract unique name ID for {uName}", LogLevel.WARNING);
            }
            else
            {
                _logger.Log($"Found unique name ID for {uName}: {uniqueNameId}", LogLevel.DEBUG);
            }

            return uniqueNameId;
        }

        /// <summary>
        /// Parses the JSON response to extract a unique name ID.
        /// </summary>
        /// <param name="json">The JSON response from the API call</param>
        /// <returns>The unique name ID if found; otherwise, null</returns>
        private string? ExtractUniqueNameId(string json)
        {
            _logger.Log("Extracting unique name ID from response", LogLevel.TRACE);

            try
            {
                using var doc = JsonDocument.Parse(json);
                // Look for the "results" property
                if (doc.RootElement.TryGetProperty("results", out JsonElement results) &&
                    results.ValueKind == JsonValueKind.Array &&
                    results.GetArrayLength() > 0)
                {
                    // Get the first result
                    var firstResult = results[0];

                    // Then retrieve the "NodeId" property
                    if (firstResult.TryGetProperty("NodeId", out JsonElement idElement))
                    {
                        string nodeId = idElement.GetRawText().Trim('\"');
                        _logger.Log($"Found NodeId: {nodeId}", LogLevel.TRACE);
                        return nodeId;
                    }
                }

                _logger.Log("No NodeId found in response", LogLevel.TRACE);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Exception in ExtractUniqueNameId: {ex.Message}", LogLevel.ERROR);
            }

            return null;
        }
    }
}