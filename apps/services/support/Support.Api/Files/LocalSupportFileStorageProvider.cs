using Microsoft.Extensions.Options;
using Support.Api.Configuration;

namespace Support.Api.Files;

/// <summary>
/// Standalone / dev mode: writes the upload to a local directory
/// segmented by <c>{root}/{tenant}/{ticket}/{document_id}/{safe_file_name}</c>.
/// Generates an opaque <c>local-{guid}</c> document id. NEVER use in the
/// integrated LegalSynq deployment — Documents Service must own storage there.
/// </summary>
public sealed class LocalSupportFileStorageProvider : ISupportFileStorageProvider
{
    public const string ProviderName = "local";

    private readonly IOptionsMonitor<FileStorageOptions> _options;
    private readonly ILogger<LocalSupportFileStorageProvider> _log;

    public LocalSupportFileStorageProvider(
        IOptionsMonitor<FileStorageOptions> options,
        ILogger<LocalSupportFileStorageProvider> log)
    {
        _options = options;
        _log = log;
    }

    public async Task<SupportFileUploadResult> UploadAsync(
        SupportFileUploadRequest request,
        CancellationToken ct = default)
    {
        var opts = _options.CurrentValue;
        var documentId = $"local-{Guid.NewGuid():N}";
        var safeName = SanitizeFileName(request.FileName);
        var safeTenant = SanitizePathSegment(request.TenantId);

        var rootFull = Path.GetFullPath(opts.LocalRootPath);
        var dir = Path.Combine(rootFull, safeTenant, request.TicketId.ToString("N"), documentId);
        var fullPath = Path.Combine(dir, safeName);

        // Defence in depth: ensure the resolved path is still under root.
        if (!Path.GetFullPath(fullPath).StartsWith(rootFull, StringComparison.Ordinal))
        {
            throw new SupportFileStorageException("Resolved upload path escapes the configured root.");
        }

        Directory.CreateDirectory(dir);

        long bytes;
        await using (var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await request.Stream.CopyToAsync(fs, ct);
            bytes = fs.Length;
        }

        _log.LogInformation(
            "Local upload stored ticket={TicketId} tenant={TenantId} document={DocumentId} bytes={Bytes}",
            request.TicketId, request.TenantId, documentId, bytes);

        return new SupportFileUploadResult(
            DocumentId: documentId,
            FileName: safeName,
            ContentType: request.ContentType,
            FileSizeBytes: bytes,
            StorageProvider: ProviderName,
            // We deliberately do NOT expose the local filesystem path in the
            // API response — see SUP-INT-08 security rules.
            StorageUri: null);
    }

    /// <summary>
    /// Strip path separators and reserved characters; collapse to at most 200 chars.
    /// Always returns a non-empty name (falls back to "upload.bin").
    /// </summary>
    internal static string SanitizeFileName(string raw)
    {
        var name = Path.GetFileName(raw ?? string.Empty);
        if (string.IsNullOrWhiteSpace(name)) return "upload.bin";

        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Where(c => !invalid.Contains(c) && c is not '\\' and not '/').ToArray());
        if (string.IsNullOrWhiteSpace(cleaned)) cleaned = "upload.bin";
        if (cleaned.Length > 200) cleaned = cleaned[..200];

        // Disallow names that resolve to relative-traversal segments.
        if (cleaned is "." or "..") cleaned = "upload.bin";
        return cleaned;
    }

    private static string SanitizePathSegment(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "_";
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(raw.Where(c => !invalid.Contains(c) && c is not '\\' and not '/' and not '.').ToArray());
        if (string.IsNullOrWhiteSpace(cleaned)) cleaned = "_";
        if (cleaned.Length > 64) cleaned = cleaned[..64];
        return cleaned;
    }
}
