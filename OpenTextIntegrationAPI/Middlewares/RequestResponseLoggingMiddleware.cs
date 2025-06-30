// RequestResponseLoggingMiddleware.cs
// Middleware for logging inbound HTTP requests and responses with support for selective endpoint logging
// Author: Ignacio Lucano
// Date: 05/13/2025
// Description: This middleware checks if an endpoint is enabled for logging based on appsettings.json.
//              If enabled, it logs the full HTTP request and response. If not explicitly configured, it logs under "NOT_MAPPED".

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using OpenTextIntegrationAPI.Services;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenTextIntegrationAPI.Middlewares
{
    /// <summary>
    /// Middleware that logs full HTTP requests and responses for enabled endpoints.
    /// Uses configuration section "EndpointLogging" to determine which endpoints to log.
    /// </summary>
    public class RequestResponseLoggingMiddleware
    {
        #region Fields

        private readonly RequestDelegate _next;
        private readonly ILogService _logger;
        private readonly IConfiguration _configuration;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes the middleware with dependencies.
        /// </summary>
        public RequestResponseLoggingMiddleware(
            RequestDelegate next,
            ILogService logger,
            IConfiguration configuration)
        {
            _next = next;
            _logger = logger;
            _configuration = configuration;
        }

        #endregion

        #region Middleware Logic

        /// <summary>
        /// Invokes the middleware for the current HTTP context.
        /// Logs request and response based on appsettings configuration.
        /// </summary>
        public async Task InvokeAsync(HttpContext ctx)
        {
            //────────────────────────────────────────────────────────────
            // Build the key based on the raw request path (e.g. "/api/Auth/login")
            // This will be used to match against the EndpointLogging section in appsettings.json
            //────────────────────────────────────────────────────────────
            string pathKey = ctx.Request.Path.Value ?? "/unknown";

            if (pathKey.Contains("loganalyzer", StringComparison.OrdinalIgnoreCase))
            {
                await _next(ctx); // Continue pipeline without logging
                return;
            }

            string finalKey = pathKey.Trim('/').Replace("/", "_"); // Used for log file naming

            //────────────────────────────────────────────────────────────
            // Check if logging is enabled for this exact path
            // If not defined, log anyway with "NOT_MAPPED" prefix
            // If explicitly disabled, skip logging entirely
            //────────────────────────────────────────────────────────────
            bool? loggingEnabled = _configuration
                .GetSection("EndpointLogging")
                .GetChildren()
                .FirstOrDefault(x => x.Key.StartsWith(pathKey, StringComparison.OrdinalIgnoreCase))
                ?.Get<bool>();

            if (loggingEnabled is null)
            {
                finalKey = $"NOT_MAPPED_{finalKey}";
            }
            else if (loggingEnabled == false)
            {
                await _next(ctx); // Skip logging and continue pipeline
                return;
            }

            // ─────────────────────────────────────
            // REQUEST DUMP
            // ─────────────────────────────────────

            ctx.Request.EnableBuffering();
            using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8, leaveOpen: true);
            string requestBody = await reader.ReadToEndAsync();
            ctx.Request.Body.Position = 0;

            var requestDump = JsonSerializer.Serialize(new
            {
                Method = ctx.Request.Method,
                Scheme = ctx.Request.Scheme,
                Host = ctx.Request.Host.Value,
                Path = ctx.Request.Path,
                QueryString = ctx.Request.QueryString.Value,
                Protocol = ctx.Request.Protocol,
                Headers = ctx.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
                Body = requestBody,
                Timestamp = DateTime.UtcNow
            }, new JsonSerializerOptions { WriteIndented = true });

            _logger.LogRawInbound(finalKey, requestDump);

            // ─────────────────────────────────────
            // RESPONSE DUMP
            // ─────────────────────────────────────

            var originalBody = ctx.Response.Body;
            using var responseBody = new MemoryStream();
            ctx.Response.Body = responseBody;

            // Execute the next middleware/controller
            await _next(ctx);

            // Read response body from memory stream
            ctx.Response.Body.Seek(0, SeekOrigin.Begin);
            string responseText = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
            ctx.Response.Body.Seek(0, SeekOrigin.Begin);

            var responseDump = JsonSerializer.Serialize(new
            {
                StatusCode = ctx.Response.StatusCode,
                Headers = ctx.Response.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
                Body = responseText,
                Timestamp = DateTime.UtcNow
            }, new JsonSerializerOptions { WriteIndented = true });

            _logger.LogRawInboundResponse(finalKey, responseDump);

            // Copy the response body back to the original stream
            await responseBody.CopyToAsync(originalBody);
        }

        #endregion
    }
}
