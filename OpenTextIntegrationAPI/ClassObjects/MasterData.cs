using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OpenTextIntegrationAPI.Models;
using OpenTextIntegrationAPI.Services;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace OpenTextIntegrationAPI.ClassObjects
{
    public class MasterData
    {
        private readonly OpenTextSettings _settings;
        private readonly HttpClient _httpClient;
        private readonly CSUtilities _csUtilities;
        private readonly Node _csNode;
        private readonly ILogService _logger;

        public MasterData(IOptions<OpenTextSettings> settings, HttpClient httpClient, CSUtilities csUtilities, Node csNode, ILogService logger)
        {
            _settings = settings.Value;
            _httpClient = httpClient;
            _csUtilities = csUtilities;
            _csNode = csNode;
            _logger = logger;
        }

        public async Task<MasterDataDocumentsResponse?> GetMasterDataDocumentsAsync(string boType, string boId, string ticket)
        {
            _logger.Log("Starting GetMasterDataDocumentsAsync", LogLevel.DEBUG);

            if (string.IsNullOrEmpty(ticket))
            {
                _logger.Log("Ticket is null or empty", LogLevel.ERROR);
                throw new Exception("Authentication failed: OTCS ticket is empty.");
            }

            _logger.Log("Calling SearchBusinessWorkspaceAsync", LogLevel.DEBUG);
            var wsResponse = await SearchBusinessWorkspaceAsync(boType, boId, ticket);

            string? workspaceNodeId = null;
            string? workspaceName = null;

            if (wsResponse != null && wsResponse.results.Count > 0)
            {
                var first = wsResponse.results[0].data.properties;
                workspaceNodeId = first.id.ToString();
                workspaceName = first.name;

                _logger.Log($"Business workspace found: {workspaceName} with nodeId: {workspaceNodeId}", LogLevel.INFO);

                var expDateCatId = await _csUtilities.GetExpirationDateCatIdAsync(ticket);
                _logger.Log($"Expiration category ID retrieved: {expDateCatId}", LogLevel.DEBUG);

                var documents = await _csNode.GetNodeSubNodesAsync(workspaceNodeId, ticket, expDateCatId, "Master", null);
                _logger.Log($"Documents retrieved: {documents.Count}", LogLevel.INFO);

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
                _logger.Log("No Business Workspace found", LogLevel.WARNING);
                return null;
            }
        }

        public async Task<BusinessWorkspaceResponse?> SearchBusinessWorkspaceAsync(string boType, string boId, string ticket)
        {
            _logger.Log("Starting SearchBusinessWorkspaceAsync", LogLevel.DEBUG);

            (string validatedBoType, string formattedBoId) = ValidateAndFormatBoParams(boType, boId);
            _logger.Log($"Validated and formatted boType: {validatedBoType}, boId: {formattedBoId}", LogLevel.DEBUG);

            var baseUrl = _settings.BaseUrl;
            var extSystemId = _settings.ExtSystemId;

            var url = $"{baseUrl}/api/v2/businessworkspaces?where_bo_type={boType}&where_bo_id={formattedBoId}&where_ext_system_id={extSystemId}&expanded_view=true";
            _logger.Log($"Calling URL: {url}", LogLevel.DEBUG);

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("OTCSTICKET", ticket);

            string logId = _logger.LogRawApi("api_request_opentext", JsonSerializer.Serialize(new { boType, boId, extSystemId, timestamp = DateTime.UtcNow }));

            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            _logger.LogRawApi("api_response_opentext", json);

            if (!response.IsSuccessStatusCode)
            {
                _logger.Log($"SearchBusinessWorkspaceAsync failed with status {response.StatusCode}: {json}", LogLevel.ERROR);
                throw new Exception($"Business Workspace search failed with status {response.StatusCode}: {json}");
            }

            try
            {
                var wsResponse = JsonSerializer.Deserialize<BusinessWorkspaceResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                _logger.Log("Deserialization of BusinessWorkspaceResponse successful", LogLevel.DEBUG);
                return wsResponse;
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                throw;
            }
        }

        public (string validatedBoType, string formattedBoId) ValidateAndFormatBoParams(string boType, string boId)
        {
            _logger.Log("Validating and formatting boType and boId", LogLevel.DEBUG);

            if (string.IsNullOrWhiteSpace(boId))
            {
                _logger.Log("boId cannot be empty.", LogLevel.ERROR);
                throw new ArgumentException("boId cannot be empty.");
            }

            switch (boType.ToUpperInvariant())
            {
                case "BUS1001006":
                case "BUS1001001":
                    boId = boId.PadLeft(18, '0');
                    break;
                case "BUS1006":
                    boId = boId.PadLeft(10, '0');
                    break;
                case "BUS2250":
                    boId = boId.PadLeft(12, '0');
                    break;
                default:
                    _logger.Log("Invalid boType", LogLevel.ERROR);
                    throw new ArgumentException("Invalid boType");
            }

            _logger.Log($"Formatted boId: {boId}", LogLevel.DEBUG);
            return (boType, boId);
        }
    }
}
