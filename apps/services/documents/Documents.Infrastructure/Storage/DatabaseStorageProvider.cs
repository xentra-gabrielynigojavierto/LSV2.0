using Documents.Domain.Entities;
using Documents.Domain.Interfaces;
using Documents.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Documents.Infrastructure.Storage;

public sealed class DatabaseStorageProvider : IStorageProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseStorageProvider> _log;

    public string ProviderName => "database";

    public DatabaseStorageProvider(
        IServiceScopeFactory scopeFactory,
        ILogger<DatabaseStorageProvider> log)
    {
        _scopeFactory = scopeFactory;
        _log = log;
    }

    public async Task<string> UploadAsync(string key, Stream content, string mimeType, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocsDbContext>();

        var existing = await db.FileBlobs.FindAsync(new object[] { key }, ct);
        if (existing is not null)
        {
            existing.Content = bytes;
            existing.MimeType = mimeType;
            existing.SizeBytes = bytes.Length;
            existing.CreatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            db.FileBlobs.Add(new FileBlob
            {
                StorageKey = key,
                Content = bytes,
                MimeType = mimeType,
                SizeBytes = bytes.Length,
                CreatedAtUtc = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync(ct);
        _log.LogDebug("DatabaseStorage: uploaded {Key} ({MimeType}, {Size} bytes)", key, mimeType, bytes.Length);
        return "docs-database";
    }

    public async Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocsDbContext>();

        var blob = await db.FileBlobs
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.StorageKey == key, ct)
            ?? throw new FileNotFoundException($"DatabaseStorage: key not found: {key}");

        return new MemoryStream(blob.Content);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocsDbContext>();
        return await db.FileBlobs.AnyAsync(b => b.StorageKey == key, ct);
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocsDbContext>();

        var blob = await db.FileBlobs.FindAsync(new object[] { key }, ct);
        if (blob is not null)
        {
            db.FileBlobs.Remove(blob);
            await db.SaveChangesAsync(ct);
        }
    }

    public Task<string> GenerateSignedUrlAsync(string key, int ttlSeconds, string disposition, CancellationToken ct = default)
    {
        var token = Guid.NewGuid().ToString("N");
        return Task.FromResult($"/internal/files?token={token}&disposition={disposition}");
    }
}
