// Models/OpenTextSettings.cs
using System.Runtime;

namespace OpenTextIntegrationAPI.Models
{
    public class OpenTextSettings
    {
        public string BaseUrl { get; set; }
        public string ExtSystemId { get; set; }
        public string expDateName { get; set; }
        public string uNamePIBUS1001006 { get; set; }
        public string uNameTIBUS1001006 { get; set; }
        public string uNameWTIBUS1001006 { get; set; }
        public string uNamePIBUS1001001 { get; set; }
        public string uNameTIBUS1001001 { get; set; }
        public string uNameWTIBUS1001001 { get; set; }
        public string uNamePIBUS1006 { get; set; }
        public string uNameTIBUS1006 { get; set; }
        public string uNameWTIBUS1006 { get; set; }
        public string uNamePIBUS2250 { get; set; }
        public string uNameTIBUS2250 { get; set; }
        public string uNameWTIBUS2250 { get; set; }
        public string ChangeRequestWSKtype { get; set; }
        public bool CreateFolderOnMove { get; set; }
        public string RootFolderId { get; set; }
        public string AssetsRootFolderId { get; set; }

        public Dictionary<string, string> DocumentTypeMapping { get; set; }

        public string GetDocTypeName(string docTypeString)
        {
            if (DocumentTypeMapping.TryGetValue(docTypeString, out var value))
            {
                return value;
            }

            throw new Exception($"Document type '{docTypeString}' not found in mapping.");
        }

    }
}
