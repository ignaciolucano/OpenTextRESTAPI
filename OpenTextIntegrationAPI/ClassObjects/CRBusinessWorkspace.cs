using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OpenTextIntegrationAPI.Models;
using OpenTextIntegrationAPI.Services;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;

// This file contains the implementation for Change Request Business Workspace operations
// It handles creation, search, and management of CR workspaces in OpenText Content Server

namespace OpenTextIntegrationAPI.ClassObjects
{
    /// <summary>
    /// Manages Change Request Business Workspaces in OpenText Content Server.
    /// Provides methods for creating, searching, and updating CR workspaces,
    /// as well as handling document operations within these workspaces.
    /// </summary>
    public class CRBusinessWorkspace
    {
        private readonly OpenTextSettings _settings;
        private readonly HttpClient _httpClient;
        private readonly MasterData _masterData;
        private readonly CSUtilities _csUtilities;
        private readonly Node _csNode;
        private readonly ILogService _logger;

        /// <summary>
        /// Initializes a new instance of the CRBusinessWorkspace class with required dependencies.
        /// Uses dependency injection to receive all necessary services.
        /// </summary>
        /// <param name="settings">Configuration settings for OpenText API</param>
        /// <param name="masterData">Service for master data operations</param>
        /// <param name="httpClient">HTTP client for API calls</param>
        /// <param name="csUtilities">Utility methods for Content Server operations</param>
        /// <param name="csNode">Service for node operations in Content Server</param>
        /// <param name="logger">Logging service for tracking operations</param>
        public CRBusinessWorkspace(IOptions<OpenTextSettings> settings,
                                   MasterData masterData,
                                   HttpClient httpClient,
                                   CSUtilities csUtilities,
                                   Node csNode,
                                   ILogService logger)
        {
            _settings = settings.Value;
            _masterData = masterData;
            _httpClient = httpClient;
            _csUtilities = csUtilities;
            _csNode = csNode;
            _logger = logger;

            // Log initialization of the CRBusinessWorkspace service
            _logger.Log("CRBusinessWorkspace service initialized", LogLevel.DEBUG);
        }

        /// <summary>
        /// Validates and formats the Business Object parameters.
        /// Throws exception if boType or boId is invalid.
        /// </summary>
        /// <param name="boType">Business Object type (must be BUS2250 for CR workspaces)</param>
        /// <param name="boId">Business Object ID (will be padded according to standards)</param>
        /// <returns>Tuple containing validated boType and formatted boId</returns>
        /// <exception cref="ArgumentException">Thrown when parameters are invalid</exception>
        public static (string validatedBoType, string formattedBoId) ValidateAndFormatBoParams(string boType, string boId)
        {
            // Validate business object type - must be BUS2250 for Change Requests
            if (boType != "BUS2250")
                throw new ArgumentException("Invalid boType");

            // Ensure business object ID is not empty
            if (string.IsNullOrWhiteSpace(boId))
                throw new ArgumentException("boId cannot be empty.");

            // Pad boId based on standard for this type
            // For BUS2250 (Change Requests), we pad to 12 digits
            if (boType.Equals("BUS2250", StringComparison.OrdinalIgnoreCase))
                boId = boId.PadLeft(12, '0');

            return (boType, boId);
        }

        /// <summary>
        /// Searches for a Business Workspace by formatted BO ID
        /// Logs request/response and parses the workspace ID from result
        /// </summary>
        /// <param name="httpClient">HTTP client for making the request</param>
        /// <param name="formattedBoId">Properly formatted business object ID</param>
        /// <param name="ticket">Authentication ticket (OTCSTICKET)</param>
        /// <returns>String containing the workspace ID if found</returns>
        /// <exception cref="Exception">Thrown when search fails or workspace not found</exception>
        public async Task<string> SearchBusinessWorkspaceAsync(HttpClient httpClient, string formattedBoId, string ticket)
        {
            _logger.Log($"Starting search for business workspace with BO ID: {formattedBoId}", LogLevel.INFO);

            // Construct the URL with query parameters for the OpenText API
            string baseUrl = _settings.BaseUrl;
            string extSystemId = ""; // _settings.ExtSystemId;
            string url = $"{baseUrl}/api/v2/businessworkspaces?where_bo_type=BUS2250&where_column_query=name LIKE '{formattedBoId} -*'&where_ext_system_id={extSystemId}&expanded_view=true";

            // Create HTTP request with authentication ticket
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("OTCSTICKET", ticket);

            // Log the request details
            _logger.Log($"[SearchBusinessWorkspaceAsync] Request URL: {url}", LogLevel.DEBUG);

            // Log raw API request if enabled in settings
            _logger.LogRawOutbound("request_search_workspace", JsonSerializer.Serialize(new { formattedBoId, extSystemId, url }));

            // Execute the request
            _logger.Log("Sending HTTP request to OpenText API", LogLevel.TRACE);
            var response = await httpClient.SendAsync(request);

            // Read response content
            string json = await response.Content.ReadAsStringAsync();

            // Log raw API response if enabled in settings
            _logger.LogRawOutbound("response_search_workspace", json);

            // Check for successful response
            if (!response.IsSuccessStatusCode)
            {
                _logger.Log($"[SearchBusinessWorkspaceAsync] Error: Status {response.StatusCode}, Body: {json}", LogLevel.ERROR);
                throw new Exception($"Business Workspace search failed with status {response.StatusCode}");
            }

            // Parse response to extract workspace ID
            try
            {
                _logger.Log("Parsing JSON response to extract workspace ID", LogLevel.TRACE);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("results", out var resultsElem) &&
                    resultsElem.ValueKind == JsonValueKind.Array &&
                    resultsElem.GetArrayLength() > 0)
                {
                    var first = resultsElem[0];
                    if (first.TryGetProperty("data", out var dataElem) &&
                        dataElem.TryGetProperty("properties", out var propsElem))
                    {
                        var id = propsElem.GetProperty("id").GetInt32().ToString();
                        _logger.Log($"[SearchBusinessWorkspaceAsync] Found Workspace ID: {id}", LogLevel.INFO);
                        return id;
                    }
                }

                _logger.Log("No workspace found in response JSON", LogLevel.WARNING);
                throw new Exception("No workspace found in response.");
            }
            catch (Exception ex)
            {
                // Log the exception with full details
                _logger.LogException(ex, LogLevel.ERROR);
                throw;
            }
        }

