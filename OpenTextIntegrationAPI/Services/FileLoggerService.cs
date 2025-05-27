// FileLoggerService.cs
// Advanced logging utility for file-based tracing and raw dumps with trace mapping support
// Author: Ignacio Lucano
// Date: 2025-05-13

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

#region LogLevel & Options

/// <summary>
/// Defines supported log levels for filtering.
/// </summary>
public enum LogLevel
{
    TRACE,
    DEBUG,
    INFO,
    WARNING,
    ERROR,
    FATAL
}

/// <summary>
/// Configurable options loaded from appsettings.json for the FileLogger.
/// </summary>
public class FileLoggerOptions
{
    public string LogDirectory { get; set; } // Directory where logs will be stored
    public string LogLevel { get; set; } // Minimum log level to record
    public int MaxFileSizeMB { get; set; } // Maximum size of a log file before rotation
    public string FileNamePattern { get; set; } // Pattern for log file names, e.g. "log_{date}.txt"
    public bool EnableRawInboundLogs { get; set; } // Flag to enable raw inbound HTTP request/response logging
    public bool EnableRawApiLogs { get; set; } // Flag to enable raw outbound API logging
    public string RawApiSubfolder { get; set; } // Subfolder for raw API logs
    public string RawInboundSubfolder { get; set; } // Subfolder for raw inbound logs
    public string RawMapSubfolder { get; set; }  // Subfolder for trace mapping files
    public string TraceHeaderName { get; set; } // HTTP header name used to extract trace ID
}

#endregion

#region ILogService

/// <summary>
/// Interface for unified logging across the application.
/// </summary>
public interface ILogService
{
    /// <summary>
    /// Logs a message with a specified log level.
    /// </summary>
    void Log(string message, LogLevel level);

    /// <summary>
    /// Asynchronously logs a message with a specified log level.
    /// </summary>
    Task LogAsync(string message, LogLevel level);

    /// <summary>
    /// Logs an exception with an optional log level (default is ERROR).
    /// </summary>
    void LogException(Exception ex, LogLevel level = LogLevel.ERROR);

    /// <summary>
    /// Logs raw inbound HTTP request data for the specified caller method.
    /// </summary>
    string LogRawInbound(string callerMethod);

    /// <summary>
    /// Logs raw inbound HTTP request data with provided content.
    /// </summary>
    string LogRawInbound(string callerMethod, string content);

    /// <summary>
    /// Logs raw inbound HTTP response data with provided content.
    /// </summary>
    string LogRawInboundResponse(string callerMethod, string content);

    /// <summary>
    /// Logs raw outbound data with provided content.
    /// </summary>
    string LogRawOutbound(string callerMethod, string content);

    /// <summary>
    /// Logs raw outbound data for the specified caller method.
    /// </summary>
    string LogRawOutbound(string callerMethod);

    /// <summary>
    /// Legacy method for logging raw API data with content. Returns empty string.
    /// </summary>
    //string LogRawApi(string callerMethod, string content);

    /// <summary>
    /// Legacy method for logging raw API data. Returns empty string.
    /// </summary>
    //string LogRawApi(string callerMethod);
}

#endregion

#region FileLoggerService

/// <summary>
/// File-based implementation of ILogService.
/// Includes detailed raw dump logging and per-trace mapping.
/// </summary>
public class FileLoggerService : ILogService
{
    private readonly FileLoggerOptions _options; // Configuration options for the logger
    private readonly LogLevel _minLevel; // Minimum log level to record
    private readonly object _lock = new(); // Lock object for thread-safe file writes
    private readonly IHttpContextAccessor _httpContextAccessor; // Accessor to current HTTP context
    private readonly string _traceHeaderName; // Name of the HTTP header containing trace ID
    private static readonly Dictionary<string, int> TraceStepCounter = new(); // Tracks step count per trace ID
    private static readonly Dictionary<string, DateTime> TraceTimestamps = new(); // Tracks first timestamp per trace ID
    private readonly Dictionary<string, string> _traceMapCache = new(); // Cache for trace map file paths

