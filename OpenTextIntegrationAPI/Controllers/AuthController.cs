using Microsoft.AspNetCore.Mvc;
using OpenTextIntegrationAPI.Models;
using OpenTextIntegrationAPI.DTOs;
using OpenTextIntegrationAPI.Services;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.Filters;

namespace OpenTextIntegrationAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        /// <summary>
        /// Constructor that receives an AuthService instance via Dependency Injection.
        /// </summary>
        /// <param name="authService">Service used to authenticate against OpenText.</param>
        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Public endpoint for external callers to authenticate to OpenText via form fields.
        /// </summary>
        /// <remarks>
        /// Expects a form POST with fields:
        ///  - Username (required)
        ///  - Password (required)
        ///  - Domain (optional)
        ///
        /// Returns a JSON object containing the OpenText ticket:
        /// {
        ///     "ticket": "<the retrieved ticket>"
        /// }
        /// </remarks>
        /// <param name="requestDto">
        /// The form data object containing Username, Password, and optionally Domain.
        /// </param>
        /// <returns>HTTP 200 with a JSON object containing the ticket, or an error status code.</returns>
        [HttpPost("login")]
        [SwaggerResponse(200, "OK", typeof(AuthResponse))]
        [SwaggerResponse(400, "User, Password or Domain are incorrect/not completed")]
        [SwaggerResponse(401, "Unauthorized: User, Password or Domain are incorrect")]
        [SwaggerResponse(500, "Internal Error. Contact API Admin")]
        [Consumes("application/x-www-form-urlencoded")]
        public async Task<IActionResult> Login([FromForm] AuthRequest requestDto)
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(requestDto.Username) ||
                string.IsNullOrWhiteSpace(requestDto.Password))
            {
                // Return 400 if Username or Password is missing
                return BadRequest("Username and Password are required.");
            }

            try
            {
                // Attempt to authenticate with OpenText
                var ticket = await _authService.AuthenticateExternalAsync(
                    requestDto.Username,
                    requestDto.Password,
                    requestDto.Domain // Domain may be null or empty
                );

                // Return the ticket in a JSON object
                // Note: It's often good practice to name the property something consistent,
                // like "ticket" (all lowercase).
                return Ok(new { ticket });
            }
            catch (Exception ex)
            {
                // Catch any exceptions from the authentication process
                // Return a generic 500 error to the client with a simple error message
                return StatusCode(401, $"Authentication failed: {ex.Message}");
            }
        }
    }
}
