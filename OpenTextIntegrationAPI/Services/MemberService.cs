// MemberService.cs
// Service for retrieving OpenText member (user/group/privilege) information via REST API
// Author: Ignacio Lucano
// Date: 2025-05-14

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OpenTextIntegrationAPI.Models;
using OpenTextIntegrationAPI.Services;

namespace OpenTextIntegrationAPI.Services
{
    /// <summary>
    /// Service that encapsulates calls to the OpenText /v2/members/{id} endpoint.
    /// Retrieves detailed information about users, groups, or restricted privileges.
    /// </summary>
    public class MemberService
    {
        #region Fields & Constructor

        private readonly HttpClient _httpClient;
        private readonly ILogService _logger;
        private readonly string _baseUrl;
        private readonly string _ticketHeaderName;

        /// <summary>
        /// Initializes a new instance of MemberService with required dependencies.
        /// </summary>
        /// <param name="httpClient">HTTP client configured with base address and default headers</param>
        /// <param name="configuration">Application configuration for OpenText settings</param>
        /// <param name="logger">Logging service for diagnostic and error recording</param>
        public MemberService(HttpClient httpClient,
                             IConfiguration configuration,
                             ILogService logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Read base URL and trace header name from configuration
            _baseUrl = configuration["OpenText:BaseUrl"]?.TrimEnd('/')
                       ?? throw new ArgumentException("Missing OpenText:BaseUrl in configuration");
            _ticketHeaderName = configuration["OpenText:TicketHeaderName"] ?? "OTCSTICKET";

            _logger.Log("MemberService initialized", LogLevel.DEBUG);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Retrieves the member (user, group or privilege) info for the specified ID.
        /// </summary>
        /// <param name="id">The ID of the user, group, or restricted privilege</param>
        /// <param name="fields">
        /// Optional fields filter, e.g. "properties{id,name}" or "versions{mime_type}.element(0)"
        /// </param>
        /// <param name="metadata">If true, includes metadata about each field</param>
        /// <returns>A <see cref="MemberProperties"/> instance populated with the returned data</returns>
        public async Task<MemberProperties> GetMemberAsync(int id, string ticket, string fields = null, bool metadata = false)
        {
            _logger.Log($"Starting GetMemberAsync for ID={id}", LogLevel.INFO);

            try
            {
                // Build request URL
                var urlBuilder = new StringBuilder($"{_baseUrl}/api/v2/members/{id}");
                var hasQuery = false;

                // Append "fields" query if provided
                if (!string.IsNullOrWhiteSpace(fields))
                {
                    urlBuilder.Append(hasQuery ? '&' : '?')
                              .Append("fields=").Append(Uri.EscapeDataString(fields));
                    hasQuery = true;
                }

                // Append "metadata" flag if requested
                if (metadata)
                {
                    urlBuilder.Append(hasQuery ? '&' : '?')
                              .Append("metadata");
                    hasQuery = true;
                }

                var requestUrl = urlBuilder.ToString();
                _logger.Log($"Constructed request URL: {requestUrl}", LogLevel.DEBUG);

                // Prepare HTTP GET request
                using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

                // Ensure authentication header is present
                request.Headers.Add("OTCSTICKET", ticket);

                // Send the request
                _logger.Log("Sending HTTP GET to OpenText", LogLevel.DEBUG);
                var response = await _httpClient.SendAsync(request);

                // Dump raw response for debugging
                var rawResponse = await response.Content.ReadAsStringAsync();
                _logger.LogRawOutbound("response_get_member_async", rawResponse);

                // Handle non-success status codes
                if (!response.IsSuccessStatusCode)
                {
                    _logger.Log($"GetMemberAsync returned HTTP {(int)response.StatusCode}", LogLevel.ERROR);
                    throw new HttpRequestException(
                        $"OpenText API returned {(int)response.StatusCode} for member ID {id}");
                }

                _logger.Log("Parsing JSON response", LogLevel.DEBUG);

                // Parse JSON document
                using var doc = JsonDocument.Parse(rawResponse);

                // Navigate to results[0].data[0].properties[0]
                var root = doc.RootElement;
                var propsElement = root
                    .GetProperty("results")
                    .GetProperty("data")
                    .GetProperty("properties");

                // Deserialize into our DTO
                var member = JsonSerializer.Deserialize<MemberProperties>(
                    propsElement.GetRawText(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                _logger.Log("GetMemberAsync completed successfully", LogLevel.INFO);
                return member;
            }
            catch (Exception ex)
            {
                _logger.LogException(ex);
                throw;
            }
        }

        #endregion
    }
}
