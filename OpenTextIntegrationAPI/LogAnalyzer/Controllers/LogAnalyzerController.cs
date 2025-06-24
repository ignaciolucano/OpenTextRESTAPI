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
                var fullPath = Path.Combine(_logDirectory, filePath);

                if (!System.IO.File.Exists(fullPath))
                    return NotFound(new { error = "File not found" });

                var content = await System.IO.File.ReadAllTextAsync(fullPath);
                return Json(new { content, fileName = Path.GetFileName(filePath) });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
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
    }
}