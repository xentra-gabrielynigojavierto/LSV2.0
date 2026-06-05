namespace Support.Api.Files;

/// <summary>
/// Caller-provided context for a file upload. The <see cref="Stream"/> is
/// owned by the caller and must remain readable for the duration of
/// <see cref="ISupportFileStorageProvider.UploadAsync"/>.
/// </summary>
public sealed record SupportFileUploadRequest(
    string TenantId,
    Guid TicketId,
    string FileName,
    string? ContentType,
    long FileSizeBytes,
    Stream Stream,
    string? UploadedByUserId,
    IReadOnlyDictionary<string, string?>? Metadata = null);

/// <summary>
/// Provider-returned metadata after a successful upload. The
/// <see cref="DocumentId"/> is opaque to Support and is what gets persisted
/// in <c>support_ticket_attachments.document_id</c>.
/// </summary>
public sealed record SupportFileUploadResult(
    string DocumentId,
    string FileName,
    string? ContentType,
    long FileSizeBytes,
    string StorageProvider,
    string? StorageUri = null,
    string? MetadataJson = null);