    /// <summary>
    /// Constructor initializes options, HTTP context accessor, and ensures log directory exists.
    /// </summary>
    public FileLoggerService(IOptions<FileLoggerOptions> options, IHttpContextAccessor httpContextAccessor)
    {
        _options = options.Value;
        _httpContextAccessor = httpContextAccessor;
        _traceHeaderName = _options.TraceHeaderName;

        // Ensure the log directory exists
        Directory.CreateDirectory(_options.LogDirectory);

        // Parse minimum log level from options, default to INFO if invalid
        if (!Enum.TryParse(_options.LogLevel.ToUpper(), out _minLevel))
            _minLevel = LogLevel.INFO;
    }

    #region Public Logging Methods

    /// <summary>
    /// Logs a message if its level is at or above the configured minimum.
    /// Thread-safe with locking.
    /// </summary>
    public void Log(string message, LogLevel level)
    {
        if (level < _minLevel) return; // Skip if below minimum level

        lock (_lock)
        {
            var path = GetLogFilePath(); // Get current log file path
            RotateIfNeeded(path); // Rotate file if too large
            File.AppendAllText(path, FormatLine(message, level) + Environment.NewLine, Encoding.UTF8); // Append log line
        }
    }

    /// <summary>
    /// Asynchronously logs a message if its level is at or above the configured minimum.
    /// </summary>
    public async Task LogAsync(string message, LogLevel level)
    {
        if (level < _minLevel) return; // Skip if below minimum level

        var path = GetLogFilePath(); // Get current log file path
        RotateIfNeeded(path); // Rotate file if too large
        var entry = FormatLine(message, level); // Format log line

        using var w = File.AppendText(path);
        await w.WriteLineAsync(entry); // Write asynchronously
    }

    /// <summary>
    /// Logs detailed exception information including inner exceptions if present.
    /// </summary>
    public void LogException(Exception ex, LogLevel level = LogLevel.ERROR)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Exception: {ex.Message}");
        sb.AppendLine($"StackTrace: {ex.StackTrace}");

        if (ex.InnerException != null)
        {
            sb.AppendLine($"InnerException: {ex.InnerException.Message}");
            sb.AppendLine($"InnerStackTrace: {ex.InnerException.StackTrace}");
        }

