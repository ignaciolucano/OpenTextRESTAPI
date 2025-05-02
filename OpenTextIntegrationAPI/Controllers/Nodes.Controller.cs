using Microsoft.AspNetCore.Mvc;
using OpenTextIntegrationAPI.Models;
using OpenTextIntegrationAPI.Services;
using Swashbuckle.AspNetCore.Annotations;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using OpenTextIntegrationAPI.DTOs;
using System.Text;
using OpenTextIntegrationAPI.Utilities;
using System.Globalization;
using OpenTextIntegrationAPI.ClassObjects;
using Microsoft.Extensions.Options;

namespace OpenTextIntegrationAPI.Controllers
{
    [ApiController]
    [Route("v1/[controller]")]
    public class NodesController : ControllerBase
    {
        private readonly HttpClient _httpClient;


        // NEW REFERENCES
        private readonly AuthManager _authManager;
        private readonly CSUtilities _csUtilities;
        private static OpenTextSettings _settings;
        private readonly CRBusinessWorkspace _crBusinessWorkspace;
        private readonly Node _csNode;

        public NodesController(CRBusinessWorkspace crBusinessWorkspace, IOptions<OpenTextSettings> settings, CSUtilities csUtilities, HttpClient httpClient, AuthManager authManager, Node csNode)
        {

            _httpClient = httpClient;

            // NEW REFERENCES
            _csUtilities = csUtilities;
            _authManager = authManager;
            _settings = settings.Value;
            _crBusinessWorkspace = crBusinessWorkspace;
            _csNode = csNode;
        }

        [HttpGet("{id}")]
        [SwaggerResponse(200, "OK", typeof(NodeResponse))]
        [SwaggerResponse(400, "Invalid parameter value")]
        [SwaggerResponse(401, "Authentication Required")]
        [SwaggerResponse(404, "Node Not Found")]
        [SwaggerResponse(500, "Internal Error. Contact API Admin")]
        [Produces("application/json")]
        public async Task<IActionResult> GetNode(int id)
        {
            // Get's Ticket from Request
            string ticket = "";
            try
            {
                ticket = _authManager.ExtractTicket(Request);
            }
            catch (Exception ex)
            {
                // Return a 401 status code if auth fails.
                return StatusCode(401, $"Error getting node {id} : {ex.Message}");
            }

            try
            {
                var node = await _csNode.GetNodeByIdAsync(id, ticket);
                if (node == null)
                {
                    // Return 400 or 404, depending on how you want to handle "not found" nodes
                    return StatusCode(404, $"Could not get a node for {id}");
                }
                return Ok(node);
            }
            catch (Exception ex)
            {
                // Return 500 if something unexpected happens
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("create")]
        [SwaggerResponse(200, "OK", typeof(CreateDocumentNodeResponse))]
        [SwaggerResponse(400, "Invalid parameter value")]
        [SwaggerResponse(401, "Authentication Required")]
        [SwaggerResponse(404, "Parent folder not found")]
        [SwaggerResponse(500, "Internal Error. Contact API Admin")]
        [Produces("application/json")]
        public async Task<IActionResult>  CreateDocumentNodeAsync( //Task<CreateDocumentNodeResponse?>
            string boType,
            string boId,
            string docName,
            IFormFile file,
            DateTime? expirationDate = null,
            string documentType = null)
        {
            // Get's Ticket from Request
            string ticket = "";
            try
            {
                ticket = _authManager.ExtractTicket(Request);
            }
            catch (Exception ex)
            {
                // Return a 401 status code if auth fails.
                return StatusCode(401, $"Error creating node {ex.Message}");
            }

            // Validate input parameters.
            if (string.IsNullOrWhiteSpace(boType))
                throw new ArgumentException("Business Object Type is required.", nameof(boType));
            if (string.IsNullOrWhiteSpace(boId))
                throw new ArgumentException("Business Object ID is required.", nameof(boId));
            if (string.IsNullOrWhiteSpace(documentType))
                throw new ArgumentException("Document Type is required.", nameof(documentType));
            if (string.IsNullOrWhiteSpace(docName))
                throw new ArgumentException("Document name is required.", nameof(docName));
            if (file == null || file.Length == 0)
                throw new ArgumentException("File cannot be null or empty.", nameof(file));
            if (string.IsNullOrWhiteSpace(ticket))
                throw new ArgumentException("OTCS ticket is required.", nameof(ticket));

            // Validate Bo Type and format
            var (validatedBoType, formattedBoId) = CRBusinessWorkspace.ValidateAndFormatBoParams(boType, boId);

            string parentId;
            try
            {
                parentId = await _crBusinessWorkspace.SearchBusinessWorkspaceAsync(_httpClient, formattedBoId, ticket);
            }
            catch (Exception ex)
            {
                return StatusCode(404, $"Failed to search Business Workspace: {ex.Message}"); 
                //Debug.WriteLine($"[ERROR] Failed to search Business Workspace: {ex.Message}");
                //throw;
            }

            string nodeCreationJson = "";

            // Validates expiration date
            if (expirationDate.HasValue)
            {
                string formattedExpirationDate = expirationDate.Value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

                // Attempt to parse the formatted string exactly with the given format
                if (DateTime.TryParseExact(formattedExpirationDate,
                                        "yyyy-MM-ddTHH:mm:ss",
                                        CultureInfo.InvariantCulture,
                                        DateTimeStyles.None,
                                        out DateTime parsedDate))
                {
                    var expDateCatId = await _csUtilities.GetExpirationDateCatIdAsync(ticket);

                    // Build the JSON object for node creation (Document subtype = 144).
                    // With the Expiration Date
                    var nodeCreationObjectWE = new
                    {
                        type = 144,           // Document subtype.
                        parent_id = parentId,
                        name = docName,
                        roles = new
                        {
                            categories = new Dictionary<string, string>
                            {
                                { $"{expDateCatId}_2",  formattedExpirationDate}  // Example category key-value pair.
                            }
                        }
                    };
                    nodeCreationJson = JsonSerializer.Serialize(nodeCreationObjectWE);
                }
                else
                {
                    return StatusCode(404, "expirationDate must be in the format yyyy-MM-ddTHH:mm:ss}");
                    //throw new ArgumentException("expirationDate must be in the format yyyy-MM-ddTHH:mm:ss");
                }
            }
            else 
            {
                // Build the JSON object for node creation (Document subtype = 144)
                // Without the Expiration Date
                var nodeCreationObjectNE = new
                {
                    type = 144,           // Document subtype.
                    parent_id = parentId,
                    name = docName
                };
                nodeCreationJson = JsonSerializer.Serialize(nodeCreationObjectNE);
            }

            // Build the URL for the POST /v2/nodes endpoint.
            var baseUrl = _settings.BaseUrl;
            var createUrl = $"{baseUrl}/api/v2/nodes";

            // Create the HttpRequestMessage.
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, createUrl);
            requestMessage.Headers.Add("OTCSTICKET", ticket);

            // Build the multipart/form-data content.
            using var formDataContent = new MultipartFormDataContent();

            // Part A: "body" part containing the JSON string.
            // Use content type "text/plain" so the API receives a raw non-empty string.
            var bodyContent = new StringContent(nodeCreationJson, Encoding.UTF8, "text/plain");
            formDataContent.Add(bodyContent, "body");

            // Part B: "file" part containing the file's binary content.
            using var fileStream = file.OpenReadStream();
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                !string.IsNullOrWhiteSpace(file.ContentType) ? file.ContentType : "application/octet-stream");
            // The key "file" is expected by the API.
            formDataContent.Add(fileContent, "file", Uri.EscapeDataString(file.FileName)); 

