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
    /// Controller that handles SimpleMDG asset operations with OpenText Content Server.
    /// Provides endpoints for logos, background images, and user files.
    /// </summary>
    [ApiController]
    [Route("v1/[controller]")]
    public class SimpleMDGAssetsController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly AuthManager _authManager;
        private readonly CSUtilities _csUtilities;
        private static OpenTextSettings _settings;
        private readonly Node _csNode;
        private readonly ILogService _logger;

        /// <summary>
        /// Initializes a new instance of the SimpleMDGAssetsController with required dependencies.
        /// </summary>
        /// <param name="settings">Configuration settings for OpenText API</param>
        /// <param name="csUtilities">Utility methods for Content Server operations</param>
        /// <param name="httpClient">HTTP client for API calls</param>
        /// <param name="authManager">Service for authentication management</param>
        /// <param name="csNode">Service for node operations in Content Server</param>
        /// <param name="logger">Service for logging operations and errors</param>
        public SimpleMDGAssetsController(
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
            _csNode = csNode;
            _logger = logger;

            _logger.Log("SimpleMDGAssetsController initialized", LogLevel.DEBUG);
        }

        /// <summary>
        /// Creates or updates the global logo of SimpleMDG in OpenText.
        /// If a logo already exists, creates a new version.
        /// </summary>
        /// <param name="file">The logo file to upload</param>
        /// <returns>HTTP response with logo information</returns>
        [HttpPost("global/logo")]
        [SwaggerOperation(
            Summary = "Creates or updates the global logo",
            Description = "Stores the SimpleMDG global logo. If it already exists, creates a new version"
        )]
        [SwaggerResponse(200, "OK", typeof(LogoResponse))]
        [SwaggerResponse(400, "Invalid parameters")]
        [SwaggerResponse(401, "Authentication required")]
        [SwaggerResponse(500, "Internal error")]
        [Produces("application/json")]
        public async Task<IActionResult> UpsertGlobalLogo(IFormFile file)
        {
            _logger.Log("UpsertGlobalLogo called", LogLevel.INFO);

            // Get ticket from Request
            string ticket;
            try
            {
                ticket = _authManager.ExtractTicket(Request);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                return StatusCode(401, "Authentication error: " + ex.Message);
            }

            // Validate parameters
            if (file == null || file.Length == 0)
            {
                _logger.Log("Logo file not provided or empty", LogLevel.WARNING);
                return BadRequest("Logo file is required");
            }

            try
            {
                // 1. Ensure folder structure exists
                string assetsNodeId = await EnsureAssetsStructureAsync(ticket);
                string globalFolderId = await EnsureGlobalFolderAsync(assetsNodeId, ticket);
                string logosFolderId = await EnsureSpecificFolderAsync(globalFolderId, "Logos", ticket);

                // 2. Check if logo already exists
                var existingLogos = await _csNode.GetNodeSubNodesAsync(logosFolderId, ticket, "Request");
                string? existingLogoId = null;

                if (existingLogos != null && existingLogos.Count > 0)
                {
                    // Look for a document called "GlobalLogo"
                    var globalLogo = existingLogos.FirstOrDefault(d =>
                        d.Name.Equals("GlobalLogo", StringComparison.OrdinalIgnoreCase));

                    if (globalLogo != null)
                    {
                        existingLogoId = globalLogo.NodeId;
                        _logger.Log($"Existing global logo found: {existingLogoId}", LogLevel.DEBUG);
                    }
                }

                // 3. Create logo or update version
                LogoResponse response = new LogoResponse { Success = true };

                if (existingLogoId != null)
                {
                    // Update version by adding a new version
                    _logger.Log($"Adding new version to existing logo: {existingLogoId}", LogLevel.DEBUG);

                    // Prepare URL for adding a new version
                    var baseUrl = _settings.BaseUrl;
                    var updateUrl = $"{baseUrl}/api/v2/nodes/{existingLogoId}/versions";

                    using var requestMessage = new HttpRequestMessage(HttpMethod.Post, updateUrl);
                    requestMessage.Headers.Add("OTCSTICKET", ticket);

                    // Create multipart content for file upload
                    using var formDataContent = new MultipartFormDataContent();

                    using var fileStream = file.OpenReadStream();
                    var fileContent = new StreamContent(fileStream);
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                        !string.IsNullOrWhiteSpace(file.ContentType) ? file.ContentType : "application/octet-stream");
                    formDataContent.Add(fileContent, "file", Uri.EscapeDataString(file.FileName));

                    // Add description for version (optional)
                    var versionDescription = $"Updated on {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}";
                    var descriptionContent = new StringContent(versionDescription);
                    formDataContent.Add(descriptionContent, "description");

                    // Set as major version (optional, set to true for major version)
                    var majorVersionContent = new StringContent("true");
                    formDataContent.Add(majorVersionContent, "add_major_version");

                    requestMessage.Content = formDataContent;

                    // Send request
                    var httpResponse = await _httpClient.SendAsync(requestMessage);

                    if (!httpResponse.IsSuccessStatusCode)
                    {
                        var errorContent = await httpResponse.Content.ReadAsStringAsync();
                        _logger.Log($"Error adding version: {httpResponse.StatusCode} - {errorContent}", LogLevel.ERROR);
                        throw new Exception($"Error adding version to logo: {httpResponse.StatusCode}");
                    }

                    // Read and process response
                    var responseContent = await httpResponse.Content.ReadAsStringAsync();
                    _logger.LogRawOutbound("response_update_logo", responseContent);

                    // Prepare response
                    response.Message = "Global logo updated successfully with new version";
                    response.FileId = existingLogoId;
                    response.FileName = file.FileName;
                    response.DownloadUrl = $"{baseUrl}/api/v2/nodes/{existingLogoId}/content";
                    response.FileSize = file.Length;
                    response.LastModified = DateTime.Now;

                    // Try to extract version information from the response
                    try
                    {
                        using var jsonDoc = JsonDocument.Parse(responseContent);

                        if (jsonDoc.RootElement.TryGetProperty("results", out var resultsElem) &&
                            resultsElem.ValueKind == JsonValueKind.Array &&
                            resultsElem.GetArrayLength() > 0)
                        {
                            var firstResult = resultsElem[0];

                            if (firstResult.TryGetProperty("data", out var dataElem) &&
                                dataElem.ValueKind == JsonValueKind.Array &&
                                dataElem.GetArrayLength() > 0)
                            {
                                var data = dataElem[0];

                                if (data.TryGetProperty("versions", out var versionsElem) &&
                                    versionsElem.ValueKind == JsonValueKind.Array &&
                                    versionsElem.GetArrayLength() > 0)
                                {
                                    var version = versionsElem[0];

                                    if (version.TryGetProperty("version_number", out var versionNumberElem))
                                    {
                                        response.Version = versionNumberElem.GetInt32();
                                    }
                                    else if (version.TryGetProperty("version_major_number", out var majorNumberElem) &&
                                             version.TryGetProperty("version_minor_number", out var minorNumberElem))
                                    {
                                        // Construct version number from major and minor parts if available
                                        response.Version = majorNumberElem.GetInt32();
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogException(ex, LogLevel.WARNING);
                        // Continue even if we can't get version
                    }
                }
                else
                {
                    // Create new logo
                    _logger.Log("Creating new global logo", LogLevel.DEBUG);

                    // Create object for node creation
                    var nodeCreationObject = new
                    {
                        type = 144,  // Document
                        parent_id = logosFolderId,
                        name = "GlobalLogo"
                    };

                    var nodeCreationJson = JsonSerializer.Serialize(nodeCreationObject);

                    // Prepare URL and request for creation
                    var baseUrl = _settings.BaseUrl;
                    var createUrl = $"{baseUrl}/api/v2/nodes";

                    using var requestMessage = new HttpRequestMessage(HttpMethod.Post, createUrl);
                    requestMessage.Headers.Add("OTCSTICKET", ticket);

                    // Create multipart content
                    using var formDataContent = new MultipartFormDataContent();

                    var bodyContent = new StringContent(nodeCreationJson, Encoding.UTF8, "text/plain");
                    formDataContent.Add(bodyContent, "body");

                    using var fileStream = file.OpenReadStream();
                    var fileContent = new StreamContent(fileStream);
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                        !string.IsNullOrWhiteSpace(file.ContentType) ? file.ContentType : "application/octet-stream");
                    formDataContent.Add(fileContent, "file", Uri.EscapeDataString(file.FileName));

                    requestMessage.Content = formDataContent;

                    // Send request
                    var httpResponse = await _httpClient.SendAsync(requestMessage);

                    if (!httpResponse.IsSuccessStatusCode)
                    {
                        throw new Exception($"Error creating logo: {httpResponse.StatusCode}");
                    }

                    // Read and process response
                    var responseContent = await httpResponse.Content.ReadAsStringAsync();
                    _logger.LogRawOutbound("response_create_logo", responseContent);

                    // Extract new node ID
                    string createdNodeId = "";
                    try
                    {
                        using var doc = JsonDocument.Parse(responseContent);
                        if (doc.RootElement.TryGetProperty("results", out var resultsElem) &&
                            resultsElem.TryGetProperty("data", out var dataElem) &&
                            dataElem.TryGetProperty("properties", out var propsElem) &&
                            propsElem.TryGetProperty("id", out var idElem))
                        {
                            createdNodeId = idElem.GetInt32().ToString();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogException(ex, LogLevel.ERROR);
                        throw new Exception("Error extracting created logo ID");
                    }

                    // Prepare response
                    response.Message = "Global logo created successfully";
                    response.FileId = createdNodeId;
                    response.FileName = "GlobalLogo";
                    response.DownloadUrl = $"{baseUrl}/api/v2/nodes/{createdNodeId}/content";
                    response.LastModified = DateTime.Now;
                    response.Version = 1;
                }

                _logger.Log("Global logo processed successfully", LogLevel.INFO);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                return StatusCode(500, new LogoResponse
                {
                    Success = false,
                    Message = $"Error processing logo: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Retrieves the global logo of SimpleMDG from OpenText.
        /// </summary>
        /// <returns>HTTP response with logo node information including binary content</returns>
        [HttpGet("global/logo")]
        [SwaggerOperation(
            Summary = "Gets the global logo",
            Description = "Retrieves the SimpleMDG global logo stored in OpenText"
        )]
        [SwaggerResponse(200, "OK", typeof(NodeResponse))]
        [SwaggerResponse(401, "Authentication required")]
        [SwaggerResponse(404, "Logo not found")]
        [SwaggerResponse(500, "Internal error")]
        [Produces("application/json")]
        public async Task<IActionResult> GetGlobalLogo()
        {
            _logger.Log("GetGlobalLogo called", LogLevel.INFO);

            // Get ticket from Request
            string ticket;
            try
            {
                ticket = _authManager.ExtractTicket(Request);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                return StatusCode(401, "Authentication error: " + ex.Message);
            }

            try
            {
                // 1. Find folder structure
                string assetsNodeId = await FindAssetsStructureAsync(ticket);
                if (assetsNodeId == null)
                {
                    return StatusCode(404, "Assets folder does not exist");
                }

                string globalFolderId = await FindFolderAsync(assetsNodeId, "Global", ticket);
                if (globalFolderId == null)
                {
                    return StatusCode(404, "Global resources folder does not exist");
                }

                string logosFolderId = await FindFolderAsync(globalFolderId, "Logos", ticket);
                if (logosFolderId == null)
                {
                    return StatusCode(404, "Logos folder does not exist");
                }

                // 2. Find the logo
                var existingLogos = await _csNode.GetNodeSubNodesAsync(logosFolderId, ticket, "Request");
                var globalLogo = existingLogos?.FirstOrDefault(d =>
                    d.Name.Equals("GlobalLogo", StringComparison.OrdinalIgnoreCase));

                if (globalLogo == null)
                {
                    return StatusCode(404, "Global logo does not exist");
                }

                // 3. Get logo information including binary content
                int logoId = int.Parse(globalLogo.NodeId);
                var logoNode = await _csNode.GetNodeByIdAsync(logoId, ticket);
                if (logoNode == null)
                {
                    return StatusCode(404, "Could not retrieve global logo content");
                }

                // 4. Log successful retrieval (metadata only, not content)
                _logger.Log($"Successfully retrieved logo with ID: {logoId}, Name: {logoNode.file_name}", LogLevel.INFO);
                _logger.LogRawInbound("response_get_logo",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        nodeId = logoNode.nodeId,
                        fileName = logoNode.file_name,
                        type = logoNode.type,
                        typeName = logoNode.type_name,
                        contentSize = logoNode.Content?.Length ?? 0
                    }));

                // 5. Return the entire node response with binary content
                return Ok(logoNode);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error retrieving logo: {ex.Message}", LogLevel.ERROR);

                // Log error response
                _logger.LogRawInbound("response_get_logo_error",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        error = ex.Message,
                        stack_trace = ex.StackTrace
                    }));

                // Return 500 if something unexpected happens
                return StatusCode(500, ex.Message);
            }
        }

        /// <summary>
        /// Deletes the global logo of SimpleMDG from OpenText.
        /// </summary>
        /// <returns>HTTP response with deletion result</returns>
        [HttpDelete("global/logo")]
        [SwaggerOperation(
            Summary = "Deletes the global logo",
            Description = "Deletes the SimpleMDG global logo stored in OpenText"
        )]
        [SwaggerResponse(200, "OK")]
        [SwaggerResponse(401, "Authentication required")]
        [SwaggerResponse(404, "Logo not found")]
        [SwaggerResponse(500, "Internal error")]
        [Produces("application/json")]
        public async Task<IActionResult> DeleteGlobalLogo()
        {
            _logger.Log("DeleteGlobalLogo called", LogLevel.INFO);

            // Get ticket from Request
            string ticket;
            try
            {
                ticket = _authManager.ExtractTicket(Request);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                return StatusCode(401, "Authentication error: " + ex.Message);
            }

            try
            {
                // 1. Find folder structure
                string assetsNodeId = await FindAssetsStructureAsync(ticket);
                if (assetsNodeId == null)
                {
                    return StatusCode(404, "Assets folder does not exist");
                }

                string globalFolderId = await FindFolderAsync(assetsNodeId, "Global", ticket);
                if (globalFolderId == null)
                {
                    return StatusCode(404, "Global resources folder does not exist");
                }

                string logosFolderId = await FindFolderAsync(globalFolderId, "Logos", ticket);
                if (logosFolderId == null)
                {
                    return StatusCode(404, "Logos folder does not exist");
                }

                // 2. Find the logo
                var existingLogos = await _csNode.GetNodeSubNodesAsync(logosFolderId, ticket, "Request");
                var globalLogo = existingLogos?.FirstOrDefault(d =>
                    d.Name.Equals("GlobalLogo", StringComparison.OrdinalIgnoreCase));

                if (globalLogo == null)
                {
                    return StatusCode(404, "Global logo does not exist");
                }

                // 3. Delete the logo
                await _csNode.DeleteNodeAsync(globalLogo.NodeId, ticket, "MDG");

                _logger.Log("Global logo deleted successfully", LogLevel.INFO);
                return Ok("Global logo deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                return StatusCode(500, $"Error deleting logo: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a new background image in OpenText.
        /// </summary>
        /// <param name="file">The image file to upload</param>
        /// <param name="displayName">The display name for the background image</param>
        /// <returns>HTTP response with background image information</returns>
        [HttpPost("global/background")]
        [SwaggerOperation(
            Summary = "Creates a new background image",
            Description = "Stores a new background image in OpenText"
        )]
        [SwaggerResponse(200, "OK", typeof(BackgroundImageResponse))]
        [SwaggerResponse(400, "Invalid parameters")]
        [SwaggerResponse(401, "Authentication required")]
        [SwaggerResponse(500, "Internal error")]
        [Produces("application/json")]
        public async Task<IActionResult> CreateBackgroundImage(IFormFile file, [FromForm] string displayName)
        {
            _logger.Log($"CreateBackgroundImage called: displayName={displayName}", LogLevel.INFO);

            // Get ticket from Request
            string ticket;
            try
            {
                ticket = _authManager.ExtractTicket(Request);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                return StatusCode(401, "Authentication error: " + ex.Message);
            }

            // Validate parameters
            if (file == null || file.Length == 0)
            {
                _logger.Log("Image file not provided or empty", LogLevel.WARNING);
                return BadRequest("Image file is required");
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                _logger.Log("Display name not provided", LogLevel.WARNING);
                return BadRequest("Display name is required");
            }

            try
            {
                // 1. Ensure folder structure exists
                string assetsNodeId = await EnsureAssetsStructureAsync(ticket);
                string globalFolderId = await EnsureGlobalFolderAsync(assetsNodeId, ticket);
                string backgroundsFolderId = await EnsureSpecificFolderAsync(globalFolderId, "Background Images", ticket);

                // 2. Check if image with the same name already exists
                var existingBackground = await _csNode.FindBackgroundImageByNameAsync(backgroundsFolderId, displayName, ticket);
                if (existingBackground != null)
                {
                    _logger.Log($"A background image with name {displayName} already exists", LogLevel.WARNING);
                    return BadRequest(new BackgroundImageResponse
                    {
                        Success = false,
                        Message = $"A background image with name {displayName} already exists"
                    });
                }

                // 3. Create the new background image
                _logger.Log($"Creating new background image: {displayName}", LogLevel.DEBUG);

                // Create object for node creation
                var nodeCreationObject = new
                {
                    type = 144,  // Document
                    parent_id = backgroundsFolderId,
                    name = displayName
                };

                var nodeCreationJson = JsonSerializer.Serialize(nodeCreationObject);

                // Prepare URL and request for creation
                var baseUrl = _settings.BaseUrl;
                var createUrl = $"{baseUrl}/api/v2/nodes";

                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, createUrl);
                requestMessage.Headers.Add("OTCSTICKET", ticket);

                // Create multipart content
                using var formDataContent = new MultipartFormDataContent();

                var bodyContent = new StringContent(nodeCreationJson, Encoding.UTF8, "text/plain");
                formDataContent.Add(bodyContent, "body");

                using var fileStream = file.OpenReadStream();
                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                    !string.IsNullOrWhiteSpace(file.ContentType) ? file.ContentType : "application/octet-stream");
                formDataContent.Add(fileContent, "file", Uri.EscapeDataString(file.FileName));

                requestMessage.Content = formDataContent;

                // Send request
                var httpResponse = await _httpClient.SendAsync(requestMessage);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    throw new Exception($"Error creating background image: {httpResponse.StatusCode}");
                }

                // Read and process response
                var responseContent = await httpResponse.Content.ReadAsStringAsync();
                _logger.LogRawOutbound("response_create_background", responseContent);

                // Extract new node ID
                string createdNodeId = "";
                try
                {
                    using var doc = JsonDocument.Parse(responseContent);
                    if (doc.RootElement.TryGetProperty("results", out var resultsElem) &&
                        resultsElem.TryGetProperty("data", out var dataElem) &&
                        dataElem.TryGetProperty("properties", out var propsElem) &&
                        propsElem.TryGetProperty("id", out var idElem))
                    {
                        createdNodeId = idElem.GetInt32().ToString();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, LogLevel.ERROR);
                    throw new Exception("Error extracting created image ID");
                }

                // Prepare response
                var response = new BackgroundImageResponse
                {
                    Success = true,
                    Message = "Background image created successfully",
                    BackgroundId = createdNodeId,
                    DisplayName = displayName,
                    FileName = file.FileName,
                    DownloadUrl = $"{baseUrl}/api/v2/nodes/{createdNodeId}/content",
                    FileSize = file.Length,
                    LastModified = DateTime.Now
                };

                _logger.Log("Background image created successfully", LogLevel.INFO);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                return StatusCode(500, new BackgroundImageResponse
                {
                    Success = false,
                    Message = $"Error processing background image: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Retrieves a background image by its name.
        /// </summary>
        /// <param name="name">Name of the background image to retrieve</param>
        /// <returns>HTTP response with background image node information including binary content</returns>
        [HttpGet("global/background/byname/{name}")]
        [SwaggerOperation(
            Summary = "Gets a background image by name",
            Description = "Retrieves a specific background image by its exact name"
        )]
        [SwaggerResponse(200, "OK", typeof(NodeResponse))]
        [SwaggerResponse(401, "Authentication required")]
        [SwaggerResponse(404, "Image not found")]
        [SwaggerResponse(500, "Internal error")]
        [Produces("application/json")]
        public async Task<IActionResult> GetBackgroundImageByName(string name)
        {
            _logger.Log($"GetBackgroundImageByName called: name={name}", LogLevel.INFO);

            // Get ticket from Request
            string ticket;
            try
            {
                ticket = _authManager.ExtractTicket(Request);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                return StatusCode(401, "Authentication error: " + ex.Message);
            }

            try
            {
                // 1. Find folder structure
                string assetsNodeId = await FindAssetsStructureAsync(ticket);
                if (assetsNodeId == null)
                {
                    return StatusCode(404, "Assets folder does not exist");
                }

                string globalFolderId = await FindFolderAsync(assetsNodeId, "Global", ticket);
                if (globalFolderId == null)
                {
                    return StatusCode(404, "Global resources folder does not exist");
                }

                string backgroundsFolderId = await FindFolderAsync(globalFolderId, "Background Images", ticket);
                if (backgroundsFolderId == null)
                {
                    return StatusCode(404, "Background images folder does not exist");
                }

                // 2. Find the background image by name
                var backgroundImages = await _csNode.GetNodeSubNodesAsync(backgroundsFolderId, ticket, "Request");
                var backgroundImage = backgroundImages?.FirstOrDefault(d =>
                    d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                if (backgroundImage == null)
                {
                    return StatusCode(404, $"Background image with name '{name}' not found");
                }

                // 3. Get the node with its binary content
                int bgId = int.Parse(backgroundImage.NodeId);
                var backgroundNode = await _csNode.GetNodeByIdAsync(bgId, ticket);
                if (backgroundNode == null)
                {
                    return StatusCode(404, "Could not retrieve background image content");
                }

                // 4. Log successful retrieval (metadata only, not content)
                _logger.Log($"Successfully retrieved background image with name: {name}, ID: {bgId}", LogLevel.INFO);
                _logger.LogRawInbound("response_get_background",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        nodeId = backgroundNode.nodeId,
                        fileName = backgroundNode.file_name,
                        type = backgroundNode.type,
                        typeName = backgroundNode.type_name,
                        contentSize = backgroundNode.Content?.Length ?? 0
                    }));

                // 5. Return the entire node response with binary content
                return Ok(backgroundNode);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error retrieving background image: {ex.Message}", LogLevel.ERROR);

                // Log error response
                _logger.LogRawInbound("response_get_background_error",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        error = ex.Message,
                        stack_trace = ex.StackTrace
                    }));

                // Return 500 if something unexpected happens
                return StatusCode(500, ex.Message);
            }
        }

        /// <summary>
        /// Updates an existing background image by name.
        /// </summary>
        /// <param name="name">Name of the background image to update</param>
        /// <param name="file">The new image file</param>
        /// <returns>HTTP response with updated background image information</returns>
        [HttpPut("global/background/byname/{name}")]
        [SwaggerOperation(
            Summary = "Updates a background image by name",
            Description = "Updates an existing background image in OpenText by its name"
        )]
        [SwaggerResponse(200, "OK", typeof(BackgroundImageResponse))]
        [SwaggerResponse(400, "Invalid parameters")]
        [SwaggerResponse(401, "Authentication required")]
        [SwaggerResponse(404, "Image not found")]
        [SwaggerResponse(500, "Internal error")]
        [Produces("application/json")]
        public async Task<IActionResult> UpdateBackgroundImageByName(string name, IFormFile file)
        {
            _logger.Log($"UpdateBackgroundImageByName called: name={name}", LogLevel.INFO);

            // Get ticket from Request
            string ticket;
            try
            {
                ticket = _authManager.ExtractTicket(Request);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                return StatusCode(401, "Authentication error: " + ex.Message);
            }

            // Validate parameters
            if (file == null || file.Length == 0)
            {
                _logger.Log("Image file not provided or empty", LogLevel.WARNING);
                return BadRequest("Image file is required");
            }

            try
            {
                // 1. Find folder structure
                string assetsNodeId = await FindAssetsStructureAsync(ticket);
                if (assetsNodeId == null)
                {
                    return StatusCode(404, new BackgroundImageResponse
                    {
                        Success = false,
                        Message = "Assets folder does not exist"
                    });
                }

                string globalFolderId = await FindFolderAsync(assetsNodeId, "Global", ticket);
                if (globalFolderId == null)
                {
                    return StatusCode(404, new BackgroundImageResponse
                    {
                        Success = false,
                        Message = "Global resources folder does not exist"
                    });
                }

                string backgroundsFolderId = await FindFolderAsync(globalFolderId, "Background Images", ticket);
                if (backgroundsFolderId == null)
                {
                    return StatusCode(404, new BackgroundImageResponse
                    {
                        Success = false,
                        Message = "Background images folder does not exist"
                    });
                }

                // 2. Find the background image by name
                var backgroundImages = await _csNode.GetNodeSubNodesAsync(backgroundsFolderId, ticket, "Request");
                var backgroundImage = backgroundImages?.FirstOrDefault(d =>
                    d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                if (backgroundImage == null)
                {
                    return StatusCode(404, new BackgroundImageResponse
                    {
                        Success = false,
                        Message = $"Background image with name '{name}' not found"
                    });
                }

                string backgroundId = backgroundImage.NodeId;

                // 3. Add new version using the correct API
                var baseUrl = _settings.BaseUrl;
                var updateUrl = $"{baseUrl}/api/v2/nodes/{backgroundId}/versions";

                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, updateUrl);
                requestMessage.Headers.Add("OTCSTICKET", ticket);

                // Create multipart content for file upload
                using var formDataContent = new MultipartFormDataContent();

                using var fileStream = file.OpenReadStream();
                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                    !string.IsNullOrWhiteSpace(file.ContentType) ? file.ContentType : "application/octet-stream");
                formDataContent.Add(fileContent, "file", Uri.EscapeDataString(file.FileName));

                // Add description for version (optional)
                var versionDescription = $"Updated on {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}";
                var descriptionContent = new StringContent(versionDescription);
                formDataContent.Add(descriptionContent, "description");

                // Set as major version (optional, set to true for major version)
                var majorVersionContent = new StringContent("true");
                formDataContent.Add(majorVersionContent, "add_major_version");

                requestMessage.Content = formDataContent;

                // Send request
                var httpResponse = await _httpClient.SendAsync(requestMessage);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    var errorContent = await httpResponse.Content.ReadAsStringAsync();
                    _logger.Log($"Error adding version: {httpResponse.StatusCode} - {errorContent}", LogLevel.ERROR);
                    throw new Exception($"Error adding version to background image: {httpResponse.StatusCode}");
                }

                // Read and process response
                var responseContent = await httpResponse.Content.ReadAsStringAsync();
                _logger.LogRawOutbound("response_update_background", responseContent);

                // Get updated node information
                var updatedNode = await _csNode.GetNodeByIdAsync(int.Parse(backgroundId), ticket);

                // Prepare response
                var response = new BackgroundImageResponse
                {
                    Success = true,
                    Message = "Background image updated successfully with new version",
                    BackgroundId = backgroundId,
                    DisplayName = name,
                    FileName = file.FileName,
                    DownloadUrl = $"{baseUrl}/api/v2/nodes/{backgroundId}/content",
                    FileSize = file.Length,
                    LastModified = DateTime.Now
                };

                // Try to extract version information from the response
                try
                {
                    using var jsonDoc = JsonDocument.Parse(responseContent);

                    if (jsonDoc.RootElement.TryGetProperty("results", out var resultsElem) &&
                        resultsElem.ValueKind == JsonValueKind.Array &&
                        resultsElem.GetArrayLength() > 0)
                    {
                        var firstResult = resultsElem[0];

                        if (firstResult.TryGetProperty("data", out var dataElem) &&
                            dataElem.ValueKind == JsonValueKind.Array &&
                            dataElem.GetArrayLength() > 0)
                        {
                            var data = dataElem[0];

                            if (data.TryGetProperty("versions", out var versionsElem) &&
                                versionsElem.ValueKind == JsonValueKind.Array &&
                                versionsElem.GetArrayLength() > 0)
                            {
                                var version = versionsElem[0];

                                if (version.TryGetProperty("version_number", out var versionNumberElem))
                                {
                                    // No property available to store version in BackgroundImageResponse
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, LogLevel.WARNING);
                    // Continue even if we can't get version
                }

                _logger.Log($"Background image '{name}' updated successfully", LogLevel.INFO);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                return StatusCode(500, new BackgroundImageResponse
                {
                    Success = false,
                    Message = $"Error updating background image: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Lists all available background images.
        /// </summary>
        /// <param name="searchTerm">Optional search term to filter images by name</param>
        /// <param name="pageSize">Number of items per page</param>
        /// <param name="pageNumber">Page number (1-based)</param>
        /// <returns>HTTP response with list of background images</returns>
        [HttpGet("global/backgrounds")]
        [SwaggerOperation(
            Summary = "Lists all background images",
            Description = "Retrieves all available background images in OpenText"
        )]
        [SwaggerResponse(200, "OK", typeof(BackgroundImagesListResponse))]
        [SwaggerResponse(401, "Authentication required")]
        [SwaggerResponse(500, "Internal error")]
        [Produces("application/json")]
        public async Task<IActionResult> ListBackgroundImages([FromQuery] string? searchTerm = null, [FromQuery] int pageSize = 20, [FromQuery] int pageNumber = 1)
        {
            _logger.Log($"ListBackgroundImages called: searchTerm={searchTerm}, pageSize={pageSize}, pageNumber={pageNumber}", LogLevel.INFO);

            // Get ticket from Request
            string ticket;
            try
            {
                ticket = _authManager.ExtractTicket(Request);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                return StatusCode(401, "Authentication error: " + ex.Message);
            }

            try
            {
                // 1. Find folder structure
                string assetsNodeId = await FindAssetsStructureAsync(ticket);
                if (assetsNodeId == null)
                {
                    return Ok(new BackgroundImagesListResponse
                    {
                        Success = true,
                        Message = "No background images available",
                        TotalCount = 0,
                        PageSize = pageSize,
                        PageNumber = pageNumber,
                        TotalPages = 0,
                        Images = new List<BackgroundImageInfo>()
                    });
                }

                string globalFolderId = await FindFolderAsync(assetsNodeId, "Global", ticket);
                if (globalFolderId == null)
                {
                    return Ok(new BackgroundImagesListResponse
                    {
                        Success = true,
                        Message = "No background images available",
                        TotalCount = 0,
                        PageSize = pageSize,
                        PageNumber = pageNumber,
                        TotalPages = 0,
                        Images = new List<BackgroundImageInfo>()
                    });
                }

                string backgroundsFolderId = await FindFolderAsync(globalFolderId, "Background Images", ticket);
                if (backgroundsFolderId == null)
                {
                    return Ok(new BackgroundImagesListResponse
                    {
                        Success = true,
                        Message = "No background images available",
                        TotalCount = 0,
                        PageSize = pageSize,
                        PageNumber = pageNumber,
                        TotalPages = 0,
                        Images = new List<BackgroundImageInfo>()
                    });
                }

                // 2. Get all background images
                var allBackgrounds = await _csNode.GetNodeSubNodesAsync(backgroundsFolderId, ticket, "Request");
                var filteredBackgrounds = allBackgrounds;

                // Filter by search term if provided
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    filteredBackgrounds = allBackgrounds.Where(b =>
                        b.Name.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                }

                // Calculate pagination
                int totalCount = filteredBackgrounds.Count;
                int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                // Apply pagination
                var pagedBackgrounds = filteredBackgrounds
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // Build response
                var baseUrl = _settings.BaseUrl;
                var backgroundInfos = new List<BackgroundImageInfo>();

                foreach (var background in pagedBackgrounds)
                {
                    try
                    {
                        var node = await _csNode.GetNodeByIdAsync(int.Parse(background.NodeId), ticket);
                        backgroundInfos.Add(new BackgroundImageInfo
                        {
                            BackgroundId = background.NodeId,
                            DisplayName = background.Name,
                            //FileName = node.file_name,
                            //ThumbnailUrl = $"{baseUrl}/api/v1/nodes/{background.NodeId}/thumbnails/medium/content",
                            FileSize = node.Content?.Length ?? 0,
                            //LastModified = DateTime.Now  // Ideally get this from the node
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogException(ex, LogLevel.WARNING);
                        // Continue with next if there's an error
                    }
                }

                var response = new BackgroundImagesListResponse
                {
                    Success = true,
                    TotalCount = totalCount,
                    PageSize = pageSize,
                    PageNumber = pageNumber,
                    TotalPages = totalPages,
                    Images = backgroundInfos
                };

                if (backgroundInfos.Count == 0)
                {
                    response.Message = "No background images found";
                }
                else
                {
                    response.Message = $"Found {totalCount} background images";
                }

                _logger.Log($"ListBackgroundImages completed: {backgroundInfos.Count} images found", LogLevel.INFO);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                return StatusCode(500, new BackgroundImagesListResponse
                {
                    Success = false,
                    Message = $"Error listing background images: {ex.Message}",
                    Images = new List<BackgroundImageInfo>()
                });
            }
        }

        /// <summary>
        /// Deletes a background image by its name.
        /// </summary>
        /// <param name="name">Name of the background image to delete</param>
        /// <returns>HTTP response with deletion result</returns>
        [HttpDelete("global/background/byname/{name}")]
        [SwaggerOperation(
            Summary = "Deletes a background image by name",
            Description = "Deletes a specific background image from OpenText by its name"
        )]
        [SwaggerResponse(200, "OK")]
        [SwaggerResponse(401, "Authentication required")]
        [SwaggerResponse(404, "Image not found")]
        [SwaggerResponse(500, "Internal error")]
        [Produces("application/json")]
        public async Task<IActionResult> DeleteBackgroundImageByName(string name)
        {
            _logger.Log($"DeleteBackgroundImageByName called: name={name}", LogLevel.INFO);

            // Get ticket from Request
            string ticket;
            try
            {
                ticket = _authManager.ExtractTicket(Request);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                return StatusCode(401, "Authentication error: " + ex.Message);
            }

            try
            {
                // 1. Find folder structure
                string assetsNodeId = await FindAssetsStructureAsync(ticket);
                if (assetsNodeId == null)
                {
                    return StatusCode(404, "Assets folder does not exist");
                }

                string globalFolderId = await FindFolderAsync(assetsNodeId, "Global", ticket);
                if (globalFolderId == null)
                {
                    return StatusCode(404, "Global resources folder does not exist");
                }

                string backgroundsFolderId = await FindFolderAsync(globalFolderId, "Background Images", ticket);
                if (backgroundsFolderId == null)
                {
                    return StatusCode(404, "Background images folder does not exist");
                }

                // 2. Find the background image by name
                var backgroundImages = await _csNode.GetNodeSubNodesAsync(backgroundsFolderId, ticket, "Request");
                var backgroundImage = backgroundImages?.FirstOrDefault(d =>
                    d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                if (backgroundImage == null)
                {
                    return StatusCode(404, $"Background image with name '{name}' not found");
                }

                // 3. Delete the node
                await _csNode.DeleteNodeAsync(backgroundImage.NodeId, ticket, "MDG");

                _logger.Log($"Background image '{name}' deleted successfully", LogLevel.INFO);
                return Ok($"Background image '{name}' deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                return StatusCode(500, $"Error deleting background image: {ex.Message}");
            }
        }

        // Additional methods for user avatar and attachments can be implemented here

        #region Helper Methods

        /// <summary>
        /// Ensures the Assets folder structure exists.
        /// </summary>
        /// <param name="ticket">Authentication ticket</param>
        /// <returns>Node ID of the Assets folder</returns>
        private async Task<string> EnsureAssetsStructureAsync(string ticket)
        {
            _logger.Log("Ensuring Assets folder structure", LogLevel.DEBUG);

            // Find or create "Assets" folder in root
            var baseUrl = _settings.BaseUrl;
            var enterpriseWsId = _settings.RootFolderId;

            // First try to find the folder
            string? assetsNodeId = await FindFolderAsync(enterpriseWsId, "Assets", ticket);

            if (assetsNodeId == null)
            {
                // Create folder if it doesn't exist
                _logger.Log("'Assets' folder not found, creating...", LogLevel.DEBUG);
                assetsNodeId = (await _csNode.CreateFolderAsync(enterpriseWsId, "Assets", ticket)).ToString();
            }

            return assetsNodeId;
        }

        /// <summary>
        /// Ensures the Global folder exists within Assets.
        /// </summary>
        /// <param name="assetsNodeId">Node ID of the Assets folder</param>
        /// <param name="ticket">Authentication ticket</param>
        /// <returns>Node ID of the Global folder</returns>
        private async Task<string> EnsureGlobalFolderAsync(string assetsNodeId, string ticket)
        {
            _logger.Log("Ensuring Global folder", LogLevel.DEBUG);

            // Find or create "Global" folder within Assets
            string? globalNodeId = await FindFolderAsync(assetsNodeId, "Global", ticket);

            if (globalNodeId == null)
            {
                // Create folder if it doesn't exist
                _logger.Log("'Global' folder not found, creating...", LogLevel.DEBUG);
                globalNodeId = (await _csNode.CreateFolderAsync(assetsNodeId, "Global", ticket)).ToString();
            }

            return globalNodeId;
        }


        /// <summary>
        /// Ensures a specific folder exists within a parent node.
        /// </summary>
        /// <param name="parentNodeId">Node ID of the parent folder</param>
        /// <param name="folderName">Name of the folder to ensure</param>
        /// <param name="ticket">Authentication ticket</param>
        /// <returns>Node ID of the specific folder</returns>
        private async Task<string> EnsureSpecificFolderAsync(string parentNodeId, string folderName, string ticket)
        {
            _logger.Log($"Ensuring specific folder: {folderName}", LogLevel.DEBUG);

            // Find or create specific folder
            string? folderNodeId = await FindFolderAsync(parentNodeId, folderName, ticket);

            if (folderNodeId == null)
            {
                // Create folder if it doesn't exist
                _logger.Log($"'{folderName}' folder not found, creating...", LogLevel.DEBUG);
                folderNodeId = (await _csNode.CreateFolderAsync(parentNodeId, folderName, ticket)).ToString();
            }

            return folderNodeId;
        }

        /// <summary>
        /// Finds the Assets folder structure.
        /// </summary>
        /// <param name="ticket">Authentication ticket</param>
        /// <returns>Node ID of the Assets folder, or null if not found</returns>
        private async Task<string?> FindAssetsStructureAsync(string ticket)
        {
            _logger.Log("Finding Assets folder structure", LogLevel.DEBUG);

            // Find "Assets" folder in root
            var baseUrl = _settings.BaseUrl;
            var enterpriseWsId = _settings.RootFolderId;

            return await FindFolderAsync(enterpriseWsId, "Assets", ticket);
        }

        /// <summary>
        /// Finds a specific folder within a parent node.
        /// </summary>
        /// <param name="parentNodeId">Node ID of the parent folder</param>
        /// <param name="folderName">Name of the folder to find</param>
        /// <param name="ticket">Authentication ticket</param>
        /// <returns>Node ID of the folder, or null if not found</returns>
        private async Task<string?> FindFolderAsync(string parentNodeId, string folderName, string ticket)
        {
            _logger.Log($"Finding folder: {folderName} in parent node: {parentNodeId}", LogLevel.DEBUG);

            try
            {
                // Get subnodes
                var folders = await _csNode.GetNodeSubFoldersAsync(parentNodeId, ticket, "Request");

                // Find folder by name
                var folder = folders?.FirstOrDefault(f =>
                    f.Name.Equals(folderName, StringComparison.OrdinalIgnoreCase));

                if (folder != null)
                {
                    _logger.Log($"Folder '{folderName}' found: {folder.NodeId}", LogLevel.DEBUG);
                    return folder.NodeId;
                }

                _logger.Log($"Folder '{folderName}' not found", LogLevel.DEBUG);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.WARNING);
                return null;
            }
        }

        #endregion
    }
}