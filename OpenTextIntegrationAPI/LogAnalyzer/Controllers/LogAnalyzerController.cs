using Microsoft.AspNetCore.Mvc;
using OpenTextIntegrationAPI.LogAnalyzer.Models;
using OpenTextIntegrationAPI.LogAnalyzer.Services;

namespace OpenTextIntegrationAPI.LogAnalyzer.Controllers
{
    [Route("loganalyzer")]
    public class LogAnalyzerController : Controller
    {
        private readonly string _logDirectory;
        private readonly IConfiguration _configuration;

        public LogAnalyzerController(IConfiguration configuration)
        {
            _configuration = configuration;
            _logDirectory = configuration.GetValue<string>("FileLogger:LogDirectory") ?? "logs";
        }

        [HttpGet("")]
        public IActionResult Index()
        {
            return View("~/LogAnalyzer/Views/LogAnalyzer/Index.cshtml");
        }

        [HttpGet("api/traces")]
        public async Task<IActionResult> GetTraces([FromQuery] SearchFilters filters)
        {
            try
            {
                var logAnalyzer = new LogAnalyzerService(_logDirectory);
                var traces = await logAnalyzer.GetTracesAsync(filters);
                return Json(traces.OrderByDescending(t => t.StartTime));
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("api/trace/{traceId}")]
        public async Task<IActionResult> GetTraceDetails(string traceId)
        {
            try
            {
                var logAnalyzer = new LogAnalyzerService(_logDirectory);
                var timeline = await logAnalyzer.GetTraceTimelineAsync(traceId);

                if (timeline == null)
                    return NotFound(new { error = "Trace not found" });

                return Json(timeline);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("api/file/{*filePath}")]
        public async Task<IActionResult> GetFileContent(string filePath)
        {
            try
            {
                // Decode the URL-encoded path
                var decodedPath = Uri.UnescapeDataString(filePath);

                // Log the incoming request for debugging
                Console.WriteLine($"[DEBUG] Original file path: {filePath}");
                Console.WriteLine($"[DEBUG] Decoded file path: {decodedPath}");

                var fullPath = Path.Combine(_logDirectory, decodedPath);
                Console.WriteLine($"[DEBUG] Full file path: {fullPath}");
                Console.WriteLine($"[DEBUG] File exists: {System.IO.File.Exists(fullPath)}");

                if (!System.IO.File.Exists(fullPath))
                {
                    // Try to list files in the directory to help debug
                    var directory = Path.GetDirectoryName(fullPath);
                    if (Directory.Exists(directory))
                    {
                        var filesInDir = Directory.GetFiles(directory, "*.txt").Take(5);
                        Console.WriteLine($"[DEBUG] Files in directory {directory}: {string.Join(", ", filesInDir.Select(Path.GetFileName))}");
                    }

                    return NotFound(new
                    {
                        error = "File not found",
                        originalPath = filePath,
                        decodedPath = decodedPath,
                        fullPath = fullPath,
                        directoryExists = Directory.Exists(directory),
                        logDirectory = _logDirectory
                    });
                }

                var content = await System.IO.File.ReadAllTextAsync(fullPath);
                return Json(new { content, fileName = Path.GetFileName(decodedPath) });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Exception: {ex.Message}");
                return BadRequest(new { error = ex.Message, filePath = filePath });
            }
        }

        [HttpGet("debug")]
        public IActionResult Debug()
        {
            var logDir = _logDirectory;
            var exists = Directory.Exists(logDir);
            var files = exists ? Directory.GetFiles(logDir, "*.log") : new string[0];
            var rawInbound = Path.Combine(logDir, "Raw", "Inbound");
            var rawOutbound = Path.Combine(logDir, "Raw", "Outbound");
            var rawMaps = Path.Combine(logDir, "Raw", "Maps");

            return Json(new
            {
                logDirectory = logDir,
                directoryExists = exists,
                logFiles = files.Select(Path.GetFileName),
                rawInboundExists = Directory.Exists(rawInbound),
                rawOutboundExists = Directory.Exists(rawOutbound),
                rawMapsExists = Directory.Exists(rawMaps),
                rawInboundFiles = Directory.Exists(rawInbound) ? Directory.GetFiles(rawInbound, "*.txt").Length : 0,
                rawOutboundFiles = Directory.Exists(rawOutbound) ? Directory.GetFiles(rawOutbound, "*.txt").Length : 0,
                rawMapsFiles = Directory.Exists(rawMaps) ? Directory.GetFiles(rawMaps, "*.txt").Length : 0
            });
        }

        [HttpGet("api/filters")]
        public async Task<IActionResult> GetFilterOptions()
        {
            try
            {
                var logAnalyzer = new LogAnalyzerService(_logDirectory);
                var options = await logAnalyzer.GetFilterOptionsAsync();
                return Json(options);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("api/trace/{traceId}/files")]
        public async Task<IActionResult> GetTraceFiles(string traceId)
        {
            try
            {
                var logAnalyzer = new LogAnalyzerService(_logDirectory);
                var timeline = await logAnalyzer.GetTraceTimelineAsync(traceId);

                if (timeline == null)
                    return NotFound(new { error = "Trace not found" });

                var files = new List<object>();

                foreach (var entry in timeline.Entries)
                {
                    if (!string.IsNullOrEmpty(entry.RequestFile))
                    {
                        files.Add(new
                        {
                            type = "request",
                            path = entry.RequestFile,
                            timestamp = entry.Timestamp,
                            operation = entry.Operation,
                            stepNumber = entry.StepNumber,
                            durationMs = entry.DurationMs
                        });
                    }

                    if (!string.IsNullOrEmpty(entry.ResponseFile))
                    {
                        files.Add(new
                        {
                            type = "response",
                            path = entry.ResponseFile,
                            timestamp = entry.Timestamp,
                            operation = entry.Operation,
                            stepNumber = entry.StepNumber,
                            durationMs = entry.DurationMs,
                            statusCode = entry.StatusCode
                        });
                    }

                    if (!string.IsNullOrEmpty(entry.MapFile))
                    {
                        files.Add(new
                        {
                            type = "map",
                            path = entry.MapFile,
                            timestamp = entry.Timestamp
                        });
                    }
                }

                return Json(files.OrderBy(f => {
                    if (f.GetType().GetProperty("timestamp")?.GetValue(f) is DateTime timestamp)
                        return timestamp;
                    return DateTime.MinValue;
                }));
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("api/trace/{traceId}/stats")]
        public async Task<IActionResult> GetTraceStats(string traceId)
        {
            try
            {
                var logAnalyzer = new LogAnalyzerService(_logDirectory);
                var timeline = await logAnalyzer.GetTraceTimelineAsync(traceId);

                if (timeline == null)
                    return NotFound(new { error = "Trace not found" });

                var stats = new
                {
                    traceId = timeline.TraceId,
                    totalDurationMs = timeline.TotalDurationMs,
                    totalSteps = timeline.TotalSteps,
                    avgStepDurationMs = timeline.AvgStepDurationMs,
                    hasErrors = timeline.HasErrors,
                    boType = timeline.BoType,
                    boId = timeline.BoId,
                    operation = timeline.Operation,
                    startTime = timeline.StartTime,
                    endTime = timeline.EndTime,
                    stepBreakdown = timeline.Entries.Where(e => e.DurationMs.HasValue).Select(e => new
                    {
                        step = e.StepNumber,
                        operation = e.Operation,
                        type = e.Type.ToString(),
                        durationMs = e.DurationMs,
                        relativeDurationMs = e.RelativeDurationMs,
                        statusCode = e.StatusCode,
                        direction = e.Direction
                    }).OrderBy(s => s.step)
                };

                return Json(stats);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}