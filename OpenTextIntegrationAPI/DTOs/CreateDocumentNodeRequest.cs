using System;

namespace OpenTextIntegrationAPI.DTOs
{
    public class CreateDocumentNodeRequest
    {
        // Required fields for creating a document node
        public int ParentId { get; set; }
        public string Name { get; set; }

        // Optional metadata
        public string DocumentType { get; set; }
        public DateTime? ExpirationDate { get; set; }

        // Additional fields can be added here as needed (e.g., file upload info)
    }
}
