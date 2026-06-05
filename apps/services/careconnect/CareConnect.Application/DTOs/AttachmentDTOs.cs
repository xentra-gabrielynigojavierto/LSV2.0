namespace CareConnect.Application.DTOs;

public class CreateAttachmentMetadataRequest
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? ExternalDocumentId { get; set; }
    public string? ExternalStorageProvider { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Notes { get; set; }
}

public class AttachmentMetadataResponse
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public string? ExternalDocumentId { get; init; }
    public string? ExternalStorageProvider { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public Guid? CreatedByUserId { get; init; }
}

/// <summary>
/// CC2-INT-B03: Request model for server-side upload proxying.
/// The file stream is passed separately — this model carries the scope metadata.
/// </summary>
public class UploadAttachmentRequest
{
    /// <summary>
    /// "shared" (default) — any referral participant may access.
    /// "provider-specific" — only the assigned provider org or admins may access.
    /// </summary>
    public string Scope { get; set; } = AttachmentScope.Shared;
    public string? Notes { get; set; }
}

/// <summary>
/// CC2-INT-B03: Response returned from the signed URL endpoint.
/// The client uses this URL directly to access the document (browser redirect / fetch).
/// </summary>
public class SignedUrlResponse
{
    public string Url { get; init; } = string.Empty;
    public int ExpiresInSeconds { get; init; }
}

public static class AttachmentScope
{
    public const string Shared           = "shared";
    public const string ProviderSpecific = "provider-specific";
}
