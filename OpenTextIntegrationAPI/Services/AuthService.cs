// ==================================================================================================
// File: AuthService.cs
// Description: Handles external and internal authentication against OpenText Content Server.
// Author: Ignacio Lucano
// Date: 2025-05-14
// ==================================================================================================

using System.Net.Http.Headers;
using System.Text.Json;
using OpenTextIntegrationAPI.Models;
using OpenTextIntegrationAPI.DTOs;

namespace OpenTextIntegrationAPI.Services
{
    /// <summary>
    /// Service for handling authentication operations with OpenText Content Server.
    /// Provides methods for authenticating with external or internal credentials.
    /// </summary>
    public class AuthService
    {
        private readonly HttpClient _httpClient; // HTTP client for making requests to OpenText
        private readonly IConfiguration _configuration; // Configuration for URLs and credentials
        private readonly ILogService _logger; // Logger for tracing and error logging

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the AuthService with dependencies.
        /// </summary>
        public AuthService(HttpClient httpClient, IConfiguration configuration, ILogService logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            // Log service initialization
            _logger.Log("AuthService initialized", LogLevel.DEBUG);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Authenticates an external user against OpenText using provided credentials.
        /// </summary>
        /// <param name="username">User's username.</param>
        /// <param name="password">User's password.</param>
        /// <param name="domain">User's domain.</param>
        /// <returns>Authentication ticket string if successful.</returns>
        public async Task<string> AuthenticateExternalAsync(string username, string password, string domain)
        {
            _logger.Log($"Starting external authentication for user: {username}, Domain: {domain}", LogLevel.INFO);

            // Retrieve base URL and authentication path from configuration
            var baseUrl = _configuration["OpenText:BaseUrl"];
            var authPath = _configuration["OpenText:AuthPath"];

            // Validate configuration presence
            if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(authPath))
                throw new Exception("OpenText authentication configuration is missing");

            var authUrl = $"{baseUrl}{authPath}";
            _logger.Log($"Authentication URL: {authUrl}", LogLevel.DEBUG);

            // Serialize request details for logging
            var requestDump = JsonSerializer.Serialize(new
            {
                username,
                domain,
                auth_url = authUrl,
                timestamp = DateTime.UtcNow
            }, new JsonSerializerOptions { WriteIndented = true });

            // Log raw outbound request
            _logger.LogRawOutbound("request_auth_external", requestDump);

            // Prepare form data for POST request
            var formData = new Dictionary<string, string>
            {
                { "username", username },
                { "password", password },
                { "domain", domain }
            };

            var content = new FormUrlEncodedContent(formData);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            // Create HTTP POST request message
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, authUrl)
            {
                Content = content
            };

            // Set headers to accept JSON and indicate AJAX request
            requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            requestMessage.Headers.Add("X-Requested-With", "XMLHttpRequest");

            HttpResponseMessage response;
            try
            {
                // Send the HTTP request
                response = await _httpClient.SendAsync(requestMessage);
                _logger.Log($"Authentication response status: {response.StatusCode}", LogLevel.DEBUG);

                // Handle unauthorized response explicitly
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.Log("Authentication failed: Invalid credentials", LogLevel.WARNING);
                    throw new Exception("Invalid credentials");
                }

                // Throw if response indicates failure
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                // Log exception and raw error response
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Authentication request failed: {ex.Message}", LogLevel.ERROR);

                var errorPayload = JsonSerializer.Serialize(new
                {
                    status = "error",
                    error_message = ex.Message,
                    error_type = ex.GetType().Name
                }, new JsonSerializerOptions { WriteIndented = true });

                _logger.LogRawOutbound("response_auth_external_error", errorPayload);

                // Rethrow wrapped exception
                throw new Exception($"Authentication failed: {ex.Message}", ex);
            }

            // Read response content as string
            var responseJson = await response.Content.ReadAsStringAsync();

