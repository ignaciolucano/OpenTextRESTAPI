using Microsoft.Extensions.Options;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// Enum representing supported logging levels.
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
/// Configuration options for the file logger, loaded from appsettings.json.
/// </summary>
public class FileLoggerOptions
{
    public string LogDirectory { get; set; } = "logs";
    public string LogLevel { get; set; } = "INFO";
    public int MaxFileSizeMB { get; set; } = 5;
    public string FileNamePattern { get; set; } = "application_{date}.log"; // {date} will be replaced by yyyy-MM-dd
    public bool EnableRawInboundLogs { get; set; } = false;
    public bool EnableRawApiLogs { get; set; } = false;
    public string RawApiSubfolder { get; set; } = "Raw\\OpenText";
    public string RawInboundSubfolder { get; set; } = "Raw\\Inbound";
}

/// <summary>
/// Interface for application-wide logging service.
/// </summary>
public interface ILogService
{
    void Log(string message, LogLevel level);
    Task LogAsync(string message, LogLevel level);
    void LogException(Exception ex, LogLevel level = LogLevel.ERROR);
    string LogRawInbound(string namePrefix, string content);
    string LogRawApi(string namePrefix, string content);
}

/// <summary>
/// File-based implementation of the logging service.
/// Supports log level filtering, file rotation, and optional raw logging.
/// </summary>
public class FileLoggerService : ILogService
{
    private readonly FileLoggerOptions _options;
    private readonly LogLevel _minLevel;
    private readonly object _lock = new();

    public FileLoggerService(IOptions<FileLoggerOptions> options)
    {
        _options = options.Value;
        Directory.CreateDirectory(_options.LogDirectory);

        if (!Enum.TryParse(_options.LogLevel.ToUpper(), out _minLevel))
        {
            _minLevel = LogLevel.INFO;
        }
    }

    /// <summary>
    /// Logs a message synchronously if it meets the minimum configured log level.
    /// </summary>
    public void Log(string message, LogLevel level)
    {
        if (level < _minLevel) return;

        lock (_lock)
        {
            string path = GetLogFilePath();
            RotateIfNeeded(path);
            File.AppendAllText(path, FormatLine(message, level) + Environment.NewLine, Encoding.UTF8);
        }
    }

    /// <summary>
    /// Logs a message asynchronously if it meets the minimum configured log level.
    /// </summary>
    public async Task LogAsync(string message, LogLevel level)
    {
        if (level < _minLevel) return;

        string path = GetLogFilePath();
        RotateIfNeeded(path);
        var logEntry = FormatLine(message, level);

        using var writer = File.AppendText(path);
        await writer.WriteLineAsync(logEntry);
    }

    /// <summary>
    /// Logs an exception with full stack trace and optional inner exception.
    /// </summary>
    public void LogException(Exception ex, LogLevel level = LogLevel.ERROR)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Exception: {ex.Message}");
        sb.AppendLine($"StackTrace: {ex.StackTrace}");

        if (ex.InnerException != null)
        {
            sb.AppendLine($"Inner Exception: {ex.InnerException.Message}");
            sb.AppendLine($"Inner StackTrace: {ex.InnerException.StackTrace}");
        }

        Log(sb.ToString(), level);
    }

    /// <summary>
    /// Logs raw inbound data (requests to your API) if enabled.
    /// </summary>
    public string LogRawInbound(string namePrefix, string content)
    {
        if (!_options.EnableRawInboundLogs) return string.Empty;
        return LogRawToSubfolder(namePrefix, content, _options.RawInboundSubfolder);
    }

    /// <summary>
    /// Logs raw API data (calls to OpenText API) if enabled.
    /// </summary>
    public string LogRawApi(string namePrefix, string content)
    {
        if (!_options.EnableRawApiLogs) return string.Empty;
        return LogRawToSubfolder(namePrefix, content, _options.RawApiSubfolder);
    }

    private string LogRawToSubfolder(string namePrefix, string content, string subfolder)
    {
        try
        {
            string uniqueId = $"{DateTime.Now:yyyyMMdd_HHmmssfff}_{Guid.NewGuid():N}";
            string folder = Path.Combine(_options.LogDirectory, subfolder);
            Directory.CreateDirectory(folder);

            string fileName = $"{namePrefix}_{uniqueId}.txt";
            File.WriteAllText(Path.Combine(folder, fileName), content, Encoding.UTF8);

            Log($"RAW log saved: {subfolder}\\{fileName}", LogLevel.DEBUG);
            return uniqueId;
        }
        catch (Exception ex)
        {
            Log($"Failed to write RAW log for {namePrefix}: {ex.Message}", LogLevel.WARNING);
            return string.Empty;
        }
    }

    /// <summary>
    /// Formats a log line with timestamp and level.
    /// </summary>
    private string FormatLine(string message, LogLevel level)
    {
        return $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
    }

    /// <summary>
    /// Returns the full log file path for the current date.
    /// </summary>
    private string GetLogFilePath()
    {
        string fileName = _options.FileNamePattern.Replace("{date}", DateTime.Now.ToString("yyyy-MM-dd"));
        return Path.Combine(_options.LogDirectory, fileName);
    }

    /// <summary>
    /// Rotates the current log file if it exceeds the maximum file size.
    /// </summary>
    private void RotateIfNeeded(string path)
    {
        if (!File.Exists(path)) return;

        var fileInfo = new FileInfo(path);
        long maxBytes = _options.MaxFileSizeMB * 1024 * 1024;

        if (fileInfo.Length >= maxBytes)
        {
            string oldPath = path + ".old";
            if (File.Exists(oldPath)) File.Delete(oldPath);
            File.Move(path, oldPath);
        }
    }
}