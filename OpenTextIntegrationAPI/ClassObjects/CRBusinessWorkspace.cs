using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OpenTextIntegrationAPI.Models;
using OpenTextIntegrationAPI.Services;
using System.Diagnostics;
using System.Net.Http;
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
            string extSystemId = _settings.ExtSystemId;
            string url = $"{baseUrl}/api/v2/businessworkspaces?where_bo_type=BUS2250&where_column_query=name LIKE '{formattedBoId} -*'&where_ext_system_id={extSystemId}&expanded_view=true";

            // Create HTTP request with authentication ticket
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("OTCSTICKET", ticket);

            // Log the request details
            _logger.Log($"[SearchBusinessWorkspaceAsync] Request URL: {url}", LogLevel.DEBUG);

            // Log raw API request if enabled in settings
            _logger.LogRawApi("opentext_request_search_workspace", JsonSerializer.Serialize(new { formattedBoId, extSystemId, url }));

            // Execute the request
            _logger.Log("Sending HTTP request to OpenText API", LogLevel.TRACE);
            var response = await httpClient.SendAsync(request);

            // Read response content
            string json = await response.Content.ReadAsStringAsync();

            // Log raw API response if enabled in settings
            _logger.LogRawApi("opentext_response_search_workspace", json);

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
            string url = $"{_settings.BaseUrl}/api/v2/businessworkspaces?where_bo_type={boType}&where_column_query=name LIKE '{formattedBoId} -*'&where_ext_system_id={_settings.ExtSystemId}&expanded_view=true";

            // Create HTTP request with authentication ticket
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("OTCSTICKET", ticket);

            // Log the request details
            _logger.Log($"[SearchCRBusinessWorkspaceAsync] Requesting CR Business Workspace from: {url}", LogLevel.INFO);

            // Log raw API request if enabled in settings
            _logger.LogRawApi("opentext_request_search_cr_workspace", JsonSerializer.Serialize(new { boType, formattedBoId, url }));

            // Execute the request
            _logger.Log("Sending HTTP request to OpenText API", LogLevel.TRACE);
            var response = await httpClient.SendAsync(request);

            // Read response content
            var json = await response.Content.ReadAsStringAsync();

            // Log raw API response
            _logger.LogRawApi("opentext_response_search_cr_workspace", json);

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
        public async Task<MasterDataDocumentsResponse?> GetDocumentsChangeRequestAsync(string boType, string boId, string ticket)
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
                var documents = await _csNode.GetNodeSubNodesAsync(workspaceNodeId, ticket, expDateCatId, "Request", null);
                _logger.Log($"[GetDocumentsChangeRequestAsync] Retrieved {documents?.Count} documents", LogLevel.INFO);

                // Construct and return the response object
                return new MasterDataDocumentsResponse
                {
                    Header = new MasterDataDocumentsHeader
                    {
                        BoType = boType,
                        BoId = boId,
                        BwName = workspaceName
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

                        // Determine destination folder based on document type
                        string trimmedBo = updateRequest.MainBOType.Length >= 7 ? updateRequest.MainBOType.Substring(0, 7) : updateRequest.MainBOType;
                        string docTypeString = $"{trimmedBo}.{classificationId}";
                        string strFolder = _csUtilities.GetDocTypeName(docTypeString);
                        _logger.Log($"Document type: {docTypeString}, Target folder: {strFolder}", LogLevel.DEBUG);

                        // Search for the original business workspace
                        _logger.Log($"Searching for original workspace with BO Type: {updateRequest.MainBOType}, BO ID: {updateRequest.MainBOId}", LogLevel.DEBUG);
                        var wsOriginalBW = await _masterData.SearchBusinessWorkspaceAsync(updateRequest.MainBOType, updateRequest.MainBOId, ticket);
                        string? originalBWnodeId = wsOriginalBW?.results?.FirstOrDefault()?.data?.properties.id.ToString();

                        if (!string.IsNullOrEmpty(originalBWnodeId))
                        {
                            // Get folders from the original workspace
                            _logger.Log($"Getting subfolders for original workspace: {originalBWnodeId}", LogLevel.DEBUG);
                            var oriBWFolders = await _csNode.GetNodeSubFoldersAsync(originalBWnodeId, ticket, "Master");
                            string? folderNodeId = oriBWFolders.FirstOrDefault(f => f.Name == strFolder)?.NodeId;

                            // If target folder exists, move document
                            if (!string.IsNullOrEmpty(folderNodeId))
                            {
                                _logger.Log($"[UpdateChangeRequestDataAsync] Moving document {document.Name} to {strFolder}", LogLevel.DEBUG);
                                bool resultMove = await _csNode.MoveNodeAsync(document.NodeId, folderNodeId, ticket);

                                if (!resultMove)
                                {
                                    _logger.Log($"[UpdateChangeRequestDataAsync] Failed to move document {document.Name}", LogLevel.ERROR);
                                    throw new Exception($"Could not move node ({document.Name}) to folder.");
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