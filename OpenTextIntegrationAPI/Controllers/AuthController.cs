// AuthController.cs
// Controller for handling authentication requests to OpenText Content Server.
// Author: Ignacio Lucano
// Date: 2025-05-13

using Microsoft.AspNetCore.Mvc;
using OpenTextIntegrationAPI.Models;
using OpenTextIntegrationAPI.DTOs;
using OpenTextIntegrationAPI.Services;
using Swashbuckle.AspNetCore.Annotations;
using System.Text.Json;

namespace OpenTextIntegrationAPI.Controllers
{
    /// <summary>
    /// Provides API endpoints for authenticating users against OpenText Content Server.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService; // Service to handle authentication logic
        private readonly ILogService _logger; // Logger service for logging events and errors
        private readonly IWebHostEnvironment _environment; // Environment info to check dev/prod mode

        /// <summary>
        /// Constructor injecting dependencies.
        /// </summary>
        public AuthController(AuthService authService, ILogService logger, IWebHostEnvironment environment)
        {
            _authService = authService;
            _logger = logger;
            _environment = environment;

            // Log controller initialization at debug level
            _logger.Log("AuthController initialized", LogLevel.DEBUG);
        }

        #region ENDPOINTS

        /// <summary>
        /// Authenticates external users and retrieves OpenText tickets.
        /// </summary>
        /// <param name="requestDto">Login credentials via form data.</param>
        [HttpPost("login")]
        [SwaggerResponse(200, "OK", typeof(AuthResponse))]
        [SwaggerResponse(400, "User, Password or Domain are incorrect/not completed", typeof(ValidationProblemDetails))]
        [SwaggerResponse(401, "Unauthorized: User, Password or Domain are incorrect")]
        [SwaggerResponse(500, "Internal Error. Contact API Admin", typeof(ProblemDetails))]
        [Consumes("application/x-www-form-urlencoded")]
        public async Task<IActionResult> Login([FromForm] AuthRequest requestDto)
        {
            // Log entry into login endpoint
            _logger.Log("Login endpoint called", LogLevel.INFO);

            // ─────────────────────────────────────
            // 1. Validate request input
            // ─────────────────────────────────────
            if (!ModelState.IsValid)
            {
                // Log validation failure warning
                _logger.Log("Authentication failed: Validation error", LogLevel.WARNING);

                // Return 400 Bad Request with validation details
                return BadRequest(new ValidationProblemDetails(ModelState)
                {
                    Status = 400,
                    Title = "Validation Failed",
                    Instance = HttpContext.Request.Path
                });
            }

            try
            {
                // ─────────────────────────────────────
                // 2. Log inbound request (only in dev)
                // ─────────────────────────────────────
                if (_environment.IsDevelopment())
                {
                    // Log debug info about authentication attempt with username and domain
                    _logger.Log($"Authentication attempt for user: {requestDto.Username}, Domain: {requestDto.Domain}", LogLevel.DEBUG);
                }

                // Serialize request details for raw outbound logging
                var requestJson = JsonSerializer.Serialize(new
                {
                    Timestamp = DateTime.UtcNow,
                    Method = HttpContext.Request.Method,
                    Url = HttpContext.Request.Path,
                    Headers = HttpContext.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
                    Payload = new { requestDto.Username, requestDto.Domain }
                }, new JsonSerializerOptions { WriteIndented = true });

                // Log raw outbound request data for traceability
                _logger.LogRawOutbound("request_login", requestJson);

                // ─────────────────────────────────────
                // 3. Authenticate against OpenText
                // ─────────────────────────────────────
                var ticket = await _authService.AuthenticateExternalAsync(
                    requestDto.Username,
                    requestDto.Password,
                    requestDto.Domain
                );

                // Check if authentication returned a ticket
                if (string.IsNullOrEmpty(ticket))
                {
                    // Log warning if no ticket received
                    _logger.Log("Authentication failed: No ticket received", LogLevel.WARNING);
                    return Unauthorized("Authentication failed: No ticket received");
                }

                // Log successful authentication info
                _logger.Log($"User {requestDto.Username} successfully authenticated with OpenText", LogLevel.INFO);

                // Mask ticket in logs for security
                if (ticket.Length > 12)
                {
                    string maskedTicket = ticket[..8] + "..." + ticket[^4..];
                    _logger.Log($"Authentication ticket obtained: {maskedTicket}", LogLevel.DEBUG);
                }
                else
                {
                    _logger.Log("Authentication ticket obtained (short format)", LogLevel.DEBUG);
                }

                // ─────────────────────────────────────
                // 4. Log successful response payload
                // ─────────────────────────────────────
                var successPayload = new
                {
                    status = "success",
                    ticket_provided = true,
                    masked_ticket = ticket.Length > 12 ? ticket[..8] + "..." + ticket[^4..] : "short_ticket",
                    timestamp = DateTime.UtcNow
                };

                // Serialize response details for raw outbound logging
                var responseJson = JsonSerializer.Serialize(new
                {
                    Timestamp = DateTime.UtcNow,
                    Status = 200,
                    Headers = HttpContext.Response.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
                    Body = successPayload
                }, new JsonSerializerOptions { WriteIndented = true });

                // Log raw outbound response data
                _logger.LogRawOutbound( "response_login", responseJson);

                // Return 200 OK with authentication ticket
                return Ok(new AuthResponse { Ticket = ticket });
            }
            catch (Exception ex)
            {
                // ─────────────────────────────────────
                // 5. Handle and log authentication errors
                // ─────────────────────────────────────

                // Determine error type based on exception message
                string errorLevel = ex.Message.Contains("Invalid credentials") || ex.Message.Contains("No ticket received")
                    ? "authentication_error"
                    : "internal_error";

                // Set log level accordingly
                LogLevel level = errorLevel == "authentication_error" ? LogLevel.WARNING : LogLevel.ERROR;

                // Log error event with user info
                _logger.Log($"{errorLevel.Replace('_', ' ').ToUpper()} for user {requestDto.Username}", level);

                // Log exception details
                _logger.LogException(ex, level);

                // Prepare error payload for response
                var errorPayload = new
                {
                    status = errorLevel,
                    error_message = ex.Message,
                    error_type = ex.GetType().Name,
                    timestamp = DateTime.UtcNow
                };

                // Serialize error response for raw outbound logging
                var responseJson = JsonSerializer.Serialize(new
                {
                    Timestamp = DateTime.UtcNow,
                    Status = 500,
                    Headers = HttpContext.Response.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
                    Body = errorPayload
                }, new JsonSerializerOptions { WriteIndented = true });

                // Log raw outbound error response
                _logger.LogRawOutbound("response_login", responseJson);

                // Return 401 Unauthorized for authentication errors
                if (errorLevel == "authentication_error")
                    return Unauthorized("Authentication failed: Invalid credentials");

                // Return 500 Internal Server Error for other errors
                return StatusCode(500, new ProblemDetails
                {
                    Status = 500,
                    Title = "Internal Server Error",
                    Detail = "An unexpected error occurred. Contact API Admin",
                    Instance = HttpContext.Request.Path
                });
            }
        }
        #endregion
    }
}