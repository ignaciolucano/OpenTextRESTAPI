namespace OpenTextIntegrationAPI.DTOs
{
    /// <summary>
    /// Response for global logo operations.
    /// </summary>
    public class LogoResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? FileId { get; set; }
        public string? FileName { get; set; }
        public string? DownloadUrl { get; set; }
        public long? FileSize { get; set; }
        public DateTime? LastModified { get; set; }
        public int? Version { get; set; }
    }

    /// <summary>
    /// Response for background image operations.
    /// </summary>
    public class BackgroundImageResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? BackgroundId { get; set; }
        public string? DisplayName { get; set; }
        public string? FileName { get; set; }
        public string? DownloadUrl { get; set; }
        public long? FileSize { get; set; }
        public DateTime? LastModified { get; set; }
    }

    /// <summary>
    /// Response for listing background images.
    /// </summary>
    public class BackgroundImagesListResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int TotalCount { get; set; }
        public int PageSize { get; set; }
        public int PageNumber { get; set; }
        public int TotalPages { get; set; }
        public List<BackgroundImageInfo> Images { get; set; } = new List<BackgroundImageInfo>();
    }

    /// <summary>
    /// Summary information about a background image.
    /// </summary>
    public class BackgroundImageInfo
    {
        public string? BackgroundId { get; set; }
        public string? DisplayName { get; set; }
        //public string? FileName { get; set; }
        //public string? ThumbnailUrl { get; set; }
        public long FileSize { get; set; }
        //public DateTime LastModified { get; set; }
    }

    /// <summary>
    /// Response for user avatar operations.
    /// </summary>
    public class UserAvatarResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? UserEmail { get; set; }
        public string? FileId { get; set; }
        public string? FileName { get; set; }
        public string? DownloadUrl { get; set; }
        public long? FileSize { get; set; }
        public DateTime? LastModified { get; set; }
        public int? Version { get; set; }
    }

    /// <summary>
    /// Response for user attachment operations.
    /// </summary>
    public class UserAttachmentResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? UserEmail { get; set; }
        public string? AttachmentId { get; set; }
        public string? FileName { get; set; }
        public string? Description { get; set; }
        public string? ContentType { get; set; }
        public string? DownloadUrl { get; set; }
        public long FileSize { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastModified { get; set; }
        public int Version { get; set; }
    }

    /// <summary>
    /// Response for listing user attachments.
    /// </summary>
    public class UserAttachmentsListResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? UserEmail { get; set; }
        public int TotalCount { get; set; }
        public int PageSize { get; set; }
        public int PageNumber { get; set; }
        public int TotalPages { get; set; }
        public List<UserAttachmentInfo> Attachments { get; set; } = new List<UserAttachmentInfo>();
    }

    /// <summary>
    /// Summary information about a user attachment.
    /// </summary>
    public class UserAttachmentInfo
    {
        public string? AttachmentId { get; set; }
        public string? FileName { get; set; }
        public string? Description { get; set; }
        public string? ContentType { get; set; }
        public string? ThumbnailUrl { get; set; }
        public long FileSize { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastModified { get; set; }
    }
}