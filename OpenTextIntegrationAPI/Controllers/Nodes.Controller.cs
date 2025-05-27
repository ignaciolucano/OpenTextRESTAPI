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
using Microsoft.AspNetCore.Http.Features;

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
        private readonly HttpClient _httpClient; // HTTP client for API calls
        private readonly AuthManager _authManager; // Service to manage authentication tickets
        private readonly CSUtilities _csUtilities; // Utilities for Content Server operations
        private static OpenTextSettings _settings; // Configuration settings for OpenText API
        private readonly CRBusinessWorkspace _crBusinessWorkspace; // Business workspace service
        private readonly Node _csNode; // Node service for Content Server
        private readonly ILogService _logger; // Logger service for logging events and errors

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
            Summary = "",
            Description = ""
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

            // Initialize ticket variable
            string ticket = "";
            try
            {
                // Attempt to extract authentication ticket from the request
                _logger.Log("Extracting authentication ticket from request", LogLevel.DEBUG);
                ticket = _authManager.ExtractTicket(Request);

                // Log masked ticket for security
                if (ticket != null && ticket.Length > 12)
                {
                    string maskedTicket = ticket.Substring(0, 8) + "..." + ticket.Substring(ticket.Length - 4);
                    _logger.Log($"Authentication ticket extracted: {maskedTicket}", LogLevel.DEBUG);
                }
            }
            catch (Exception ex)
            {
                // Log exception and return 401 Unauthorized if extraction fails
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error extracting authentication ticket: {ex.Message}", LogLevel.ERROR);
                return StatusCode(401, $"Error getting node {id} : {ex.Message}");
            }

            try
            {
                // Log inbound request details with node ID
                _logger.LogRawOutbound("request_get_node",
                     System.Text.Json.JsonSerializer.Serialize(new
                     {
                         nodeId = id
                     })
                );

                // Call service to retrieve node by ID using the ticket
                _logger.Log($"Calling GetNodeByIdAsync for node ID: {id}", LogLevel.DEBUG);
                var node = await _csNode.GetNodeByIdAsync(id, ticket);

                // Handle case where node is not found
                if (node == null)
                {
                    _logger.Log($"Node with ID {id} not found", LogLevel.WARNING);

                    // Log response indicating node not found
                    _logger.LogRawInbound("inbound_response_get_node_notfound",
                        System.Text.Json.JsonSerializer.Serialize(new
                        {
                            status = "not_found",
                            message = $"Could not get a node for {id}"
                        })
                    );

                    return StatusCode(404, $"Could not get a node for {id}");
                }

                // Log successful retrieval with node details
                _logger.Log($"Successfully retrieved node with ID: {id}, Name: {node.file_name}", LogLevel.INFO);

                // Log response metadata (excluding content)
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

                // Return node data with 200 OK
                return Ok(node);
            }
            catch (Exception ex)
            {
                // Log exception and error response details
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error retrieving node: {ex.Message}", LogLevel.ERROR);

                _logger.LogRawOutbound("inbound_response_get_node_error",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        error = ex.Message,
                        stack_trace = ex.StackTrace
                    })
                );

                // Return 500 Internal Server Error on failure
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
        [HttpPost("create")]
        [SwaggerOperation(
            Summary = "",
            Description = ""
        )]
        [SwaggerResponse(200, "OK", typeof(CreateDocumentNodeResponse))]
        [SwaggerResponse(400, "Invalid parameter value")]
        [SwaggerResponse(401, "Authentication Required")]
        [SwaggerResponse(404, "Parent folder not found")]
        [SwaggerResponse(500, "Internal Error. Contact API Admin")]
        [Produces("application/json")]
        [DisableRequestSizeLimit] // removes the 2 GB Kestrel cap and any per-endpoint limit
        [RequestFormLimits(
        MultipartBodyLengthLimit = long.MaxValue,
        MultipartHeadersLengthLimit = int.MaxValue,
        ValueLengthLimit = int.MaxValue)]
        public async Task<IActionResult> CreateDocumentNodeAsync(
        string? boType,
        string? boId,
        string? docName,
        IFormFile? file,
        DateTime? expirationDate = null,
        string? documentType = null)
        {
            // Start stopwatch for performance measurement
            var sw = Stopwatch.StartNew(); 
            _logger.Log("Starting CreateDocumentNodeAsync...", LogLevel.INFO);

            // Log method entry with parameters
            _logger.Log($"CreateDocumentNodeAsync called for BO: {boType}/{boId}, Document: {docName}", LogLevel.INFO);
            _logger.Log("---- ENTER CreateDocumentNodeAsync ----", LogLevel.DEBUG);

            // Enable buffering to allow multiple reads of the request body
            Request.EnableBuffering();

            // Read raw request body as string for logging
            string rawBody;
            using (var sr = new StreamReader(Request.Body,
                                             encoding: Encoding.UTF8,
                                             detectEncodingFromByteOrderMarks: false,
                                             leaveOpen: true))          // keep stream open for MVC
            {
                rawBody = await sr.ReadToEndAsync();
                Request.Body.Position = 0;                             // rewind for the model‑binder
            }

            // Attempt to parse form data without triggering a second body read
            IFormCollection? form = null;
            try
            {
                if (Request.HasFormContentType)
                    form = await Request.ReadFormAsync();              // first (and only) parse
            }
            catch (Exception ex)
            {
                _logger.Log($"Form parse failed: {ex.Message}", LogLevel.WARNING);
            }

            // Build a full snapshot of the request for logging
            var dump = new
            {
                Method = Request.Method,
                Scheme = Request.Scheme,
                Host = Request.Host.Value,
                Path = Request.Path.Value,
                QueryString = Request.QueryString.Value,
                Protocol = Request.Protocol,
                ContentType = Request.ContentType,
                ContentLength = Request.ContentLength,
                Headers = Request.Headers
                                     .ToDictionary(h => h.Key, h => h.Value.ToString()),
                HasFormContentType = Request.HasFormContentType,
                FormKeys = form?.Keys,
                FormFiles = form?.Files.Select(f => new
                {
                    f.Name,
                    f.FileName,
                    f.Length,
                    f.ContentType
                }),
                RawBody = rawBody            // ⚠ large; remove if size is an issue
            };

            // Persist the dump (pretty‑printed) using the logger
            _logger.LogRawInbound(
                $"sap_is_dump_{Guid.NewGuid():N}",
                JsonSerializer.Serialize(dump, new JsonSerializerOptions
                {
                    WriteIndented = true
                })
            );

            // Log request size limits for debugging
            _logger.Log($"MaxRequestBodySize feature: {HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>()?.MaxRequestBodySize}", LogLevel.DEBUG);
            _logger.Log($"Content-Length header: {Request.Headers.ContentLength}", LogLevel.DEBUG);

            // Attempt to read form keys and files for logging
            try
            {
                form = Request.HasFormContentType ? Request.Form : null;
                if (form is not null)
                    _logger.Log($"Form keys: {string.Join(", ", form.Keys)} | Files: {form.Files.Count}", LogLevel.DEBUG);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                return StatusCode(400, $"Unable to read multipart body: {ex.Message}");
            }

            // Fill missing parameters from form data if available
            if (form is not null)
            {
                boType ??= form["boType"].FirstOrDefault();
                boId ??= form["boId"].FirstOrDefault();
                docName ??= form["docName"].FirstOrDefault();
                documentType ??= form["documentType"].FirstOrDefault();

                if (expirationDate is null &&
                    DateTime.TryParse(form["expirationDate"].FirstOrDefault(), out var dt))
                {
                    expirationDate = dt;
                }

                file ??= form.Files.FirstOrDefault();
            }

            // Extract authentication ticket from request
            string ticket = "";
            try
            {
                _logger.Log("Extracting authentication ticket from request", LogLevel.DEBUG);
                ticket = _authManager.ExtractTicket(Request);

                // Log masked ticket for security
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

                // Return 401 Unauthorized if extraction fails
                return StatusCode(401, $"Error creating node {ex.Message}");
            }

            // Validate input parameters and log warnings if invalid
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

            // Validate and format Business Object parameters
            _logger.Log($"Validating and formatting BO parameters: {boType}/{boId}", LogLevel.DEBUG);
            var (validatedBoType, formattedBoId) = CRBusinessWorkspace.ValidateAndFormatBoParams(boType, boId);
            _logger.Log($"Validated BO parameters: {validatedBoType}/{formattedBoId}", LogLevel.DEBUG);

            // Search for parent business workspace by BO ID
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

            // Process expiration date if provided
            if (expirationDate.HasValue)
            {
                _logger.Log($"Processing expiration date: {expirationDate.Value}", LogLevel.DEBUG);
                string formattedExpirationDate = expirationDate.Value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

                // Validate expiration date format
                if (DateTime.TryParseExact(formattedExpirationDate,
                                        "yyyy-MM-ddTHH:mm:ss",
                                        CultureInfo.InvariantCulture,
                                        DateTimeStyles.None,
                                        out DateTime parsedDate))
                {
                    _logger.Log("Getting expiration date category ID", LogLevel.DEBUG);
                    var expDateCatId = await _csUtilities.GetExpirationDateCatIdAsync(ticket);
                    _logger.Log($"Expiration date category ID: {expDateCatId}", LogLevel.DEBUG);

                    // Build JSON for node creation with expiration date category
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
                // Build JSON for node creation without expiration date
                var nodeCreationObjectNE = new
                {
                    type = 144,           // Document subtype
                    parent_id = parentId,
                    name = docName
                };
                nodeCreationJson = JsonSerializer.Serialize(nodeCreationObjectNE);
                _logger.Log("Created node creation JSON without expiration date", LogLevel.DEBUG);
            }

            // Log request details excluding file content for size reasons
            _logger.LogRawOutbound("request_create_document_node",
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

            // Build URL for node creation endpoint
            var baseUrl = _settings.BaseUrl;
            var createUrl = $"{baseUrl}/api/v2/nodes";
            _logger.Log($"Node creation URL: {createUrl}", LogLevel.DEBUG);

            // Create HTTP POST request message
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, createUrl);
            requestMessage.Headers.Add("OTCSTICKET", ticket);

            // Build multipart/form-data content for request
            using var formDataContent = new MultipartFormDataContent();

            // Add JSON body part
            var bodyContent = new StringContent(nodeCreationJson, Encoding.UTF8, "text/plain");
            formDataContent.Add(bodyContent, "body");

            // Add file content part
            using var fileStream = file.OpenReadStream();
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                !string.IsNullOrWhiteSpace(file.ContentType) ? file.ContentType : "application/octet-stream");
            formDataContent.Add(fileContent, "file", Uri.EscapeDataString(file.FileName));

            // Attach multipart content to request
            requestMessage.Content = formDataContent;

            HttpResponseMessage response;
            try
            {
                // Send the request to create the document node
                _logger.Log($"Sending request to create document node: {docName}", LogLevel.DEBUG);
                response = await _httpClient.SendAsync(requestMessage);

                // Read response content as string
                var responseJson = await response.Content.ReadAsStringAsync();

                // Log raw inbound response with status and content length (not full content)
                _logger.LogRawOutbound("response_create_document_node",
                    $"Status: {response.StatusCode}, Content length: {responseJson.Length}"
                );

                // Check if response indicates success
                if (!response.IsSuccessStatusCode)
                {
                    _logger.Log($"CreateDocumentNode failed with status code {response.StatusCode}", LogLevel.ERROR);
                    return StatusCode(500, $"CreateDocumentNode failed with status code {response.StatusCode}: {responseJson}");
                }

                _logger.Log("Document node created successfully, parsing response", LogLevel.DEBUG);

                // Parse JSON response to extract node details
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

                    // Apply classification to the created document node
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
                    // Log any errors during parsing or classification
                    _logger.LogException(ex, LogLevel.ERROR);
                    _logger.Log($"Error parsing node response: {ex.Message}", LogLevel.ERROR);
                    nodeResponse.Message = "Node Created but error retrieving node Id.";
                }

                // Log completion of document node creation
                _logger.Log($"Document node creation completed. NodeId: {nodeResponse.NodeId}, Name: {nodeResponse.Name}", LogLevel.INFO);

                // Log the time taken for the operation
                sw.Stop();
                _logger.Log($"CreateDocumentNodeAsync finished in {sw.ElapsedMilliseconds} ms", LogLevel.INFO);


                // Return the node response object
                return Ok(nodeResponse);
            }
            catch (Exception ex)
            {
                // Log exceptions during node creation request
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

            // Initialize ticket variable
            string ticket = "";
            try
            {
                // Extract authentication ticket from request
                _logger.Log("Extracting authentication ticket from request", LogLevel.DEBUG);
                ticket = _authManager.ExtractTicket(Request);

                // Log masked ticket for security
                if (ticket != null && ticket.Length > 12)
                {
                    string maskedTicket = ticket.Substring(0, 8) + "..." + ticket.Substring(ticket.Length - 4);
                    _logger.Log($"Authentication ticket extracted: {maskedTicket}", LogLevel.DEBUG);
                }
            }
            catch (Exception ex)
            {
                // Log exception and return 401 Unauthorized if extraction fails
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error extracting authentication ticket: {ex.Message}", LogLevel.ERROR);
                return StatusCode(401, $"Error deleting node {nodeId}: {ex.Message}");
            }

            try
            {
                // Log inbound request details with node ID
                _logger.LogRawOutbound("request_delete_node",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        nodeId
                    })
                );

                // Call service to delete node by ID using the ticket
                _logger.Log($"Calling DeleteNodeAsync for node ID: {nodeId}", LogLevel.DEBUG);
                await _csNode.DeleteNodeAsync(nodeId, ticket);

                // Log successful deletion
                _logger.Log($"Successfully deleted node with ID: {nodeId}", LogLevel.INFO);

                // Log response indicating success
                _logger.LogRawOutbound("response_delete_node",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        status = "success",
                        message = $"Node {nodeId} deleted successfully"
                    })
                );

                // Return success message with 200 OK
                return Ok($"Node {nodeId} deleted succesfully");
            }
            catch (Exception ex)
            {
                // Log exception and error response details
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error deleting node: {ex.Message}", LogLevel.ERROR);

                _logger.LogRawOutbound("response_delete_node_error",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        error = ex.Message,
                        stack_trace = ex.StackTrace
                    })
                );

                // Return 500 Internal Server Error on failure
                return StatusCode(500, $"Error deleting node {nodeId}: {ex.Message}");
            }
        }
    }
}