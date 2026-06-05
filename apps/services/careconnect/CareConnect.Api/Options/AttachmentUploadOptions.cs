namespace CareConnect.Api.Options;

/// <summary>
/// Configuration for upload validation on attachment endpoints.
/// Bound from the "AttachmentUpload" section of appsettings.json.
/// </summary>
public class AttachmentUploadOptions
{
    public const string SectionName = "AttachmentUpload";

    /// <summary>
    /// Maximum allowed file size in bytes. Defaults to 52,428,800 (50 MB).
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 50L * 1024 * 1024;

    /// <summary>
    /// Allowlist of MIME types that may be uploaded. Requests with a content-type
    /// not in this list are rejected with HTTP 400 before bytes are forwarded.
    /// </summary>
    public List<string> AllowedContentTypes { get; set; } =
    [
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
        "image/tiff",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-powerpoint",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "text/plain",
        "text/csv",
    ];
}
