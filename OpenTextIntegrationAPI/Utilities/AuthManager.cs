using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using OpenTextIntegrationAPI.Services;
using OpenTextIntegrationAPI.Services.Interfaces;
using System;
using System.Linq;

namespace OpenTextIntegrationAPI.Utilities
{
    /// <summary>
    /// Provides helper methods for authentication tasks in the OpenText Integration API.
    /// Handles ticket extraction and validation from HTTP requests.
    /// </summary>
    public class AuthManager
    {
        private readonly AuthService _authService;
        private readonly IHostEnvironment _environment;
        private readonly ILogService _logger;

        /// <summary>
        /// Initializes a new instance of the AuthManager class with required dependencies.
        /// </summary>
        /// <param name="authService">Service for authentication operations with OpenText</param>
        /// <param name="environment">Host environment information</param>
        /// <param name="logger">Service for logging operations and errors</param>
        public AuthManager(AuthService authService, IHostEnvironment environment, ILogService logger)
        {
            _authService = authService;
            _environment = environment;
            _logger = logger;

            // Log manager initialization
            _logger.Log("AuthManager initialized", LogLevel.DEBUG);
        }

        /// <summary>
        /// Validates the Authorization header from the HttpRequest and extracts the ticket.
        /// In development environment, automatically generates a ticket if none is provided.
        /// </summary>
        /// <param name="request">The HTTP request containing the Authorization header</param>
        /// <returns>The authentication ticket as a string</returns>
        /// <exception cref="ArgumentException">Thrown when the Authorization header is missing or empty in non-development environments</exception>
        public string ExtractTicket(HttpRequest request)
        {
            _logger.Log("Extracting authentication ticket from request", LogLevel.DEBUG);

            // Log request details
            _logger.Log($"Request path: {request.Path}, Method: {request.Method}", LogLevel.DEBUG);

            // Retrieve the Authorization header value
            string authorizationHeader = request.Headers["Authorization"].FirstOrDefault();

            // Check if header is missing or empty
            if (string.IsNullOrWhiteSpace(authorizationHeader))
            {
                _logger.Log("No Authorization header found in request", LogLevel.WARNING);

                // In development, automatically create a ticket
                if (_environment.IsDevelopment())
                {
                    _logger.Log("Development environment detected - generating internal authentication ticket", LogLevel.INFO);

                    try
                    {
                        // Get internal authentication ticket
                        authorizationHeader = _authService.AuthenticateInternalAsync().GetAwaiter().GetResult();

                        // Log successful authentication with masked ticket
                        if (authorizationHeader != null && authorizationHeader.Length > 12)
                        {
                            string maskedTicket = authorizationHeader.Substring(0, 8) + "..." +
                                                 authorizationHeader.Substring(authorizationHeader.Length - 4);
                            _logger.Log($"Internal authentication successful. Ticket: {maskedTicket}", LogLevel.DEBUG);
                        }
                        else
                        {
                            _logger.Log("Internal authentication successful", LogLevel.DEBUG);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogException(ex, LogLevel.ERROR);
                        _logger.Log($"Internal authentication failed: {ex.Message}", LogLevel.ERROR);
                        throw new ArgumentException("Failed to generate internal authentication ticket", ex);
                    }
                }
                else
                {
                    // In production, require a valid token
                    _logger.Log("Production environment requires valid Authorization header", LogLevel.ERROR);
                    throw new ArgumentException("No Bearer token found in the Authorization header.");
                }
            }
            else
            {
                _logger.Log("Authorization header found in request", LogLevel.DEBUG);

                // Log that we found a ticket (without revealing the full ticket for security)
                if (authorizationHeader.Length > 12)
                {
                    string maskedTicket = authorizationHeader.Substring(0, 8) + "..." +
                                         authorizationHeader.Substring(authorizationHeader.Length - 4);
                    _logger.Log($"Extracted ticket: {maskedTicket}", LogLevel.DEBUG);
                }
                else
                {
                    _logger.Log("Extracted ticket (short format)", LogLevel.DEBUG);
                }
            }

            // Here, additional validation logic could be added if needed
            // For example, verifying token format, checking expiration, etc.

            // Return the extracted ticket
            return authorizationHeader;
        }
    }
}