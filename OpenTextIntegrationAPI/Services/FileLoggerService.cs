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
    public string LogDirectory { get; set; } = "logs";
    public string LogLevel { get; set; } = "INFO";
    public int MaxFileSizeMB { get; set; } = 5;
    public string FileNamePattern { get; set; } = "application_{date}.log";
    public bool EnableRawInboundLogs { get; set; } = false;
    public bool EnableRawApiLogs { get; set; } = false;
    public string RawApiSubfolder { get; set; } = "Raw\\Outbound";
    public string RawInboundSubfolder { get; set; } = "Raw\\Inbound";
    public string RawMapSubfolder { get; set; } = "Raw\\Maps";
    public string TraceHeaderName { get; set; } = "SimpleMDG-TraceLogID";
}

#endregion

#region ILogService

/// <summary>
/// Interface for unified logging across the application.
/// </summary>
public interface ILogService
{
    void Log(string message, LogLevel level);
    Task LogAsync(string message, LogLevel level);
    void LogException(Exception ex, LogLevel level = LogLevel.ERROR);
    string LogRawInbound(string callerMethod);
    string LogRawInbound(string callerMethod, string content);
    string LogRawInboundResponse(string callerMethod, string content);
    string LogRawOutbound(string callerMethod, string content);
    string LogRawOutbound(string callerMethod);
}

#endregion

#region FileLoggerService

/// <summary>
/// File-based implementation of ILogService.
/// Includes detailed raw dump logging and per-trace mapping with improved timing.
/// </summary>
public class FileLoggerService : ILogService
{
    private readonly FileLoggerOptions _options;
    private readonly LogLevel _minLevel;
    private readonly object _lock = new();
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _traceHeaderName;
    private static readonly Dictionary<string, int> TraceStepCounter = new();
    private static readonly Dictionary<string, DateTime> TraceTimestamps = new();
    private static readonly Dictionary<string, DateTime> TraceLastStepTimestamps = new(); // New: track last step timestamp
    private readonly Dictionary<string, string> _traceMapCache = new();

    public FileLoggerService(IOptions<FileLoggerOptions> options, IHttpContextAccessor httpContextAccessor)
    {
        _options = options.Value;
        _httpContextAccessor = httpContextAccessor;
        _traceHeaderName = _options.TraceHeaderName;

        Directory.CreateDirectory(_options.LogDirectory);

        if (!Enum.TryParse(_options.LogLevel.ToUpper(), out _minLevel))
            _minLevel = LogLevel.INFO;
    }

    #region Public Logging Methods

    public void Log(string message, LogLevel level)
    {
        if (level < _minLevel) return;

        lock (_lock)
        {
            var path = GetLogFilePath();
            RotateIfNeeded(path);
            File.AppendAllText(path, FormatLine(message, level) + Environment.NewLine, Encoding.UTF8);
        }
    }

    public async Task LogAsync(string message, LogLevel level)
    {
        if (level < _minLevel) return;

        var path = GetLogFilePath();
        RotateIfNeeded(path);
        var entry = FormatLine(message, level);

        using var w = File.AppendText(path);
        await w.WriteLineAsync(entry);
    }

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

    public string LogRawInbound(string callerMethod)
    {
        if (!_options.EnableRawInboundLogs) return string.Empty;

        var ctx = _httpContextAccessor.HttpContext;
        if (ctx == null) return string.Empty;

        var dump = BuildFullHttpDump(ctx.Request);
        return LogRawInbound(callerMethod, dump);
    }

    public string LogRawInbound(string callerMethod, string content)
    {
        if (!_options.EnableRawInboundLogs) return string.Empty;

        string traceId = GetTraceId();
        string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var fileName = $"{timestamp}_request_{callerMethod}_{traceId}";

        return LogRawToSubfolder(fileName, content, _options.RawInboundSubfolder, traceId, "inbound", "request", 200);
    }

    public string LogRawInboundResponse(string callerMethod, string content)
    {
        if (!_options.EnableRawInboundLogs) return string.Empty;

        string traceId = GetTraceId();
        string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var fileName = $"{timestamp}_response_{callerMethod}_{traceId}";

        return LogRawToSubfolder(fileName, content, _options.RawInboundSubfolder, traceId, "inbound", "response", GetCurrentStatusCode());
    }

    public string LogRawOutbound(string callerMethod, string content)
    {
        if (!_options.EnableRawApiLogs) return string.Empty;

        string traceId = GetTraceId();
        string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var fileName = $"{timestamp}_{callerMethod}_{traceId}";

        return LogRawToSubfolder(fileName, content, _options.RawApiSubfolder, traceId, "outbound", "response", GetCurrentStatusCode());
    }

    public string LogRawOutbound(string callerMethod)
    {
        if (!_options.EnableRawApiLogs) return string.Empty;

        var ctx = _httpContextAccessor.HttpContext;
        if (ctx == null) return string.Empty;

        var dump = BuildFullHttpResponseDump(ctx.Response);
        return LogRawOutbound(callerMethod, dump);
    }

    #endregion

    #region Private Helpers

