using Microsoft.Extensions.Logging.Abstractions;
using OpenTextIntegrationAPI.LogAnalyzer.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace OpenTextIntegrationAPI.LogAnalyzer.Services
{
    public class LogAnalyzerService
    {
        private readonly string _logDirectory;
        private static readonly Regex LogLineRegex = new(
            @"^(?<timestamp>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}) \[(?<level>\w+)\] (?<message>.*)$",
            RegexOptions.Compiled);

        private static readonly Regex TraceIdRegex = new(
            @"(?<traceid>[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex BusinessObjectRegex = new(
            @"(?<botype>BUS\d{7})_(?<boid>\d{6})",
            RegexOptions.Compiled);

        public LogAnalyzerService(string logDirectory)
        {
            _logDirectory = logDirectory;
        }

        public async Task<List<TraceTimeline>> GetTracesAsync(SearchFilters filters)
        {
            var allEntries = await ParseLogFilesAsync();

            // Filter by date range
            if (filters.From.HasValue)
                allEntries = allEntries.Where(e => e.Timestamp >= filters.From.Value).ToList();
            if (filters.To.HasValue)
                allEntries = allEntries.Where(e => e.Timestamp <= filters.To.Value).ToList();

            // Group by trace ID
            var traceGroups = allEntries
                .Where(e => !string.IsNullOrEmpty(e.TraceId))
                .GroupBy(e => e.TraceId)
                .ToList();

            var timelines = new List<TraceTimeline>();

            foreach (var group in traceGroups)
            {
                var entries = group.OrderBy(e => e.Timestamp).ToList();
                var timeline = new TraceTimeline
                {
                    TraceId = group.Key!,
                    Entries = entries,
                    StartTime = entries.First().Timestamp,
                    EndTime = entries.Last().Timestamp,
                    BoType = entries.FirstOrDefault(e => !string.IsNullOrEmpty(e.BoType))?.BoType,
                    BoId = entries.FirstOrDefault(e => !string.IsNullOrEmpty(e.BoId))?.BoId,
                    Operation = entries.FirstOrDefault(e => !string.IsNullOrEmpty(e.Operation))?.Operation
                };

                // Apply filters
                if (!PassesFilters(timeline, filters))
                    continue;

                timelines.Add(timeline);
            }

            return timelines;
        }

        public async Task<TraceTimeline?> GetTraceTimelineAsync(string traceId)
        {
            var allEntries = await ParseLogFilesAsync();
            var traceEntries = allEntries
                .Where(e => e.TraceId == traceId)
                .OrderBy(e => e.Timestamp)
                .ToList();

            if (!traceEntries.Any())
                return null;

            return new TraceTimeline
            {
                TraceId = traceId,
                Entries = traceEntries,
                StartTime = traceEntries.First().Timestamp,
                EndTime = traceEntries.Last().Timestamp,
                BoType = traceEntries.FirstOrDefault(e => !string.IsNullOrEmpty(e.BoType))?.BoType,
                BoId = traceEntries.FirstOrDefault(e => !string.IsNullOrEmpty(e.BoId))?.BoId,
                Operation = traceEntries.FirstOrDefault(e => !string.IsNullOrEmpty(e.Operation))?.Operation
            };
        }

        public async Task<object> GetFilterOptionsAsync()
        {
            var allEntries = await ParseLogFilesAsync();

            var boTypes = allEntries
                .Where(e => !string.IsNullOrEmpty(e.BoType))
                .Select(e => e.BoType!)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            var operations = allEntries
                .Where(e => !string.IsNullOrEmpty(e.Operation))
                .Select(e => e.Operation!)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            return new
            {
                boTypes,
                operations
            };
        }

        private bool PassesFilters(TraceTimeline timeline, SearchFilters filters)
        {
            // General search
            if (!string.IsNullOrEmpty(filters.Search))
            {
                var searchLower = filters.Search.ToLowerInvariant();
                if (!timeline.TraceId.Contains(searchLower, StringComparison.OrdinalIgnoreCase) &&
                    !timeline.BoType?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) == true &&
                    !timeline.BoId?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) == true &&
                    !timeline.Operation?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) == true &&
                    !timeline.Entries.Any(e => e.Message.Contains(searchLower, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }
            }

            // BO Type filter
            if (!string.IsNullOrEmpty(filters.BoType) && timeline.BoType != filters.BoType)
                return false;

            // BO ID filter
            if (!string.IsNullOrEmpty(filters.BoId) && timeline.BoId != filters.BoId)
                return false;

            // Operation filter
            if (!string.IsNullOrEmpty(filters.Operation) && timeline.Operation != filters.Operation)
                return false;

            // Has errors filter
            if (filters.HasErrors.HasValue && timeline.HasErrors != filters.HasErrors.Value)
                return false;

            return true;
        }

        private async Task<List<LogEntry>> ParseLogFilesAsync()
        {
            var entries = new List<LogEntry>();

            if (!Directory.Exists(_logDirectory))
                return entries;

            // Parse main log files
            var logFiles = Directory.GetFiles(_logDirectory, "*.log");
            foreach (var file in logFiles)
            {
                var fileEntries = await ParseMainLogFileAsync(file);
                entries.AddRange(fileEntries);
            }

            // Parse raw files FIRST to create trace entries
            await ParseRawFilesAsync(entries);

            // Populate Business Objects from file names if not found in main logs
            PopulateBusinessObjectsFromFiles(entries);

            return entries;
        }

        private void PopulateBusinessObjectsFromFiles(List<LogEntry> entries)
        {
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.BoType) && !string.IsNullOrEmpty(entry.RequestFile))
                {
                    // Buscar BO en el nombre del archivo: v1_MasterData_BUS1001006_403669
                    var fileName = Path.GetFileName(entry.RequestFile);
                    var boMatch = System.Text.RegularExpressions.Regex.Match(fileName, @"BUS(\d{7})_(\d{6})");
                    if (boMatch.Success)
                    {
                        entry.BoType = "BUS" + boMatch.Groups[1].Value;
                        entry.BoId = boMatch.Groups[2].Value;
                    }

                    // Determinar operación del nombre del archivo
                    if (fileName.Contains("masterdata", StringComparison.OrdinalIgnoreCase))
                        entry.Operation = "MasterData";
                    else if (fileName.Contains("auth", StringComparison.OrdinalIgnoreCase))
                        entry.Operation = "Authentication";
                    else if (fileName.Contains("classification", StringComparison.OrdinalIgnoreCase))
                        entry.Operation = "Classification";
                    else if (fileName.Contains("workspace", StringComparison.OrdinalIgnoreCase))
                        entry.Operation = "BusinessWorkspace";
                    else if (fileName.Contains("nodes", StringComparison.OrdinalIgnoreCase))
                        entry.Operation = "Node";
                }
            }
        }

        private async Task<List<LogEntry>> ParseMainLogFileAsync(string filePath)
        {
            var entries = new List<LogEntry>();
            var lines = await System.IO.File.ReadAllLinesAsync(filePath);

            foreach (var line in lines)
            {
                var match = LogLineRegex.Match(line);
                if (!match.Success) continue;

                var entry = new LogEntry
                {
                    Timestamp = DateTime.ParseExact(match.Groups["timestamp"].Value,
                        "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
                    Level = match.Groups["level"].Value,
                    Message = match.Groups["message"].Value
                };

                // Extract trace ID - look for both formats and your header
                var traceMatch = TraceIdRegex.Match(entry.Message);
                if (traceMatch.Success)
                {
                    // Normalize underscores to hyphens for consistency
                    entry.TraceId = traceMatch.Groups["traceid"].Value.Replace("_", "-");
                }
                else
                {
                    // Try to find trace ID in SimpleMDG_TraceLogID format
                    var headerMatch = System.Text.RegularExpressions.Regex.Match(entry.Message,
                        @"SimpleMDG.TraceLogID[:\s]*([a-f0-9]{8}[-_][a-f0-9]{4}[-_][a-f0-9]{4}[-_][a-f0-9]{4}[-_][a-f0-9]{12})",
                        RegexOptions.IgnoreCase);
                    if (headerMatch.Success)
                    {
                        entry.TraceId = headerMatch.Groups[1].Value.Replace("_", "-");
                    }
                }

                // Extract business object parts
                var boMatch = BusinessObjectRegex.Match(entry.Message);
                if (boMatch.Success)
                {
                    entry.BoType = boMatch.Groups["botype"].Value;
                    entry.BoId = boMatch.Groups["boid"].Value;
                }

                // Determine operation and type
                AnalyzeLogEntry(entry);

                entries.Add(entry);
            }

            return entries;
        }

        private async Task ParseRawFilesAsync(List<LogEntry> entries)
        {
            var allTraceIds = new HashSet<string>();

            // Primero recopilar todos los trace IDs únicos de TODOS los archivos
            var rawDirs = new[] {
        ("Raw\\Inbound", "Inbound"),     // Llamadas a tu API
        ("Raw\\Outbound", "Outbound")    // Llamadas desde tu API a OpenText
    };

            foreach (var (rawDir, directionType) in rawDirs)
            {
                var fullPath = Path.Combine(_logDirectory, rawDir);
                if (!Directory.Exists(fullPath)) continue;

                var files = Directory.GetFiles(fullPath, "*.txt");
                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    string traceId;

                    if (fileName.Contains("NoTrace"))
                    {
                        // Para archivos NoTrace, usar el timestamp completo como trace ID único
                        var timestampMatch = System.Text.RegularExpressions.Regex.Match(fileName, @"^(\d{14})_");
                        if (timestampMatch.Success)
                        {
                            var timestamp = timestampMatch.Groups[1].Value;
                            // Buscar el identificador único al final del nombre
                            var parts = fileName.Replace(".txt", "").Split('_');
                            if (parts.Length > 0)
                            {
                                var lastPart = parts[parts.Length - 1];
                                traceId = $"NoTrace_{timestamp}_{lastPart}";
                                allTraceIds.Add(traceId);
                            }
                        }
                    }
                    else
                    {
                        // Para archivos con trace ID real
                        var parts = fileName.Replace(".txt", "").Split('_');
                        if (parts.Length > 0)
                        {
                            var lastPart = parts[parts.Length - 1];
                            // Verificar si es un trace ID válido (20+ caracteres alfanuméricos)
                            if (lastPart.Length >= 20 && System.Text.RegularExpressions.Regex.IsMatch(lastPart, @"^[A-Za-z0-9]+$"))
                            {
                                allTraceIds.Add(lastPart);
                            }
                        }
                    }
                }
            }

            // Ahora crear/actualizar entradas para cada trace ID encontrado
            foreach (var traceId in allTraceIds)
            {
                var traceEntries = new List<LogEntry>();

                // Buscar TODOS los archivos para este trace ID
                foreach (var (rawDir, directionType) in rawDirs)
                {
                    var fullPath = Path.Combine(_logDirectory, rawDir);
                    if (!Directory.Exists(fullPath)) continue;

                    List<string> files;

                    if (traceId.StartsWith("NoTrace_"))
                    {
                        // Para NoTrace, buscar por el patrón completo
                        var parts = traceId.Split('_');
                        if (parts.Length >= 3)
                        {
                            var timestamp = parts[1];
                            var uniqueId = parts[2];
                            files = Directory.GetFiles(fullPath, $"{timestamp}_*{uniqueId}.txt").ToList();
                        }
                        else
                        {
                            files = new List<string>();
                        }
                    }
                    else
                    {
                        // Para trace IDs reales
                        files = Directory.GetFiles(fullPath, $"*{traceId}.txt").ToList();
                    }

                    foreach (var file in files)
                    {
                        var fileName = Path.GetFileName(file);
                        var fileInfo = new FileInfo(file);

                        // Extraer timestamp del nombre del archivo
                        var timestampMatch = System.Text.RegularExpressions.Regex.Match(fileName, @"^(\d{14})_");
                        DateTime timestamp = fileInfo.LastWriteTime; // fallback
                        if (timestampMatch.Success && DateTime.TryParseExact(timestampMatch.Groups[1].Value,
                            "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTime))
                        {
                            timestamp = parsedTime;
                        }

                        // Determinar tipo de operación y entry
                        LogEntryType entryType = LogEntryType.General;
                        string operation = "Unknown";
                        string message = $"Trace found in {directionType} files: {fileName}";

                        if (fileName.Contains("_request_"))
                        {
                            entryType = LogEntryType.Request;
                            message = $"{directionType} Request: {ExtractOperationFromFileName(fileName)}";
                        }
                        else if (fileName.Contains("_response_"))
                        {
                            entryType = LogEntryType.Response;
                            message = $"{directionType} Response: {ExtractOperationFromFileName(fileName)}";
                        }

                        // Determinar operación principal
                        operation = ExtractOperationFromFileName(fileName);

                        // Crear entrada de log
                        var logEntry = new LogEntry
                        {
                            TraceId = traceId,
                            Timestamp = timestamp,
                            Level = "INFO",
                            Message = message,
                            Type = entryType,
                            Operation = operation
                        };

                        // Asignar archivos según el tipo
                        if (fileName.Contains("_request_"))
                            logEntry.RequestFile = Path.Combine(rawDir, fileName);
                        else if (fileName.Contains("_response_"))
                            logEntry.ResponseFile = Path.Combine(rawDir, fileName);

                        // Extraer Business Object del nombre del archivo
                        var boMatch = System.Text.RegularExpressions.Regex.Match(fileName, @"BUS(\d{7})_(\d{6})");
                        if (boMatch.Success)
                        {
                            logEntry.BoType = "BUS" + boMatch.Groups[1].Value;
                            logEntry.BoId = boMatch.Groups[2].Value;
                        }

                        traceEntries.Add(logEntry);
                    }
                }

                // Agregar todas las entradas del trace
                entries.AddRange(traceEntries);
            }

            // Buscar Map files
            var mapsPath = Path.Combine(_logDirectory, "Raw", "Maps");
            if (Directory.Exists(mapsPath))
            {
                var mapFiles = Directory.GetFiles(mapsPath, "Map_*.txt");
                foreach (var file in mapFiles)
                {
                    var fileName = Path.GetFileName(file);
                    if (fileName.StartsWith("Map_"))
                    {
                        string correspondingTraceId;

                        if (fileName.Contains("NoTrace"))
                        {
                            // Para archivos NoTrace, extraer el identificador único
                            var noTraceMatch = System.Text.RegularExpressions.Regex.Match(fileName, @"Map_NoTrace_(\d{14})\.txt");
                            if (noTraceMatch.Success)
                            {
                                var timestamp = noTraceMatch.Groups[1].Value;
                                correspondingTraceId = $"NoTrace_{timestamp}_{timestamp}";

                                // Buscar entrada existente para este trace
                                var existingEntries = entries.Where(e => e.TraceId.Contains($"NoTrace_{timestamp}")).ToList();
                                foreach (var entry in existingEntries)
                                {
                                    entry.MapFile = Path.Combine("Raw", "Maps", fileName);
                                }
                            }
                        }
                        else
                        {
                            // Para trace IDs reales
                            var traceId = fileName.Replace("Map_", "").Replace(".txt", "");
                            if (traceId.Length >= 20)
                            {
                                // Buscar entrada existente para este trace
                                var existingEntries = entries.Where(e => e.TraceId == traceId).ToList();
                                foreach (var entry in existingEntries)
                                {
                                    entry.MapFile = Path.Combine("Raw", "Maps", fileName);
                                }
                            }
                        }
                    }
                }
            }
        }

        private string ExtractOperationFromFileName(string fileName)
        {
            var lowerFileName = fileName.ToLowerInvariant();

            if (lowerFileName.Contains("masterdata")) return "MasterData";
            if (lowerFileName.Contains("auth")) return "Authentication";
            if (lowerFileName.Contains("classification")) return "Classification";
            if (lowerFileName.Contains("workspace")) return "BusinessWorkspace";
            if (lowerFileName.Contains("child_node") || lowerFileName.Contains("nodes")) return "Node";
            if (lowerFileName.Contains("member")) return "Member";
            if (lowerFileName.Contains("search")) return "Search";

            return "Unknown";
        }

        private static void AnalyzeLogEntry(LogEntry entry)
        {
            var message = entry.Message.ToLowerInvariant();

            if (message.Contains("request") || message.Contains("calling"))
            {
                entry.Type = LogEntryType.Request;
            }
            else if (message.Contains("response") || message.Contains("received"))
            {
                entry.Type = LogEntryType.Response;
            }
            else if (message.Contains("error") || message.Contains("exception") || entry.Level == "ERROR")
            {
                entry.Type = LogEntryType.Error;
            }
            else if (message.Contains("raw log saved"))
            {
                entry.Type = LogEntryType.RawLog;
            }
            else if (message.Contains("otcsticket") || message.Contains("authentication"))
            {
                entry.Type = LogEntryType.Authentication;
            }
            else
            {
                entry.Type = LogEntryType.General;
            }

            // Determine operation
            if (message.Contains("masterdata"))
                entry.Operation = "MasterData";
            else if (message.Contains("classification"))
                entry.Operation = "Classification";
            else if (message.Contains("workspace"))
                entry.Operation = "BusinessWorkspace";
            else if (message.Contains("node"))
                entry.Operation = "Node";
            else if (message.Contains("authentication") || message.Contains("otcsticket"))
                entry.Operation = "Authentication";
        }
    }
}