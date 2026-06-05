namespace Documents.Domain.Interfaces;

public interface IStorageProvider
{
    string ProviderName { get; }

    Task<string> UploadAsync(
        string key,
        Stream content,
        string mimeType,
        CancellationToken ct = default);

    /// <summary>Download file as a readable stream. Caller is responsible for disposing.</summary>
    Task<Stream> DownloadAsync(string key, CancellationToken ct = default);

    Task<string> GenerateSignedUrlAsync(
        string key,
        int ttlSeconds,
        string disposition,
        CancellationToken ct = default);

    Task DeleteAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}
