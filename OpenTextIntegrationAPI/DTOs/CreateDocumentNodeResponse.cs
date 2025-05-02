namespace OpenTextIntegrationAPI.DTOs
{
    public class CreateDocumentNodeResponse
    {
        public int NodeId { get; set; }
        public string Name { get; set; }
        public int Type { get; set; }
        public string TypeName { get; set; }
        // Add additional fields as needed
        public string? Message { get; set; }
    }
}
