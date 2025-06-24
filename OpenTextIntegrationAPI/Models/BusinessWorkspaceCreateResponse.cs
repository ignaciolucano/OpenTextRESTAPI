using System.Text.Json.Serialization;

namespace OpenTextIntegrationAPI.Models
{
    /// <summary>
    /// Response model for Business Workspace creation API
    /// </summary>
    public class BusinessWorkspaceCreateResponse
    {
        /// <summary>
        /// Links related to the API request
        /// </summary>
        public Links? Links { get; set; }

        /// <summary>
        /// Results of the creation operation
        /// </summary>
        public CreateResults? Results { get; set; }
    }

    /// <summary>
    /// Link information structure
    /// </summary>
    public class LinkInfo
    {
        /// <summary>
        /// Request body
        /// </summary>
        public string? Body { get; set; }

        /// <summary>
        /// Content type
        /// </summary>
        [JsonPropertyName("content_type")]
        public string? ContentType { get; set; }

        /// <summary>
        /// Link URL
        /// </summary>
        public string? Href { get; set; }

        /// <summary>
        /// HTTP method
        /// </summary>
        public string? Method { get; set; }

        /// <summary>
        /// Link name
        /// </summary>
        public string? Name { get; set; }
    }

    /// <summary>
    /// Results of a Business Workspace creation operation
    /// </summary>
    public class CreateResults
    {
        /// <summary>
        /// Whether to open the workspace directly
        /// </summary>
        [JsonPropertyName("direct_open")]
        public bool DirectOpen { get; set; }

        /// <summary>
        /// ID of the created workspace
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// ID of the sub-folder
        /// </summary>
        [JsonPropertyName("sub_folder_id")]
        public long SubFolderId { get; set; }
    }
}