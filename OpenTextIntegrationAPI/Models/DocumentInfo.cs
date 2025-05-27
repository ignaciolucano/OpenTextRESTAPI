namespace OpenTextIntegrationAPI.Models
{
    public class DocumentInfo
    {
        public string NodeId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;

        // En lugar de DateTime?, lo pasamos como string.
        public string? ExpirationDate { get; set; }
        public long? fileSize { get; set; } = 0;
        public string? fileType { get; set; } = string.Empty;
        public string? createdAt { get; set; } = string.Empty;
        public string? createdBy { get; set; } = string.Empty;
        public string? updatedAt { get; set; } = string.Empty;
        public string? updatedBy { get; set; } = string.Empty;
    }

    public class DocumentInfoCR
    {
        public string NodeId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public string DocumentTypeId { get; set; } = string.Empty;
        public string? ExpirationDate { get; set; }
        public long? fileSize { get; set; } = 0;
        public string? fileType { get; set; } = string.Empty;
        public string? createdAt { get; set; } = string.Empty;
        public string? createdBy { get; set; } = string.Empty;
        public string? updatedAt { get; set; } = string.Empty;
        public string? updatedBy { get; set; } = string.Empty;

    }
}
