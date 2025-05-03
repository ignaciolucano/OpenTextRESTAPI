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
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogService _logger;

        /// <summary>
        /// Initializes a new instance of the AuthService class with required dependencies.
        /// </summary>
        /// <param name="httpClient">HTTP client for API calls</param>
        /// <param name="configuration">Application configuration</param>
        /// <param name="logger">Service for logging operations and errors</param>
        public AuthService(HttpClient httpClient, IConfiguration configuration, ILogService logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            // Log service initialization
            _logger.Log("AuthService initialized", LogLevel.DEBUG);
        }

        /// <summary>
        /// Authenticates to OpenText with externally supplied credentials (from the API caller).
        /// </summary>
        /// <param name="username">User name for authentication</param>
        /// <param name="password">Password for authentication</param>
        /// <param name="domain">Domain for authentication</param>
        /// <returns>Authentication ticket string</returns>
        /// <exception cref="Exception">Thrown when authentication fails</exception>
        public async Task<string> AuthenticateExternalAsync(string username, string password, string domain)
        {
            _logger.Log($"Starting external authentication for user: {username}, Domain: {domain}", LogLevel.INFO);

            // Retrieve configuration values
            var baseUrl = _configuration["OpenText:BaseUrl"];
            var authPath = _configuration["OpenText:AuthPath"];

            // Validate configuration
            if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(authPath))
            {
                _logger.Log("Authentication configuration missing", LogLevel.ERROR);
                throw new Exception("OpenText authentication configuration is missing");
            }

            var authUrl = $"{baseUrl}{authPath}";
            _logger.Log($"Authentication URL: {authUrl}", LogLevel.DEBUG);

            // Log authentication attempt (without password)
            _logger.LogRawApi("api_request_auth_external",
                JsonSerializer.Serialize(new
                {
                    username,
                    domain,
                    auth_url = authUrl,
                    timestamp = DateTime.UtcNow
                })
            );

            // Create form data with credentials
            var formData = new Dictionary<string, string>
            {
                { "username", username },
                { "password", password },
                { "domain", domain }
            };

            var content = new FormUrlEncodedContent(formData);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            // Build request
            _logger.Log("Creating authentication request", LogLevel.DEBUG);
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, authUrl)
            {
                Content = content
            };

            requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            requestMessage.Headers.Add("X-Requested-With", "XMLHttpRequest");

            // Send request
            _logger.Log("Sending authentication request to OpenText", LogLevel.DEBUG);
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(requestMessage);

                // Log response status
                _logger.Log($"Authentication response status: {response.StatusCode}", LogLevel.DEBUG);

                // Handle different response status codes
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.Log($"Authentication failed: Invalid credentials for user {username}", LogLevel.WARNING);
                    throw new Exception("Invalid credentials");
                }

                // Ensure successful response
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Authentication request failed: {ex.Message}", LogLevel.ERROR);

                // Log failed authentication
                _logger.LogRawApi("api_response_auth_external_error",
                    JsonSerializer.Serialize(new
                    {
                        status = "error",
                        error_message = ex.Message,
                        error_type = ex.GetType().Name
                    })
                );

                throw new Exception($"Authentication failed: {ex.Message}", ex);
            }

            // Parse response JSON
            var responseJson = await response.Content.ReadAsStringAsync();

            try
            {
                _logger.Log("Parsing authentication response", LogLevel.DEBUG);
                var authResponse = JsonSerializer.Deserialize<AuthResponse>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (authResponse == null || string.IsNullOrEmpty(authResponse.Ticket))
                {
                    _logger.Log("No ticket received from OpenText", LogLevel.ERROR);

                    // Log authentication failure
                    _logger.LogRawApi("api_response_auth_external_error",
                        JsonSerializer.Serialize(new
                        {
                            status = "error",
                            error_message = "No ticket received from OpenText"
                        })
                    );

                    throw new Exception("No ticket received from OpenText");
                }

                // Log successful authentication with masked ticket
                string maskedTicket = MaskTicket(authResponse.Ticket);
                _logger.Log($"Successfully authenticated user {username}. Ticket obtained: {maskedTicket}", LogLevel.INFO);

                // Log successful authentication response (with masked ticket)
                _logger.LogRawApi("api_response_auth_external_success",
                    JsonSerializer.Serialize(new
                    {
                        status = "success",
                        masked_ticket = maskedTicket
                    })
                );

                return authResponse.Ticket;
            }
            catch (JsonException ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error parsing authentication response: {ex.Message}", LogLevel.ERROR);

                // Log parsing error
                _logger.LogRawApi("api_response_auth_external_parse_error",
                    JsonSerializer.Serialize(new
                    {
                        status = "error",
                        error_message = ex.Message,
                        error_type = ex.GetType().Name
                    })
                );

                throw new Exception($"Error parsing authentication response: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Authenticates internally with a special user (not exposed as an endpoint).
        /// Uses credentials stored in application configuration.
        /// </summary>
        /// <returns>Authentication ticket string</returns>
        /// <exception cref="Exception">Thrown when authentication fails</exception>
        public async Task<string> AuthenticateInternalAsync()
        {
            _logger.Log("Starting internal authentication with system credentials", LogLevel.INFO);

            var baseUrl = _configuration["OpenText:BaseUrl"];
            var authPath = _configuration["OpenText:AuthPath"];
            var internalUsername = _configuration["OpenText:internalUsername"];
            var internalPassword = _configuration["OpenText:internalPassword"];
            var internalDomain = _configuration["OpenText:internalDomain"];

            // Validate configuration
            if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(authPath) ||
                string.IsNullOrEmpty(internalUsername) || string.IsNullOrEmpty(internalPassword))
            {
                _logger.Log("Internal authentication credentials not configured", LogLevel.ERROR);
                throw new Exception("Internal authentication credentials not configured");
            }

            var authUrl = $"{baseUrl}{authPath}";
            _logger.Log($"Authentication URL: {authUrl}", LogLevel.DEBUG);

            // Log authentication attempt (without password)
            _logger.LogRawApi("api_request_auth_internal",
                JsonSerializer.Serialize(new
                {
                    username = internalUsername,
                    domain = internalDomain,
                    auth_url = authUrl,
                    timestamp = DateTime.UtcNow
                })
            );

            var formData = new Dictionary<string, string>
            {
                { "username", internalUsername },
                { "password", internalPassword },
                { "domain", internalDomain ?? string.Empty }
            };

            var content = new FormUrlEncodedContent(formData);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            // Build request
            _logger.Log("Creating internal authentication request", LogLevel.DEBUG);
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, authUrl)
            {
                Content = content
            };

            requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            requestMessage.Headers.Add("X-Requested-With", "XMLHttpRequest");

            // Send request
            _logger.Log("Sending internal authentication request to OpenText", LogLevel.DEBUG);
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(requestMessage);

                // Log response status
                _logger.Log($"Internal authentication response status: {response.StatusCode}", LogLevel.DEBUG);

                // Handle different response status codes
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.Log("Internal authentication failed: Invalid credentials", LogLevel.ERROR);
                    throw new Exception("Invalid internal credentials");
                }

                // Ensure successful response
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Internal authentication request failed: {ex.Message}", LogLevel.ERROR);

                // Log failed authentication
                _logger.LogRawApi("api_response_auth_internal_error",
                    JsonSerializer.Serialize(new
                    {
                        status = "error",
                        error_message = ex.Message,
                        error_type = ex.GetType().Name
                    })
                );

                throw new Exception($"Internal authentication failed: {ex.Message}", ex);
            }

            // Parse response JSON
            var responseJson = await response.Content.ReadAsStringAsync();

            try
            {
                _logger.Log("Parsing internal authentication response", LogLevel.DEBUG);
                var authResponse = JsonSerializer.Deserialize<AuthResponse>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (authResponse == null || string.IsNullOrEmpty(authResponse.Ticket))
                {
                    _logger.Log("No ticket received from OpenText for internal authentication", LogLevel.ERROR);

                    // Log authentication failure
                    _logger.LogRawApi("api_response_auth_internal_error",
                        JsonSerializer.Serialize(new
                        {
                            status = "error",
                            error_message = "No ticket received from OpenText"
                        })
                    );

                    throw new Exception("No ticket received from OpenText (internal)");
                }

                // Log successful authentication with masked ticket
                string maskedTicket = MaskTicket(authResponse.Ticket);
                _logger.Log($"Successfully authenticated internally with system credentials. Ticket obtained: {maskedTicket}", LogLevel.INFO);

                // Log successful authentication response (with masked ticket)
                _logger.LogRawApi("api_response_auth_internal_success",
                    JsonSerializer.Serialize(new
                    {
                        status = "success",
                        masked_ticket = maskedTicket
                    })
                );

                return authResponse.Ticket;
            }
            catch (JsonException ex)
            {
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error parsing internal authentication response: {ex.Message}", LogLevel.ERROR);

                // Log parsing error
                _logger.LogRawApi("api_response_auth_internal_parse_error",
                    JsonSerializer.Serialize(new
                    {
                        status = "error",
                        error_message = ex.Message,
                        error_type = ex.GetType().Name
                    })
                );

                throw new Exception($"Error parsing internal authentication response: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Masks authentication ticket for logging purposes.
        /// </summary>
        /// <param name="ticket">The ticket to mask</param>
        /// <returns>Masked ticket string</returns>
        private string MaskTicket(string ticket)
        {
            if (string.IsNullOrEmpty(ticket))
                return "********";

            if (ticket.Length > 12)
                return ticket.Substring(0, 8) + "..." + ticket.Substring(ticket.Length - 4);

            return "********";
        }
    }
}