        Log(sb.ToString(), level);
    }

    /// <summary>
    /// Logs raw inbound HTTP request data by building a full dump from the current HTTP context.
    /// Returns empty string if raw inbound logging is disabled or no HTTP context.
    /// </summary>
    public string LogRawInbound(string callerMethod)
    {
        if (!_options.EnableRawInboundLogs) return string.Empty;

        var ctx = _httpContextAccessor.HttpContext;
        if (ctx == null) return string.Empty;

        var dump = BuildFullHttpDump(ctx.Request);
        return LogRawInbound(callerMethod, dump);
    }

    /// <summary>
    /// Logs raw inbound HTTP request data with provided content.
    /// Returns empty string if raw inbound logging is disabled.
    /// </summary>
    public string LogRawInbound(string callerMethod, string content)
    {
        if (!_options.EnableRawInboundLogs) return string.Empty;

        string traceId = GetTraceId(); // Get trace ID from headers or fallback
        string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss"); // Timestamp for filename
        var fileName = $"{timestamp}_request_{callerMethod}_{traceId}";

        // Log raw data to inbound subfolder with fixed status code 200 for requests
        return LogRawToSubfolder(fileName, content, _options.RawInboundSubfolder, traceId, "inbound", "request", 200);
    }

    /// <summary>
    /// Logs raw inbound HTTP response data with provided content.
    /// Returns empty string if raw inbound logging is disabled.
    /// </summary>
    public string LogRawInboundResponse(string callerMethod, string content)
    {
        if (!_options.EnableRawInboundLogs) return string.Empty;

        string traceId = GetTraceId();
        string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var fileName = $"{timestamp}_response_{callerMethod}_{traceId}";

        // Log raw data to inbound subfolder with current HTTP status code
        return LogRawToSubfolder(fileName, content, _options.RawInboundSubfolder, traceId, "inbound", "response", GetCurrentStatusCode());
    }

    /// <summary>
    /// Logs raw outbound data with provided content.
    /// Returns empty string if raw API logging is disabled.
    /// </summary>
    public string LogRawOutbound(string callerMethod, string content)
    {
        if (!_options.EnableRawApiLogs) return string.Empty;

        string traceId = GetTraceId();
        string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var fileName = $"{timestamp}_{callerMethod}_{traceId}";

        // Log raw data to API subfolder with current HTTP status code
        return LogRawToSubfolder(fileName, content, _options.RawApiSubfolder, traceId, "outbound", "response", GetCurrentStatusCode());
    }

    /// <summary>
    /// Logs raw outbound data by building a full HTTP response dump from the current HTTP context.
    /// Returns empty string if raw API logging is disabled or no HTTP context.
    /// </summary>
    public string LogRawOutbound(string callerMethod)
    {
        if (!_options.EnableRawApiLogs) return string.Empty;

        var ctx = _httpContextAccessor.HttpContext;
        if (ctx == null) return string.Empty;

        var dump = BuildFullHttpResponseDump(ctx.Response);
        return LogRawOutbound(callerMethod, dump);
    }

    /// <summary>
    /// Legacy method for logging raw API data with content. Returns empty string.
    /// </summary>
    //public string LogRawApi(string callerMethod, string content) => string.Empty;

    /// <summary>
    /// Legacy method for logging raw API data. Returns empty string.
    /// </summary>
    //public string LogRawApi(string callerMethod) => string.Empty;

    #endregion

    #region Private Helpers

    /// <summary>
    /// Writes the raw dump file for a given trace and updates the corresponding trace map file.
    /// </summary>
    /// <param name="namePrefix">Base name for the dump file (includes method and traceId).</param>
    /// <param name="content">Content to dump (JSON or text).</param>
    /// <param name="subfolder">Subfolder to store the dump.</param>
    /// <param name="traceId">Trace ID string.</param>
    /// <param name="direction">'inbound' or 'outbound'.</param>
    /// <param name="type">'request' or 'response'.</param>
    /// <param name="statusCode">HTTP status code (optional).</param>
    /// <returns>Filename of the saved dump or empty string on failure.</returns>
    private string LogRawToSubfolder(string namePrefix, string content, string subfolder, string traceId, string direction, string type, int? statusCode)
    {
        try
        {
            // Ensure the target folder exists
            var folder = Path.Combine(_options.LogDirectory, subfolder);
            Directory.CreateDirectory(folder);

            // Compose filename and full path
            var fileName = namePrefix + ".txt";
            var fullPath = Path.Combine(folder, fileName);

            // Prepend trace ID to content for traceability
            var traceStampedContent = $"SimpleMDG_TraceLogID: {traceId}\n\n" + content;

            // Write the content to file
            File.WriteAllText(fullPath, traceStampedContent, Encoding.UTF8);

            // Log debug message about saved raw log
            Log($"[Trace: {traceId}] RAW log saved: {subfolder}\\{fileName}", LogLevel.DEBUG);

            // Normalize relative path for map file
            var relativePath = Path.Combine(subfolder, fileName).Replace("\\", "/");

            // Update the trace map file with this entry
            WriteTraceMap(traceId, namePrefix, type, direction, statusCode, relativePath);

            return fileName;
        }
        catch (Exception ex)
        {
            // Log warning if writing raw log fails
            Log($"Failed to write RAW log '{namePrefix}': {ex.Message}", LogLevel.WARNING);
            return string.Empty;
        }
    }

    /// <summary>
    /// Writes a single trace entry to the per-trace map file inside Raw\Maps.
    /// Each line includes timestamp, method, type, status code, direction, source, duration, and relative path.
    /// </summary>
    private void WriteTraceMap(string traceId, string methodKey, string type, string direction, int? statusCode, string relativePath)
    {
        try
        {
            var ctx = _httpContextAccessor.HttpContext;

            // Increment the step counter for this trace ID
            int traceStep = 1;
            lock (TraceStepCounter)
            {
                if (TraceStepCounter.ContainsKey(traceId))
                    traceStep = ++TraceStepCounter[traceId];
                else
                    TraceStepCounter[traceId] = 1;
            }

            // Store the first timestamp for inbound requests to calculate duration later
            if (type == "request" && direction == "inbound")
            {
                lock (TraceTimestamps)
                {
                    if (!TraceTimestamps.ContainsKey(traceId))
                        TraceTimestamps[traceId] = DateTime.UtcNow;
                }
            }

            // Calculate duration in milliseconds for responses
            long? durationMs = null;
            if (type == "response" && TraceTimestamps.ContainsKey(traceId))
            {
                var start = TraceTimestamps[traceId];
                durationMs = (long)(DateTime.UtcNow - start).TotalMilliseconds;
            }
            else if (type == "request")
            {
                durationMs = 0;
            }

            // Determine controller and action names from route data if available
            string controllerAction = "Unknown";
            if (ctx?.Request?.RouteValues != null &&
                ctx.Request.RouteValues.TryGetValue("controller", out var c) &&
                ctx.Request.RouteValues.TryGetValue("action", out var a))
            {
                controllerAction = $"{c}_{a}";
            }

            // Extract source from User-Agent header
            string source = "Unknown";
            if (ctx?.Request?.Headers.TryGetValue("User-Agent", out var ua) == true)
            {
                var userAgent = ua.ToString().ToLower();
                if (userAgent.Contains("postman")) source = "Postman";
                else if (userAgent.Contains("mozilla")) source = "Browser";
                else if (userAgent.Contains("curl")) source = "Curl";
                else source = "Other";
            }

            // Normalize the relative path to use forward slashes
            var normalizedPath = relativePath.Replace("\\", "/");

            // Prepare CSV line with all trace details
            var mapLine = string.Join(",",
                DateTime.UtcNow.ToString("o"), // ISO 8601 timestamp
                traceStep,
                controllerAction,
                methodKey,
                type,
                statusCode?.ToString() ?? "N/A",
                direction,
                source,
                durationMs?.ToString() ?? "",
                normalizedPath
            );

            // Get the map file path for this trace ID
            var mapFilePath = GetMapFilePath(traceId);

            // Append the trace line to the map file
            File.AppendAllText(mapFilePath, mapLine + Environment.NewLine, Encoding.UTF8);

            // Output trace map line to console for debugging
            Console.WriteLine($"[TRACE MAP] {mapLine}");
        }
        catch (Exception ex)
        {
            // Log warning if writing trace map fails
            Log($"[Trace: {traceId}] Failed to write trace map: {ex.Message}", LogLevel.WARNING);
        }
    }

    /// <summary>
    /// Safely gets the current HTTP response status code.
    /// Returns 0 if unavailable or on error.
    /// </summary>
    private int GetCurrentStatusCode()
    {
        try
        {
            return _httpContextAccessor.HttpContext?.Response?.StatusCode ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Formats a log line with timestamp and log level.
    /// </summary>
    private string FormatLine(string message, LogLevel level)
        => $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";

    /// <summary>
    /// Gets the current log file path based on configured file name pattern and date.
    /// </summary>
    private string GetLogFilePath()
    {
        var fn = _options.FileNamePattern.Replace("{date}", DateTime.Now.ToString("yyyy-MM-dd"));
        return Path.Combine(_options.LogDirectory, fn);
    }

    /// <summary>
    /// Rotates the log file if it exceeds the configured maximum size.
    /// Renames current log file to .old and deletes any existing .old file.
    /// </summary>
    private void RotateIfNeeded(string path)
    {
        if (!File.Exists(path)) return;

        var info = new FileInfo(path);
        long maxBytes = _options.MaxFileSizeMB * 1024L * 1024L;

        if (info.Length < maxBytes) return;

        var old = path + ".old";

        if (File.Exists(old)) File.Delete(old);

        File.Move(path, old);
    }

    /// <summary>
    /// Retrieves the trace ID from the configured HTTP header or generates a fallback ID.
    /// </summary>
    private string GetTraceId()
    {
        var ctx = _httpContextAccessor.HttpContext;

        if (ctx?.Request?.Headers != null &&
            ctx.Request.Headers.TryGetValue(_traceHeaderName, out var traceIdHeader) &&
            !string.IsNullOrWhiteSpace(traceIdHeader))
        {
            return traceIdHeader.ToString();
        }

        // Fallback trace ID with timestamp
        return $"NoTrace_{DateTime.UtcNow:yyyyMMddHHmmssfff}";
    }

    /// <summary>
    /// Builds a full HTTP request dump including method, headers, body, form data, etc.
    /// </summary>
    private string BuildFullHttpDump(HttpRequest req)
    {
        string body = string.Empty;

        try
        {
            // Read request body if available and readable
            if (req.Body.CanRead && req.ContentLength > 0)
            {
                req.EnableBuffering();
                req.Body.Position = 0;

                using var reader = new StreamReader(req.Body, Encoding.UTF8, leaveOpen: true);
                body = reader.ReadToEnd();

                req.Body.Position = 0; // Reset stream position for downstream middleware
            }
        }
        catch (Exception ex)
        {
            // Include error message in body dump if reading fails
            body = $"[[ERROR reading body: {ex.Message}]]";
        }

        // Build anonymous object with request details
        var result = new
        {
            Method = req.Method,
            Scheme = req.Scheme,
            Host = req.Host.Value,
            Path = req.Path.Value,
            QueryString = req.QueryString.Value,
            Protocol = req.Protocol,
            ContentType = req.ContentType,
            ContentLength = req.ContentLength,
            Headers = req.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
            HasFormContentType = req.HasFormContentType,
            FormKeys = req.HasFormContentType ? req.Form.Keys : Array.Empty<string>(),
            FormFiles = req.HasFormContentType ? req.Form.Files.Select(f => new
            {
                f.Name,
                f.FileName,
                f.Length,
                f.ContentType
            }) : Enumerable.Empty<object>(),
            RawBody = body
        };

        // Serialize with indentation for readability
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Builds a full HTTP response dump including status code, headers, and timestamp.
    /// </summary>
    private string BuildFullHttpResponseDump(HttpResponse res)
    {
        var result = new
        {
            StatusCode = res.StatusCode,
            Headers = res.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
            Timestamp = DateTime.UtcNow
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Returns an effective trace ID by checking if a map file already exists.
    /// If it exists, appends suffixes _002, _003, etc. to find a unique ID.
    /// </summary>
    private string GetEffectiveTraceId(string baseTraceId)
    {
        var mapFolder = Path.Combine(_options.LogDirectory, _options.RawMapSubfolder ?? "Raw\\Maps");
        Directory.CreateDirectory(mapFolder);

        string baseFile = Path.Combine(mapFolder, $"Map_{baseTraceId}.txt");

        if (!File.Exists(baseFile))
            return baseTraceId;

        // Search for next available suffix
        for (int i = 2; i < 1000; i++)
        {
            var candidateId = $"{baseTraceId}_{i:D3}";
            var candidateFile = Path.Combine(mapFolder, $"Map_{candidateId}.txt");

            if (!File.Exists(candidateFile))
                return candidateId;
        }

        // Fallback if too many attempts
        return baseTraceId + "_999";
    }

    /// <summary>
    /// Returns the correct map file path for a given traceId.
    /// If the map file already exists, adds suffixes _002, _003, etc. to find a unique file path.
    /// Caches results for performance.
    /// </summary>
    private string GetMapFilePath(string traceId)
    {
        if (_traceMapCache.ContainsKey(traceId))
            return _traceMapCache[traceId];

        var mapFolder = Path.Combine(_options.LogDirectory, _options.RawMapSubfolder ?? "Raw\\Maps");
        Directory.CreateDirectory(mapFolder);

        var baseName = $"Map_{traceId}.txt";
        var basePath = Path.Combine(mapFolder, baseName);

        if (!File.Exists(basePath))
        {
            _traceMapCache[traceId] = basePath;
            return basePath;
        }

        // Search next available version
        for (int i = 2; i < 1000; i++)
        {
            var versionedName = $"Map_{traceId}_{i:D3}.txt";
            var versionedPath = Path.Combine(mapFolder, versionedName);
            if (!File.Exists(versionedPath))
            {
                _traceMapCache[traceId] = versionedPath;
                return versionedPath;
            }
        }

        // Fallback path if all attempts fail
        var fallbackPath = Path.Combine(mapFolder, $"Map_{traceId}_999.txt");
        _traceMapCache[traceId] = fallbackPath;
        return fallbackPath;
    }

    #endregion
}

#endregion