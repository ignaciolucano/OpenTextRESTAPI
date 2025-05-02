using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using OpenTextIntegrationAPI.Services;
using OpenTextIntegrationAPI.Services.Interfaces;
using System;
using System.Linq;

namespace OpenTextIntegrationAPI.Utilities
{
    /// <summary>
    /// Provides helper methods for authentication tasks.
    /// </summary>
    public class AuthManager
    {
        private readonly AuthService _authService;
        private readonly IHostEnvironment _environment;

        public AuthManager(AuthService authService, IHostEnvironment environment)
        {
            _authService = authService;
            _environment = environment;
        }
        /// <summary>
        /// Validates the Authorization header from the HttpRequest and extracts the ticket.
        /// </summary>
        /// <param name="request">The HTTP request containing the Authorization header.</param>
        /// <returns>The ticket as a string.</returns>
        /// <exception cref="ArgumentException">Thrown when the Authorization header is missing or empty.</exception>
        public string ExtractTicket(HttpRequest request)
        {
            // Retrieve the Authorization header value.
            string authorizationHeader = request.Headers["Authorization"].FirstOrDefault();

            // Validate that the header is present.
            if (string.IsNullOrWhiteSpace(authorizationHeader))
            {
                
                if (_environment.IsDevelopment())
                {
                    authorizationHeader = _authService.AuthenticateInternalAsync().GetAwaiter().GetResult();
                } else
                {
                    throw new ArgumentException("No Bearer token found in the Authorization header.");
                }
            }

            // Here, you could add further validation logic if needed
            // For now, simply return the header value (which is our ticket).
            return authorizationHeader;
        }
    }
}
