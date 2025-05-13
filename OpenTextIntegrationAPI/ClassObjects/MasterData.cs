using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OpenTextIntegrationAPI.Models;
using OpenTextIntegrationAPI.Services;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace OpenTextIntegrationAPI.ClassObjects
{
    /// <summary>
    /// Handles master data operations for business workspaces in OpenText Content Server.
    /// Provides functionality for retrieving and managing master data documents attached to business objects.
    /// </summary>
    public class MasterData
    {
        private readonly OpenTextSettings _settings;
        private readonly HttpClient _httpClient;
        private readonly CSUtilities _csUtilities;
        private readonly Node _csNode;
        private readonly ILogService _logger;

        /// <summary>
        /// Initializes a new instance of the MasterData class with required dependencies.
        /// </summary>
        /// <param name="settings">Configuration settings for OpenText API</param>
        /// <param name="httpClient">HTTP client for API calls</param>
        /// <param name="csUtilities">Utility methods for Content Server operations</param>
        /// <param name="csNode">Service for node operations in Content Server</param>
        /// <param name="logger">Logging service for tracking operations</param>
        public MasterData(IOptions<OpenTextSettings> settings, HttpClient httpClient, CSUtilities csUtilities, Node csNode, ILogService logger)
        {
            _settings = settings.Value;
            _httpClient = httpClient;
            _csUtilities = csUtilities;
            _csNode = csNode;
            _logger = logger;

            // Log initialization of the MasterData service
            _logger.Log("MasterData service initialized", LogLevel.DEBUG);
        }

        /// <summary>
        /// Retrieves master data documents associated with a specific business object.
        /// </summary>
        /// <param name="boType">Business Object type</param>
        /// <param name="boId">Business Object ID</param>
        /// <param name="ticket">Authentication ticket (OTCSTICKET)</param>
        /// <returns>Response containing header information and document list</returns>
        /// <exception cref="Exception">Thrown when authentication fails</exception>
        public async Task<ChangeRequestDocumentsResponse?> GetMasterDataDocumentsAsync(string boType, string boId, string ticket)
        {
            _logger.Log("Starting GetMasterDataDocumentsAsync", LogLevel.DEBUG);
            _logger.Log($"Parameters: boType={boType}, boId={boId}", LogLevel.TRACE);

            // Validate authentication ticket
            if (string.IsNullOrEmpty(ticket))
            {
                _logger.Log("Ticket is null or empty", LogLevel.ERROR);
                throw new Exception("Authentication failed: OTCS ticket is empty.");
            }

            // Search for business workspace by business object parameters
            _logger.Log("Calling SearchBusinessWorkspaceAsync", LogLevel.DEBUG);
            var wsResponse = await SearchBusinessWorkspaceAsync(boType, boId, ticket);

            string? workspaceNodeId = null;
            string? workspaceName = null;

            // Process search results if workspace found
            if (wsResponse != null && wsResponse.results.Count > 0)
            {
                // Extract workspace properties from response
                var first = wsResponse.results[0].data.properties;
                workspaceNodeId = first.id.ToString();
                workspaceName = first.name;

                _logger.Log($"Business workspace found: {workspaceName} with nodeId: {workspaceNodeId}", LogLevel.INFO);

                // Get expiration date category ID for document filtering
                _logger.Log("Retrieving expiration date category ID", LogLevel.TRACE);
                var expDateCatId = await _csUtilities.GetExpirationDateCatIdAsync(ticket);
                _logger.Log($"Expiration category ID retrieved: {expDateCatId}", LogLevel.DEBUG);

                // Get master documents from the workspace
                _logger.Log($"Retrieving documents from workspace node {workspaceNodeId}", LogLevel.DEBUG);
                var documents = await _csNode.CRGetNodeSubNodesAsync(workspaceNodeId, ticket, expDateCatId, "Master", null);
                _logger.Log($"Documents retrieved: {documents.Count}", LogLevel.INFO);

                // Construct and return the response object
                return new ChangeRequestDocumentsResponse
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
                _logger.Log("No Business Workspace found", LogLevel.WARNING);
                return null;
            }
        }

        /// <summary>
        /// Searches for a Business Workspace by business object type and ID.
        /// </summary>
        /// <param name="boType">Business Object type</param>
        /// <param name="boId">Business Object ID</param>
        /// <param name="ticket">Authentication ticket (OTCSTICKET)</param>
        /// <returns>BusinessWorkspaceResponse containing workspace details if found</returns>
        /// <exception cref="Exception">Thrown when search fails</exception>
        public async Task<BusinessWorkspaceResponse?> SearchBusinessWorkspaceAsync(string boType, string boId, string ticket)
        {
            _logger.Log("Starting SearchBusinessWorkspaceAsync", LogLevel.DEBUG);
            _logger.Log($"Search parameters: boType={boType}, boId={boId}", LogLevel.DEBUG);

            // Validate and format business object parameters
            (string validatedBoType, string formattedBoId) = ValidateAndFormatBoParams(boType, boId);
            _logger.Log($"Validated and formatted boType: {validatedBoType}, boId: {formattedBoId}", LogLevel.DEBUG);

            // Prepare URL and request parameters
            var baseUrl = _settings.BaseUrl;
            var extSystemId = _settings.ExtSystemId;

            // Construct URL with query parameters
            var url = $"{baseUrl}/api/v2/businessworkspaces?where_bo_type={boType}&where_bo_id={formattedBoId}&where_ext_system_id={extSystemId}&expanded_view=true";
            _logger.Log($"Calling URL: {url}", LogLevel.DEBUG);

            // Create HTTP request with authentication ticket
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("OTCSTICKET", ticket);

            // Log raw API request if enabled in settings
            _logger.Log("Logging raw API request details", LogLevel.TRACE);
            string logId = _logger.LogRawApi("opentext_request_search_business_workspace", JsonSerializer.Serialize(new { boType, boId, extSystemId, timestamp = DateTime.UtcNow }));
            // Execute the request
            _logger.Log("Sending HTTP request to OpenText API", LogLevel.TRACE);
            var response = await _httpClient.SendAsync(request);

            // Read response content
            var json = await response.Content.ReadAsStringAsync();

            // Log raw API response if enabled in settings
            _logger.LogRawApi("opentext_response_search_business_workspace", json);

            // Check for successful response
            if (!response.IsSuccessStatusCode)
            {
                _logger.Log($"SearchBusinessWorkspaceAsync failed with status {response.StatusCode}: {json}", LogLevel.ERROR);
                throw new Exception($"Business Workspace search failed with status {response.StatusCode}: {json}");
            }

            // Deserialize response to BusinessWorkspaceResponse object
            try
            {
                _logger.Log("Deserializing response to BusinessWorkspaceResponse", LogLevel.TRACE);
                var wsResponse = JsonSerializer.Deserialize<BusinessWorkspaceResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                _logger.Log("Deserialization of BusinessWorkspaceResponse successful", LogLevel.DEBUG);

                // Log number of results found
                if (wsResponse != null)
                {
                    _logger.Log($"Search returned {wsResponse.results.Count} results", LogLevel.INFO);
                }

                return wsResponse;
            }
            catch (Exception ex)
            {
                // Log the exception with full details
                _logger.Log("Error deserializing response", LogLevel.ERROR);
                _logger.LogException(ex, LogLevel.ERROR);
                throw;
            }
        }

        /// <summary>
        /// Validates business object parameters and formats the business object ID
        /// according to specific padding rules for each business object type.
        /// </summary>
        /// <param name="boType">Business Object type</param>
        /// <param name="boId">Business Object ID</param>
        /// <returns>Tuple with validated boType and formatted boId</returns>
        /// <exception cref="ArgumentException">Thrown when parameters are invalid</exception>
        public (string validatedBoType, string formattedBoId) ValidateAndFormatBoParams(string boType, string boId)
        {
            _logger.Log("Validating and formatting boType and boId", LogLevel.DEBUG);
            _logger.Log($"Input parameters: boType={boType}, boId={boId}", LogLevel.TRACE);

            // Validate business object ID is not empty
            if (string.IsNullOrWhiteSpace(boId))
            {
                _logger.Log("boId cannot be empty.", LogLevel.ERROR);
                throw new ArgumentException("boId cannot be empty.");
            }

            // Format boId based on boType - each type has specific padding requirements
            switch (boType.ToUpperInvariant())
            {
                case "BUS1001006":  // Equipment
                case "BUS1001001":  // Functional Locations
                    // Pad to 18 digits for equipment and functional locations
                    _logger.Log("Applying padding rule for BUS1001006 or BUS1001001 (18 digits)", LogLevel.TRACE);
                    boId = boId.PadLeft(18, '0');
                    break;

                case "BUS1006":     // Plant Maintenance
                    // Pad to 10 digits for plant maintenance
                    _logger.Log("Applying padding rule for BUS1006 (10 digits)", LogLevel.TRACE);
                    boId = boId.PadLeft(10, '0');
                    break;

                case "BUS2250":     // Change Request
                    // Pad to 12 digits for change requests
                    _logger.Log("Applying padding rule for BUS2250 (12 digits)", LogLevel.TRACE);
                    boId = boId.PadLeft(12, '0');
                    break;

                default:
                    // Invalid business object type
                    _logger.Log($"Invalid boType: {boType}", LogLevel.ERROR);
                    throw new ArgumentException("Invalid boType");
            }

            _logger.Log($"Formatted boId: {boId}", LogLevel.DEBUG);
            return (boType, boId);
        }
    }
}