namespace Support.Api.Files;

/// <summary>
/// Default provider when no storage mode is configured. Always rejects with a
/// clear, actionable error so misconfiguration cannot silently swallow uploads.
/// </summary>
public sealed class NoOpSupportFileStorageProvider : ISupportFileStorageProvider
{
    public Task<SupportFileUploadResult> UploadAsync(
        SupportFileUploadRequest request,
        CancellationToken ct = default)
        => throw new SupportFileStorageNotConfiguredException();
}
