namespace OpenTextIntegrationAPI.Models
{
    public class DocumentInfo
    {
        public string NodeId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;

        // En lugar de DateTime?, lo pasamos como string.
        public string? ExpirationDate { get; set; }
    }
}
