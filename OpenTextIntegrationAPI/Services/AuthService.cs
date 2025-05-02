using System.Net.Http.Headers;
using System.Text.Json;
using OpenTextIntegrationAPI.Models;
using OpenTextIntegrationAPI.DTOs;

namespace OpenTextIntegrationAPI.Services
{
    public class AuthService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public AuthService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        /// <summary>
        /// Authenticates to OpenText with externally supplied credentials (from the API caller).
        /// </summary>
        public async Task<string> AuthenticateExternalAsync(string username, string password, string domain)
        {
            // For example, the baseUrl and authPath come from appsettings.json
            var baseUrl = _configuration["OpenText:BaseUrl"];    // e.g. "http://myserver/OTCS/cs.exe"
            var authPath = _configuration["OpenText:AuthPath"];  // e.g. "/api/v1/auth"
            var authUrl = $"{baseUrl}{authPath}";

            // Prepare form data
            var formData = new Dictionary<string, string>
            {
                { "username", username },
                { "password", password },
                { "domain", domain ?? string.Empty }
            };

            var content = new FormUrlEncodedContent(formData);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            // Build request
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, authUrl)
            {
                Content = content
            };

            requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            requestMessage.Headers.Add("X-Requested-With", "XMLHttpRequest");

            // Send request
            var response = await _httpClient.SendAsync(requestMessage);
            response.EnsureSuccessStatusCode();

            // Parse response JSON
            var responseJson = await response.Content.ReadAsStringAsync();
            var authResponse = JsonSerializer.Deserialize<AuthResponse>(responseJson);

            if (authResponse == null || string.IsNullOrEmpty(authResponse.Ticket))
            {
                throw new Exception("No ticket received from OpenText (external).");
            }

            return authResponse.Ticket;
        }

        /// <summary>
        /// Authenticates internally with a special user (not exposed as an endpoint).
        /// </summary>
        public async Task<string> AuthenticateInternalAsync()
        {
            var baseUrl = _configuration["OpenText:BaseUrl"];
            var authPath = _configuration["OpenText:AuthPath"];
            var authUrl = $"{baseUrl}{authPath}";

            // Special credentials from appsettings.json
            var internalUsername = _configuration["OpenText:InternalUsername"];
            var internalPassword = _configuration["OpenText:InternalPassword"];
            var internalDomain = _configuration["OpenText:InternalDomain"];

            var formData = new Dictionary<string, string>
    {
        { "username", internalUsername },
        { "password", internalPassword },
        { "domain", internalDomain ?? string.Empty }
    };

            var content = new FormUrlEncodedContent(formData);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, authUrl)
            {
                Content = content
            };

            requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            requestMessage.Headers.Add("X-Requested-With", "XMLHttpRequest");

            var response = await _httpClient.SendAsync(requestMessage);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var authResponse = JsonSerializer.Deserialize<AuthResponse>(responseJson);

            if (authResponse == null || string.IsNullOrEmpty(authResponse.Ticket))
            {
                throw new Exception("No ticket received from OpenText (internal).");
            }

            return authResponse.Ticket;
        }

    }
}