            try
            {
                // Deserialize response into AuthResponse object
                var authResponse = JsonSerializer.Deserialize<AuthResponse>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                // Validate presence of ticket
                if (authResponse == null || string.IsNullOrEmpty(authResponse.Ticket))
                    throw new Exception("No ticket received from OpenText");

                // Mask ticket for logging
                var maskedTicket = MaskTicket(authResponse.Ticket);

                _logger.Log($"Successfully authenticated user {username}. Ticket: {maskedTicket}", LogLevel.INFO);

                // Log raw outbound success response
                var successPayload = JsonSerializer.Serialize(new
                {
                    status = "success",
                    masked_ticket = maskedTicket
                }, new JsonSerializerOptions { WriteIndented = true });

                _logger.LogRawOutbound("response_auth_external_success", successPayload);

                // Return the actual ticket
                return authResponse.Ticket;
            }
            catch (JsonException ex)
            {
                // Log JSON parsing exceptions and raw error response
                _logger.LogException(ex, LogLevel.ERROR);

                var errorPayload = JsonSerializer.Serialize(new
                {
                    status = "error",
                    error_message = ex.Message,
                    error_type = ex.GetType().Name
                }, new JsonSerializerOptions { WriteIndented = true });

                _logger.LogRawOutbound("response_auth_external_parse_error", errorPayload);

                // Rethrow wrapped exception
                throw new Exception($"Error parsing authentication response: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Authenticates internally using system credentials configured in app settings.
        /// </summary>
        /// <returns>Authentication ticket string if successful.</returns>
        public async Task<string> AuthenticateInternalAsync()
        {
            _logger.Log("Starting internal authentication with system credentials", LogLevel.INFO);

            // Retrieve configuration values for internal authentication
            var baseUrl = _configuration["OpenText:BaseUrl"];
            var authPath = _configuration["OpenText:AuthPath"];
            var username = _configuration["OpenText:internalUsername"];
            var password = _configuration["OpenText:internalPassword"];
            var domain = _configuration["OpenText:internalDomain"];

            // Validate presence of required configuration
            if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(authPath) ||
                string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                throw new Exception("Internal credentials not configured");

            var authUrl = $"{baseUrl}{authPath}";
            _logger.Log($"Authentication URL: {authUrl}", LogLevel.DEBUG);

            // Serialize request details for logging
            var requestDump = JsonSerializer.Serialize(new
            {
                username,
                domain,
                auth_url = authUrl,
                timestamp = DateTime.UtcNow
            }, new JsonSerializerOptions { WriteIndented = true });

            // Log raw outbound request
            _logger.LogRawOutbound("request_auth_internal", requestDump);

            // Prepare form data for POST request
            var formData = new Dictionary<string, string>
            {
                { "username", username },
                { "password", password },
                { "domain", domain ?? string.Empty }
            };

            var content = new FormUrlEncodedContent(formData);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            // Create HTTP POST request message
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, authUrl)
            {
                Content = content
            };

            // Set headers to accept JSON and indicate AJAX request
            requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            requestMessage.Headers.Add("X-Requested-With", "XMLHttpRequest");

            HttpResponseMessage response;
            try
            {
                // Send the HTTP request
                response = await _httpClient.SendAsync(requestMessage);
                _logger.Log($"Internal authentication status: {response.StatusCode}", LogLevel.DEBUG);

                // Handle unauthorized response explicitly
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    throw new Exception("Invalid internal credentials");

                // Throw if response indicates failure
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                // Log exception and raw error response
                _logger.LogException(ex, LogLevel.ERROR);

                var errorPayload = JsonSerializer.Serialize(new
                {
                    status = "error",
                    error_message = ex.Message,
                    error_type = ex.GetType().Name
                }, new JsonSerializerOptions { WriteIndented = true });

                _logger.LogRawOutbound("response_auth_internal_error", errorPayload);

                // Rethrow wrapped exception
                throw new Exception($"Internal authentication failed: {ex.Message}", ex);
            }

            // Read response content as string
            var responseJson = await response.Content.ReadAsStringAsync();

            try
            {
                // Deserialize response into AuthResponse object
                var authResponse = JsonSerializer.Deserialize<AuthResponse>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                // Validate presence of ticket
                if (authResponse == null || string.IsNullOrEmpty(authResponse.Ticket))
                    throw new Exception("No ticket received from OpenText (internal)");

                // Mask ticket for logging
                var maskedTicket = MaskTicket(authResponse.Ticket);
                _logger.Log($"Internal authentication OK. Ticket: {maskedTicket}", LogLevel.INFO);

                // Log raw outbound success response
                var successPayload = JsonSerializer.Serialize(new
                {
                    status = "success",
                    masked_ticket = maskedTicket
                }, new JsonSerializerOptions { WriteIndented = true });

                _logger.LogRawOutbound("response_auth_internal_success", successPayload);

                // Return the actual ticket
                return authResponse.Ticket;
            }
            catch (JsonException ex)
            {
                // Log JSON parsing exceptions and raw error response
                _logger.LogException(ex, LogLevel.ERROR);

                var errorPayload = JsonSerializer.Serialize(new
                {
                    status = "error",
                    error_message = ex.Message,
                    error_type = ex.GetType().Name
                }, new JsonSerializerOptions { WriteIndented = true });

                _logger.LogRawOutbound("response_auth_internal_parse_error", errorPayload);

                // Rethrow wrapped exception
                throw new Exception($"Error parsing internal auth response: {ex.Message}", ex);
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Masks the authentication ticket for safe logging.
        /// Shows first 8 and last 4 characters if ticket is long enough, otherwise masks completely.
        /// </summary>
        private string MaskTicket(string ticket)
        {
            if (string.IsNullOrEmpty(ticket)) return "********";
            return ticket.Length > 12 ? ticket[..8] + "..." + ticket[^4..] : "********";
        }

        #endregion
    }
}