    private string LogRawToSubfolder(string namePrefix, string content, string subfolder, string traceId, string direction, string type, int? statusCode)
    {
        try
        {
            var folder = Path.Combine(_options.LogDirectory, subfolder);
            Directory.CreateDirectory(folder);

            var fileName = namePrefix + ".txt";
            var fullPath = Path.Combine(folder, fileName);

            var traceStampedContent = $"SimpleMDG_TraceLogID: {traceId}\n\n" + content;
            File.WriteAllText(fullPath, traceStampedContent, Encoding.UTF8);

            Log($"[Trace: {traceId}] RAW log saved: {subfolder}\\{fileName}", LogLevel.DEBUG);

            var relativePath = Path.Combine(subfolder, fileName).Replace("\\", "/");
            WriteTraceMap(traceId, namePrefix, type, direction, statusCode, relativePath);

            return fileName;
        }
        catch (Exception ex)
        {
            Log($"Failed to write RAW log '{namePrefix}': {ex.Message}", LogLevel.WARNING);
            return string.Empty;
        }
    }

    private void WriteTraceMap(string traceId, string methodKey, string type, string direction, int? statusCode, string relativePath)
    {
        try
        {
            var ctx = _httpContextAccessor.HttpContext;
            var now = DateTime.UtcNow;

            // Increment the step counter for this trace ID
            int traceStep = 1;
            lock (TraceStepCounter)
            {
                if (TraceStepCounter.ContainsKey(traceId))
                    traceStep = ++TraceStepCounter[traceId];
                else
                    TraceStepCounter[traceId] = 1;
            }

            // Initialize trace timestamp for first request
            if (type == "request" && direction == "inbound" && traceStep == 1)
            {
                lock (TraceTimestamps)
                {
                    if (!TraceTimestamps.ContainsKey(traceId))
                    {
                        TraceTimestamps[traceId] = now;
                        TraceLastStepTimestamps[traceId] = now;
                    }
                }
            }

            // Calculate durations
            long? durationMs = null;
            long? relativeDurationMs = null;

            if (TraceTimestamps.ContainsKey(traceId))
            {
                var traceStart = TraceTimestamps[traceId];
                durationMs = (long)(now - traceStart).TotalMilliseconds;

                // Calculate relative duration from last step
                if (TraceLastStepTimestamps.ContainsKey(traceId))
                {
                    var lastStep = TraceLastStepTimestamps[traceId];
                    relativeDurationMs = (long)(now - lastStep).TotalMilliseconds;
                }

                // Update last step timestamp
                lock (TraceLastStepTimestamps)
                {
                    TraceLastStepTimestamps[traceId] = now;
                }
            }
            else
            {
                // For orphaned entries, initialize timing
                lock (TraceTimestamps)
                {
                    TraceTimestamps[traceId] = now;
                    TraceLastStepTimestamps[traceId] = now;
                }
                durationMs = 0;
                relativeDurationMs = 0;
            }

            // Determine controller and action names
            string controllerAction = "Unknown";
            if (ctx?.Request?.RouteValues != null &&
                ctx.Request.RouteValues.TryGetValue("controller", out var c) &&
                ctx.Request.RouteValues.TryGetValue("action", out var a))
            {
                controllerAction = $"{c}_{a}";
            }

            // Extract source from User-Agent
            string source = "Unknown";
            if (ctx?.Request?.Headers.TryGetValue("User-Agent", out var ua) == true)
            {
                var userAgent = ua.ToString().ToLower();
                if (userAgent.Contains("postman")) source = "Postman";
                else if (userAgent.Contains("mozilla")) source = "Browser";
                else if (userAgent.Contains("curl")) source = "Curl";
                else source = "Other";
            }

            var normalizedPath = relativePath.Replace("\\", "/");

            // Enhanced CSV line with relative duration
            var mapLine = string.Join(",",
                now.ToString("o"), // ISO 8601 timestamp
                traceStep,
                controllerAction,
                methodKey,
                type,
                statusCode?.ToString() ?? "N/A",
                direction,
                source,
                durationMs?.ToString() ?? "",
                relativeDurationMs?.ToString() ?? "", // New field
                normalizedPath
            );

            var mapFilePath = GetMapFilePath(traceId);
            File.AppendAllText(mapFilePath, mapLine + Environment.NewLine, Encoding.UTF8);

            Console.WriteLine($"[TRACE MAP] {mapLine}");
        }
        catch (Exception ex)
        {
            Log($"[Trace: {traceId}] Failed to write trace map: {ex.Message}", LogLevel.WARNING);
        }
    }

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

    private string FormatLine(string message, LogLevel level)
        => $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";

    private string GetLogFilePath()
    {
        var fn = _options.FileNamePattern.Replace("{date}", DateTime.Now.ToString("yyyy-MM-dd"));
        return Path.Combine(_options.LogDirectory, fn);
    }

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

    private string GetTraceId()
    {
        var ctx = _httpContextAccessor.HttpContext;

        if (ctx?.Request?.Headers != null &&
            ctx.Request.Headers.TryGetValue(_traceHeaderName, out var traceIdHeader) &&
            !string.IsNullOrWhiteSpace(traceIdHeader))
        {
            return traceIdHeader.ToString();
        }

        return $"NoTrace_{DateTime.UtcNow:yyyyMMddHHmmssfff}";
    }

    private string BuildFullHttpDump(HttpRequest req)
    {
        string body = string.Empty;

        try
        {
            if (req.Body.CanRead && req.ContentLength > 0)
            {
                req.EnableBuffering();
                req.Body.Position = 0;

                using var reader = new StreamReader(req.Body, Encoding.UTF8, leaveOpen: true);
                body = reader.ReadToEnd();

                req.Body.Position = 0;
            }
        }
        catch (Exception ex)
        {
            body = $"[[ERROR reading body: {ex.Message}]]";
        }

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

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

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

        var fallbackPath = Path.Combine(mapFolder, $"Map_{traceId}_999.txt");
        _traceMapCache[traceId] = fallbackPath;
        return fallbackPath;
    }

    #endregion
}

#endregion