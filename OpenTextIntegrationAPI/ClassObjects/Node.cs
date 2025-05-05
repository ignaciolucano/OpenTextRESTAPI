using Microsoft.Extensions.Options;
using OpenTextIntegrationAPI.Models;
using OpenTextIntegrationAPI.Services;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace OpenTextIntegrationAPI.ClassObjects
{
    /// <summary>
    /// Handles node operations in OpenText Content Server.
    /// Provides functionality for creating, deleting, moving, and retrieving node information.
    /// A node in OpenText can be a document, folder, or other object types.
    /// </summary>
    public class Node
    {
        private readonly OpenTextSettings _settings;
        private readonly HttpClient _httpClient;
        private readonly CSUtilities _csUtilities;
        private readonly ILogService _logger;

        /// <summary>
        /// Initializes a new instance of the Node class with required dependencies.
        /// </summary>
        /// <param name="settings">Configuration settings for OpenText API</param>
        /// <param name="httpClient">HTTP client for API calls</param>
        /// <param name="csUtilities">Utility methods for Content Server operations</param>
        /// <param name="logger">Logging service for tracking operations</param>
        public Node(IOptions<OpenTextSettings> settings, HttpClient httpClient, CSUtilities csUtilities, ILogService logger)
        {
            _settings = settings.Value;
            _httpClient = httpClient;
            _csUtilities = csUtilities;
            _logger = logger;

            // Log initialization of the Node service
            _logger.Log("Node service initialized", LogLevel.DEBUG);
        }

        /// <summary>
        /// Deletes a node from OpenText Content Server.
        /// Validates that the node is in a Change Request workspace before deletion.
        /// </summary>
        /// <param name="nodeId">ID of the node to delete</param>
        /// <param name="ticket">Authentication ticket (OTCSTICKET)</param>
        /// <returns>True if deletion was successful</returns>
        /// <exception cref="ArgumentException">Thrown when ticket is invalid</exception>
        /// <exception cref="Exception">Thrown when node is not in a Change Request or deletion fails</exception>
        public async Task<bool> DeleteNodeAsync(string nodeId, string ticket)
        {
            _logger.Log($"Starting DeleteNodeAsync for nodeId: {nodeId}", LogLevel.INFO);

            // Validate that the ticket is provided.
            if (string.IsNullOrWhiteSpace(ticket))
            {
                _logger.Log("OTCS ticket is missing or empty", LogLevel.ERROR);
                throw new ArgumentException("OTCS ticket must be provided.", nameof(ticket));
            }

            // Check that the Node is on a Change Request
            _logger.Log($"Verifying node {nodeId} is in a Change Request", LogLevel.DEBUG);
            if (await GetBWforNode(_httpClient, nodeId, ticket) == false)
            {
                _logger.Log($"Node {nodeId} is not in a Change Request workspace", LogLevel.ERROR);
                throw new Exception("The node is not in a Change Request");
            }

            // Build the URL for DELETE /v1/nodes/{id}
            var baseUrl = _settings.BaseUrl;
            var deleteUrl = $"{baseUrl}/api/v1/nodes/{nodeId}";
            _logger.Log($"Delete URL: {deleteUrl}", LogLevel.DEBUG);

            // Create the DELETE request
            var requestMessage = new HttpRequestMessage(HttpMethod.Delete, deleteUrl);
            requestMessage.Headers.Add("OTCSTICKET", ticket);

            // Log raw API request
            _logger.LogRawApi("api_request_delete_node", JsonSerializer.Serialize(new { nodeId, url = deleteUrl }));

            HttpResponseMessage response;
            try
            {
                // Send the request
                _logger.Log($"Sending DELETE request for node {nodeId}", LogLevel.DEBUG);
                response = await _httpClient.SendAsync(requestMessage);

                // Log raw API response
                string responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogRawApi("api_response_delete_node", responseContent);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                throw new Exception($"Error sending DELETE request to {deleteUrl}: {ex.Message}", ex);
            }

            // Check for a successful response
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.Log($"DeleteNodeAsync failed with status code {response.StatusCode}: {errorBody}", LogLevel.ERROR);
                throw new Exception($"DeleteNodeAsync failed with status code {response.StatusCode}: {errorBody}");
            }

            _logger.Log($"Node {nodeId} successfully deleted", LogLevel.INFO);
            return true;
        }

        /// <summary>
        /// Checks if a node belongs to a Change Request Business Workspace.
        /// </summary>
        /// <param name="httpClient">HTTP client for API calls</param>
        /// <param name="nodeId">ID of the node to check</param>
        /// <param name="ticket">Authentication ticket (OTCSTICKET)</param>
        /// <returns>True if the node is in a Change Request workspace</returns>
        public async Task<bool> GetBWforNode(HttpClient httpClient, string nodeId, string ticket)
        {
            _logger.Log($"Checking if node {nodeId} belongs to a Change Request workspace", LogLevel.DEBUG);

            // Construct URL to get the business workspace for a node
            var baseUrl = _settings.BaseUrl;
            var extSystemId = _settings.ExtSystemId;
            var url = $"{baseUrl}/api/v1/nodes/{nodeId}/businessworkspace";
            _logger.Log($"Request URL: {url}", LogLevel.DEBUG);

            // Create request with authentication ticket
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("OTCSTICKET", ticket);

            // Log raw API request
            _logger.LogRawApi("api_request_get_bw_for_node", JsonSerializer.Serialize(new { nodeId, url }));

            // Execute the request
            _logger.Log("Sending request to OpenText API", LogLevel.TRACE);
            var response = await httpClient.SendAsync(request);

            // Read response content
            var json = await response.Content.ReadAsStringAsync();

            // Log raw API response
            _logger.LogRawApi("api_response_get_bw_for_node", json);

            // Check for successful response
            if (!response.IsSuccessStatusCode)
            {
                _logger.Log($"Failed to get business workspace for node {nodeId}: {response.StatusCode}", LogLevel.WARNING);
                return false;
            }

            _logger.Log($"Business Workspace search response received for node {nodeId}", LogLevel.DEBUG);

            try
            {
                // Get the Change Request workspace type from settings
                var crWSKType = _settings.ChangeRequestWSKtype;

                // Deserialize response to check workspace type
                var wsResponse = JsonSerializer.Deserialize<WorkspaceTypeResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                // Check if workspace type matches Change Request type
                if (wsResponse.workspace_type_id.ToString() != crWSKType)
                {
                    _logger.Log($"Node {nodeId} is not in a Change Request workspace. " +
                                $"Workspace type: {wsResponse.workspace_type_id}, Expected: {crWSKType}", LogLevel.DEBUG);
                    return false;
                }

                _logger.Log($"Node {nodeId} belongs to a Change Request workspace", LogLevel.DEBUG);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Failed to deserialize WorkspaceTypeResponse: {ex.Message}", LogLevel.ERROR);
                return false;
            }
        }

        /// <summary>
        /// Retrieves a node by its ID, including its content.
        /// </summary>
        /// <param name="nodeId">ID of the node to retrieve</param>
        /// <param name="ticket">Authentication ticket (OTCSTICKET)</param>
        /// <returns>NodeResponse containing node information and content</returns>
        /// <exception cref="Exception">Thrown when authentication fails or node cannot be retrieved</exception>
        /// <summary>
        /// Retrieves a node by its ID, including its content.
        /// </summary>
        /// <param name="nodeId">ID of the node to retrieve</param>
        /// <param name="ticket">Authentication ticket (OTCSTICKET)</param>
        /// <returns>NodeResponse containing node information and content</returns>
        /// <exception cref="Exception">Thrown when authentication fails or node cannot be retrieved</exception>
        public async Task<NodeResponse?> GetNodeByIdAsync(int nodeId, string ticket)
        {
            _logger.Log($"Starting GetNodeByIdAsync with nodeId={nodeId}", LogLevel.DEBUG);

            // Validate authentication ticket
            if (string.IsNullOrEmpty(ticket))
            {
                _logger.Log("Authentication failed: OTCS ticket is empty", LogLevel.ERROR);
                throw new Exception("Authentication failed: OTCS ticket is empty.");
            }

            // Build URL for getting node metadata
            var baseUrl = _settings.BaseUrl;
            var url = $"{baseUrl}/api/v2/nodes/{nodeId}";
            _logger.Log($"GET node metadata URL: {url}", LogLevel.DEBUG);

            // Initialize variables for node properties
            int retNodeId = 0;
            string retFileName = "";
            int retNodeType = 0;
            string retNodeTypeName = "";

            // Create request with authentication ticket
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
            requestMessage.Headers.Add("OTCSTICKET", ticket);

            // Log raw API request
            _logger.LogRawApi("api_request_get_node", JsonSerializer.Serialize(new { nodeId, url }));

            // Send request to get node metadata
            HttpResponseMessage response;
            try
            {
                _logger.Log("Sending request for node metadata", LogLevel.TRACE);
                response = await _httpClient.SendAsync(requestMessage);

                // Check if response is successful
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.Log($"Failed to get node metadata: {response.StatusCode} - {errorContent}", LogLevel.ERROR);
                    throw new Exception($"Failed to get node metadata: {response.StatusCode} - {errorContent}");
                }

                // Log raw API response
                var wsNodesInfoJson = await response.Content.ReadAsStringAsync();
                _logger.LogRawApi("api_response_get_node", wsNodesInfoJson);

                // Parse node properties from response with improved error handling
                try
                {
                    _logger.Log("Parsing node properties from response", LogLevel.TRACE);
                    using (JsonDocument doc = JsonDocument.Parse(wsNodesInfoJson))
                    {
                        JsonElement root = doc.RootElement;

                        // Extract properties directly from the root or navigate to the correct location
                        // The structure might vary depending on the API
                        if (root.TryGetProperty("data", out JsonElement data) &&
                            data.ValueKind == JsonValueKind.Object)
                        {
                            // Extract properties from data object
                            retNodeId = data.TryGetProperty("id", out JsonElement idElem) ?
                                idElem.GetInt32() : nodeId;

                            retFileName = data.TryGetProperty("name", out JsonElement nameElem) ?
                                nameElem.GetString() ?? "" : "";

                            retNodeType = data.TryGetProperty("type", out JsonElement typeElem) ?
                                typeElem.GetInt32() : 0;

                            retNodeTypeName = data.TryGetProperty("type_name", out JsonElement typeNameElem) ?
                                typeNameElem.GetString() ?? "" : "";
                        }
                        // If not found in that structure, try alternative paths
                        else if (root.TryGetProperty("results", out JsonElement results) )
                        {
                            //var firstResult = results[0];
                            if (results.TryGetProperty("data", out JsonElement dataArray) &&
                                dataArray.ValueKind == JsonValueKind.Object)
                            {
                                if (dataArray.TryGetProperty("properties", out JsonElement propertiesArray) &&
                                propertiesArray.ValueKind == JsonValueKind.Object)
                                {
                                    // Extract properties from this path
                                    retNodeId = propertiesArray.TryGetProperty("id", out JsonElement idElem) ?
                                    idElem.GetInt32() : nodeId;

                                    retFileName = propertiesArray.TryGetProperty("name", out JsonElement nameElem) ?
                                        nameElem.GetString() ?? "" : "";

                                    retNodeType = propertiesArray.TryGetProperty("type", out JsonElement typeElem) ?
                                        typeElem.GetInt32() : 0;

                                    retNodeTypeName = propertiesArray.TryGetProperty("type_name", out JsonElement typeNameElem) ?
                                        typeNameElem.GetString() ?? "" : "";
                                }
                            }
                        }

                        _logger.Log($"Parsed node properties: ID={retNodeId}, Name={retFileName}, Type={retNodeType}, TypeName={retNodeTypeName}", LogLevel.DEBUG);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, LogLevel.ERROR);
                    _logger.Log($"Exception in parsing node properties: {ex.Message}", LogLevel.ERROR);
                    // Continue with default values rather than failing
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                throw;
            }

            string? DocTypeRule = await _csUtilities.GetClassifications(retNodeId.ToString(), ticket);
            string documentType;

            if (string.IsNullOrEmpty(DocTypeRule))
            {
                retNodeTypeName = ""; // Folder
                retNodeType = 0;
                _logger.Log($"Using parent folder name as document type: ", LogLevel.TRACE);
            }
            else
            {
                retNodeTypeName = DocTypeRule;
                retNodeType = 0;
                _logger.Log($"Using classification as document type: {retNodeTypeName}", LogLevel.TRACE);
            }
            

            // Build URL for getting node content
            url = $"{baseUrl}/api/v2/nodes/{nodeId}/content?suppress_response_codes";
            _logger.Log($"GET node content URL: {url}", LogLevel.DEBUG);

            // Create request for node content
            requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
            requestMessage.Headers.Add("OTCSTICKET", ticket);

            // Log raw API request for content
            _logger.LogRawApi("api_request_get_node_content", JsonSerializer.Serialize(new { nodeId, url }));

            // Send request to get node content
            try
            {
                _logger.Log("Sending request for node content", LogLevel.TRACE);
                response = await _httpClient.SendAsync(requestMessage);

                // Note: Not logging raw response content here as it could be binary data
                _logger.Log($"GET Node Content HTTP status: {response.StatusCode}", LogLevel.DEBUG);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                throw;
            }

            // Check for successful response
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.Log($"GET Node Content failed: {errorBody}", LogLevel.ERROR);
                throw new Exception($"GET Node Content failed with status code {response.StatusCode}: {errorBody}");
            }

            // Read node content as byte array
            byte[] contentBytes;
            try
            {
                contentBytes = await response.Content.ReadAsByteArrayAsync();
                _logger.Log($"GET Node Content downloaded {contentBytes.Length} bytes", LogLevel.DEBUG);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                throw new Exception("Error reading node content: " + ex.Message);
            }

            // Create response object with node properties and content
            var nodeResponse = new NodeResponse
            {
                nodeId = retNodeId,
                file_name = retFileName,
                type = retNodeType,
                type_name = retNodeTypeName,
                Content = contentBytes
            };

            _logger.Log($"Successfully retrieved node {nodeId} with {contentBytes.Length} bytes of content", LogLevel.INFO);
            return nodeResponse;
        }

        /// <summary>
        /// Gets all subnodes (documents) from a node with expiration date filtering.
        /// </summary>
        /// <param name="nodeId">ID of the parent node</param>
        /// <param name="ticket">Authentication ticket (OTCSTICKET)</param>
        /// <param name="expDateCatId">Category ID for expiration date</param>
        /// <param name="MasterRequest">Filter for "Master" or "Request" nodes</param>
        /// <param name="CatName">Optional category name for filtering</param>
        /// <returns>List of DocumentInfo objects representing the subnodes</returns>
        /// <exception cref="Exception">Thrown when retrieval fails</exception>
        public async Task<List<DocumentInfo>> GetNodeSubNodesAsync(string nodeId, string ticket, string expDateCatId, string MasterRequest, string? CatName = null)
        {
            _logger.Log($"Starting GetNodeSubNodesAsync for nodeId={nodeId}, MasterRequest={MasterRequest}", LogLevel.DEBUG);

            // Build base URL for API call
            var baseUrl = _settings.BaseUrl;
            var extSystemId = _settings.ExtSystemId;

            var docs = new List<DocumentInfo>();

            // Helper function to add authentication ticket to requests
            void AddTicketHeader(HttpRequestMessage req)
            {
                req.Headers.Remove("OTCSTICKET");
                req.Headers.Add("OTCSTICKET", ticket);
            }

            // Build URL for getting child nodes
            var wsChildNodesUrl = $"{baseUrl}/api/v2/nodes/{nodeId}/nodes";
            var wsChildNodesRequest = new HttpRequestMessage(HttpMethod.Get, wsChildNodesUrl);
            AddTicketHeader(wsChildNodesRequest);

            _logger.Log($"Requesting child nodes from: {wsChildNodesUrl}", LogLevel.DEBUG);

            // Log raw API request
            _logger.LogRawApi("api_request_get_child_nodes", JsonSerializer.Serialize(new { nodeId, url = wsChildNodesUrl }));

            // Execute the request
            var wsChildNodesResponse = await _httpClient.SendAsync(wsChildNodesRequest);

            // Check for successful response
            if (!wsChildNodesResponse.IsSuccessStatusCode)
            {
                var err = await wsChildNodesResponse.Content.ReadAsStringAsync();
                _logger.Log($"Business Workspace search failed: {err}", LogLevel.ERROR);

                // Log raw API response
                _logger.LogRawApi("api_response_get_child_nodes_error", err);

                throw new Exception($"Business Workspace search failed with status {wsChildNodesResponse.StatusCode}: {err}");
            }

            // Read response content
            var wsChildNodesJson = await wsChildNodesResponse.Content.ReadAsStringAsync();

            // Log raw API response
            _logger.LogRawApi("api_response_get_child_nodes", wsChildNodesJson);

            _logger.Log($"Successfully retrieved child nodes for nodeId={nodeId}", LogLevel.DEBUG);

            // Parse documents from response
            _logger.Log("Parsing documents from response", LogLevel.DEBUG);
            List<DocumentInfo> documents = await ParseDocumentsFromBWAsync(wsChildNodesJson, ticket, baseUrl, expDateCatId, MasterRequest, CatName);

            _logger.Log($"Found {documents.Count} documents in nodeId={nodeId}", LogLevel.INFO);
            return documents;
        }

        /// <summary>
        /// Gets all subnodes (documents) from a node without expiration date filtering.
        /// Overload of GetNodeSubNodesAsync that doesn't require expiration date category ID.
        /// </summary>
        /// <param name="nodeId">ID of the parent node</param>
        /// <param name="ticket">Authentication ticket (OTCSTICKET)</param>
        /// <param name="MasterRequest">Filter for "Master" or "Request" nodes</param>
        /// <param name="CatName">Optional category name for filtering</param>
        /// <returns>List of DocumentInfo objects representing the subnodes</returns>
        /// <exception cref="Exception">Thrown when retrieval fails</exception>
        public async Task<List<DocumentInfo>> GetNodeSubNodesAsync(string nodeId, string ticket, string MasterRequest, string? CatName = null)
        {
            _logger.Log($"Starting GetNodeSubNodesAsync (no expDateCatId) for nodeId={nodeId}, MasterRequest={MasterRequest}", LogLevel.DEBUG);

            // Build base URL for API call
            var baseUrl = _settings.BaseUrl;
            var extSystemId = _settings.ExtSystemId;

            var docs = new List<DocumentInfo>();

            // Helper function to add authentication ticket to requests
            void AddTicketHeader(HttpRequestMessage req)
            {
                req.Headers.Remove("OTCSTICKET");
                req.Headers.Add("OTCSTICKET", ticket);
            }

            // Build URL for getting child nodes
            var wsChildNodesUrl = $"{baseUrl}/api/v2/nodes/{nodeId}/nodes";
            var wsChildNodesRequest = new HttpRequestMessage(HttpMethod.Get, wsChildNodesUrl);
            AddTicketHeader(wsChildNodesRequest);

            _logger.Log($"Requesting child nodes from: {wsChildNodesUrl}", LogLevel.DEBUG);

            // Log raw API request
            _logger.LogRawApi("api_request_get_child_nodes", JsonSerializer.Serialize(new { nodeId, url = wsChildNodesUrl }));

            // Execute the request
            var wsChildNodesResponse = await _httpClient.SendAsync(wsChildNodesRequest);

            // Check for successful response
            if (!wsChildNodesResponse.IsSuccessStatusCode)
            {
                var err = await wsChildNodesResponse.Content.ReadAsStringAsync();
                _logger.Log($"Business Workspace search failed: {err}", LogLevel.ERROR);

                // Log raw API response
                _logger.LogRawApi("api_response_get_child_nodes_error", err);

                throw new Exception($"Business Workspace search failed with status {wsChildNodesResponse.StatusCode}: {err}");
            }

            // Read response content
            var wsChildNodesJson = await wsChildNodesResponse.Content.ReadAsStringAsync();

            // Log raw API response
            _logger.LogRawApi("api_response_get_child_nodes", wsChildNodesJson);

            _logger.Log($"Successfully retrieved child nodes for nodeId={nodeId}", LogLevel.DEBUG);

            // Parse documents from response
            _logger.Log("Parsing documents from response", LogLevel.DEBUG);
            List<DocumentInfo> documents = await ParseDocumentsFromBWAsync(wsChildNodesJson, ticket, baseUrl, MasterRequest, CatName);

            _logger.Log($"Found {documents.Count} documents in nodeId={nodeId}", LogLevel.INFO);
            return documents;
        }

        /// <summary>
        /// Gets all subfolders from a node.
        /// </summary>
        /// <param name="nodeId">ID of the parent node</param>
        /// <param name="ticket">Authentication ticket (OTCSTICKET)</param>
        /// <param name="MasterRequest">Filter for "Master" or "Request" nodes</param>
        /// <returns>List of DocumentInfo objects representing the subfolders</returns>
        /// <exception cref="Exception">Thrown when retrieval fails</exception>
        public async Task<List<DocumentInfo>> GetNodeSubFoldersAsync(string nodeId, string ticket, string MasterRequest)
        {
            _logger.Log($"Starting GetNodeSubFoldersAsync for nodeId={nodeId}, MasterRequest={MasterRequest}", LogLevel.DEBUG);

            // Build base URL for API call
            var baseUrl = _settings.BaseUrl;
            var extSystemId = _settings.ExtSystemId;

            var docs = new List<DocumentInfo>();

            // Helper function to add authentication ticket to requests
            void AddTicketHeader(HttpRequestMessage req)
            {
                req.Headers.Remove("OTCSTICKET");
                req.Headers.Add("OTCSTICKET", ticket);
            }

            // Build URL for getting child nodes
            var wsChildNodesUrl = $"{baseUrl}/api/v2/nodes/{nodeId}/nodes";
            var wsChildNodesRequest = new HttpRequestMessage(HttpMethod.Get, wsChildNodesUrl);
            AddTicketHeader(wsChildNodesRequest);

            _logger.Log($"Requesting child nodes from: {wsChildNodesUrl}", LogLevel.DEBUG);

            // Log raw API request
            _logger.LogRawApi("api_request_get_child_folders", JsonSerializer.Serialize(new { nodeId, url = wsChildNodesUrl }));

            // Execute the request
            var wsChildNodesResponse = await _httpClient.SendAsync(wsChildNodesRequest);

            // Check for successful response
            if (!wsChildNodesResponse.IsSuccessStatusCode)
            {
                var err = await wsChildNodesResponse.Content.ReadAsStringAsync();
                _logger.Log($"Business Workspace search failed: {err}", LogLevel.ERROR);

                // Log raw API response
                _logger.LogRawApi("api_response_get_child_folders_error", err);

                throw new Exception($"Business Workspace search failed with status {wsChildNodesResponse.StatusCode}: {err}");
            }

            // Read response content
            var wsChildNodesJson = await wsChildNodesResponse.Content.ReadAsStringAsync();

            // Log raw API response
            _logger.LogRawApi("api_response_get_child_folders", wsChildNodesJson);

            _logger.Log($"Successfully retrieved child nodes for nodeId={nodeId}", LogLevel.DEBUG);

            // Parse folders from response
            _logger.Log("Parsing folders from response", LogLevel.DEBUG);
            List<DocumentInfo> documents = await ParseFoldersFromBWAsync(wsChildNodesJson, ticket, baseUrl, MasterRequest);

            _logger.Log($"Found {documents.Count} folders in nodeId={nodeId}", LogLevel.INFO);
            return documents;
        }

        /// <summary>
        /// Parses document information from a Business Workspace response JSON with expiration date category.
        /// </summary>
        /// <param name="json">JSON response from API</param>
        /// <param name="ticket">Authentication ticket (OTCSTICKET)</param>
        /// <param name="baseUrl">Base URL for the API</param>
        /// <param name="expDateCatId">Category ID for expiration date</param>
        /// <param name="masterRequest">Filter for "Master" or "Request" nodes</param>
        /// <param name="CatName">Optional category name for filtering</param>
        /// <returns>List of DocumentInfo objects</returns>
        private async Task<List<DocumentInfo>> ParseDocumentsFromBWAsync(string json, string ticket, string baseUrl, string expDateCatId, string masterRequest, string? CatName = null)
        {
            _logger.Log("Parsing documents from Business Workspace response JSON (no expDateCatId)", LogLevel.DEBUG);

            var docs = new List<DocumentInfo>();
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty("results", out JsonElement results) &&
                        results.ValueKind == JsonValueKind.Array)
                    {
                        _logger.Log($"Found {results.GetArrayLength()} results to process", LogLevel.DEBUG);

                        // Recolectar todas las carpetas primero
                        var folderTasks = new List<Task<List<DocumentInfo>>>();
                        var documentItems = new List<(JsonElement item, string id, string name, string mimeType)>();

                        foreach (var item in results.EnumerateArray())
                        {
                            long idInt = 0;
                            string id = "";
                            string? mimeType = null;
                            string name = null;

                            // Extract properties from the item
                            if (item.TryGetProperty("data", out JsonElement data) &&
                                data.TryGetProperty("properties", out JsonElement props))
                            {
                                // Extract common properties
                                if (props.TryGetProperty("id", out JsonElement idElem))
                                {
                                    if (idElem.ValueKind == JsonValueKind.Number)
                                    {
                                        idInt = idElem.GetInt64();
                                    }
                                    else if (idElem.ValueKind == JsonValueKind.String && long.TryParse(idElem.GetString(), out long parsedId))
                                    {
                                        idInt = parsedId;
                                    }
                                    id = idInt.ToString();
                                }

                                name = props.TryGetProperty("name", out JsonElement nameElem)
                                    ? nameElem.GetString() ?? ""
                                    : "";

                                // Check mime_type to determine if it's a document
                                mimeType = props.TryGetProperty("mime_type", out JsonElement mimeElem)
                                    ? mimeElem.GetString()
                                    : null;
                            }

                            // Process based on mime_type - guardar documentos para procesar después y lanzar carpetas en paralelo
                            if (string.IsNullOrEmpty(mimeType))
                            {
                                // Procesar carpetas en paralelo
                                if (string.Equals(masterRequest, "Master", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (!string.Equals(name, "Staging", StringComparison.OrdinalIgnoreCase))
                                    {
                                        _logger.Log($"Processing Master folder: {name} (NodeId: {id})", LogLevel.DEBUG);
                                        // Crear tarea para procesar la carpeta
                                        folderTasks.Add(GetNodeSubNodesAsync(id, ticket, expDateCatId, masterRequest, name));
                                    }
                                }
                                else if (string.Equals(masterRequest, "Request", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (string.Equals(name, "Staging", StringComparison.OrdinalIgnoreCase))
                                    {
                                        _logger.Log($"Processing Request folder: {name} (NodeId: {id})", LogLevel.DEBUG);
                                        // Crear tarea para procesar la carpeta
                                        folderTasks.Add(GetNodeSubNodesAsync(id, ticket, expDateCatId, masterRequest, name));
                                    }
                                }
                            }
                            else
                            {
                                // Guardar documentos para procesar después
                                documentItems.Add((item, id, name, mimeType));
                            }
                        }

                        // Esperar a que todas las carpetas terminen de procesarse en paralelo
                        if (folderTasks.Count > 0)
                        {
                            var folderResults = await Task.WhenAll(folderTasks);
                            foreach (var folderDocs in folderResults)
                            {
                                if (folderDocs != null)
                                {
                                    _logger.Log($"Adding {folderDocs.Count} documents from subfolder", LogLevel.DEBUG);
                                    docs.AddRange(folderDocs);
                                }
                            }
                        }

                        // Ahora procesar los documentos (mantenemos el procesamiento secuencial para evitar problemas)
                        foreach (var (item, id, name, mimeType) in documentItems)
                        {
                            _logger.Log($"Processing document: {name} (NodeId: {id})", LogLevel.DEBUG);

                            // Get document classification
                            string? DocTypeRule = await _csUtilities.GetClassifications(id, ticket);
                            string documentType;

                            if (string.IsNullOrEmpty(DocTypeRule))
                            {
                                documentType = CatName;
                                _logger.Log($"Using category name as document type: {documentType}", LogLevel.TRACE);
                            }
                            else
                            {
                                documentType = DocTypeRule;
                                _logger.Log($"Using classification as document type: {documentType}", LogLevel.TRACE);
                            }

                            // Add document to result list
                            docs.Add(new DocumentInfo
                            {
                                NodeId = id,
                                Name = name,
                                DocumentType = documentType,
                                ExpirationDate = null
                            });

                            _logger.Log($"Added document: {name} with type: {documentType}", LogLevel.DEBUG);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error parsing documents from response: {ex.Message}", LogLevel.ERROR);
            }

            _logger.Log($"Parsed {docs.Count} documents from response", LogLevel.INFO);
            return docs;
        }

        /// <summary>
        /// Parses folder information from a Business Workspace response JSON.
        /// </summary>
        /// <param name="json">JSON response from API</param>
        /// <param name="ticket">Authentication ticket (OTCSTICKET)</param>
        /// <param name="baseUrl">Base URL for the API</param>
        /// <param name="masterRequest">Filter for "Master" or "Request" nodes</param>
        /// <returns>List of DocumentInfo objects representing folders</returns>
        private async Task<List<DocumentInfo>> ParseFoldersFromBWAsync(string json, string ticket, string baseUrl, string masterRequest)
        {
            _logger.Log("Parsing folders from Business Workspace response JSON", LogLevel.DEBUG);

            var docs = new List<DocumentInfo>();
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty("results", out JsonElement results) &&
                        results.ValueKind == JsonValueKind.Array)
                    {
                        _logger.Log($"Found {results.GetArrayLength()} results to process", LogLevel.DEBUG);

                        foreach (var item in results.EnumerateArray())
                        {
                            long idInt = 0;
                            string idStr = "";
                            string? mimeType = null;
                            string name = null;
                            bool isContainer = false;
                            string? expDate = null;
                            string documentType = null;

                            // Extract properties from the item
                            if (item.TryGetProperty("data", out JsonElement data) &&
                                data.TryGetProperty("properties", out JsonElement props))
                            {
                                // Check if item is a container (folder)
                                if (props.TryGetProperty("container", out JsonElement containerElem))
                                {
                                    if (containerElem.ValueKind == JsonValueKind.True)
                                    {
                                        _logger.Log("Found a container (folder) node", LogLevel.TRACE);

                                        // Extract folder ID
                                        if (props.TryGetProperty("id", out JsonElement idElem))
                                        {
                                            if (idElem.ValueKind == JsonValueKind.Number)
                                            {
                                                idInt = idElem.GetInt64();
                                            }
                                            else if (idElem.ValueKind == JsonValueKind.String && long.TryParse(idElem.GetString(), out long parsedId))
                                            {
                                                idInt = parsedId;
                                            }
                                            idStr = idInt.ToString();
                                        }

                                        // Extract folder name
                                        name = props.TryGetProperty("name", out JsonElement nameElem)
                                            ? nameElem.GetString() ?? ""
                                            : "";

                                        _logger.Log($"Adding folder to list: {name} (NodeId: {idStr})", LogLevel.DEBUG);

                                        // Add folder to result list
                                        docs.Add(new DocumentInfo
                                        {
                                            NodeId = idStr,
                                            Name = name,
                                            DocumentType = null,
                                            ExpirationDate = null
                                        });
                                    }
                                }

                                // Check mime_type (for non-folder items)
                                mimeType = props.TryGetProperty("mime_type", out JsonElement mimeElem)
                                    ? mimeElem.GetString()
                                    : null;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error parsing folders from response: {ex.Message}", LogLevel.ERROR);
            }

            _logger.Log($"Parsed {docs.Count} folders from response", LogLevel.INFO);
            return docs;
        }

        /// <summary>
        /// Parses document information from a Business Workspace response JSON without expiration date category.
        /// Overload of ParseDocumentsFromBWAsync for use when expiration date is not needed.
        /// </summary>
        /// <param name="json">JSON response from API</param>
        /// <param name="ticket">Authentication ticket (OTCSTICKET)</param>
        /// <param name="baseUrl">Base URL for the API</param>
        /// <param name="masterRequest">Filter for "Master" or "Request" nodes</param>
        /// <param name="CatName">Optional category name for filtering</param>
        /// <returns>List of DocumentInfo objects</returns>
        private async Task<List<DocumentInfo>> ParseDocumentsFromBWAsync(string json, string ticket, string baseUrl, string masterRequest, string? CatName = null)
        {
            _logger.Log("Parsing documents from Business Workspace response JSON (no expDateCatId)", LogLevel.DEBUG);

            var docs = new List<DocumentInfo>();
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty("results", out JsonElement results) &&
                        results.ValueKind == JsonValueKind.Array)
                    {
                        _logger.Log($"Found {results.GetArrayLength()} results to process", LogLevel.DEBUG);

                        foreach (var item in results.EnumerateArray())
                        {
                            long idInt = 0;
                            string id = "";
                            string? mimeType = null;
                            string name = null;
                            string? expDate = null;
                            string documentType = null;

                            // Extract properties from the item
                            if (item.TryGetProperty("data", out JsonElement data) &&
                                data.TryGetProperty("properties", out JsonElement props))
                            {
                                // Extract common properties
                                if (props.TryGetProperty("id", out JsonElement idElem))
                                {
                                    if (idElem.ValueKind == JsonValueKind.Number)
                                    {
                                        idInt = idElem.GetInt64();
                                    }
                                    else if (idElem.ValueKind == JsonValueKind.String && long.TryParse(idElem.GetString(), out long parsedId))
                                    {
                                        idInt = parsedId;
                                    }
                                    id = idInt.ToString();
                                }

                                name = props.TryGetProperty("name", out JsonElement nameElem)
                                    ? nameElem.GetString() ?? ""
                                    : "";

                                // Check mime_type to determine if it's a document
                                mimeType = props.TryGetProperty("mime_type", out JsonElement mimeElem)
                                    ? mimeElem.GetString()
                                    : null;
                            }

                            // Process based on mime_type
                            if (string.IsNullOrEmpty(mimeType))
                            {
                                // For items with no mime_type (folders), recursively get contents
                                _logger.Log($"Node {id} ({name}) has no mime_type, checking if it's a folder to process", LogLevel.DEBUG);

                                if (string.Equals(masterRequest, "Master", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (!string.Equals(name, "Staging", StringComparison.OrdinalIgnoreCase))
                                    {
                                        _logger.Log($"Processing Master folder: {name} (NodeId: {id})", LogLevel.DEBUG);
                                        List<DocumentInfo> additionalDocs = await GetNodeSubNodesAsync(id, ticket, masterRequest, name);

                                        if (additionalDocs != null)
                                        {
                                            _logger.Log($"Adding {additionalDocs.Count} documents from subfolder {name}", LogLevel.DEBUG);
                                            docs.AddRange(additionalDocs);
                                        }
                                    }
                                }
                                else if (string.Equals(masterRequest, "Request", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (string.Equals(name, "Staging", StringComparison.OrdinalIgnoreCase))
                                    {
                                        _logger.Log($"Processing Request folder: {name} (NodeId: {id})", LogLevel.DEBUG);
                                        List<DocumentInfo> additionalDocs = await GetNodeSubNodesAsync(id, ticket, masterRequest, name);

                                        if (additionalDocs != null)
                                        {
                                            _logger.Log($"Adding {additionalDocs.Count} documents from subfolder {name}", LogLevel.DEBUG);
                                            docs.AddRange(additionalDocs);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // For items with mime_type (documents), add directly
                                _logger.Log($"Processing document: {name} (NodeId: {id})", LogLevel.DEBUG);

                                // Get document classification
                                string? DocTypeRule = await _csUtilities.GetClassifications(id, ticket);
                                if (string.IsNullOrEmpty(DocTypeRule))
                                {
                                    documentType = CatName;
                                    _logger.Log($"Using category name as document type: {documentType}", LogLevel.TRACE);
                                }
                                else
                                {
                                    documentType = DocTypeRule;
                                    _logger.Log($"Using classification as document type: {documentType}", LogLevel.TRACE);
                                }

                                // Add document to result list
                                docs.Add(new DocumentInfo
                                {
                                    NodeId = id,
                                    Name = name,
                                    DocumentType = documentType,
                                    ExpirationDate = expDate
                                });

                                _logger.Log($"Added document: {name} with type: {documentType}", LogLevel.DEBUG);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error parsing documents from response: {ex.Message}", LogLevel.ERROR);
            }

            _logger.Log($"Parsed {docs.Count} documents from response", LogLevel.INFO);
            return docs;
        }

        /// <summary>
        /// Moves a node to a different parent folder.
        /// </summary>
        /// <param name="nodeId">ID of the node to move</param>
        /// <param name="origBoFolderId">ID of the destination folder</param>
        /// <param name="ticket">Authentication ticket (OTCSTICKET)</param>
        /// <returns>True if the move was successful</returns>
        /// <exception cref="ArgumentException">Thrown when ticket is invalid or move fails</exception>
        public async Task<bool> MoveNodeAsync(string nodeId, string origBoFolderId, string ticket)
        {
            _logger.Log($"Starting MoveNodeAsync: nodeId={nodeId}, destinationFolder={origBoFolderId}", LogLevel.INFO);

            // Validate authentication ticket
            if (string.IsNullOrWhiteSpace(ticket))
            {
                _logger.Log("OTCS ticket is missing or empty", LogLevel.ERROR);
                throw new ArgumentException("OTCS ticket must be provided.", nameof(ticket));
            }

            // Build URL for node move API call
            var baseUrl = _settings.BaseUrl;
            var copyUrl = $"{baseUrl}/api/v2/nodes";
            _logger.Log($"Move URL: {copyUrl}", LogLevel.DEBUG);

            // Prepare move request body
            var nodeMove = new
            {
                original_id = nodeId,
                parent_id = origBoFolderId
            };

            // Serialize request to JSON
            string nodeCreationJson = JsonSerializer.Serialize(nodeMove);
            _logger.Log($"Move request body: {nodeCreationJson}", LogLevel.DEBUG);

            // Log raw API request
            _logger.LogRawApi("api_request_move_node", nodeCreationJson);

            // Create request with authentication ticket
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, copyUrl);
            requestMessage.Headers.Add("OTCSTICKET", ticket);

            // Build the multipart/form-data content
            using var formDataContent = new MultipartFormDataContent();

            // Part A: "body" part containing the JSON string
            var bodyContent = new StringContent(nodeCreationJson, Encoding.UTF8, "text/plain");
            formDataContent.Add(bodyContent, "body");

            // Attach the multipart content to the request
            requestMessage.Content = formDataContent;

            HttpResponseMessage response;
            try
            {
                // Send the request
                _logger.Log($"Sending request to move node {nodeId} to folder {origBoFolderId}", LogLevel.DEBUG);
                response = await _httpClient.SendAsync(requestMessage);

                // Log raw API response
                string responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogRawApi("api_response_move_node", responseContent);

                // Check for successful response
                if (!response.IsSuccessStatusCode)
                {
                    _logger.Log($"Failed to move node {nodeId}: {response.StatusCode} - {responseContent}", LogLevel.ERROR);
                    throw new ArgumentException("Node Move didn't work.", nameof(ticket));
                }

                _logger.Log($"Successfully moved node {nodeId} to folder {origBoFolderId}", LogLevel.INFO);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                throw new ArgumentException("Node Move didn't work.", nameof(ticket));
            }
        }

        /// <summary>
        /// Creates a new folder under a specified parent node.
        /// </summary>
        /// <param name="nodeId">ID of the parent node</param>
        /// <param name="folderName">Name for the new folder</param>
        /// <param name="ticket">Authentication ticket (OTCSTICKET)</param>
        /// <returns>ID of the newly created folder</returns>
        /// <exception cref="ArgumentException">Thrown when ticket is invalid or folder creation fails</exception>
        public async Task<int> CreateFolderAsync(string nodeId, string folderName, string ticket)
        {
            _logger.Log($"Starting CreateFolderAsync: parentNodeId={nodeId}, folderName={folderName}", LogLevel.INFO);

            // Validate authentication ticket
            if (string.IsNullOrWhiteSpace(ticket))
            {
                _logger.Log("OTCS ticket is missing or empty", LogLevel.ERROR);
                throw new ArgumentException("OTCS ticket must be provided.", nameof(ticket));
            }

            // Build URL for folder creation API call
            var baseUrl = _settings.BaseUrl;
            var copyUrl = $"{baseUrl}/api/v2/nodes";
            _logger.Log($"Create folder URL: {copyUrl}", LogLevel.DEBUG);

            // Prepare folder creation request body
            var nodeMove = new
            {
                type = "0",
                parent_id = nodeId,
                name = folderName
            };

            // Serialize request to JSON
            string nodeCreationJson = JsonSerializer.Serialize(nodeMove);
            _logger.Log($"Create folder request body: {nodeCreationJson}", LogLevel.DEBUG);

            // Log raw API request
            _logger.LogRawApi("api_request_create_folder", nodeCreationJson);

            // Create request with authentication ticket
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, copyUrl);
            requestMessage.Headers.Add("OTCSTICKET", ticket);

            // Build the multipart/form-data content
            using var formDataContent = new MultipartFormDataContent();

            // Part A: "body" part containing the JSON string
            var bodyContent = new StringContent(nodeCreationJson, Encoding.UTF8, "text/plain");
            formDataContent.Add(bodyContent, "body");

            // Attach the multipart content to the request
            requestMessage.Content = formDataContent;

            HttpResponseMessage response;
            try
            {
                // Send the request
                _logger.Log($"Sending request to create folder '{folderName}' under node {nodeId}", LogLevel.DEBUG);
                response = await _httpClient.SendAsync(requestMessage);

                // Read response content
                var wsChildNodesJson = await response.Content.ReadAsStringAsync();

                // Log raw API response
                _logger.LogRawApi("api_response_create_folder", wsChildNodesJson);

                // Check for successful response
                if (!response.IsSuccessStatusCode)
                {
                    _logger.Log($"Failed to create folder: {response.StatusCode} - {wsChildNodesJson}", LogLevel.ERROR);
                    throw new Exception($"Error Creating Folder {response.StatusCode}: {wsChildNodesJson}");
                }

                // Parse folder creation response
                _logger.Log("Parsing folder creation response", LogLevel.DEBUG);
                var folderResponse = JsonSerializer.Deserialize<FolderResponse>(wsChildNodesJson);

                int newFolderNodeId = 0;
                // Uncomment and implement when FolderResponse class is available
                // newFolderNodeId = folderResponse?.FolderCreationResponse.results?.data?.properties?.id
                //              ?? throw new Exception("No se pudo obtener el NodeId del folder creado.");

                _logger.Log($"Folder '{folderName}' created successfully with ID: {newFolderNodeId}", LogLevel.INFO);
                return newFolderNodeId;
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                throw new ArgumentException("Create folder operation failed.", nameof(ticket));
            }
        }
    }
}
