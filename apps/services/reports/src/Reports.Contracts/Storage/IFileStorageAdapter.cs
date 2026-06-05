namespace Reports.Contracts.Storage;

public sealed class FileStorageRequest
{
    public string TenantId { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public byte[] Content { get; init; } = Array.Empty<byte>();
    public string ContentType { get; init; } = "application/octet-stream";
    public string? SubPath { get; init; }
}

public sealed class FileStorageResult
{
    public string StorageKey { get; init; } = string.Empty;
    public string? Url { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string BucketName { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public DateTimeOffset StoredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public interface IFileStorageAdapter
{
    bool IsEnabled { get; }
    Task<FileStorageResult> UploadAsync(FileStorageRequest request, CancellationToken ct = default);
}