        /// <summary>
        /// Search for the CR (Change Request) business workspace with expanded details.
        /// Returns the full workspace response object with all properties.
        /// </summary>
        /// <param name="httpClient">HTTP client for making the request</param>
        /// <param name="boType">Business Object type (must be BUS2250)</param>
        /// <param name="boId">Business Object ID</param>
        /// <param name="ticket">Authentication ticket (OTCSTICKET)</param>
        /// <returns>BusinessWorkspaceResponse object with full workspace details</returns>
        /// <exception cref="Exception">Thrown when search fails</exception>
        public async Task<BusinessWorkspaceResponse?> SearchCRBusinessWorkspaceAsync(HttpClient httpClient, string boType, string boId, string ticket)
        {
            _logger.Log($"Starting search for CR business workspace. BO Type: {boType}, BO ID: {boId}", LogLevel.INFO);

            // Validate and format parameters according to business rules
            (string validatedBoType, string formattedBoId) = ValidateAndFormatBoParams(boType, boId);
            _logger.Log($"Parameters validated. Using formatted BO ID: {formattedBoId}", LogLevel.DEBUG);

            // Construct the URL with query parameters
            //string url = $"{_settings.BaseUrl}/api/v2/businessworkspaces?where_bo_type={boType}&where_column_query=name LIKE '{formattedBoId} -*'&where_ext_system_id={_settings.ExtSystemId}&expanded_view=true";
            string url = $"{_settings.BaseUrl}/api/v2/businessworkspaces?where_bo_type={boType}&where_column_query=name LIKE '{formattedBoId} -*'&where_ext_system_id=&expanded_view=true";

            // Create HTTP request with authentication ticket
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("OTCSTICKET", ticket);

            // Log the request details
            _logger.Log($"[SearchCRBusinessWorkspaceAsync] Requesting CR Business Workspace from: {url}", LogLevel.INFO);

            // Log raw API request if enabled in settings
            _logger.LogRawOutbound("request_search_cr_workspace",
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    boType,
                    formattedBoId,
                    url
                }));

            // Execute the request
            _logger.Log("Sending HTTP request to OpenText API", LogLevel.TRACE);
            var response = await httpClient.SendAsync(request);

            // Read response content
            var json = await response.Content.ReadAsStringAsync();

            // Log raw API response
            _logger.LogRawOutbound("response_search_cr_workspace", json);

            // Check for successful response
            if (!response.IsSuccessStatusCode)
            {
                _logger.Log($"[SearchCRBusinessWorkspaceAsync] Error: {response.StatusCode} - {json}", LogLevel.ERROR);
                throw new Exception($"SearchCRBusinessWorkspaceAsync failed with status {response.StatusCode}");
            }

            // Deserialize response to BusinessWorkspaceResponse object
            try
            {
                _logger.Log("Deserializing JSON response to BusinessWorkspaceResponse", LogLevel.DEBUG);
                var result = JsonSerializer.Deserialize<BusinessWorkspaceResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                _logger.Log($"[SearchCRBusinessWorkspaceAsync] Deserialized response successfully", LogLevel.DEBUG);
                return result;
            }
            catch (Exception ex)
            {
                // Log the exception with full details
                _logger.LogException(ex, LogLevel.ERROR);
                throw;
            }
        }

        /// <summary>
        /// Retrieves documents associated with the Change Request Business Workspace.
        /// Searches for the workspace and gets all document nodes with appropriate categories.
        /// </summary>
        /// <param name="boType">Business Object type</param>
        /// <param name="boId">Business Object ID</param>
        /// <param name="ticket">Authentication ticket (OTCSTICKET)</param>
        /// <returns>MasterDataDocumentsResponse containing workspace info and documents</returns>
        /// <exception cref="Exception">Thrown when retrieval fails</exception>
        public async Task<ChangeRequestDocumentsResponse?> GetDocumentsChangeRequestAsync(string boType, string boId, string ticket)
        {
            _logger.Log("[GetDocumentsChangeRequestAsync] Starting document retrieval", LogLevel.INFO);

            // Validate authentication ticket
            if (string.IsNullOrEmpty(ticket))
            {
                _logger.Log("[GetDocumentsChangeRequestAsync] Ticket is null or empty", LogLevel.ERROR);
                throw new Exception("Authentication failed: OTCS ticket is empty.");
            }

            // Create helper service for workspace operations
            _logger.Log("Creating BusinessWorkspaceService instance", LogLevel.TRACE);
            var workspaceService = new BusinessWorkspaceService(_httpClient, ticket, _masterData, _settings, _csUtilities, _csNode,_logger);

            // Search for the workspace by business object parameters
            _logger.Log($"Searching for CR workspace with BO Type: {boType}, BO ID: {boId}", LogLevel.DEBUG);
            var wsResponse = await SearchCRBusinessWorkspaceAsync(_httpClient, boType, boId, ticket);
            string? workspaceNodeId = null;
            string? workspaceName = null;

            // Process the search response if workspace found
            if (wsResponse != null && wsResponse.results.Count > 0)
            {
                _logger.Log("[GetDocumentsChangeRequestAsync] Workspace found. Processing...", LogLevel.DEBUG);

                // Extract workspace properties from response
                var first = wsResponse.results[0].data.properties;
                workspaceNodeId = first.id.ToString();
                workspaceName = first.name;
                _logger.Log($"Found workspace. Node ID: {workspaceNodeId}, Name: {workspaceName}", LogLevel.DEBUG);

                // Get expiration date category ID for document filtering
                var expDateCatId = await _csUtilities.GetExpirationDateCatIdAsync(ticket);
                _logger.Log("[GetDocumentsChangeRequestAsync] Retrieved expiration date category ID", LogLevel.DEBUG);

                // Get documents from the workspace with expiration date category
                _logger.Log($"Retrieving documents from workspace node {workspaceNodeId}", LogLevel.DEBUG);
                var documents = await _csNode.CRGetNodeSubNodesAsync(workspaceNodeId, ticket, expDateCatId, "Request", null);
                _logger.Log($"[GetDocumentsChangeRequestAsync] Retrieved {documents?.Count} documents", LogLevel.INFO);

                // Construct and return the response object
                return new ChangeRequestDocumentsResponse
                {
                    Header = new MasterDataDocumentsHeader
                    {
                        BoType = boType,
                        BoId = boId,
                        BwName = workspaceName,
                        docCount = documents?.Count.ToString()
                    },
                    Files = documents
                };
            }
            else
            {
                _logger.Log("[GetDocumentsChangeRequestAsync] Workspace not found or created", LogLevel.ERROR);
                throw new Exception("Business Workspace not found or created.");
            }
        }

        /// <summary>
        /// Updates the Change Request Workspace including classification or status changes.
        /// Optionally moves documents if approved.
        /// </summary>
        /// <param name="boType">Business Object type</param>
        /// <param name="boId">Business Object ID</param>
        /// <param name="ticket">Authentication ticket (OTCSTICKET)</param>
        /// <param name="updateRequest">Update request with new values</param>
        /// <param name="newStatus">Optional new status (e.g., "APPROVED")</param>
        /// <returns>Response indicating success or failure</returns>
        /// <exception cref="Exception">Thrown when update fails</exception>
        public async Task<ChangeRequestUpdateResponse> UpdateChangeRequestDataAsync(string boType, string boId, string ticket, DTOs.ChangeRequestUpdateRequest updateRequest, string? newStatus = "")
        {
            _logger.Log("[UpdateChangeRequestDataAsync] Start processing CR update", LogLevel.INFO);
            _logger.Log($"BO Type: {boType}, BO ID: {boId}, New Status: {newStatus ?? "None"}", LogLevel.DEBUG);

            // Validate authentication ticket
            if (string.IsNullOrEmpty(ticket))
            {
                _logger.Log("[UpdateChangeRequestDataAsync] Ticket is empty", LogLevel.ERROR);
                throw new Exception("Authentication failed: OTCS ticket is empty.");
            }

            // Create helper services for workspace operations
            _logger.Log("Creating BusinessWorkspaceService instance", LogLevel.TRACE);
            var workspaceService = new BusinessWorkspaceService(_httpClient, ticket, _masterData, _settings, _csUtilities, _csNode, _logger);

            // Search for existing workspace
            _logger.Log("Searching for existing CR workspace", LogLevel.DEBUG);
            var wsResponse = await SearchCRBusinessWorkspaceAsync(_httpClient, boType, boId, ticket);

            string? workspaceNodeId = null;
            string? workspaceName = null;

            // Process search results
            if (wsResponse != null && wsResponse.results.Count > 0)
            {
                // Extract workspace properties
                var first = wsResponse.results[0].data.properties;
                workspaceNodeId = first.id.ToString();
                workspaceName = first.name;
                _logger.Log($"[UpdateChangeRequestDataAsync] Workspace found: {workspaceNodeId}", LogLevel.DEBUG);

                // If status is APPROVED, handle document movement
                if (newStatus == "APPROVED")
                {
                    _logger.Log("[UpdateChangeRequestDataAsync] Status is APPROVED - moving documents", LogLevel.INFO);

                    // Get all master documents from the workspace
                    _logger.Log("Retrieving master documents from workspace", LogLevel.DEBUG);
                    var documents = await _csNode.GetNodeSubNodesAsync(workspaceNodeId, ticket, "Master", null);
                    _logger.Log($"[UpdateChangeRequestDataAsync] Found {documents.Count} documents to move", LogLevel.DEBUG);

                    // Determine template based on business object type
                    string boTypeTemplate = updateRequest.MainBOType switch
                    {
                        "BUS1001006" => _settings.uNameTIBUS1001006,
                        "BUS1001001" => _settings.uNameTIBUS1001001,
                        "BUS1006" => _settings.uNameTIBUS1006,
                        _ => ""
                    };
                    _logger.Log($"Selected template: {boTypeTemplate} for BO type: {updateRequest.MainBOType}", LogLevel.DEBUG);

                    // Process each document for movement
                    foreach (DocumentInfo document in documents)
                    {
                        _logger.Log($"Processing document: {document.Name}", LogLevel.DEBUG);

                        // Validate document ID
                        if (!int.TryParse(document.NodeId, out int documentId))
                        {
                            _logger.Log($"Invalid document node ID: {document.NodeId}", LogLevel.WARNING);
                            continue;
                        }

                        // Get document classifications
                        _logger.Log($"Getting classifications for document {documentId}", LogLevel.TRACE);
                        var nodeDoc = await _csUtilities.GetNodeClassifications(documentId, ticket);
                        int? classificationId = nodeDoc?.Data?.FirstOrDefault()?.Id;

                        // Check if RM should be applied - handle string value from settings
                        _logger.Log($"EnableRecordsManagement setting value: '{_settings.EnableRecordsManagement}'", LogLevel.DEBUG);

                        bool enableRM = false;
                        // Handle different possible types of the configuration value
                        if (_settings.EnableRecordsManagement is bool boolValue)
                        {
                            enableRM = boolValue;
                        }
                        else
                        {
                            // Convertir a string y luego verificar si es "true"
                            string strValue = _settings.EnableRecordsManagement?.ToString() ?? "false";
                            enableRM = strValue.Equals("true", StringComparison.OrdinalIgnoreCase);
                        }

                        _logger.Log($"EnableRecordsManagement computed value: {enableRM}", LogLevel.DEBUG);

                        // Apply Records Management classification if explicitly enabled
                        if (enableRM)
                        {
                            _logger.Log($"Records Management is enabled - applying RM classifications", LogLevel.DEBUG);
                            await _csUtilities.ApplyRecordsManagementClassification(documentId, ticket);
                        }
                        else
                        {
                            _logger.Log("Records Management is disabled - skipping RM classifications", LogLevel.DEBUG);
                        }

                        // Determine destination folder based on document type
                        string trimmedBo = updateRequest.MainBOType.Length >= 7 ? updateRequest.MainBOType.Substring(0, 7) : updateRequest.MainBOType;
                        string docTypeString = $"{trimmedBo}.{classificationId}";
                        string strFolder = _csUtilities.GetDocTypeName(docTypeString);
                        _logger.Log($"Document type: {docTypeString}, Target folder: {strFolder}", LogLevel.DEBUG);

                        // Search for the original business workspace
                        _logger.Log($"Searching for original workspace with BO Type: {updateRequest.MainBOType}, BO ID: {updateRequest.MainBOId}", LogLevel.DEBUG);
                        var wsOriginalBW = await _masterData.SearchBusinessWorkspaceAsync(updateRequest.MainBOType, updateRequest.MainBOId, ticket);
                        string? originalBWnodeId = wsOriginalBW?.results?.FirstOrDefault()?.data?.properties.id.ToString();

                        // If original workspace doesn't exist, create it
                        if (string.IsNullOrEmpty(originalBWnodeId))
                        {
                            _logger.Log($"Original Business Workspace not found. Creating new one for {updateRequest.MainBOType}/{updateRequest.MainBOId}", LogLevel.INFO);
                            var createResponse = await workspaceService.CreateBusinessWorkspaceAsync(updateRequest.MainBOType, updateRequest.MainBOId);

                            if (createResponse != null && createResponse.results != null && createResponse.results.Count > 0)
                            {
                                // Acceder al ID usando la estructura correcta
                                originalBWnodeId = createResponse.results[0].data.properties.id.ToString();
                                _logger.Log($"Created new Business Workspace with ID: {originalBWnodeId}", LogLevel.INFO);
                            }
                            else
                            {
                                _logger.Log("[UpdateChangeRequestDataAsync] Failed to create original Business Workspace", LogLevel.ERROR);
                                throw new Exception("Failed to create original Business Workspace.");
                            }
                        }

                        if (!string.IsNullOrEmpty(originalBWnodeId))
                        {
                            // Get folders from the original workspace
                            _logger.Log($"Getting subfolders for original workspace: {originalBWnodeId}", LogLevel.DEBUG);
                            var oriBWFolders = await _csNode.GetNodeSubFoldersAsync(originalBWnodeId, ticket, "Master");
                            string? folderNodeId = oriBWFolders.FirstOrDefault(f => f.Name == strFolder)?.NodeId;

                            // If target folder exists
                            if (!string.IsNullOrEmpty(folderNodeId))
                            {
                                // Check if a document with same name already exists in the target folder
                                _logger.Log($"Checking for existing document with same name in target folder", LogLevel.DEBUG);
                                var existingDocs = await _csNode.GetNodeSubNodesAsync(folderNodeId, ticket, null, document.Name);
                                var existingDoc = existingDocs.FirstOrDefault(d => d.Name == document.Name);

                                if (existingDoc != null)
                                {
                                    // Document exists - add as a new version using API v2
                                    _logger.Log($"Document '{document.Name}' already exists in target folder (ID: {existingDoc.NodeId}). Adding as new version.", LogLevel.INFO);

                                    bool versionAdded = await AddDocumentAsVersionAsync(document.NodeId, existingDoc.NodeId, ticket);

                                    if (!versionAdded)
                                    {
                                        _logger.Log($"Failed to add version to existing document {existingDoc.NodeId}", LogLevel.ERROR);
                                        throw new Exception($"Could not add version to existing document ({document.Name}).");
                                    }
                                }
                                else
                                {
                                    // No existing document - move the document
                                    _logger.Log($"[UpdateChangeRequestDataAsync] Moving document {document.Name} to {strFolder}", LogLevel.DEBUG);
                                    bool resultMove = await _csNode.MoveNodeAsync(document.NodeId, folderNodeId, ticket);

                                    if (!resultMove)
                                    {
                                        _logger.Log($"[UpdateChangeRequestDataAsync] Failed to move document {document.Name}", LogLevel.ERROR);
                                        throw new Exception($"Could not move node ({document.Name}) to folder.");
                                    }
                                }
                            }
                            // If target folder doesn't exist but creation is allowed, create and move
                            else if (_settings.CreateFolderOnMove)
                            {
                                _logger.Log($"[UpdateChangeRequestDataAsync] Folder '{strFolder}' not found. Creating it...", LogLevel.INFO);
                                int newFolderId = await _csNode.CreateFolderAsync(originalBWnodeId, strFolder, ticket);
                                _logger.Log($"Created new folder with ID: {newFolderId}", LogLevel.DEBUG);

                                bool resultMove = await _csNode.MoveNodeAsync(document.NodeId, newFolderId.ToString(), ticket);

                                if (!resultMove)
                                {
                                    _logger.Log($"[UpdateChangeRequestDataAsync] Failed to move document {document.Name} after creating folder", LogLevel.ERROR);
                                    throw new Exception($"Could not move node ({document.Name}) to newly created folder.");
                                }
                            }
                            // If folder doesn't exist and creation not allowed, log warning
                            else
                            {
                                _logger.Log($"[UpdateChangeRequestDataAsync] Folder '{strFolder}' not found and creation not allowed", LogLevel.WARNING);
                            }
                        }
                        else
                        {
                            _logger.Log("[UpdateChangeRequestDataAsync] Could not get original business workspace node ID", LogLevel.ERROR);
                            throw new Exception("Original Business Workspace not found.");
                        }
                    }
                }

                // Update the workspace with new values
                _logger.Log($"Updating workspace {workspaceNodeId} with new values", LogLevel.DEBUG);
                bool updated = await workspaceService.UpdateBusinessWorkspaceCRAsync(boType, boId, workspaceNodeId, ticket, updateRequest, newStatus);
                if (updated)
                {
                    _logger.Log("[UpdateChangeRequestDataAsync] Workspace updated successfully", LogLevel.INFO);
                    return new ChangeRequestUpdateResponse { Message = "(OK) Workspace Updated" };
                }
                else
                {
                    _logger.Log("[UpdateChangeRequestDataAsync] Update failed", LogLevel.ERROR);
                    throw new Exception("Business Workspace update failed.");
                }
            }
            else
            {
                // If workspace not found, create new one
                _logger.Log("[UpdateChangeRequestDataAsync] Workspace not found. Creating new one...", LogLevel.INFO);
                var wsCreateResponse = await workspaceService.CreateBusinessWorkspaceCRAsync(boType, boId, ticket, updateRequest);
                if (wsCreateResponse != null)
                {
                    _logger.Log("[UpdateChangeRequestDataAsync] New workspace created successfully", LogLevel.INFO);
                    return new ChangeRequestUpdateResponse { Message = $"(OK) Workspace Created: {wsCreateResponse}" };
                }
                else
                {
                    _logger.Log("[UpdateChangeRequestDataAsync] Workspace creation failed", LogLevel.ERROR);
                    throw new Exception("Business Workspace creation failed.");
                }
            }
        }

        /// <summary>
        /// Adds a document as a new version to an existing document using the OpenText API v2
        /// </summary>
        /// <param name="sourceNodeId">ID of the document to add as a new version</param>
        /// <param name="targetNodeId">ID of the existing document that will receive the new version</param>
        /// <param name="ticket">Authentication ticket (OTCSTICKET)</param>
        /// <returns>True if version was added successfully</returns>
        private async Task<bool> AddDocumentAsVersionAsync(string sourceNodeId, string targetNodeId, string ticket)
        {
            try
            {
                _logger.Log($"Adding document {sourceNodeId} as new version to document {targetNodeId} using API v2", LogLevel.INFO);

                // First, get the metadata of the source document to determine its type
                string metadataUrl = $"{_settings.BaseUrl}/api/v2/nodes/{sourceNodeId}";
                using var metadataRequest = new HttpRequestMessage(HttpMethod.Get, metadataUrl);
                metadataRequest.Headers.Add("OTCSTicket", ticket);

                var metadataResponse = await _httpClient.SendAsync(metadataRequest);
                if (!metadataResponse.IsSuccessStatusCode)
                {
                    _logger.Log($"Failed to get document metadata: {metadataResponse.StatusCode}", LogLevel.ERROR);
                    return false;
                }

                string metadataJson = await metadataResponse.Content.ReadAsStringAsync();
                string fileName = "document.bin";
                string mimeType = "application/octet-stream";
                bool isPdf = false;

                // Log raw JSON for debugging purposes
                _logger.Log($"Document metadata JSON preview: {metadataJson.Substring(0, Math.Min(200, metadataJson.Length))}...", LogLevel.DEBUG);

                try
                {
                    using var jsonDoc = System.Text.Json.JsonDocument.Parse(metadataJson);
                    var root = jsonDoc.RootElement;

                    // Fix: Correctly navigate the JSON structure based on actual format
                    if (root.TryGetProperty("results", out var resultsObj))
                    {
                        // results is an object, not an array
                        if (resultsObj.TryGetProperty("data", out var dataObj))
                        {
                            // Extract file name from properties
                            if (dataObj.TryGetProperty("properties", out var props) &&
                                props.TryGetProperty("name", out var name))
                            {
                                fileName = name.GetString() ?? fileName;
                                _logger.Log($"Got file name from metadata: {fileName}", LogLevel.DEBUG);

                                // Check if this is a PDF based on file extension
                                isPdf = fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
                            }

                            // Extract mime type from properties
                            if (dataObj.TryGetProperty("properties", out var propsForMime) &&
                                propsForMime.TryGetProperty("mime_type", out var mime))
                            {
                                mimeType = mime.GetString() ?? mimeType;
                                _logger.Log($"Got MIME type from properties: {mimeType}", LogLevel.DEBUG);

                                // Also check if this is a PDF based on mime type
                                if (mimeType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                                {
                                    isPdf = true;
                                }
                            }

                            // Also try to get from versions array if available
                            if (dataObj.TryGetProperty("versions", out var versions) &&
                                versions.ValueKind == System.Text.Json.JsonValueKind.Array &&
                                versions.GetArrayLength() > 0)
                            {
                                var firstVersion = versions[0];

                                // Try to get mime type from version
                                if (firstVersion.TryGetProperty("mime_type", out var versionMime))
                                {
                                    string versionMimeType = versionMime.GetString() ?? mimeType;
                                    if (!string.IsNullOrEmpty(versionMimeType))
                                    {
                                        mimeType = versionMimeType;
                                        _logger.Log($"Got MIME type from version: {mimeType}", LogLevel.DEBUG);

                                        // Update PDF flag if mime type indicates PDF
                                        if (mimeType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                                        {
                                            isPdf = true;
                                        }
                                    }
                                }

                                // Try to get file name from version
                                if (firstVersion.TryGetProperty("file_name", out var versionFileName))
                                {
                                    string versionName = versionFileName.GetString() ?? fileName;
                                    if (!string.IsNullOrEmpty(versionName))
                                    {
                                        fileName = versionName;
                                        _logger.Log($"Got file name from version: {fileName}", LogLevel.DEBUG);

                                        // Update PDF flag if file extension indicates PDF
                                        isPdf = fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log($"Error parsing metadata: {ex.Message}", LogLevel.WARNING);
                    // Continue with default values if parsing fails
                }

                // Log PDF status and file info
                _logger.Log($"Document is PDF: {isPdf}, File name: {fileName}, MIME type: {mimeType}", LogLevel.INFO);

                // Now download the content of the source document
                string contentUrl = $"{_settings.BaseUrl}/api/v2/nodes/{sourceNodeId}/content";
                using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, contentUrl);
                downloadRequest.Headers.Add("OTCSTicket", ticket);

                var downloadResponse = await _httpClient.SendAsync(downloadRequest);
                if (!downloadResponse.IsSuccessStatusCode)
                {
                    string errorBody = await downloadResponse.Content.ReadAsStringAsync();
                    _logger.Log($"Failed to download document content: {downloadResponse.StatusCode} - {errorBody}", LogLevel.ERROR);
                    return false;
                }

                // Get the document content as byte array
                byte[] fileContent = await downloadResponse.Content.ReadAsByteArrayAsync();
                _logger.Log($"Successfully downloaded document content, size: {fileContent.Length} bytes", LogLevel.INFO);

                // Try API v1 first for PDF files - sometimes v2 has issues with PDFs
                if (isPdf)
                {
                    _logger.Log("Detected PDF file, trying API v1 first", LogLevel.INFO);
                    bool v1Success = await TryAddVersionWithV1Api(targetNodeId, fileContent, fileName, mimeType, ticket);
                    if (v1Success)
                    {
                        return true;
                    }
                    _logger.Log("API v1 attempt failed, will try v2", LogLevel.WARNING);
                }

                // Create the version URL for API v2
                string versionUrl = $"{_settings.BaseUrl}/api/v2/nodes/{targetNodeId}/versions";

                // Create a MultipartFormDataContent for the request
                using var multipartContent = new MultipartFormDataContent();

                // Add the file content
                using var fileStreamContent = new ByteArrayContent(fileContent);
                fileStreamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);

                // Add the file with explicit name parameter
                multipartContent.Add(fileStreamContent, "file", fileName);

                // Add description
                multipartContent.Add(new StringContent("true"), "add_major_version");
                multipartContent.Add(new StringContent($"Added from Change Request on {DateTime.Now:yyyy-MM-dd HH:mm:ss}"), "description");

                // Create the HTTP request
                using var versionRequest = new HttpRequestMessage(HttpMethod.Post, versionUrl);
                versionRequest.Headers.Add("OTCSTicket", ticket);

                // Remove any traceparent header if present
                if (versionRequest.Headers.Contains("traceparent"))
                {
                    versionRequest.Headers.Remove("traceparent");
                }

                versionRequest.Content = multipartContent;

                // Log request details
                _logger.Log($"Sending version request to URL: {versionUrl}", LogLevel.DEBUG);
                _logger.Log($"Content type: {mimeType}, File name: {fileName}, Size: {fileContent.Length} bytes", LogLevel.DEBUG);

                // Send the request
                var versionResponse = await _httpClient.SendAsync(versionRequest);

                // Read the response
                string responseContent = await versionResponse.Content.ReadAsStringAsync();

                // Check if the request was successful
                if (versionResponse.IsSuccessStatusCode)
                {
                    _logger.Log($"Successfully added document {sourceNodeId} as new version to {targetNodeId} using API v2", LogLevel.INFO);
                    return true;
                }
                else
                {
                    _logger.Log($"Failed to add version using API v2: {versionResponse.StatusCode} - {responseContent}", LogLevel.ERROR);

                    // If v2 fails and we haven't tried v1 yet (for non-PDFs), try v1 as fallback
                    if (!isPdf)
                    {
                        _logger.Log("API v2 failed, trying API v1 as fallback", LogLevel.WARNING);
                        return await TryAddVersionWithV1Api(targetNodeId, fileContent, fileName, mimeType, ticket);
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex);
                _logger.Log($"Exception adding document as version using API v2: {ex.Message}", LogLevel.ERROR);
                return false;
            }
        }

        /// <summary>
        /// Attempts to add a version using the API v1 endpoint
        /// </summary>
        private async Task<bool> TryAddVersionWithV1Api(string targetNodeId, byte[] fileContent, string fileName, string mimeType, string ticket)
        {
            try
            {
                _logger.Log($"Trying to add version using API v1 for node {targetNodeId}", LogLevel.INFO);

                // Create the version URL for API v1
                string versionUrl = $"{_settings.BaseUrl}/api/v1/nodes/{targetNodeId}/versions";

                // Create a MultipartFormDataContent for the request
                using var multipartContent = new MultipartFormDataContent();

                // Add the file content
                using var fileStreamContent = new ByteArrayContent(fileContent);
                fileStreamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);

                // Add the file with explicit name parameter
                multipartContent.Add(fileStreamContent, "file", fileName);

                // Add description and version type
                multipartContent.Add(new StringContent("true"), "add_major_version");
                multipartContent.Add(new StringContent($"Added from Change Request on {DateTime.Now:yyyy-MM-dd HH:mm:ss}"), "description");

                // Create the HTTP request
                using var versionRequest = new HttpRequestMessage(HttpMethod.Post, versionUrl);
                versionRequest.Headers.Add("OTCSTicket", ticket);

                // Remove any traceparent header if present
                if (versionRequest.Headers.Contains("traceparent"))
                {
                    versionRequest.Headers.Remove("traceparent");
                }

                versionRequest.Content = multipartContent;

                // Log request details
                _logger.Log($"Sending v1 version request to URL: {versionUrl}", LogLevel.DEBUG);

                // Send the request
                var versionResponse = await _httpClient.SendAsync(versionRequest);

                // Read the response
                string responseContent = await versionResponse.Content.ReadAsStringAsync();

                // Check if the request was successful
                if (versionResponse.IsSuccessStatusCode)
                {
                    _logger.Log($"Successfully added version using API v1", LogLevel.INFO);
                    return true;
                }
                else
                {
                    _logger.Log($"Failed to add version using API v1: {versionResponse.StatusCode} - {responseContent}", LogLevel.ERROR);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex);
                _logger.Log($"Exception in API v1 version attempt: {ex.Message}", LogLevel.ERROR);
                return false;
            }
        }

        /// <summary>
        /// Moves a document to an existing document as a new version instead of creating a duplicate
        /// </summary>
        /// <param name="sourceNodeId">ID of the document to add as a new version</param>
        /// <param name="targetNodeId">ID of the existing document that will receive the new version</param>
        /// <param name="ticket">Authentication ticket (OTCSTICKET)</param>
        /// <returns>True if the operation was successful</returns>
        private async Task<bool> CopyDocumentAsVersionAsync(string sourceNodeId, string targetNodeId, string ticket)
        {
            try
            {
                _logger.Log($"Adding document {sourceNodeId} as new version to document {targetNodeId}", LogLevel.INFO);

                // Crear la URL para la API de OpenText
                string url = $"{_settings.BaseUrl}/api/v2/nodes/{targetNodeId}/versions";

                // Datos que enviaremos
                var requestData = new
                {
                    original_id = int.Parse(sourceNodeId),
                    add_major_version = true,
                    description = $"Added from Change Request on {DateTime.Now:yyyy-MM-dd HH:mm:ss}"
                };

                // Convertir los datos a JSON
                string jsonBody = System.Text.Json.JsonSerializer.Serialize(requestData);

                // Crear la solicitud HTTP
                using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("OTCSTicket", ticket);
                request.Content = content;

                // Registrar los detalles de la solicitud (para depuración)
                string fullRequest = $"URL: {url}\nMethod: POST\nHeaders: OTCSTicket: {ticket}\nContent: {jsonBody}";
                _logger.LogRawOutbound("request_add_version_detailed", fullRequest);

                // Enviar la solicitud
                var response = await _httpClient.SendAsync(request);

                // Leer la respuesta
                var responseBody = await response.Content.ReadAsStringAsync();

                // Registrar la respuesta (para depuración)
                string fullResponse = $"Status Code: {response.StatusCode}\nHeaders: {string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}"))})\nBody: {responseBody}";
                _logger.LogRawOutbound("response_add_version_detailed", fullResponse);

                if (response.IsSuccessStatusCode)
                {
                    _logger.Log($"Successfully added document {sourceNodeId} as new version to document {targetNodeId}", LogLevel.INFO);
                    return true;
                }
                else
                {
                    _logger.Log($"Failed to add document as version: {response.StatusCode} - {responseBody}", LogLevel.ERROR);

                    // Si falla, intentemos un enfoque alternativo - copiar el documento a través del API de copia
                    return await CopyDocumentAlternativeAsync(sourceNodeId, targetNodeId, ticket);
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex);
                _logger.Log($"Exception adding document as version: {ex.Message}", LogLevel.ERROR);
                return false;
            }
        }



        /// <summary>
        /// Intento alternativo para manejar versiones de documentos
        /// </summary>
        private async Task<bool> CopyDocumentAlternativeAsync(string sourceNodeId, string targetNodeId, string ticket)
        {
            try
            {
                _logger.Log($"Trying alternative approach to copy document {sourceNodeId}", LogLevel.INFO);

                // Primero, obtener el contenido del documento fuente
                string downloadUrl = $"{_settings.BaseUrl}/api/v1/nodes/{sourceNodeId}/content";

                // Crear la solicitud HTTP para descargar
                using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
                downloadRequest.Headers.Add("OTCSTicket", ticket);

                // Registrar los detalles de la solicitud de descarga
                _logger.LogRawOutbound("request_download_document", $"URL: {downloadUrl}\nMethod: GET\nHeaders: OTCSTicket: {ticket}");

                // Enviar la solicitud de descarga
                var downloadResponse = await _httpClient.SendAsync(downloadRequest);

                if (!downloadResponse.IsSuccessStatusCode)
                {
                    var errorBody = await downloadResponse.Content.ReadAsStringAsync();
                    _logger.Log($"Failed to download document content: {downloadResponse.StatusCode} - {errorBody}", LogLevel.ERROR);
                    return false;
                }

                // Obtener el contenido del documento como array de bytes
                var documentContent = await downloadResponse.Content.ReadAsByteArrayAsync();
                _logger.Log($"Downloaded document content, size: {documentContent.Length} bytes", LogLevel.DEBUG);

                // Obtener el nombre y tipo de documento
                string documentName = "document.bin"; // Nombre por defecto
                string contentType = "application/octet-stream"; // Tipo por defecto

                // Intentar obtener el nombre del documento desde los headers
                if (downloadResponse.Content.Headers.ContentDisposition != null)
                {
                    documentName = downloadResponse.Content.Headers.ContentDisposition.FileName ?? documentName;
                }

                // Intentar obtener el tipo de contenido desde los headers
                if (downloadResponse.Content.Headers.ContentType != null)
                {
                    contentType = downloadResponse.Content.Headers.ContentType.ToString();
                }

                // Crear la URL para añadir versión
                string versionUrl = $"{_settings.BaseUrl}/api/v1/nodes/{targetNodeId}/versions";

                // Crear la solicitud HTTP para añadir versión
                using var multipartContent = new MultipartFormDataContent();
                using var fileContent = new ByteArrayContent(documentContent);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

                // Añadir el contenido del archivo
                multipartContent.Add(fileContent, "file", documentName);

                // Añadir otros parámetros
                multipartContent.Add(new StringContent("true"), "add_major_version");
                multipartContent.Add(new StringContent($"Added from Change Request on {DateTime.Now:yyyy-MM-dd HH:mm:ss}"), "description");

                // Crear la solicitud
                using var versionRequest = new HttpRequestMessage(HttpMethod.Post, versionUrl);
                versionRequest.Headers.Add("OTCSTicket", ticket);
                versionRequest.Content = multipartContent;

                // Registrar los detalles de la solicitud (limitado debido al tamaño)
                _logger.LogRawOutbound("request_add_version_alternative",
                    $"URL: {versionUrl}\nMethod: POST\nHeaders: OTCSTicket: {ticket}\nContent: [Binary data of size {documentContent.Length} bytes]");

                // Enviar la solicitud
                var versionResponse = await _httpClient.SendAsync(versionRequest);

                // Leer la respuesta
                var versionResponseBody = await versionResponse.Content.ReadAsStringAsync();

                // Registrar la respuesta
                _logger.LogRawOutbound("response_add_version_alternative",
                    $"Status Code: {versionResponse.StatusCode}\nBody: {versionResponseBody}");

                if (versionResponse.IsSuccessStatusCode)
                {
                    _logger.Log($"Successfully added document as new version using alternative method", LogLevel.INFO);
                    return true;
                }
                else
                {
                    _logger.Log($"Failed to add document as version (alternative method): {versionResponse.StatusCode} - {versionResponseBody}", LogLevel.ERROR);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex);
                _logger.Log($"Exception in alternative version method: {ex.Message}", LogLevel.ERROR);
                return false;
            }
        }

        /// <summary>
        /// Placeholder method to process additional logic when a Change Request is approved.
        /// Extend this method with additional business logic if needed.
        /// </summary>
        /// <param name="boType">Business Object type</param>
        /// <param name="boId">Business Object ID</param>
        /// <returns>Completed task</returns>
        public async Task ApproveChangeRequestAsync(string boType, string boId)
        {
            _logger.Log("[ApproveChangeRequestAsync] Method invoked", LogLevel.DEBUG);
            _logger.Log($"Placeholder for approval process. BO Type: {boType}, BO ID: {boId}", LogLevel.INFO);

            // This is just a placeholder - extend with actual approval logic if needed
            await Task.CompletedTask;
        }
    }
}