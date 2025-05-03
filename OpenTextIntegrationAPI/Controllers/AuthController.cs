using Microsoft.AspNetCore.Mvc;
using OpenTextIntegrationAPI.Models;
using OpenTextIntegrationAPI.DTOs;
using OpenTextIntegrationAPI.Services;
using Swashbuckle.AspNetCore.Annotations;

namespace OpenTextIntegrationAPI.Controllers
{
    /// <summary>
    /// Controller that handles authentication operations with OpenText Content Server.
    /// Provides endpoints for obtaining authentication tickets.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly ILogService _logger;
        private readonly IWebHostEnvironment _environment;

        /// <summary>
        /// Constructor that receives dependencies via Dependency Injection.
        /// </summary>
        /// <param name="authService">Service used to authenticate against OpenText.</param>
        /// <param name="logger">Service used for logging operations and errors.</param>
        /// <param name="environment">Host environment information.</param>
        public AuthController(AuthService authService, ILogService logger, IWebHostEnvironment environment)
        {
            _authService = authService;
            _logger = logger;
            _environment = environment;

            // Log controller initialization
            _logger.Log("AuthController initialized", LogLevel.DEBUG);
        }

        /// <summary>
        /// Public endpoint for external callers to authenticate to OpenText via form fields.
        /// </summary>
        /// <remarks>
        /// Expects a form POST with fields:
        ///  - Username (required)
        ///  - Password (required)
        ///  - Domain (required)
        ///
        /// Returns a JSON object containing the OpenText ticket:
        /// {
        ///     "ticket": "&lt;the retrieved ticket&gt;"
        /// }
        /// </remarks>
        /// <param name="requestDto">
        /// The form data object containing Username, Password, and Domain.
        /// </param>
        /// <returns>HTTP 200 with a JSON object containing the ticket, or an error status code.</returns>
        [HttpPost("login")]
        [SwaggerResponse(200, "OK", typeof(AuthResponse))]
        [SwaggerResponse(400, "User, Password or Domain are incorrect/not completed", typeof(ValidationProblemDetails))]
        [SwaggerResponse(401, "Unauthorized: User, Password or Domain are incorrect")]
        [SwaggerResponse(500, "Internal Error. Contact API Admin", typeof(ProblemDetails))]
        [Consumes("application/x-www-form-urlencoded")]
        public async Task<IActionResult> Login([FromForm] AuthRequest requestDto)
        {
            _logger.Log("Login endpoint called", LogLevel.INFO);

            // Log inbound request (external application calling YOUR API)
            _logger.LogRawInbound("inbound_request_authenticate",
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    username = requestDto.Username,
                    domain = requestDto.Domain,
                    timestamp = DateTime.UtcNow,
                    source_ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    user_agent = HttpContext.Request.Headers["User-Agent"].ToString()
                })
            );

            // Validate model state automatically handles required fields
            if (!ModelState.IsValid)
            {
                _logger.Log("Authentication failed: Validation error", LogLevel.WARNING);

                // Log validation error response being sent back to the caller
                _logger.LogRawInbound("inbound_response_authenticate_validation_error",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        status = "validation_error",
                        errors = ModelState,
                        timestamp = DateTime.UtcNow
                    })
                );

                return BadRequest(new ValidationProblemDetails(ModelState)
                {
                    Status = 400,
                    Title = "Validation Failed",
                    Instance = HttpContext.Request.Path
                });
            }

            try
            {
                // Log authentication attempt (without sensitive data)
                if (_environment.IsDevelopment())
                {
                    _logger.Log($"Authentication attempt for user: {requestDto.Username}, Domain: {requestDto.Domain}", LogLevel.DEBUG);
                }

                // Attempt to authenticate with OpenText
                var ticket = await _authService.AuthenticateExternalAsync(
                    requestDto.Username,
                    requestDto.Password,
                    requestDto.Domain
                );

                if (string.IsNullOrEmpty(ticket))
                {
                    _logger.Log("Authentication failed: No ticket received", LogLevel.WARNING);

                    // Log error response being sent back to the caller
                    _logger.LogRawInbound("inbound_response_authenticate_error",
                        System.Text.Json.JsonSerializer.Serialize(new
                        {
                            status = "error",
                            message = "Authentication failed: No ticket received",
                            timestamp = DateTime.UtcNow
                        })
                    );

                    return Unauthorized("Authentication failed: No ticket received");
                }

                // Log successful authentication
                _logger.Log($"User {requestDto.Username} successfully authenticated with OpenText", LogLevel.INFO);

                // Logging masked ticket for security (only first 8 and last 4 characters)
                if (ticket.Length > 12)
                {
                    string maskedTicket = ticket.Substring(0, 8) + "..." + ticket.Substring(ticket.Length - 4);
                    _logger.Log($"Authentication ticket obtained: {maskedTicket}", LogLevel.DEBUG);
                }
                else
                {
                    _logger.Log("Authentication ticket obtained (short format)", LogLevel.DEBUG);
                }

                // Log successful response being sent back to the caller
                _logger.LogRawInbound("inbound_response_authenticate_success",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        status = "success",
                        ticket_provided = true,
                        masked_ticket = ticket.Length > 12
                            ? ticket.Substring(0, 8) + "..." + ticket.Substring(ticket.Length - 4)
                            : "short_ticket",
                        timestamp = DateTime.UtcNow
                    })
                );

                // Return standardized response
                return Ok(new AuthResponse { Ticket = ticket });
            }
            catch (Exception ex)
            {
                // Differentiate between authentication failures and internal errors
                if (ex.Message.Contains("Invalid credentials") || ex.Message.Contains("No ticket received"))
                {
                    // Log authentication-specific failures
                    _logger.Log($"Authentication failed for user {requestDto.Username}", LogLevel.WARNING);
                    _logger.LogException(ex, LogLevel.WARNING);

                    // Log error response being sent back to the caller
                    _logger.LogRawInbound("inbound_response_authenticate_auth_error",
                        System.Text.Json.JsonSerializer.Serialize(new
                        {
                            status = "authentication_error",
                            error_message = ex.Message,
                            error_type = ex.GetType().Name,
                            timestamp = DateTime.UtcNow
                        })
                    );

                    // Return 401 for authentication failures
                    return Unauthorized("Authentication failed: Invalid credentials");
                }
                else
                {
                    // Log unexpected failures
                    _logger.Log($"Unexpected error during authentication", LogLevel.ERROR);
                    _logger.LogException(ex, LogLevel.ERROR);

                    // Log error response being sent back to the caller
                    _logger.LogRawInbound("inbound_response_authenticate_internal_error",
                        System.Text.Json.JsonSerializer.Serialize(new
                        {
                            status = "internal_error",
                            error_message = ex.Message,
                            error_type = ex.GetType().Name,
                            timestamp = DateTime.UtcNow
                        })
                    );

                    // Return 500 for unexpected errors
                    return StatusCode(500, new ProblemDetails
                    {
                        Status = 500,
                        Title = "Internal Server Error",
                        Detail = "An unexpected error occurred. Contact API Admin",
                        Instance = HttpContext.Request.Path
                    });
                }
            }
        }
    }
}