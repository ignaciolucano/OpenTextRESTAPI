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
    }
}