using PlatformAuditEventService.Entities;

namespace PlatformAuditEventService.Services.Archival;

/// <summary>
/// No-op implementation of <see cref="IArchivalProvider"/>.
///
/// Used when <c>Archival:Strategy = NoOp</c> (the default). Streams the incoming
/// records to count them, logs what would have been archived, and returns a
/// successful result without writing any data.
///
/// This is the correct v1 placeholder for an enterprise archival pipeline:
/// - Safe: nothing is written or deleted.
/// - Observable: logs provide full visibility into what the production pipeline
///   would process when a real provider is configured.
/// - Forward-compatible: the log output can be used to validate retention policy
///   configuration before activating a real storage backend.
///
/// Replacement:
///   Register a different <see cref="IArchivalProvider"/> in Program.cs when
///   a storage backend is available. No other code changes are required.
/// </summary>
public sealed class NoOpArchivalProvider : IArchivalProvider
{
    private readonly ILogger<NoOpArchivalProvider> _logger;

    public NoOpArchivalProvider(ILogger<NoOpArchivalProvider> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public string ProviderName => "NoOp";

    /// <inheritdoc/>
    public async Task<ArchivalResult> ArchiveAsync(
        IAsyncEnumerable<AuditEventRecord> records,
        ArchivalContext                     context,
        CancellationToken                   ct = default)
    {
        _logger.LogInformation(
            "NoOpArchival: would archive records for job {ArchiveJobId} " +
            "Window=[{From:o}, {To:o}) TenantId={TenantId} Category={Category}. " +
            "No data written (Strategy=NoOp).",
            context.ArchiveJobId, context.WindowFrom, context.WindowTo,
            context.TenantId ?? "*", context.Category ?? "*");

        // Stream to count — do not materialise into memory.
        long count = 0;
        await foreach (var _ in records.WithCancellation(ct))
            count++;

        _logger.LogInformation(
            "NoOpArchival: completed dry-run for job {ArchiveJobId}. RecordCount={Count}",
            context.ArchiveJobId, count);

        return new ArchivalResult
        {
            RecordsProcessed     = count,
            RecordsArchived      = count,   // No failure — all "archived" conceptually.
            DestinationReference = null,    // No physical destination.
            IsSuccess            = true,
            ErrorMessage         = null,
            ProviderName         = ProviderName,
            CompletedAtUtc       = DateTimeOffset.UtcNow,
        };
    }
}
