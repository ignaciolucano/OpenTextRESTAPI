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
            Console.WriteLine($"[DEBUG] Starting GetTracesAsync with filters: {System.Text.Json.JsonSerializer.Serialize(filters)}");

            try
            {
                var allEntries = await ParseLogFilesAsync();
                Console.WriteLine($"[DEBUG] Parsed {allEntries.Count} total log entries");

                // Filter by date range FIRST (most restrictive)
                if (filters.From.HasValue)
                {
                    allEntries = allEntries.Where(e => e.Timestamp >= filters.From.Value).ToList();
                    Console.WriteLine($"[DEBUG] After 'From' filter: {allEntries.Count} entries");
                }
                if (filters.To.HasValue)
                {
                    allEntries = allEntries.Where(e => e.Timestamp <= filters.To.Value).ToList();
                    Console.WriteLine($"[DEBUG] After 'To' filter: {allEntries.Count} entries");
                }

                // Group by trace ID
                var traceGroups = allEntries
                    .Where(e => !string.IsNullOrEmpty(e.TraceId))
                    .GroupBy(e => e.TraceId)
                    .ToList();

                Console.WriteLine($"[DEBUG] Found {traceGroups.Count} unique trace groups");

                var timelines = new List<TraceTimeline>();

                foreach (var group in traceGroups.Take(100)) // Limit to first 100 traces for performance
                {
                    var entries = group.OrderBy(e => e.Timestamp).ThenBy(e => e.StepNumber).ToList();

                    // Skip traces that are entirely from log analyzer
                    if (entries.All(e => ShouldExcludeBasedOnContent(e.Message)))
                        continue;

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

                Console.WriteLine($"[DEBUG] Returning {timelines.Count} filtered timelines");
                return timelines;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Exception in GetTracesAsync: {ex}");
                throw;
            }
        }

        public async Task<TraceTimeline?> GetTraceTimelineAsync(string traceId)
        {
            var allEntries = await ParseLogFilesAsync();
            var traceEntries = allEntries
                .Where(e => e.TraceId == traceId)
                .OrderBy(e => e.Timestamp)
                .ThenBy(e => e.StepNumber)
                .ToList();

            if (!traceEntries.Any())
                return null;

            // FORCE assignment of files based on actual file existence
            await ForceAssignFilesToEntries(traceEntries, traceId);

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

        private async Task ForceAssignFilesToEntries(List<LogEntry> entries, string traceId)
        {
            // Look for actual files in the Raw directories that match this trace ID
            var rawDirs = new[] {
                Path.Combine(_logDirectory, "Raw", "Inbound"),
                Path.Combine(_logDirectory, "Raw", "Outbound")
            };

            var allFiles = new List<(string filePath, string relativePath, bool isRequest)>();

            foreach (var dir in rawDirs)
            {
                if (!Directory.Exists(dir)) continue;

                var files = Directory.GetFiles(dir, "*.txt")
                    .Where(f => Path.GetFileName(f).Contains(traceId, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    var isRequest = fileName.Contains("request", StringComparison.OrdinalIgnoreCase);
                    var relativePath = Path.GetRelativePath(_logDirectory, file).Replace("\\", "/");

                    allFiles.Add((file, relativePath, isRequest));
                    Console.WriteLine($"[DEBUG] Found file for trace {traceId}: {relativePath}, isRequest: {isRequest}");
                }
            }

            // Now assign files to entries based on type
            foreach (var entry in entries)
            {
                if (entry.Type == LogEntryType.Request)
                {
                    var requestFile = allFiles.FirstOrDefault(f => f.isRequest);
                    if (requestFile.relativePath != null)
                    {
                        entry.RequestFile = requestFile.relativePath;
                        Console.WriteLine($"[DEBUG] FORCED assignment: RequestFile = {requestFile.relativePath} for entry type {entry.Type}");
                    }
                }
                else if (entry.Type == LogEntryType.Response)
                {
                    var responseFile = allFiles.FirstOrDefault(f => !f.isRequest);
                    if (responseFile.relativePath != null)
                    {
                        entry.ResponseFile = responseFile.relativePath;
                        Console.WriteLine($"[DEBUG] FORCED assignment: ResponseFile = {responseFile.relativePath} for entry type {entry.Type}");
                    }
                }
            }
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

            var directions = allEntries
                .Where(e => !string.IsNullOrEmpty(e.Direction))
                .Select(e => e.Direction!)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            return new
            {
                boTypes,
                operations,
                directions
            };
        }

        private bool PassesFilters(TraceTimeline timeline, SearchFilters filters)
        {
            // General search - be smarter about what we're searching
            if (!string.IsNullOrEmpty(filters.Search))
            {
                var searchLower = filters.Search.ToLowerInvariant();

                // If it looks like a trace ID (long alphanumeric string), search only trace ID
                if (searchLower.Length >= 15 && System.Text.RegularExpressions.Regex.IsMatch(searchLower, @"^[a-z0-9_-]+$"))
                {
                    if (!timeline.TraceId.Contains(searchLower, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                // If it looks like a BO type (BUS followed by numbers)
                else if (System.Text.RegularExpressions.Regex.IsMatch(searchLower, @"^bus\d+$"))
                {
                    if (timeline.BoType?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) != true)
                        return false;
                }
                // If it's all numbers, search BO ID
                else if (System.Text.RegularExpressions.Regex.IsMatch(searchLower, @"^\d+$"))
                {
                    if (timeline.BoId?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) != true)
                        return false;
                }
                // Otherwise, search across all fields (original behavior)
                else
                {
                    if (!timeline.TraceId.Contains(searchLower, StringComparison.OrdinalIgnoreCase) &&
                        !timeline.BoType?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) == true &&
                        !timeline.BoId?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) == true &&
                        !timeline.Operation?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) == true &&
                        !timeline.Entries.Any(e => e.Message.Contains(searchLower, StringComparison.OrdinalIgnoreCase)))
                    {
                        return false;
                    }
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

            // Duration filters
            if (filters.MinDurationMs.HasValue && timeline.TotalDurationMs < filters.MinDurationMs.Value)
                return false;

            if (filters.MaxDurationMs.HasValue && timeline.TotalDurationMs > filters.MaxDurationMs.Value)
                return false;

            // Direction filter
            if (!string.IsNullOrEmpty(filters.Direction) &&
                !timeline.Entries.Any(e => e.Direction == filters.Direction))
                return false;

            return true;
        }

        private async Task<List<LogEntry>> ParseLogFilesAsync()
        {
            var entries = new List<LogEntry>();

            if (!Directory.Exists(_logDirectory))
            {
                Console.WriteLine($"[DEBUG] Log directory does not exist: {_logDirectory}");
                return entries;
            }

            // Only process log files from the last 7 days by default for performance
            var cutoffDate = DateTime.Now.AddDays(-7);
            Console.WriteLine($"[DEBUG] Processing files newer than: {cutoffDate}");

            // Parse main log files
            var logFiles = Directory.GetFiles(_logDirectory, "*.log")
                .Where(f => File.GetLastWriteTime(f) >= cutoffDate)
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .Take(10) // Limit to 10 most recent log files
                .ToArray();

            Console.WriteLine($"[DEBUG] Found {logFiles.Length} recent log files to process");

            foreach (var file in logFiles)
            {
                Console.WriteLine($"[DEBUG] Processing log file: {Path.GetFileName(file)}");
                var fileEntries = await ParseMainLogFileAsync(file);
                entries.AddRange(fileEntries);
            }

            Console.WriteLine($"[DEBUG] Parsed {entries.Count} entries from main log files");

            // Parse Map files and Raw files together for complete timeline
            await ParseMapAndRawFilesAsync(entries);

            Console.WriteLine($"[DEBUG] Total entries after parsing raw files: {entries.Count}");
            return entries;
        }

        private async Task<List<LogEntry>> ParseMainLogFileAsync(string filePath)
        {
            var entries = new List<LogEntry>();
            var lines = await System.IO.File.ReadAllLinesAsync(filePath);

            foreach (var line in lines)
            {
                var match = LogLineRegex.Match(line);
                if (!match.Success) continue;

                var message = match.Groups["message"].Value;

                // Skip log analyzer entries
                if (ShouldExcludeLogEntry(message))
                    continue;

                var entry = new LogEntry
                {
                    Timestamp = DateTime.ParseExact(match.Groups["timestamp"].Value,
                        "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
                    Level = match.Groups["level"].Value,
                    Message = message
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

        private bool ShouldExcludeLogEntry(string message)
        {
            var lowerMessage = message.ToLowerInvariant();

            // Exclude ALL loganalyzer related logs - we don't want any logs from the analyzer tool
            if (lowerMessage.Contains("loganalyzer"))
                return true;

            // Exclude specific API paths that are internal
            if (lowerMessage.Contains("api/traces") ||
                lowerMessage.Contains("api/filters") ||
                lowerMessage.Contains("api/file") ||
                lowerMessage.Contains("/debug") ||
                lowerMessage.Contains("/config"))
                return true;

            // Exclude favicon and other browser requests
            if (lowerMessage.Contains("favicon.ico"))
                return true;

            // Exclude any RAW log saved messages that are for loganalyzer
            if (lowerMessage.Contains("raw log saved") && lowerMessage.Contains("loganalyzer"))
                return true;

            return false;
        }

        private async Task ParseMapAndRawFilesAsync(List<LogEntry> entries)
        {
            var mapEntries = await ParseMapFilesAsync();
            var traceToMapEntries = mapEntries.GroupBy(m => ExtractTraceIdFromMapPath(m.RelativePath))
                                             .ToDictionary(g => g.Key, g => g.ToList());

            // Process each trace ID found in map files
            foreach (var traceId in traceToMapEntries.Keys)
            {
                if (string.IsNullOrEmpty(traceId)) continue;

                var traceMapEntries = traceToMapEntries[traceId];
                var traceLogEntries = new List<LogEntry>();

                foreach (var mapEntry in traceMapEntries.OrderBy(m => m.StepNumber))
                {
                    // Create log entry from map entry
                    var logEntry = new LogEntry
                    {
                        TraceId = traceId,
                        Timestamp = mapEntry.Timestamp,
                        Level = "INFO",
                        StepNumber = mapEntry.StepNumber,
                        DurationMs = mapEntry.DurationMs,
                        RelativeDurationMs = mapEntry.RelativeDurationMs,
                        StatusCode = mapEntry.StatusCode,
                        Direction = mapEntry.Direction,
                        Source = mapEntry.Source,
                        ControllerAction = mapEntry.ControllerAction,
                        Operation = ExtractOperationFromMethodKey(mapEntry.MethodKey),
                        Type = mapEntry.Type.ToLowerInvariant() == "request" ? LogEntryType.Request : LogEntryType.Response
                    };

                    // Set message based on direction and type
                    logEntry.Message = $"{mapEntry.Direction} {mapEntry.Type}: {mapEntry.MethodKey}";

                    // Assign file paths
                    if (mapEntry.Type.ToLowerInvariant() == "request")
                        logEntry.RequestFile = mapEntry.RelativePath;
                    else
                        logEntry.ResponseFile = mapEntry.RelativePath;

                    // Extract Business Object from file path
                    var boMatch = BusinessObjectRegex.Match(mapEntry.RelativePath);
                    if (boMatch.Success)
                    {
                        logEntry.BoType = boMatch.Groups["botype"].Value;
                        logEntry.BoId = boMatch.Groups["boid"].Value;
                    }

                    traceLogEntries.Add(logEntry);
                }

                // Calculate relative durations between steps
                CalculateRelativeDurations(traceLogEntries);

                entries.AddRange(traceLogEntries);
            }

            // IMPORTANT: After adding Map entries, find and merge RAW log entries from main logs
            MergeRawLogEntriesWithMapEntries(entries);

            // Handle orphaned raw files (those without map entries)
            await ParseOrphanedRawFilesAsync(entries, traceToMapEntries.Keys);
        }

        private void MergeRawLogEntriesWithMapEntries(List<LogEntry> entries)
        {
            // Find all entries that have RequestFile or ResponseFile from main logs (from "RAW log saved" messages)
            var rawLogEntries = entries.Where(e =>
                (e.Type == LogEntryType.Request && !string.IsNullOrEmpty(e.RequestFile)) ||
                (e.Type == LogEntryType.Response && !string.IsNullOrEmpty(e.ResponseFile))).ToList();

            Console.WriteLine($"[DEBUG] Found {rawLogEntries.Count} raw log entries with files");

            // Find all entries from Map files (these don't have file paths yet)
            var mapEntries = entries.Where(e =>
                string.IsNullOrEmpty(e.RequestFile) &&
                string.IsNullOrEmpty(e.ResponseFile) &&
                e.StepNumber > 0).ToList();

            Console.WriteLine($"[DEBUG] Found {mapEntries.Count} map entries without files");

            // Try to match raw log entries with map entries by trace ID, timestamp, and type
            foreach (var rawEntry in rawLogEntries)
            {
                var matchingMapEntry = mapEntries
                    .Where(m => m.TraceId == rawEntry.TraceId &&
                               m.Type == rawEntry.Type &&
                               Math.Abs((m.Timestamp - rawEntry.Timestamp).TotalSeconds) < 30) // Within 30 seconds
                    .OrderBy(m => Math.Abs((m.Timestamp - rawEntry.Timestamp).TotalSeconds))
                    .FirstOrDefault();

                if (matchingMapEntry != null)
                {
                    // Transfer file information from raw entry to map entry
                    if (!string.IsNullOrEmpty(rawEntry.RequestFile))
                    {
                        matchingMapEntry.RequestFile = rawEntry.RequestFile;
                        Console.WriteLine($"[DEBUG] Merged RequestFile {rawEntry.RequestFile} to map entry for trace {rawEntry.TraceId}");
                    }
                    if (!string.IsNullOrEmpty(rawEntry.ResponseFile))
                    {
                        matchingMapEntry.ResponseFile = rawEntry.ResponseFile;
                        Console.WriteLine($"[DEBUG] Merged ResponseFile {rawEntry.ResponseFile} to map entry for trace {rawEntry.TraceId}");
                    }

                    // Update operation if needed
                    if (string.IsNullOrEmpty(matchingMapEntry.Operation) && !string.IsNullOrEmpty(rawEntry.Operation))
                    {
                        matchingMapEntry.Operation = rawEntry.Operation;
                    }
                }
                else
                {
                    Console.WriteLine($"[DEBUG] No matching map entry found for raw entry: TraceId={rawEntry.TraceId}, Type={rawEntry.Type}, Time={rawEntry.Timestamp}");
                }
            }

            // Remove the raw log entries that were merged (to avoid duplicates)
            var rawEntriesToRemove = rawLogEntries.Where(r =>
                mapEntries.Any(m => m.TraceId == r.TraceId &&
                               m.Type == r.Type &&
                               (!string.IsNullOrEmpty(m.RequestFile) || !string.IsNullOrEmpty(m.ResponseFile)))).ToList();

            foreach (var entryToRemove in rawEntriesToRemove)
            {
                entries.Remove(entryToRemove);
                Console.WriteLine($"[DEBUG] Removed duplicate raw entry for trace {entryToRemove.TraceId}");
            }
        }

        private async Task<List<MapEntry>> ParseMapFilesAsync()
        {
            var mapEntries = new List<MapEntry>();
            var mapsPath = Path.Combine(_logDirectory, "Raw", "Maps");

            if (!Directory.Exists(mapsPath))
                return mapEntries;

            var mapFiles = Directory.GetFiles(mapsPath, "Map_*.txt");

            foreach (var mapFile in mapFiles)
            {
                try
                {
                    var lines = await File.ReadAllLinesAsync(mapFile);

                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        // Skip lines that contain log analyzer patterns
                        var lowerLine = line.ToLowerInvariant();
                        if (lowerLine.Contains("loganalyzer"))
                            continue;

                        var parts = line.Split(',');
                        if (parts.Length >= 10)
                        {
                            var mapEntry = new MapEntry
                            {
                                Timestamp = DateTime.TryParse(parts[0], out var ts) ? ts : DateTime.MinValue,
                                StepNumber = int.TryParse(parts[1], out var step) ? step : 0,
                                ControllerAction = parts[2],
                                MethodKey = parts[3],
                                Type = parts[4],
                                StatusCode = int.TryParse(parts[5], out var code) ? code : null,
                                Direction = parts[6],
                                Source = parts[7],
                                DurationMs = long.TryParse(parts[8], out var duration) ? duration : null,
                                RelativePath = parts.Length > 10 ? parts[10] : parts[9]
                            };

                            // Handle new format with relative duration
                            if (parts.Length >= 11)
                            {
                                if (long.TryParse(parts[9], out var relativeDuration))
                                {
                                    mapEntry.RelativeDurationMs = relativeDuration;
                                }
                                mapEntry.RelativePath = parts[10];
                            }

                            mapEntries.Add(mapEntry);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing map file {mapFile}: {ex.Message}");
                }
            }

            return mapEntries;
        }

        private string ExtractTraceIdFromMapPath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return string.Empty;

            var fileName = Path.GetFileName(relativePath);

            // For NoTrace files
            if (fileName.Contains("NoTrace"))
            {
                var timestampMatch = Regex.Match(fileName, @"NoTrace_(\d{14})");
                if (timestampMatch.Success)
                {
                    return $"NoTrace_{timestampMatch.Groups[1].Value}";
                }
            }
            else
            {
                // For real trace IDs
                var parts = fileName.Replace(".txt", "").Split('_');
                if (parts.Length > 0)
                {
                    var lastPart = parts[parts.Length - 1];
                    if (lastPart.Length >= 20 && Regex.IsMatch(lastPart, @"^[A-Za-z0-9]+$"))
                    {
                        return lastPart;
                    }
                }
            }

            return string.Empty;
        }

        private async Task ParseOrphanedRawFilesAsync(List<LogEntry> entries, IEnumerable<string> processedTraceIds)
        {
            var processedSet = new HashSet<string>(processedTraceIds);
            var rawDirs = new[] { "Raw\\Inbound", "Raw\\Outbound" };

            foreach (var rawDir in rawDirs)
            {
                var fullPath = Path.Combine(_logDirectory, rawDir);
                if (!Directory.Exists(fullPath)) continue;

                var files = Directory.GetFiles(fullPath, "*.txt");

                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);

                    // Skip files that are excluded
                    if (ShouldExcludeRawFile(fileName)) continue;

                    var traceId = ExtractTraceIdFromFileName(fileName);

                    if (!string.IsNullOrEmpty(traceId) && !processedSet.Contains(traceId))
                    {
                        // Create basic entry for orphaned file
                        var entry = CreateBasicLogEntryFromFile(file, traceId);
                        if (entry != null)
                        {
                            entries.Add(entry);
                        }
                    }
                }
            }
        }

        private bool ShouldExcludeRawFile(string fileName)
        {
            var lowerFileName = fileName.ToLowerInvariant();

            // Exclude files that contain log analyzer patterns
            if (lowerFileName.Contains("loganalyzer"))
                return true;

            // Exclude files with "NOT_MAPPED" and "loganalyzer" in the name
            if (lowerFileName.Contains("not_mapped") && lowerFileName.Contains("loganalyzer"))
                return true;

            return false;
        }

        private string ExtractTraceIdFromFileName(string fileName)
        {
            if (fileName.Contains("NoTrace"))
            {
                var timestampMatch = Regex.Match(fileName, @"(\d{14})");
                if (timestampMatch.Success)
                {
                    return $"NoTrace_{timestampMatch.Groups[1].Value}";
                }
            }
            else
            {
                var parts = fileName.Replace(".txt", "").Split('_');
                if (parts.Length > 0)
                {
                    var lastPart = parts[parts.Length - 1];
                    if (lastPart.Length >= 20 && Regex.IsMatch(lastPart, @"^[A-Za-z0-9]+$"))
                    {
                        return lastPart;
                    }
                }
            }

            return string.Empty;
        }

        private LogEntry? CreateBasicLogEntryFromFile(string filePath, string traceId)
        {
            try
            {
                var fileName = Path.GetFileName(filePath);
                var fileInfo = new FileInfo(filePath);

                var timestampMatch = Regex.Match(fileName, @"^(\d{14})_");
                DateTime timestamp = fileInfo.LastWriteTime;

                if (timestampMatch.Success &&
                    DateTime.TryParseExact(timestampMatch.Groups[1].Value, "yyyyMMddHHmmss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTime))
                {
                    timestamp = parsedTime;
                }

                var entry = new LogEntry
                {
                    TraceId = traceId,
                    Timestamp = timestamp,
                    Level = "INFO",
                    Message = $"Raw file: {fileName}",
                    Operation = ExtractOperationFromFileName(fileName),
                    Type = fileName.Contains("_request_") ? LogEntryType.Request : LogEntryType.Response
                };

                if (fileName.Contains("_request_"))
                    entry.RequestFile = Path.GetRelativePath(_logDirectory, filePath);
                else if (fileName.Contains("_response_"))
                    entry.ResponseFile = Path.GetRelativePath(_logDirectory, filePath);

                // Extract Business Object
                var boMatch = BusinessObjectRegex.Match(fileName);
                if (boMatch.Success)
                {
                    entry.BoType = boMatch.Groups["botype"].Value;
                    entry.BoId = boMatch.Groups["boid"].Value;
                }

                return entry;
            }
            catch
            {
                return null;
            }
        }

        private void CalculateRelativeDurations(List<LogEntry> entries)
        {
            for (int i = 1; i < entries.Count; i++)
            {
                var current = entries[i];
                var previous = entries[i - 1];

                if (current.DurationMs.HasValue && previous.DurationMs.HasValue)
                {
                    current.RelativeDurationMs = current.DurationMs - previous.DurationMs;
                }
                else if (current.DurationMs.HasValue && previous.DurationMs == 0)
                {
                    current.RelativeDurationMs = current.DurationMs;
                }
            }
        }

        private bool ShouldExcludeBasedOnContent(string content)
        {
            var lowerContent = content.ToLowerInvariant();
            return lowerContent.Contains("loganalyzer");
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

        private string ExtractOperationFromMethodKey(string methodKey)
        {
            var lowerMethodKey = methodKey.ToLowerInvariant();

            if (lowerMethodKey.Contains("masterdata") || lowerMethodKey.Contains("get_masterdata")) return "MasterData";
            if (lowerMethodKey.Contains("auth")) return "Authentication";
            if (lowerMethodKey.Contains("classification")) return "Classification";
            if (lowerMethodKey.Contains("workspace") || lowerMethodKey.Contains("business_workspace")) return "BusinessWorkspace";
            if (lowerMethodKey.Contains("child_node") || lowerMethodKey.Contains("nodes")) return "Node";
            if (lowerMethodKey.Contains("member")) return "Member";
            if (lowerMethodKey.Contains("search")) return "Search";

            return "Unknown";
        }

        private static void AnalyzeLogEntry(LogEntry entry)
        {
            var message = entry.Message;
            var messageLower = message.ToLowerInvariant();

            if (messageLower.Contains("request") || messageLower.Contains("calling"))
            {
                entry.Type = LogEntryType.Request;
            }
            else if (messageLower.Contains("response") || messageLower.Contains("received"))
            {
                entry.Type = LogEntryType.Response;
            }
            else if (messageLower.Contains("error") || messageLower.Contains("exception") || entry.Level == "ERROR")
            {
                entry.Type = LogEntryType.Error;
            }
            else if (messageLower.Contains("raw log saved"))
            {
                // Extract file path from "RAW log saved: Raw\Inbound\filename.txt" message
                var rawLogMatch = System.Text.RegularExpressions.Regex.Match(message,
                    @"RAW log saved:\s*(.+\.txt)", RegexOptions.IgnoreCase);
                if (rawLogMatch.Success)
                {
                    var filePath = rawLogMatch.Groups[1].Value.Trim().Replace("\\", "/");

                    Console.WriteLine($"[DEBUG] Extracted file path: '{filePath}' from message: '{message}'");

                    if (filePath.Contains("request"))
                    {
                        entry.RequestFile = filePath;
                        entry.Type = LogEntryType.Request;
                        Console.WriteLine($"[DEBUG] Set RequestFile: {filePath}");
                    }
                    else if (filePath.Contains("response"))
                    {
                        entry.ResponseFile = filePath;
                        entry.Type = LogEntryType.Response;
                        Console.WriteLine($"[DEBUG] Set ResponseFile: {filePath}");
                    }
                }
                else
                {
                    entry.Type = LogEntryType.RawLog;
                }
            }
            else if (messageLower.Contains("otcsticket") || messageLower.Contains("authentication"))
            {
                entry.Type = LogEntryType.Authentication;
            }
            else
            {
                entry.Type = LogEntryType.General;
            }

            // Determine operation
            if (messageLower.Contains("masterdata"))
                entry.Operation = "MasterData";
            else if (messageLower.Contains("classification"))
                entry.Operation = "Classification";
            else if (messageLower.Contains("workspace"))
                entry.Operation = "BusinessWorkspace";
            else if (messageLower.Contains("node"))
                entry.Operation = "Node";
            else if (messageLower.Contains("authentication") || messageLower.Contains("otcsticket") || messageLower.Contains("auth_login"))
                entry.Operation = "Authentication";
        }
    }
}