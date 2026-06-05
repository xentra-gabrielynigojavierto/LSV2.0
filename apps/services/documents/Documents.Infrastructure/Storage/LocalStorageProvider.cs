using Documents.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Documents.Infrastructure.Storage;

public sealed class LocalStorageOptions
{
    public string BasePath { get; set; } = "/tmp/docs-local";
}

public sealed class LocalStorageProvider : IStorageProvider
{
    private readonly LocalStorageOptions    _opts;
    private readonly ILogger<LocalStorageProvider> _log;

    // In-memory redirect tokens for dev (not suitable for multi-replica production)
    private readonly Dictionary<string, (string Key, DateTime Expires)> _tokens = new();

    public string ProviderName => "local";

    public LocalStorageProvider(IOptions<LocalStorageOptions> opts, ILogger<LocalStorageProvider> log)
    {
        _opts = opts.Value;
        _log  = log;
        Directory.CreateDirectory(_opts.BasePath);
    }

    public async Task<string> UploadAsync(string key, Stream content, string mimeType, CancellationToken ct = default)
    {
        var filePath = GetFilePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        await using var fs = File.Create(filePath);
        await content.CopyToAsync(fs, ct);

        _log.LogDebug("LocalStorage: uploaded {Key} ({MimeType})", key, mimeType);
        return "docs-local";
    }

    public Task<string> GenerateSignedUrlAsync(string key, int ttlSeconds, string disposition, CancellationToken ct = default)
    {
        var token   = Guid.NewGuid().ToString("N");
        var expires = DateTime.UtcNow.AddSeconds(ttlSeconds);
        lock (_tokens) { _tokens[token] = (key, expires); }

        return Task.FromResult($"/internal/files?token={token}&disposition={disposition}");
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var path = GetFilePath(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
    {
        var path = GetFilePath(key);
        if (!File.Exists(path))
            throw new FileNotFoundException($"LocalStorage: key not found: {key}", path);

        Stream stream = File.OpenRead(path);
        return Task.FromResult(stream);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => Task.FromResult(File.Exists(GetFilePath(key)));

    public (string? Key, bool Expired) ResolveToken(string token)
    {
        lock (_tokens)
        {
            if (!_tokens.TryGetValue(token, out var entry)) return (null, false);
            if (entry.Expires < DateTime.UtcNow) { _tokens.Remove(token); return (null, true); }
            _tokens.Remove(token);
            return (entry.Key, false);
        }
    }

    private string GetFilePath(string key)
        => Path.Combine(_opts.BasePath, key.Replace("..", "_").Replace('/', Path.DirectorySeparatorChar));
}
