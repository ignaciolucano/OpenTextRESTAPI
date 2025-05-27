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
    /// Controller for managing user assets in SimpleMDG with OpenText Content Server.
    /// Provides endpoints for user folders, avatars, and attachments.
    /// </summary>
    [ApiController]
    [Route("v1/[controller]")]
    public class SimpleMDGUserAssetsController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly AuthManager _authManager;
        private readonly CSUtilities _csUtilities;
        private static OpenTextSettings _settings;
        private readonly Node _csNode;
        private readonly ILogService _logger;

        /// <summary>
        /// Initializes a new instance of the SimpleMDGUserAssetsController with required dependencies.
        /// </summary>
        /// <param name="settings">Configuration settings for OpenText API</param>
        /// <param name="csUtilities">Utility methods for Content Server operations</param>
        /// <param name="httpClient">HTTP client for API calls</param>
        /// <param name="authManager">Service for authentication management</param>
        /// <param name="csNode">Service for node operations in Content Server</param>
        /// <param name="logger">Service for logging operations and errors</param>
        public SimpleMDGUserAssetsController(
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

            _logger.Log("SimpleMDGUserAssetsController initialized", LogLevel.DEBUG);
        }

        #region User Management

        /// <summary>
        /// Creates a user folder structure in OpenText.
        /// </summary>
        /// <param name="userEmail">Email of the user to create</param>
        /// <returns>HTTP response with user creation result</returns>
        [HttpPost("user/{userEmail}")]
        [SwaggerOperation(
            Summary = "Creates a user folder structure",
            Description = "Creates a folder structure for a user in OpenText Content Server"
        )]
        [SwaggerResponse(200, "OK")]
        [SwaggerResponse(400, "Invalid parameters")]
        [SwaggerResponse(401, "Authentication required")]
        [SwaggerResponse(409, "User already exists")]
        [SwaggerResponse(500, "Internal error")]
        [Produces("application/json")]
        public async Task<IActionResult> CreateUser(string userEmail)
        {
            _logger.Log($"CreateUser called: userEmail={userEmail}", LogLevel.INFO);

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
            if (string.IsNullOrWhiteSpace(userEmail))
            {
                _logger.Log("User email not provided", LogLevel.WARNING);
                return BadRequest("User email is required");
            }

            try
            {
                // 1. Ensure Transaction Asset sets folder exists
                string transactionAssetsNodeId = await EnsureTransactionAssetsStructureAsync(ticket);

                // 2. Ensure Users folder exists
                string usersFolderId = await EnsureSpecificFolderAsync(transactionAssetsNodeId, "Users", ticket);

                // 3. Check if user folder already exists
                string? existingUserFolderId = await FindFolderAsync(usersFolderId, userEmail, ticket);

                if (existingUserFolderId != null)
                {
                    _logger.Log($"User folder for {userEmail} already exists", LogLevel.WARNING);
                    return StatusCode(409, $"User {userEmail} already exists");
                }

                // 4. Create user folder
                string userFolderId = await _csNode.CreateSimpleMDGFolderAsync(usersFolderId, userEmail, ticket);

                // 5. Create subfolders for user
                string avatarFolderId = (await _csNode.CreateSimpleMDGFolderAsync(userFolderId, "Avatar", ticket)).ToString();
                string attachmentsFolderId = (await _csNode.CreateSimpleMDGFolderAsync(userFolderId, "Attachments", ticket)).ToString();

                _logger.Log($"User {userEmail} folder structure created successfully", LogLevel.INFO);
                return Ok(new
                {
                    Message = $"User {userEmail} created successfully",
                    UserFolderId = userFolderId,
                    AvatarFolderId = avatarFolderId,
                    AttachmentsFolderId = attachmentsFolderId
                });
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                return StatusCode(500, $"Error creating user: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes a user folder and all its contents from OpenText.
        /// </summary>
        /// <param name="userEmail">Email of the user to delete</param>
        /// <returns>HTTP response with deletion result</returns>
        [HttpDelete("user/{userEmail}")]
        [SwaggerOperation(
            Summary = "Deletes a user folder",
            Description = "Deletes a user folder and all its contents from OpenText Content Server"
        )]
        [SwaggerResponse(200, "OK")]
        [SwaggerResponse(401, "Authentication required")]
        [SwaggerResponse(404, "User not found")]
        [SwaggerResponse(500, "Internal error")]
        [Produces("application/json")]
        public async Task<IActionResult> DeleteUser(string userEmail)
        {
            _logger.Log($"DeleteUser called: userEmail={userEmail}", LogLevel.INFO);

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
                // 1. Find Transaction Asset sets folder
                string? transactionAssetsNodeId = await FindTransactionAssetsStructureAsync(ticket);
                if (transactionAssetsNodeId == null)
                {
                    return StatusCode(404, "Transaction Asset sets folder does not exist");
                }

                // 2. Find Users folder
                string? usersFolderId = await FindFolderAsync(transactionAssetsNodeId, "Users", ticket);
                if (usersFolderId == null)
                {
                    return StatusCode(404, "Users folder does not exist");
                }

                // 3. Find user folder
                string? userFolderId = await FindFolderAsync(usersFolderId, userEmail, ticket);
                if (userFolderId == null)
                {
                    return StatusCode(404, $"User {userEmail} not found");
                }

                // 4. Delete user folder and all its contents
                await _csNode.DeleteNodeAsync(userFolderId, ticket, "MDG");

                _logger.Log($"User {userEmail} deleted successfully", LogLevel.INFO);
                return Ok($"User {userEmail} deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                return StatusCode(500, $"Error deleting user: {ex.Message}");
            }
        }

        #endregion

        #region User Avatar Management

        /// <summary>
        /// Creates a user avatar in OpenText.
        /// </summary>
        /// <param name="userEmail">Email of the user</param>
        /// <param name="file">Avatar image file</param>
        /// <returns>HTTP response with avatar creation result</returns>
        [HttpPost("user/{userEmail}/avatar")]
        [SwaggerOperation(
            Summary = "Creates a user avatar",
            Description = "Creates an avatar image for a user in OpenText Content Server"
        )]
        [SwaggerResponse(200, "OK", typeof(UserAvatarResponse))]
        [SwaggerResponse(400, "Invalid parameters")]
        [SwaggerResponse(401, "Authentication required")]
        [SwaggerResponse(404, "User not found")]
        [SwaggerResponse(409, "Avatar already exists")]
        [SwaggerResponse(500, "Internal error")]
        [Produces("application/json")]
        public async Task<IActionResult> CreateUserAvatar(string userEmail, IFormFile file)
        {
            _logger.Log($"CreateUserAvatar called: userEmail={userEmail}", LogLevel.INFO);

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
            if (string.IsNullOrWhiteSpace(userEmail))
            {
                _logger.Log("User email not provided", LogLevel.WARNING);
                return BadRequest("User email is required");
            }

            if (file == null || file.Length == 0)
            {
                _logger.Log("Avatar file not provided or empty", LogLevel.WARNING);
                return BadRequest("Avatar file is required");
            }

            try
            {
                // 1. Find Transaction Asset sets folder
                string? transactionAssetsNodeId = await FindTransactionAssetsStructureAsync(ticket);
                if (transactionAssetsNodeId == null)
                {
                    return StatusCode(404, "Transaction Asset sets folder does not exist");
                }

                // 2. Find Users folder
                string? usersFolderId = await FindFolderAsync(transactionAssetsNodeId, "Users", ticket);
                if (usersFolderId == null)
                {
                    return StatusCode(404, "Users folder does not exist");
                }

                // 3. Find user folder
                string? userFolderId = await FindFolderAsync(usersFolderId, userEmail, ticket);
                if (userFolderId == null)
                {
                    return StatusCode(404, $"User {userEmail} not found");
                }

                // 4. Find Avatar folder
                string? avatarFolderId = await FindFolderAsync(userFolderId, "Avatar", ticket);
                if (avatarFolderId == null)
                {
                    // Create Avatar folder if it doesn't exist
                    avatarFolderId = (await _csNode.CreateFolderAsync(userFolderId, "Avatar", ticket)).ToString();
                }

                // 5. Check if avatar already exists
                var existingAvatars = await _csNode.GetNodeSubNodesAsync(avatarFolderId, ticket, "Request");
                if (existingAvatars != null && existingAvatars.Count > 0)
                {
                    var existingAvatar = existingAvatars.FirstOrDefault(d =>
                        d.Name.Equals("UserAvatar", StringComparison.OrdinalIgnoreCase));

                    if (existingAvatar != null)
                    {
                        return StatusCode(409, new UserAvatarResponse
                        {
                            Success = false,
                            Message = "Avatar already exists. Use PUT method to update it."
                        });
                    }
                }

                // 6. Create the avatar
                // Create object for node creation
                var nodeCreationObject = new
                {
                    type = 144,  // Document
                    parent_id = avatarFolderId,
                    name = "UserAvatar"
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
                    throw new Exception($"Error creating avatar: {httpResponse.StatusCode}");
                }

                // Read and process response
                var responseContent = await httpResponse.Content.ReadAsStringAsync();
                _logger.LogRawOutbound("response_create_avatar", responseContent);

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
                    throw new Exception("Error extracting created avatar ID");
                }

                // Prepare response
                var response = new UserAvatarResponse
                {
                    Success = true,
                    Message = "Avatar created successfully",
                    UserEmail = userEmail,
                    FileId = createdNodeId,
                    FileName = file.FileName,
                    DownloadUrl = $"{baseUrl}/api/v2/nodes/{createdNodeId}/content",
                    FileSize = file.Length,
                    LastModified = DateTime.Now,
                    Version = 1
                };

                _logger.Log($"Avatar for user {userEmail} created successfully", LogLevel.INFO);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                return StatusCode(500, new UserAvatarResponse
                {
                    Success = false,
                    Message = $"Error creating avatar: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Updates a user's avatar in OpenText.
        /// </summary>
        /// <param name="userEmail">Email of the user</param>
        /// <param name="file">New avatar image file</param>
        /// <returns>HTTP response with avatar update result</returns>
        [HttpPut("user/{userEmail}/avatar")]
        [SwaggerOperation(
            Summary = "Updates a user's avatar",
            Description = "Updates an existing avatar image for a user in OpenText Content Server"
        )]
        [SwaggerResponse(200, "OK", typeof(UserAvatarResponse))]
        [SwaggerResponse(400, "Invalid parameters")]
        [SwaggerResponse(401, "Authentication required")]
        [SwaggerResponse(404, "User or avatar not found")]
        [SwaggerResponse(500, "Internal error")]
        [Produces("application/json")]
        public async Task<IActionResult> UpdateUserAvatar(string userEmail, IFormFile file)
        {
            _logger.Log($"UpdateUserAvatar called: userEmail={userEmail}", LogLevel.INFO);

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
            if (string.IsNullOrWhiteSpace(userEmail))
            {
                _logger.Log("User email not provided", LogLevel.WARNING);
                return BadRequest("User email is required");
            }

            if (file == null || file.Length == 0)
            {
                _logger.Log("Avatar file not provided or empty", LogLevel.WARNING);
                return BadRequest("Avatar file is required");
            }

            try
            {
                // 1. Find Transaction Asset sets folder
                string? transactionAssetsNodeId = await FindTransactionAssetsStructureAsync(ticket);
                if (transactionAssetsNodeId == null)
                {
                    return StatusCode(404, "Transaction Asset sets folder does not exist");
                }

                // 2. Find Users folder
                string? usersFolderId = await FindFolderAsync(transactionAssetsNodeId, "Users", ticket);
                if (usersFolderId == null)
                {
                    return StatusCode(404, "Users folder does not exist");
                }

                // 3. Find user folder
                string? userFolderId = await FindFolderAsync(usersFolderId, userEmail, ticket);
                if (userFolderId == null)
                {
                    return StatusCode(404, $"User {userEmail} not found");
                }

                // 4. Find Avatar folder
                string? avatarFolderId = await FindFolderAsync(userFolderId, "Avatar", ticket);
                if (avatarFolderId == null)
                {
                    return StatusCode(404, "Avatar folder does not exist");
                }

                // 5. Find existing avatar
                var existingAvatars = await _csNode.GetNodeSubNodesAsync(avatarFolderId, ticket, "Request");
                var existingAvatar = existingAvatars?.FirstOrDefault(d =>
                    d.Name.Equals("UserAvatar", StringComparison.OrdinalIgnoreCase));

                if (existingAvatar == null)
                {
                    return StatusCode(404, "Avatar not found");
                }

                string avatarId = existingAvatar.NodeId;

                // 6. Add new version
                var baseUrl = _settings.BaseUrl;
                var updateUrl = $"{baseUrl}/api/v2/nodes/{avatarId}/versions";

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
                    throw new Exception($"Error adding version to avatar: {httpResponse.StatusCode}");
                }

                // Read and process response
                var responseContent = await httpResponse.Content.ReadAsStringAsync();
                _logger.LogRawOutbound("response_update_avatar", responseContent);

                // Prepare response
                var response = new UserAvatarResponse
                {
                    Success = true,
                    Message = "Avatar updated successfully with new version",
                    UserEmail = userEmail,
                    FileId = avatarId,
                    FileName = file.FileName,
                    DownloadUrl = $"{baseUrl}/api/v2/nodes/{avatarId}/content",
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

                _logger.Log($"Avatar for user {userEmail} updated successfully", LogLevel.INFO);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                return StatusCode(500, new UserAvatarResponse
                {
                    Success = false,
                    Message = $"Error updating avatar: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Gets a user's avatar from OpenText.
        /// </summary>
        /// <param name="userEmail">Email of the user</param>
        /// <returns>HTTP response with avatar information</returns>
        [HttpGet("user/{userEmail}/avatar")]
        [SwaggerOperation(
            Summary = "Gets a user's avatar",
            Description = "Retrieves the avatar image for a user from OpenText Content Server"
        )]
        [SwaggerResponse(200, "OK", typeof(NodeResponse))]
        [SwaggerResponse(401, "Authentication required")]
        [SwaggerResponse(404, "User or avatar not found")]
        [SwaggerResponse(500, "Internal error")]
        [Produces("application/json")]
        public async Task<IActionResult> GetUserAvatar(string userEmail)
        {
            _logger.Log($"GetUserAvatar called: userEmail={userEmail}", LogLevel.INFO);

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
                // 1. Find Transaction Asset sets folder
                string? transactionAssetsNodeId = await FindTransactionAssetsStructureAsync(ticket);
                if (transactionAssetsNodeId == null)
                {
                    return StatusCode(404, "Transaction Asset sets folder does not exist");
                }

                // 2. Find Users folder
                string? usersFolderId = await FindFolderAsync(transactionAssetsNodeId, "Users", ticket);
                if (usersFolderId == null)
                {
                    return StatusCode(404, "Users folder does not exist");
                }

                // 3. Find user folder
                string? userFolderId = await FindFolderAsync(usersFolderId, userEmail, ticket);
                if (userFolderId == null)
                {
                    return StatusCode(404, $"User {userEmail} not found");
                }

                // 4. Find Avatar folder
                string? avatarFolderId = await FindFolderAsync(userFolderId, "Avatar", ticket);
                if (avatarFolderId == null)
                {
                    return StatusCode(404, "Avatar folder does not exist");
                }

                // 5. Find existing avatar
                var existingAvatars = await _csNode.GetNodeSubNodesAsync(avatarFolderId, ticket, "Request");
                var existingAvatar = existingAvatars?.FirstOrDefault(d =>
                    d.Name.Equals("UserAvatar", StringComparison.OrdinalIgnoreCase));

                if (existingAvatar == null)
                {
                    return StatusCode(404, "Avatar not found");
                }

                // 6. Get avatar with binary content
                int avatarId = int.Parse(existingAvatar.NodeId);
                var avatarNode = await _csNode.GetNodeByIdAsync(avatarId, ticket);
                if (avatarNode == null)
                {
                    return StatusCode(404, "Could not retrieve avatar content");
                }

                // 7. Log successful retrieval (metadata only, not content)
                _logger.Log($"Successfully retrieved avatar for user {userEmail}", LogLevel.INFO);
                _logger.LogRawOutbound("response_get_avatar",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        nodeId = avatarNode.nodeId,
                        fileName = avatarNode.file_name,
                        type = avatarNode.type,
                        typeName = avatarNode.type_name,
                        contentSize = avatarNode.Content?.Length ?? 0
                    }));

                // 8. Return the entire node response with binary content
                return Ok(avatarNode);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error retrieving avatar: {ex.Message}", LogLevel.ERROR);

                // Log error response
                _logger.LogRawOutbound("response_get_avatar_error",
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
        /// Deletes a user's avatar from OpenText.
        /// </summary>
        /// <param name="userEmail">Email of the user</param>
        /// <returns>HTTP response with deletion result</returns>
        [HttpDelete("user/{userEmail}/avatar")]
        [SwaggerOperation(
            Summary = "Deletes a user's avatar",
            Description = "Deletes the avatar image for a user from OpenText Content Server"
        )]
        [SwaggerResponse(200, "OK")]
        [SwaggerResponse(401, "Authentication required")]
        [SwaggerResponse(404, "User or avatar not found")]
        [SwaggerResponse(500, "Internal error")]
        [Produces("application/json")]
        public async Task<IActionResult> DeleteUserAvatar(string userEmail)
        {
            _logger.Log($"DeleteUserAvatar called: userEmail={userEmail}", LogLevel.INFO);

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
                // 1. Find Transaction Asset sets folder
                string? transactionAssetsNodeId = await FindTransactionAssetsStructureAsync(ticket);
                if (transactionAssetsNodeId == null)
                {
                    return StatusCode(404, "Transaction Asset sets folder does not exist");
                }

                // 2. Find Users folder
                string? usersFolderId = await FindFolderAsync(transactionAssetsNodeId, "Users", ticket);
                if (usersFolderId == null)
                {
                    return StatusCode(404, "Users folder does not exist");
                }

                // 3. Find user folder
                string? userFolderId = await FindFolderAsync(usersFolderId, userEmail, ticket);
                if (userFolderId == null)
                {
                    return StatusCode(404, $"User {userEmail} not found");
                }

                // 4. Find Avatar folder
                string? avatarFolderId = await FindFolderAsync(userFolderId, "Avatar", ticket);
                if (avatarFolderId == null)
                {
                    return StatusCode(404, "Avatar folder does not exist");
                }

                // 5. Find existing avatar
                var existingAvatars = await _csNode.GetNodeSubNodesAsync(avatarFolderId, ticket, "Request");
                var existingAvatar = existingAvatars?.FirstOrDefault(d =>
                    d.Name.Equals("UserAvatar", StringComparison.OrdinalIgnoreCase));

                if (existingAvatar == null)
                {
                    return StatusCode(404, "Avatar not found");
                }

                // 6. Delete the avatar
                await _csNode.DeleteNodeAsync(existingAvatar.NodeId, ticket, "MDG");

                _logger.Log($"Avatar for user {userEmail} deleted successfully", LogLevel.INFO);
                return Ok($"Avatar for user {userEmail} deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                return StatusCode(500, $"Error deleting avatar: {ex.Message}");
            }
        }

        #endregion

        #region User Attachments Management

        /// <summary>
        /// Adds an attachment to a user's folder in OpenText.
        /// </summary>
        /// <param name="userEmail">Email of the user</param>
        /// <param name="file">Attachment file</param>
        /// <param name="description">Optional description for the attachment</param>
        /// <returns>HTTP response with attachment creation result</returns>
        [HttpPost("user/{userEmail}/attachment")]
        [SwaggerOperation(
            Summary = "Adds an attachment",
            Description = "Adds an attachment file to a user's folder in OpenText Content Server"
        )]
        [SwaggerResponse(200, "OK", typeof(UserAttachmentResponse))]
        [SwaggerResponse(400, "Invalid parameters")]
        [SwaggerResponse(401, "Authentication required")]
        [SwaggerResponse(404, "User not found")]
        [SwaggerResponse(500, "Internal error")]
        [Produces("application/json")]
        public async Task<IActionResult> AddAttachment(string userEmail, IFormFile file, [FromForm] string? description = null)
        {
            _logger.Log($"AddAttachment called: userEmail={userEmail}", LogLevel.INFO);

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
            if (string.IsNullOrWhiteSpace(userEmail))
            {
                _logger.Log("User email not provided", LogLevel.WARNING);
                return BadRequest("User email is required");
            }

            if (file == null || file.Length == 0)
            {
                _logger.Log("Attachment file not provided or empty", LogLevel.WARNING);
                return BadRequest("Attachment file is required");
            }

            try
            {
                // 1. Find Transaction Asset sets folder
                string? transactionAssetsNodeId = await FindTransactionAssetsStructureAsync(ticket);
                if (transactionAssetsNodeId == null)
                {
                    return StatusCode(404, "Transaction Asset sets folder does not exist");
                }

                // 2. Find Users folder
                string? usersFolderId = await FindFolderAsync(transactionAssetsNodeId, "Users", ticket);
                if (usersFolderId == null)
                {
                    return StatusCode(404, "Users folder does not exist");
                }

                // 3. Find user folder
                string? userFolderId = await FindFolderAsync(usersFolderId, userEmail, ticket);
                if (userFolderId == null)
                {
                    return StatusCode(404, $"User {userEmail} not found");
                }

                // 4. Find Attachments folder
                string? attachmentsFolderId = await FindFolderAsync(userFolderId, "Attachments", ticket);
                if (attachmentsFolderId == null)
                {
                    // Create Attachments folder if it doesn't exist
                    attachmentsFolderId = (await _csNode.CreateFolderAsync(userFolderId, "Attachments", ticket)).ToString();
                }

                // 5. Create the attachment
                // Create object for node creation
                object nodeCreationObject;
                if (description != null)
                {
                    nodeCreationObject = new
                    {
                        type = 144,  // Document
                        parent_id = attachmentsFolderId,
                        name = file.FileName,
                        description = description
                    };
                }
                else
                {
                    nodeCreationObject = new
                    {
                        type = 144,  // Document
                        parent_id = attachmentsFolderId,
                        name = file.FileName
                    };
                }

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
                    throw new Exception($"Error creating attachment: {httpResponse.StatusCode}");
                }

                // Read and process response
                var responseContent = await httpResponse.Content.ReadAsStringAsync();
                _logger.LogRawOutbound("response_create_attachment", responseContent);

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
                    throw new Exception("Error extracting created attachment ID");
                }

                // Prepare response
                var response = new UserAttachmentResponse
                {
                    Success = true,
                    Message = "Attachment created successfully",
                    UserEmail = userEmail,
                    AttachmentId = createdNodeId,
                    FileName = file.FileName,
                    Description = description,
                    ContentType = file.ContentType,
                    DownloadUrl = $"{baseUrl}/api/v2/nodes/{createdNodeId}/content",
                    FileSize = file.Length,
                    CreatedDate = DateTime.Now,
                    LastModified = DateTime.Now,
                    Version = 1
                };

                _logger.Log($"Attachment for user {userEmail} created successfully", LogLevel.INFO);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                return StatusCode(500, new UserAttachmentResponse
                {
                    Success = false,
                    Message = $"Error creating attachment: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Gets an attachment by its ID.
        /// </summary>
        /// <param name="attachmentId">ID of the attachment to retrieve</param>
        /// <returns>HTTP response with attachment information</returns>
        [HttpGet("attachment/{attachmentId}")]
        [SwaggerOperation(
            Summary = "Gets an attachment by ID",
            Description = "Retrieves an attachment file by its ID from OpenText Content Server"
        )]
        [SwaggerResponse(200, "OK", typeof(NodeResponse))]
        [SwaggerResponse(401, "Authentication required")]
        [SwaggerResponse(404, "Attachment not found")]
        [SwaggerResponse(500, "Internal error")]
        [Produces("application/json")]
        public async Task<IActionResult> GetAttachment(string attachmentId)
        {
            _logger.Log($"GetAttachment called: attachmentId={attachmentId}", LogLevel.INFO);

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
                // Get the attachment with binary content
                int attId = int.Parse(attachmentId);
                var attachmentNode = await _csNode.GetNodeByIdAsync(attId, ticket);
                if (attachmentNode == null)
                {
                    return StatusCode(404, "Attachment not found");
                }

                // Log successful retrieval (metadata only, not content)
                _logger.Log($"Successfully retrieved attachment: {attachmentId}", LogLevel.INFO);
                _logger.LogRawInbound("response_get_attachment",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        nodeId = attachmentNode.nodeId,
                        fileName = attachmentNode.file_name,
                        type = attachmentNode.type,
                        typeName = attachmentNode.type_name,
                        contentSize = attachmentNode.Content?.Length ?? 0
                    }));

                // Return the entire node response with binary content
                return Ok(attachmentNode);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error retrieving attachment: {ex.Message}", LogLevel.ERROR);

                // Log error response
                _logger.LogRawInbound("response_get_attachment_error",
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
        /// Gets a list of attachments for a user.
        /// </summary>
        /// <param name="userEmail">Email of the user</param>
        /// <returns>HTTP response with list of attachments</returns>
        [HttpGet("user/{userEmail}/attachments")]
        [SwaggerOperation(
            Summary = "Gets a list of attachments",
            Description = "Retrieves a list of all attachments for a user from OpenText Content Server"
        )]
        [SwaggerResponse(200, "OK", typeof(UserAttachmentsListResponse))]
        [SwaggerResponse(401, "Authentication required")]
        [SwaggerResponse(404, "User not found")]
        [SwaggerResponse(500, "Internal error")]
        [Produces("application/json")]
        public async Task<IActionResult> GetAttachmentList(string userEmail)
        {
            _logger.Log($"GetAttachmentList called: userEmail={userEmail}", LogLevel.INFO);

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
                // 1. Find Transaction Asset sets folder
                string? transactionAssetsNodeId = await FindTransactionAssetsStructureAsync(ticket);
                if (transactionAssetsNodeId == null)
                {
                    return StatusCode(404, "Transaction Asset sets folder does not exist");
                }

                // 2. Find Users folder
                string? usersFolderId = await FindFolderAsync(transactionAssetsNodeId, "Users", ticket);
                if (usersFolderId == null)
                {
                    return StatusCode(404, "Users folder does not exist");
                }

                // 3. Find user folder
                string? userFolderId = await FindFolderAsync(usersFolderId, userEmail, ticket);
                if (userFolderId == null)
                {
                    return StatusCode(404, $"User {userEmail} not found");
                }

                // 4. Find Attachments folder
                string? attachmentsFolderId = await FindFolderAsync(userFolderId, "Attachments", ticket);
                if (attachmentsFolderId == null)
                {
                    // Return empty list if Attachments folder doesn't exist
                    return Ok(new UserAttachmentsListResponse
                    {
                        Success = true,
                        Message = "No attachments found",
                        UserEmail = userEmail,
                        TotalCount = 0,
                        PageSize = 0,
                        PageNumber = 1,
                        TotalPages = 0,
                        Attachments = new List<UserAttachmentInfo>()
                    });
                }

                // 5. Get all attachments
                var attachmentNodes = await _csNode.GetNodeSubNodesAsync(attachmentsFolderId, ticket, "Request");
                if (attachmentNodes == null || attachmentNodes.Count == 0)
                {
                    return Ok(new UserAttachmentsListResponse
                    {
                        Success = true,
                        Message = "No attachments found",
                        UserEmail = userEmail,
                        TotalCount = 0,
                        PageSize = 0,
                        PageNumber = 1,
                        TotalPages = 0,
                        Attachments = new List<UserAttachmentInfo>()
                    });
                }

                // 6. Build attachment info list
                var baseUrl = _settings.BaseUrl;
                var attachmentInfos = new List<UserAttachmentInfo>();

                foreach (var attachment in attachmentNodes)
                {
                    // Get additional metadata if needed
                    try
                    {
                        var node = await _csNode.GetNodeByIdAsync(int.Parse(attachment.NodeId), ticket);

                        attachmentInfos.Add(new UserAttachmentInfo
                        {
                            AttachmentId = attachment.NodeId,
                            FileName = attachment.Name,
                            //Description = node.description,
                            ContentType = node.type_name,
                            //ThumbnailUrl = $"{baseUrl}/api/v1/nodes/{attachment.NodeId}/thumbnails/medium/content",
                            FileSize = node.Content?.Length ?? 0,
                            //CreatedDate = DateTime.Now,  // Use actual creation date if available
                            //LastModified = DateTime.Now  // Use actual modification date if available
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogException(ex, LogLevel.WARNING);
                        // Add basic info if detailed info not available
                        attachmentInfos.Add(new UserAttachmentInfo
                        {
                            AttachmentId = attachment.NodeId,
                            FileName = attachment.Name,
                            ThumbnailUrl = $"{baseUrl}/api/v1/nodes/{attachment.NodeId}/thumbnails/medium/content",
                            CreatedDate = DateTime.Now,
                            LastModified = DateTime.Now
                        });
                    }
                }

                // 7. Create response
                var response = new UserAttachmentsListResponse
                {
                    Success = true,
                    Message = $"Found {attachmentInfos.Count} attachments",
                    UserEmail = userEmail,
                    TotalCount = attachmentInfos.Count,
                    PageSize = attachmentInfos.Count,
                    PageNumber = 1,
                    TotalPages = 1,
                    Attachments = attachmentInfos
                };

                _logger.Log($"Retrieved {attachmentInfos.Count} attachments for user {userEmail}", LogLevel.INFO);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                return StatusCode(500, new UserAttachmentsListResponse
                {
                    Success = false,
                    Message = $"Error retrieving attachments: {ex.Message}",
                    Attachments = new List<UserAttachmentInfo>()
                });
            }
        }

        /// <summary>
        /// Updates an attachment.
        /// </summary>
        /// <param name="attachmentId">ID of the attachment to update</param>
        /// <param name="file">New attachment file</param>
        /// <returns>HTTP response with update result</returns>
        [HttpPut("attachment/{attachmentId}")]
        [SwaggerOperation(
            Summary = "Updates an attachment",
            Description = "Updates an existing attachment in OpenText Content Server"
        )]
        [SwaggerResponse(200, "OK", typeof(UserAttachmentResponse))]
        [SwaggerResponse(400, "Invalid parameters")]
        [SwaggerResponse(401, "Authentication required")]
        [SwaggerResponse(404, "Attachment not found")]
        [SwaggerResponse(500, "Internal error")]
        [Produces("application/json")]
        public async Task<IActionResult> UpdateAttachment(string attachmentId, IFormFile file)
        {
            _logger.Log($"UpdateAttachment called: attachmentId={attachmentId}", LogLevel.INFO);

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
                _logger.Log("Attachment file not provided or empty", LogLevel.WARNING);
                return BadRequest("Attachment file is required");
            }

            try
            {
                // 1. Verify the attachment exists
                try
                {
                    var node = await _csNode.GetNodeByIdAsync(int.Parse(attachmentId), ticket);
                    if (node == null)
                    {
                        return StatusCode(404, "Attachment not found");
                    }
                }
                catch (Exception)
                {
                    return StatusCode(404, "Attachment not found");
                }

                // 2. Add new version
                var baseUrl = _settings.BaseUrl;
                var updateUrl = $"{baseUrl}/api/v2/nodes/{attachmentId}/versions";

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
                    throw new Exception($"Error adding version to attachment: {httpResponse.StatusCode}");
                }

                // Read and process response
                var responseContent = await httpResponse.Content.ReadAsStringAsync();
                _logger.LogRawOutbound("response_update_attachment", responseContent);

                // Get updated attachment info
                var updatedNode = await _csNode.GetNodeByIdAsync(int.Parse(attachmentId), ticket);

                // Extract user email from parent path if possible
                string userEmail = "unknown";
                try
                {
                    // Navigate up the folder structure to find the user folder
                    // This is implementation-specific and may need adjustment
                    // For now, just use a placeholder
                }
                catch (Exception)
                {
                    // Continue with unknown user email
                }

                // Prepare response
                var response = new UserAttachmentResponse
                {
                    Success = true,
                    Message = "Attachment updated successfully",
                    UserEmail = userEmail,
                    AttachmentId = attachmentId,
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    DownloadUrl = $"{baseUrl}/api/v2/nodes/{attachmentId}/content",
                    FileSize = file.Length,
                    CreatedDate = DateTime.Now,  // Use original creation date if available
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

                _logger.Log($"Attachment {attachmentId} updated successfully", LogLevel.INFO);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                return StatusCode(500, new UserAttachmentResponse
                {
                    Success = false,
                    Message = $"Error updating attachment: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Deletes an attachment.
        /// </summary>
        /// <param name="attachmentId">ID of the attachment to delete</param>
        /// <returns>HTTP response with deletion result</returns>
        [HttpDelete("attachment/{attachmentId}")]
        [SwaggerOperation(
            Summary = "Deletes an attachment",
            Description = "Deletes an attachment from OpenText Content Server"
        )]
        [SwaggerResponse(200, "OK")]
        [SwaggerResponse(401, "Authentication required")]
        [SwaggerResponse(404, "Attachment not found")]
        [SwaggerResponse(500, "Internal error")]
        [Produces("application/json")]
        public async Task<IActionResult> DeleteAttachment(string attachmentId)
        {
            _logger.Log($"DeleteAttachment called: attachmentId={attachmentId}", LogLevel.INFO);

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
                // Verify attachment exists
                try
                {
                    var node = await _csNode.GetNodeByIdAsync(int.Parse(attachmentId), ticket);
                    if (node == null)
                    {
                        return StatusCode(404, "Attachment not found");
                    }
                }
                catch (Exception)
                {
                    return StatusCode(404, "Attachment not found");
                }

                // Delete the attachment
                await _csNode.DeleteNodeAsync(attachmentId, ticket, "MDG");

                _logger.Log($"Attachment {attachmentId} deleted successfully", LogLevel.INFO);
                return Ok($"Attachment deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                return StatusCode(500, $"Error deleting attachment: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Ensures the Transaction Asset sets folder structure exists.
        /// </summary>
        /// <param name="ticket">Authentication ticket</param>
        /// <returns>Node ID of the Transaction Asset sets folder</returns>
        private async Task<string> EnsureTransactionAssetsStructureAsync(string ticket)
        {
            _logger.Log("Ensuring Transaction Asset sets folder structure", LogLevel.DEBUG);

            // Find or create "Transaction Asset sets" folder in root
            var rootFolderId = _settings.RootFolderId;

            // First try to find the folder
            string? transactionAssetsNodeId = await FindFolderAsync(rootFolderId, "Transaction Asset sets", ticket);

            if (transactionAssetsNodeId == null)
            {
                // Create folder if it doesn't exist
                _logger.Log("'Transaction Asset sets' folder not found, creating...", LogLevel.DEBUG);
                transactionAssetsNodeId = (await _csNode.CreateFolderAsync(rootFolderId, "Transaction Asset sets", ticket)).ToString();
            }

            return transactionAssetsNodeId;
        }

        /// <summary>
        /// Finds the Transaction Asset sets folder structure.
        /// </summary>
        /// <param name="ticket">Authentication ticket</param>
        /// <returns>Node ID of the Transaction Asset sets folder, or null if not found</returns>
        private async Task<string?> FindTransactionAssetsStructureAsync(string ticket)
        {
            _logger.Log("Finding Transaction Asset sets folder structure", LogLevel.DEBUG);

            // Find "Transaction Asset sets" folder in root
            var rootFolderId = _settings.RootFolderId;

            return await FindFolderAsync(rootFolderId, "Transaction Asset sets", ticket);
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