using Microsoft.Extensions.Options;
using OpenTextIntegrationAPI.Models;
using OpenTextIntegrationAPI.Services;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace OpenTextIntegrationAPI.ClassObjects
{
    public class Node
    {
        private readonly OpenTextSettings _settings;
        private readonly HttpClient _httpClient;
        private readonly CSUtilities _csUtilities;
        private readonly ILogService _logger;

        public Node(IOptions<OpenTextSettings> settings, HttpClient httpClient, CSUtilities csUtilities, ILogService logger)
        {
            _settings = settings.Value;
            _httpClient = httpClient;
            _csUtilities = csUtilities;
            _logger = logger;
        }

        public async Task<bool> DeleteNodeAsync(string nodeId, string ticket)
        {
            _logger.Log("Starting DeleteNodeAsync", LogLevel.DEBUG);

            if (string.IsNullOrWhiteSpace(ticket))
            {
                _logger.Log("OTCS ticket not provided", LogLevel.WARNING);
                throw new ArgumentException("OTCS ticket must be provided.", nameof(ticket));
            }

            if (!await GetBWforNode(nodeId, ticket))
            {
                _logger.Log($"Node {nodeId} is not part of a Change Request", LogLevel.WARNING);
                throw new Exception("The node is not in a Change Request");
            }

            var deleteUrl = $"{_settings.BaseUrl}/api/v1/nodes/{nodeId}";
            var requestMessage = new HttpRequestMessage(HttpMethod.Delete, deleteUrl);
            requestMessage.Headers.Add("OTCSTICKET", ticket);

            string logId = _logger.LogRawApi("api_request_opentext", JsonSerializer.Serialize(new { nodeId, timestamp = DateTime.UtcNow }));

            try
            {
                var response = await _httpClient.SendAsync(requestMessage);
                var responseBody = await response.Content.ReadAsStringAsync();

                _logger.LogRawApi("api_response_opentext", responseBody);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.Log($"DeleteNodeAsync failed. Status: {response.StatusCode}. Body: {responseBody}", LogLevel.ERROR);
                    throw new Exception($"DeleteNodeAsync failed with status code {response.StatusCode}: {responseBody}");
                }

                _logger.Log($"Node {nodeId} successfully deleted.", LogLevel.INFO);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.FATAL);
                throw new Exception($"Error sending DELETE request to {deleteUrl}: {ex.Message}", ex);
            }
        }

        public async Task<bool> GetBWforNode(string nodeId, string ticket)
        {
            var url = $"{_settings.BaseUrl}/api/v1/nodes/{nodeId}/businessworkspace";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("OTCSTICKET", ticket);

            _logger.LogRawApi("api_request_opentext", JsonSerializer.Serialize(new { nodeId, timestamp = DateTime.UtcNow }));

            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            _logger.LogRawApi("api_response_opentext", json);

            if (!response.IsSuccessStatusCode)
            {
                _logger.Log($"GetBWforNode failed for node {nodeId} with status {response.StatusCode}", LogLevel.WARNING);
                return false;
            }

            try
            {
                var crWSKType = _settings.ChangeRequestWSKtype;
                var wsResponse = JsonSerializer.Deserialize<WorkspaceTypeResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (wsResponse?.workspace_type_id.ToString() != crWSKType)
                {
                    _logger.Log($"Node {nodeId} does not belong to a Change Request workspace type.", LogLevel.DEBUG);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Error parsing workspace type for node {nodeId}: {ex.Message}", LogLevel.ERROR);
                return false;
            }

            return true;
        }

        public async Task<NodeResponse?> GetNodeByIdAsync(int nodeId, string ticket)
        {
            throw new NotImplementedException("This method was not yet refactored to include logging and error handling.");
        }

        public async Task<List<DocumentInfo>> GetNodeSubNodesAsync(string nodeId, string ticket, string expDateCatId, string MasterRequest, string? CatName = null)
        {
            throw new NotImplementedException("This method was not yet refactored to include logging and error handling.");
        }

        public async Task<List<DocumentInfo>> GetNodeSubNodesAsync(string nodeId, string ticket, string MasterRequest, string? CatName = null)
        {
            throw new NotImplementedException("This method was not yet refactored to include logging and error handling.");
        }

        public async Task<List<DocumentInfo>> GetNodeSubFoldersAsync(string nodeId, string ticket, string MasterRequest)
        {
            throw new NotImplementedException("This method was not yet refactored to include logging and error handling.");
        }

        public async Task<bool> MoveNodeAsync(string nodeId, string origBoFolderId, string ticket)
        {
            throw new NotImplementedException("This method was not yet refactored to include logging and error handling.");
        }

        public async Task<int> CreateFolderAsync(string nodeId, string folderName, string ticket)
        {
            throw new NotImplementedException("This method was not yet refactored to include logging and error handling.");
        }
    }
}