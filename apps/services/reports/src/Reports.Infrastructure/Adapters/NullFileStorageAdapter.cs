using Reports.Contracts.Storage;

namespace Reports.Infrastructure.Adapters;

public sealed class NullFileStorageAdapter : IFileStorageAdapter
{
    public bool IsEnabled => false;

    public Task<FileStorageResult> UploadAsync(FileStorageRequest request, CancellationToken ct = default)
    {
        return Task.FromResult(new FileStorageResult
        {
            StorageKey = string.Empty,
            Provider = "None",
            BucketName = string.Empty,
            SizeBytes = request.Content.Length,
            StoredAtUtc = DateTimeOffset.UtcNow,
        });
    }
}
