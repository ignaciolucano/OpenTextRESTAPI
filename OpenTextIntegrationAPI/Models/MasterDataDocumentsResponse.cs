using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Generic;

namespace OpenTextIntegrationAPI.Models
{
    public class MasterDataDocumentsResponse
    {
        public MasterDataDocumentsHeader Header { get; set; } = new MasterDataDocumentsHeader();
        public List<DocumentInfo> Files { get; set; } = new List<DocumentInfo>();
    }
}