            // Attach the multipart content to the request.
            requestMessage.Content = formDataContent;

            HttpResponseMessage response;
            try
            {
                // Send the request.
                response = await _httpClient.SendAsync(requestMessage);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Could not send CreateDocumentNode request: {ex.Message}");
                //Debug.WriteLine($"[ERROR] Exception during CreateDocumentNode call: {ex.Message}");
                //throw new Exception($"Could not send CreateDocumentNode request: {ex.Message}", ex);
            }

            // Verify that the response was successful.
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return StatusCode(500, $"CreateDocumentNode failed with status code {response.StatusCode}: {errorBody}");
                //throw new Exception($"CreateDocumentNode failed with status code {response.StatusCode}: {errorBody}");
            }

            // Parse the JSON response to retrieve the newly created node's details.
            var responseJson = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"[DEBUG] CreateDocumentNode response: {responseJson}");

            var nodeResponse = new CreateDocumentNodeResponse();
            try
            {
                using var doc = JsonDocument.Parse(responseJson);
                var createdNodeId = "";
                if (doc.RootElement.TryGetProperty("results", out JsonElement resultsElem))
                {
                    if (resultsElem.TryGetProperty("data", out JsonElement dataElem))
                    {
                        if (dataElem.TryGetProperty("properties", out JsonElement propsElem))
                        {
                            nodeResponse.NodeId = propsElem.TryGetProperty("id", out var idVal)
                                ? idVal.GetInt32()
                                : 0;
                            createdNodeId = propsElem.TryGetProperty("id", out var idNode)
                                ? idNode.GetInt32().ToString()
                                : "";
                            nodeResponse.Name = propsElem.TryGetProperty("name", out var nameVal)
                                ? nameVal.GetString()
                                : "";
                            nodeResponse.Type = propsElem.TryGetProperty("type", out var typeVal)
                                ? typeVal.GetInt32()
                                : 0;
                            nodeResponse.TypeName = propsElem.TryGetProperty("type_name", out var typeNameVal)
                                ? typeNameVal.GetString()
                                : "";
                        }

                    }
                }
                // Add the Classification
                if (!await _csUtilities.ApplyClassificationAsync(createdNodeId, documentType, ticket)) 
                {
                    nodeResponse.Message = "Node Created but Document Type was not assigned.";
                }

            }
            catch (Exception ex)
            {
                nodeResponse.Message = "Node Created but error retrieving node Id.";
            }

            // Return the parsed node response.
            return Ok(nodeResponse);
        }

        // DELETE: /v1/nodes/{nodeId}
        [HttpDelete("{nodeId}")]
        [SwaggerResponse(200, "OK")]
        [SwaggerResponse(400, "Could not get node for {id}")]
        [SwaggerResponse(401, "Authentication Required")]
        [SwaggerResponse(500, "Could not get a node for {id}")]
        [Produces("application/json")]
        public async Task<IActionResult> DeleteNode(string nodeId)
        {
            // Get's Ticket from Request
            string ticket = "";
            try
            {
                ticket = _authManager.ExtractTicket(Request);
            }
            catch (Exception ex)
            {
                // Return a 401 status code if auth fails.
                return StatusCode(401, $"Error deleting node {nodeId}: {ex.Message}");
            }

            try
            {
                // Call the service method to delete the node.
                await _csNode.DeleteNodeAsync(nodeId, ticket);
                return Ok($"Node {nodeId} deleted succesfully"); // Alternatively, you could use NoContent() for 204 status if preferred.
            }
            catch (Exception ex)
            {
                // Return a 500 status code if something unexpected happens.
                return StatusCode(500, $"Error deleting node {nodeId}: {ex.Message}");
            }
        }

    }
}
