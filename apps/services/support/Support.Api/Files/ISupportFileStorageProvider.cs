namespace Support.Api.Files;

/// <summary>
/// Pluggable storage provider for ticket attachment uploads.
///
/// Implementations:
///  - <see cref="NoOpSupportFileStorageProvider"/>      — rejects (default; safe).
///  - <see cref="LocalSupportFileStorageProvider"/>     — standalone/dev mode.
///  - <see cref="DocumentsServiceFileStorageProvider"/> — LegalSynq integrated mode.
///
/// Support never stores file bytes in the database; the provider is the only
/// component that touches the actual file content.
/// </summary>
public interface ISupportFileStorageProvider
{
    Task<SupportFileUploadResult> UploadAsync(
        SupportFileUploadRequest request,
        CancellationToken ct = default);
}
