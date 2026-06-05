using Microsoft.Extensions.Options;
using PlatformAuditEventService.Configuration;

namespace PlatformAuditEventService.Services.Export;

/// <summary>
/// Local filesystem implementation of <see cref="IExportStorageProvider"/>.
///
/// Writes export files into the directory specified by
/// <c>Export:LocalOutputPath</c> (defaults to <c>"exports"</c> relative to
/// the working directory). The directory is created on first write if it does
/// not already exist.
///
/// File-naming convention:
///   {FileNamePrefix}_{ExportId:N}_{yyyyMMddTHHmmss}.{ext}
///   Example: audit-export_3f4e5a6b7c8d9e0f_20260330T161234.json
///
/// Extension → Format mapping:
///   Json   → .json
///   Csv    → .csv
///   Ndjson → .ndjson
///
/// Concurrency: each export job produces a unique file name (ExportId is a Guid).
/// FileMode.CreateNew prevents accidental overwrites; if the file already exists
/// an IOException is thrown (should never happen in practice).
///
/// Replacement: swap for S3 / Azure implementations by registering a different
/// <see cref="IExportStorageProvider"/> in Program.cs based on
/// <c>Export:Provider</c> configuration.
/// </summary>
public sealed class LocalExportStorageProvider : IExportStorageProvider
{
    private readonly ExportOptions                         _opts;
    private readonly ILogger<LocalExportStorageProvider>  _logger;

    public LocalExportStorageProvider(
        IOptions<ExportOptions>                       opts,
        ILogger<LocalExportStorageProvider>           logger)
    {
        _opts   = opts.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string ProviderName => "Local";

    /// <inheritdoc/>
    public async Task<string> WriteAsync(
        Guid               exportId,
        string             format,
        Func<Stream, Task> writeContent,
        CancellationToken  ct = default)
    {
        var outputDir = string.IsNullOrWhiteSpace(_opts.LocalOutputPath)
            ? "exports"
            : _opts.LocalOutputPath;

        Directory.CreateDirectory(outputDir);

        var ext       = ExtensionFor(format);
        var prefix    = string.IsNullOrWhiteSpace(_opts.FileNamePrefix) ? "audit-export" : _opts.FileNamePrefix;
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmss");
        var fileName  = $"{prefix}_{exportId:N}_{timestamp}.{ext}";
        var fullPath  = Path.GetFullPath(Path.Combine(outputDir, fileName));

        _logger.LogDebug(
            "LocalExport: writing file ExportId={ExportId} Format={Format} Path={Path}",
            exportId, format, fullPath);

        await using var fs = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 65536,
            useAsync: true);

        await writeContent(fs);
        await fs.FlushAsync(ct);

        _logger.LogInformation(
            "LocalExport: file written ExportId={ExportId} Path={Path}",
            exportId, fullPath);

        return fullPath;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string ExtensionFor(string format) =>
        format.ToUpperInvariant() switch
        {
            "CSV"    => "csv",
            "NDJSON" => "ndjson",
            _        => "json"
        };
}
