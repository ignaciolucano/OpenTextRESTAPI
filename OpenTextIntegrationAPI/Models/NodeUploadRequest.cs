namespace OpenTextIntegrationAPI.Models
{
    // Models/NodeUploadRequest.cs
    using Microsoft.AspNetCore.Http;

    public class NodeUploadRequest
    {
        public string BoType { get; set; }
        public string BoId { get; set; }
        public string Name { get; set; }
        public IFormFile File { get; set; } // For file upload via multipart/form-data
        public DateTime? ExpirationDate { get; set; }
    }

}
