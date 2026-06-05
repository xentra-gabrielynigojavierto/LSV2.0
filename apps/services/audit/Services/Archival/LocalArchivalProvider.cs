using System.Text.Json;
using Microsoft.Extensions.Options;
using PlatformAuditEventService.Configuration;
using PlatformAuditEventService.Entities;

namespace PlatformAuditEventService.Services.Archival;

/// <summary>
/// Archival provider that writes audit records to the local filesystem as NDJSON files.
///
/// Use cases:
///   - Single-node on-premises deployments with local NFS or SAN storage.
///   - Development and staging environments that need to validate the archival pipeline
///     without a cloud storage dependency.
///   - CI/CD pipelines that test retention lifecycle end-to-end.
///
/// Output format: Newline-Delimited JSON (NDJSON). One record per line. UTF-8 encoded.
/// File naming: {Prefix}_{ArchiveJobId}_{Timestamp}.ndjson
/// File location: configured via Archival:LocalOutputPath (default: "archive/").
///
/// Thread safety: multiple concurrent archive operations write to different files
/// (keyed on ArchiveJobId) so this provider is safe under concurrent use.
///
/// Failure handling: if the directory does not exist it is created. Any write
/// failure causes an ArchivalResult with IsSuccess=false — the caller must NOT
/// delete source records in this case.
/// </summary>
public sealed class LocalArchivalProvider : IArchivalProvider
{
    private readonly ArchivalOptions                   _opts;
    private readonly ILogger<LocalArchivalProvider>    _logger;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented        = false,
    };

    public LocalArchivalProvider(
        IOptions<ArchivalOptions>       opts,
        ILogger<LocalArchivalProvider>  logger)
    {
        _opts   = opts.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string ProviderName => "LocalCopy";

    /// <inheritdoc/>
    public async Task<ArchivalResult> ArchiveAsync(
        IAsyncEnumerable<AuditEventRecord> records,
        ArchivalContext                     context,
        CancellationToken                   ct = default)
    {
        var outputDir = string.IsNullOrWhiteSpace(_opts.LocalOutputPath)
            ? Path.Combine(AppContext.BaseDirectory, "archive")
            : _opts.LocalOutputPath;

        Directory.CreateDirectory(outputDir);

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss");
        var prefix    = string.IsNullOrWhiteSpace(_opts.FileNamePrefix) ? "audit-archive" : _opts.FileNamePrefix;
        var fileName  = $"{prefix}_{context.ArchiveJobId:N}_{timestamp}.ndjson";
        var filePath  = Path.Combine(outputDir, fileName);

        _logger.LogInformation(
            "LocalArchival: starting archive. JobId={JobId} Window=[{From:o},{To:o}) Output={Path}",
            context.ArchiveJobId, context.WindowFrom, context.WindowTo, filePath);

        long written = 0;

        try
        {
            await using var fs     = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true);
            await using var writer = new StreamWriter(fs, System.Text.Encoding.UTF8);

            await foreach (var record in records.WithCancellation(ct))
            {
                var line = JsonSerializer.Serialize(record, _jsonOpts);
                await writer.WriteLineAsync(line.AsMemory(), ct);
                written++;

                // Flush every 10 000 records to limit memory pressure
                if (written % 10_000 == 0)
                    await writer.FlushAsync(ct);
            }

            await writer.FlushAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "LocalArchival: write failed for JobId={JobId} after {Written} records. File={Path}",
                context.ArchiveJobId, written, filePath);

            return new ArchivalResult
            {
                RecordsProcessed     = written,
                RecordsArchived      = 0,
                DestinationReference = null,
                IsSuccess            = false,
                ErrorMessage         = ex.Message,
                ProviderName         = ProviderName,
                CompletedAtUtc       = DateTimeOffset.UtcNow,
            };
        }

        _logger.LogInformation(
            "LocalArchival: completed. JobId={JobId} RecordsWritten={Written} Path={Path}",
            context.ArchiveJobId, written, filePath);

        return new ArchivalResult
        {
            RecordsProcessed     = written,
            RecordsArchived      = written,
            DestinationReference = filePath,
            IsSuccess            = true,
            ErrorMessage         = null,
            ProviderName         = ProviderName,
            CompletedAtUtc       = DateTimeOffset.UtcNow,
        };
    }
}
