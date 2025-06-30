using System.Text.Json.Serialization;

namespace OpenTextIntegrationAPI.LogAnalyzer.Models
{
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? TraceId { get; set; }
        public string? Operation { get; set; }
        public string? BoType { get; set; }  // BUS1001006
        public string? BoId { get; set; }    // 010498
        public string? RequestFile { get; set; }
        public string? ResponseFile { get; set; }
        public string? MapFile { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public LogEntryType Type { get; set; }

        // New timing properties
        public int StepNumber { get; set; }
        public long? DurationMs { get; set; }  // Duration from trace start
        public long? RelativeDurationMs { get; set; }  // Duration from previous step
        public int? StatusCode { get; set; }
        public string? Direction { get; set; }  // inbound/outbound
        public string? Source { get; set; }  // Browser/Postman/etc
        public string? ControllerAction { get; set; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum LogEntryType
    {
        General,
        Request,
        Response,
        Error,
        RawLog,
        Classification,
        Authentication,
        Node
    }

    public class TraceTimeline
    {
        public string TraceId { get; set; } = string.Empty;
        public List<LogEntry> Entries { get; set; } = new();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public string? BoType { get; set; }
        public string? BoId { get; set; }
        public string? Operation { get; set; }
        public bool HasErrors => Entries.Any(e => e.Type == LogEntryType.Error);

        // New timing properties
        public long TotalDurationMs => (long)Duration.TotalMilliseconds;
        public int TotalSteps => Entries.Count;
        public double AvgStepDurationMs => Entries.Any(e => e.DurationMs.HasValue)
            ? Entries.Where(e => e.DurationMs.HasValue).Average(e => e.DurationMs!.Value)
            : 0;
    }

    public class SearchFilters
    {
        public string? Search { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string? BoType { get; set; }
        public string? BoId { get; set; }
        public string? Operation { get; set; }
        public bool? HasErrors { get; set; }
        public long? MinDurationMs { get; set; }  // New timing filters
        public long? MaxDurationMs { get; set; }
        public string? Direction { get; set; }  // inbound/outbound filter
    }

    // New class to represent parsed Map file entries
    public class MapEntry
    {
        public DateTime Timestamp { get; set; }
        public int StepNumber { get; set; }
        public string ControllerAction { get; set; } = string.Empty;
        public string MethodKey { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;  // request/response
        public int? StatusCode { get; set; }
        public string Direction { get; set; } = string.Empty;  // inbound/outbound
        public string Source { get; set; } = string.Empty;
        public long? DurationMs { get; set; }
        public long? RelativeDurationMs { get; set; }  // New field for step-to-step timing
        public string RelativePath { get; set; } = string.Empty;
    }
}