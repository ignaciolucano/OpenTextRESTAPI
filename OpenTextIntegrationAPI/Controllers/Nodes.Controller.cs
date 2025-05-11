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
    /// <summary>
    /// Controller that handles Node operations with OpenText Content Server.
    /// Provides endpoints for creating, retrieving, and deleting nodes (documents, folders, etc.).
    /// </summary>
    [ApiController]
    [Route("v1/[controller]")]
    public class NodesController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly AuthManager _authManager;
        private readonly CSUtilities _csUtilities;
        private static OpenTextSettings _settings;
        private readonly CRBusinessWorkspace _crBusinessWorkspace;
        private readonly Node _csNode;
        private readonly ILogService _logger;

        /// <summary>
        /// Initializes a new instance of the NodesController with required dependencies.
        /// </summary>
        /// <param name="crBusinessWorkspace">Service for business workspace operations</param>
        /// <param name="settings">Configuration settings for OpenText API</param>
        /// <param name="csUtilities">Utility methods for Content Server operations</param>
        /// <param name="httpClient">HTTP client for API calls</param>
        /// <param name="authManager">Service for authentication management</param>
        /// <param name="csNode">Service for node operations in Content Server</param>
        /// <param name="logger">Service for logging operations and errors</param>
        public NodesController(
            CRBusinessWorkspace crBusinessWorkspace,
            IOptions<OpenTextSettings> settings,
            CSUtilities csUtilities,
            HttpClient httpClient,
            AuthManager authManager,
            Node csNode,
            ILogService logger)
        {
            _httpClient = httpClient;
            _csUtilities = csUtilities;
            _authManager = authManager;
            _settings = settings.Value;
            _crBusinessWorkspace = crBusinessWorkspace;
            _csNode = csNode;
            _logger = logger;

            // Log controller initialization
            _logger.Log("NodesController initialized", LogLevel.DEBUG);
        }

        /// <summary>
        /// Retrieves a node by its ID, including its content.
        /// </summary>
        /// <param name="id">ID of the node to retrieve</param>
        /// <returns>HTTP response with node information and content</returns>
        [HttpGet("{id}")]
        [SwaggerOperation(
            Summary = "Get a node by its ID",
            Description = "Retrieves a node (document, folder, or other object) from OpenText Content Server by its node ID"
        )]
        [SwaggerResponse(200, "OK", typeof(NodeResponse))]
        [SwaggerResponse(400, "Invalid parameter value")]
        [SwaggerResponse(401, "Authentication Required")]
        [SwaggerResponse(404, "Node Not Found")]
        [SwaggerResponse(500, "Internal Error. Contact API Admin")]
        [Produces("application/json")]
        public async Task<IActionResult> GetNode(int id)
        {
            _logger.Log($"GetNode called for ID: {id}", LogLevel.INFO);

            // Get ticket from Request
            string ticket = "";
            try
            {
                _logger.Log("Extracting authentication ticket from request", LogLevel.DEBUG);
                ticket = _authManager.ExtractTicket(Request);

                // Log successful ticket extraction (with masked ticket)
                if (ticket != null && ticket.Length > 12)
                {
                    string maskedTicket = ticket.Substring(0, 8) + "..." + ticket.Substring(ticket.Length - 4);
                    _logger.Log($"Authentication ticket extracted: {maskedTicket}", LogLevel.DEBUG);
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error extracting authentication ticket: {ex.Message}", LogLevel.ERROR);

                // Return a 401 status code if auth fails
                return StatusCode(401, $"Error getting node {id} : {ex.Message}");
            }

            try
            {
                // Log request details
                _logger.LogRawInbound("inbound_request_get_node",
                     System.Text.Json.JsonSerializer.Serialize(new
                    {
                        nodeId = id
                    })
                );

                // Retrieve node by ID
                _logger.Log($"Calling GetNodeByIdAsync for node ID: {id}", LogLevel.DEBUG);
                var node = await _csNode.GetNodeByIdAsync(id, ticket);

                // Check if node was found
                if (node == null)
                {
                    _logger.Log($"Node with ID {id} not found", LogLevel.WARNING);

                    // Log response for not found case
                    _logger.LogRawInbound("inbound_response_get_node_notfound",
                        System.Text.Json.JsonSerializer.Serialize(new
                        {
                            status = "not_found",
                            message = $"Could not get a node for {id}"
                        })
                    );

                    return StatusCode(404, $"Could not get a node for {id}");
                }

                // Log successful retrieval
                _logger.Log($"Successfully retrieved node with ID: {id}, Name: {node.file_name}", LogLevel.INFO);

                // Log response details (metadata only, not content)
                _logger.LogRawInbound("inbound_response_get_node",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        nodeId = node.nodeId,
                        fileName = node.file_name,
                        type = node.type,
                        typeName = node.type_name,
                        contentSize = node.Content?.Length ?? 0
                    })
                );

                return Ok(node);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error retrieving node: {ex.Message}", LogLevel.ERROR);

                // Log error response
                _logger.LogRawInbound("inbound_response_get_node_error",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        error = ex.Message,
                        stack_trace = ex.StackTrace
                    })
                );

                // Return 500 if something unexpected happens
                return StatusCode(500, ex.Message);
            }
        }

        /// <summary>
        /// Creates a new document node in a business workspace.
        /// </summary>
        /// <param name="boType">Business Object Type</param>
        /// <param name="boId">Business Object ID</param>
        /// <param name="docName">Document name</param>
        /// <param name="file">File content</param>
        /// <param name="expirationDate">Optional expiration date for the document</param>
        /// <param name="documentType">Document type/classification</param>
        /// <returns>HTTP response with created node information</returns>
        [HttpPost("")]
        [HttpPost("create")]
        //[Consumes("multipart/form-data")]
        [SwaggerOperation(
        Summary = "Create a new document node",
        Description = "Creates a new document in the business workspace associated with the specified business object"
    )]
        [SwaggerResponse(200, "OK", typeof(CreateDocumentNodeResponse))]
        [SwaggerResponse(400, "Invalid parameter value")]
        [SwaggerResponse(401, "Authentication Required")]
        [SwaggerResponse(404, "Parent folder not found")]
        [SwaggerResponse(500, "Internal Error. Contact API Admin")]
        [Produces("application/json")]
        public async Task<IActionResult> CreateDocumentNodeAsync(
        [FromQuery(Name = "boType")] string boType,
        [FromQuery(Name = "bold")] string boId,       // <-- bind the typo
        [FromQuery(Name = "docName")] string docName,
        [FromForm(Name = "file")] IFormFile file,
        [FromQuery(Name = "expirationDate")] DateTime? expirationDate = null,
        [FromQuery(Name = "documentType")] string documentType = null)
        {
            _logger.Log($"CreateDocumentNodeAsync called for BO: {boType}/{boId}, Document: {docName}", LogLevel.INFO);

            // 1) Read all query‐string values by hand:
            var q = Request.Query;
            boType = q.TryGetValue("boType", out var t) ? t.ToString() : null;
            boId = q.TryGetValue("boId", out var id) ? id.ToString() : null;
            docName = q.TryGetValue("docName", out var d) ? d.ToString() : null;
            documentType = q.TryGetValue("documentType", out var dt) ? dt.ToString() : null;
            
            if (q.TryGetValue("expirationDate", out var ed) &&
                DateTime.TryParse(ed, out var tmpDate))
            {
                expirationDate = tmpDate;
            }

            // 2) Read the form (and file) yourself:
            if (!Request.HasFormContentType)
                return BadRequest("Expected multipart/form-data");

            var form = await Request.ReadFormAsync();
            // if your part name is "file"
            file = form.Files.GetFile("file");
            if (file == null || file.Length == 0)
                return BadRequest("Missing file upload");


            // Get ticket from Request
            string ticket = "";
            try
            {
                _logger.Log("Extracting authentication ticket from request", LogLevel.DEBUG);
                ticket = _authManager.ExtractTicket(Request);

                // Log successful ticket extraction (with masked ticket)
                if (ticket != null && ticket.Length > 12)
                {
                    string maskedTicket = ticket.Substring(0, 8) + "..." + ticket.Substring(ticket.Length - 4);
                    _logger.Log($"Authentication ticket extracted: {maskedTicket}", LogLevel.DEBUG);
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error extracting authentication ticket: {ex.Message}", LogLevel.ERROR);

                // Return a 401 status code if auth fails
                return StatusCode(401, $"Error creating node {ex.Message}");
            }

            // Validate input parameters
            _logger.Log("Validating input parameters", LogLevel.DEBUG);
            try
            {
                if (string.IsNullOrWhiteSpace(boType))
                {
                    _logger.Log("Validation failed: Business Object Type is required", LogLevel.WARNING);
                    throw new ArgumentException("Business Object Type is required.", nameof(boType));
                }
                if (string.IsNullOrWhiteSpace(boId))
                {
                    _logger.Log("Validation failed: Business Object ID is required", LogLevel.WARNING);
                    throw new ArgumentException("Business Object ID is required.", nameof(boId));
                }
                if (string.IsNullOrWhiteSpace(documentType))
                {
                    _logger.Log("Validation failed: Document Type is required", LogLevel.WARNING);
                    throw new ArgumentException("Document Type is required.", nameof(documentType));
                }
                if (string.IsNullOrWhiteSpace(docName))
                {
                    _logger.Log("Validation failed: Document name is required", LogLevel.WARNING);
                    throw new ArgumentException("Document name is required.", nameof(docName));
                }
                if (file == null || file.Length == 0)
                {
                    _logger.Log("Validation failed: File cannot be null or empty", LogLevel.WARNING);
                    throw new ArgumentException("File cannot be null or empty.", nameof(file));
                }
                if (string.IsNullOrWhiteSpace(ticket))
                {
                    _logger.Log("Validation failed: OTCS ticket is required", LogLevel.WARNING);
                    throw new ArgumentException("OTCS ticket is required.", nameof(ticket));
                }
            }
            catch (ArgumentException ex)
            {
                _logger.LogException(ex, LogLevel.WARNING);
                return BadRequest(ex.Message);
            }

            // Validate and format BO Type and ID
            _logger.Log($"Validating and formatting BO parameters: {boType}/{boId}", LogLevel.DEBUG);
            var (validatedBoType, formattedBoId) = CRBusinessWorkspace.ValidateAndFormatBoParams(boType, boId);
            _logger.Log($"Validated BO parameters: {validatedBoType}/{formattedBoId}", LogLevel.DEBUG);

            // Search for parent business workspace
            string parentId;
            try
            {
                _logger.Log($"Searching for business workspace with BO ID: {formattedBoId}", LogLevel.DEBUG);
                parentId = await _crBusinessWorkspace.SearchBusinessWorkspaceAsync(_httpClient, formattedBoId, ticket);
                _logger.Log($"Found parent business workspace with ID: {parentId}", LogLevel.DEBUG);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Failed to search Business Workspace: {ex.Message}", LogLevel.ERROR);
                return StatusCode(404, $"Failed to search Business Workspace: {ex.Message}");
            }

            string nodeCreationJson = "";

            // Validates expiration date
            if (expirationDate.HasValue)
            {
                _logger.Log($"Processing expiration date: {expirationDate.Value}", LogLevel.DEBUG);
                string formattedExpirationDate = expirationDate.Value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

                // Attempt to parse the formatted string exactly with the given format
                if (DateTime.TryParseExact(formattedExpirationDate,
                                        "yyyy-MM-ddTHH:mm:ss",
                                        CultureInfo.InvariantCulture,
                                        DateTimeStyles.None,
                                        out DateTime parsedDate))
                {
                    _logger.Log("Getting expiration date category ID", LogLevel.DEBUG);
                    var expDateCatId = await _csUtilities.GetExpirationDateCatIdAsync(ticket);
                    _logger.Log($"Expiration date category ID: {expDateCatId}", LogLevel.DEBUG);

                    // Build the JSON object for node creation (Document subtype = 144) with expiration date
                    var nodeCreationObjectWE = new
                    {
                        type = 144,           // Document subtype
                        parent_id = parentId,
                        name = docName,
                        roles = new
                        {
                            categories = new Dictionary<string, string>
                            {
                                { $"{expDateCatId}_2",  formattedExpirationDate}  // Category key-value pair
                            }
                        }
                    };
                    nodeCreationJson = JsonSerializer.Serialize(nodeCreationObjectWE);
                    _logger.Log("Created node creation JSON with expiration date", LogLevel.DEBUG);
                }
                else
                {
                    _logger.Log("Invalid expiration date format", LogLevel.WARNING);
                    return StatusCode(404, "expirationDate must be in the format yyyy-MM-ddTHH:mm:ss}");
                }
            }
            else
            {
                // Build the JSON object for node creation (Document subtype = 144) without expiration date
                var nodeCreationObjectNE = new
                {
                    type = 144,           // Document subtype
                    parent_id = parentId,
                    name = docName
                };
                nodeCreationJson = JsonSerializer.Serialize(nodeCreationObjectNE);
                _logger.Log("Created node creation JSON without expiration date", LogLevel.DEBUG);
            }

            // Log request details (excluding file content for size reasons)
            _logger.LogRawInbound("inbound_request_create_document_node",
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    boType = validatedBoType,
                    boId = formattedBoId,
                    parentId,
                    docName,
                    documentType,
                    hasExpirationDate = expirationDate.HasValue,
                    expirationDate = expirationDate,
                    fileSize = file.Length,
                    fileName = file.FileName,
                    fileContentType = file.ContentType
                })
            );

            // Build the URL for the POST /v2/nodes endpoint
            var baseUrl = _settings.BaseUrl;
            var createUrl = $"{baseUrl}/api/v2/nodes";
            _logger.Log($"Node creation URL: {createUrl}", LogLevel.DEBUG);

            // Create the HttpRequestMessage
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, createUrl);
            requestMessage.Headers.Add("OTCSTICKET", ticket);

            // Build the multipart/form-data content
            using var formDataContent = new MultipartFormDataContent();

            // Part A: "body" part containing the JSON string
            var bodyContent = new StringContent(nodeCreationJson, Encoding.UTF8, "text/plain");
            formDataContent.Add(bodyContent, "body");

            // Part B: "file" part containing the file's binary content
            using var fileStream = file.OpenReadStream();
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                !string.IsNullOrWhiteSpace(file.ContentType) ? file.ContentType : "application/octet-stream");
            formDataContent.Add(fileContent, "file", Uri.EscapeDataString(file.FileName));

            // Attach the multipart content to the request
            requestMessage.Content = formDataContent;

            HttpResponseMessage response;
            try
            {
                // Send the request
                _logger.Log($"Sending request to create document node: {docName}", LogLevel.DEBUG);
                response = await _httpClient.SendAsync(requestMessage);

                // Read response content
                var responseJson = await response.Content.ReadAsStringAsync();

                // Log raw API response (note: not logging full response content as it could be large)
                _logger.LogRawInbound("inbound_response_create_document_node",
                    $"Status: {response.StatusCode}, Content length: {responseJson.Length}"
                );

                // Verify that the response was successful
                if (!response.IsSuccessStatusCode)
                {
                    _logger.Log($"CreateDocumentNode failed with status code {response.StatusCode}", LogLevel.ERROR);
                    return StatusCode(500, $"CreateDocumentNode failed with status code {response.StatusCode}: {responseJson}");
                }

                _logger.Log("Document node created successfully, parsing response", LogLevel.DEBUG);

                // Parse the JSON response to retrieve the newly created node's details
                var nodeResponse = new CreateDocumentNodeResponse();
                try
                {
                    string createdNodeId = "";
                    using var doc = JsonDocument.Parse(responseJson);

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

                    _logger.Log($"Successfully parsed node response. Node ID: {nodeResponse.NodeId}, Name: {nodeResponse.Name}", LogLevel.DEBUG);

                    // Apply classification to the created document
                    if (!string.IsNullOrEmpty(createdNodeId))
                    {
                        _logger.Log($"Applying classification {documentType} to node {createdNodeId}", LogLevel.DEBUG);
                        if (!await _csUtilities.ApplyClassificationAsync(createdNodeId, documentType, ticket))
                        {
                            _logger.Log($"Failed to apply classification {documentType} to node {createdNodeId}", LogLevel.WARNING);
                            nodeResponse.Message = "Node Created but Document Type was not assigned.";
                        }
                        else
                        {
                            _logger.Log($"Successfully applied classification {documentType} to node {createdNodeId}", LogLevel.INFO);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, LogLevel.ERROR);
                    _logger.Log($"Error parsing node response: {ex.Message}", LogLevel.ERROR);
                    nodeResponse.Message = "Node Created but error retrieving node Id.";
                }

                // Log the final result
                _logger.Log($"Document node creation completed. NodeId: {nodeResponse.NodeId}, Name: {nodeResponse.Name}", LogLevel.INFO);

                // Return the parsed node response
                return Ok(nodeResponse);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error creating document node: {ex.Message}", LogLevel.ERROR);
                return StatusCode(500, $"Could not send CreateDocumentNode request: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes a node from OpenText Content Server.
        /// </summary>
        /// <param name="nodeId">ID of the node to delete</param>
        /// <returns>HTTP response with deletion result</returns>
        [HttpDelete("{nodeId}")]
        [SwaggerOperation(
            Summary = "Delete a node",
            Description = "Deletes a node (document, folder, or other object) from OpenText Content Server"
        )]
        [SwaggerResponse(200, "OK")]
        [SwaggerResponse(400, "Could not get node for {id}")]
        [SwaggerResponse(401, "Authentication Required")]
        [SwaggerResponse(500, "Could not get a node for {id}")]
        [Produces("application/json")]
        public async Task<IActionResult> DeleteNode(string nodeId)
        {
            _logger.Log($"DeleteNode called for ID: {nodeId}", LogLevel.INFO);

            // Get ticket from Request
            string ticket = "";
            try
            {
                _logger.Log("Extracting authentication ticket from request", LogLevel.DEBUG);
                ticket = _authManager.ExtractTicket(Request);

                // Log successful ticket extraction (with masked ticket)
                if (ticket != null && ticket.Length > 12)
                {
                    string maskedTicket = ticket.Substring(0, 8) + "..." + ticket.Substring(ticket.Length - 4);
                    _logger.Log($"Authentication ticket extracted: {maskedTicket}", LogLevel.DEBUG);
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error extracting authentication ticket: {ex.Message}", LogLevel.ERROR);

                // Return a 401 status code if auth fails
                return StatusCode(401, $"Error deleting node {nodeId}: {ex.Message}");
            }

            try
            {
                // Log request details
                _logger.LogRawInbound("inbound_request_delete_node",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        nodeId
                    })
                );

                // Call the service method to delete the node
                _logger.Log($"Calling DeleteNodeAsync for node ID: {nodeId}", LogLevel.DEBUG);
                await _csNode.DeleteNodeAsync(nodeId, ticket);

                // Log successful deletion
                _logger.Log($"Successfully deleted node with ID: {nodeId}", LogLevel.INFO);

                // Log response details
                _logger.LogRawInbound("inbound_response_delete_node",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        status = "success",
                        message = $"Node {nodeId} deleted successfully"
                    })
                );

                return Ok($"Node {nodeId} deleted succesfully");
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error deleting node: {ex.Message}", LogLevel.ERROR);

                // Log error response
                _logger.LogRawApi("api_response_delete_node_error",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        error = ex.Message,
                        stack_trace = ex.StackTrace
                    })
                );

                // Return a 500 status code if something unexpected happens
                return StatusCode(500, $"Error deleting node {nodeId}: {ex.Message}");
            }
        }
    }
}