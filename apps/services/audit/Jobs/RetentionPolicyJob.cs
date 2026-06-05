using Microsoft.Extensions.Options;
using PlatformAuditEventService.Configuration;
using PlatformAuditEventService.DTOs.Retention;
using PlatformAuditEventService.Entities;
using PlatformAuditEventService.Repositories;
using PlatformAuditEventService.Services;
using PlatformAuditEventService.Services.Archival;
using System.Runtime.CompilerServices;

namespace PlatformAuditEventService.Jobs;

/// <summary>
/// Retention policy evaluation and enforcement job.
///
/// Phase 1 (always runs, read-only):
///   Calls <see cref="IRetentionService.EvaluateAsync"/> to classify a sample of the oldest
///   records into storage tiers (Hot / Warm / Cold / Indefinite / LegalHold).
///   Logs a structured summary. Never modifies data.
///
/// Phase 2 (active enforcement — opt-in, controlled by DryRun flag):
///   Runs only when <c>Retention:DryRun=false</c> AND Cold-tier records exist.
///   For each batch of eligible records:
///     1. Pre-checks for active legal holds (skips held records).
///     2. Archives the batch if <c>Retention:ArchiveBeforeDelete=true</c> via IArchivalProvider.
///     3. Deletes successfully archived (or unarchived if ArchiveBeforeDelete=false) records.
///
/// Safety:
///   - DryRun defaults to true in all environments. Set DryRun=false to activate.
///   - LegalHold pre-check prevents deletion of held records regardless of tier classification.
///   - MaxDeletesPerRun caps total deletes per job run to bound database load.
///   - DeleteBatchSize controls DB batch size to avoid long table-lock periods.
///
/// Scheduling:
///   <see cref="RetentionHostedService"/> drives this job on the interval defined by
///   <c>Retention:RetentionIntervalHours</c>.
/// </summary>
public sealed class RetentionPolicyJob
{
    private readonly IRetentionService             _retentionService;
    private readonly IAuditEventRecordRepository   _recordRepository;
    private readonly ILegalHoldRepository          _holdRepository;
    private readonly IArchivalProvider             _archival;
    private readonly RetentionOptions              _opts;
    private readonly ILogger<RetentionPolicyJob>   _logger;

    public RetentionPolicyJob(
        IRetentionService             retentionService,
        IAuditEventRecordRepository   recordRepository,
        ILegalHoldRepository          holdRepository,
        IArchivalProvider             archival,
        IOptions<RetentionOptions>    opts,
        ILogger<RetentionPolicyJob>   logger)
    {
        _retentionService = retentionService;
        _recordRepository = recordRepository;
        _holdRepository   = holdRepository;
        _archival         = archival;
        _opts             = opts.Value;
        _logger           = logger;
    }

    /// <summary>
    /// Execute one retention policy run.
    /// Phase 1 always runs (evaluation/read-only).
    /// Phase 2 runs when DryRun=false and Cold-tier records exist.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        if (!_opts.JobEnabled)
        {
            _logger.LogDebug(
                "RetentionPolicyJob: skipped — job is disabled (Retention:JobEnabled=false).");
            return;
        }

        _logger.LogInformation(
            "RetentionPolicyJob: starting. " +
            "DryRun={DryRun} ArchiveBeforeDelete={Archive} HotDays={Hot} DefaultDays={Default} " +
            "LegalHoldEnabled={LegalHold}",
            _opts.DryRun, _opts.ArchiveBeforeDelete, _opts.HotRetentionDays,
            _opts.DefaultRetentionDays <= 0 ? "indefinite" : _opts.DefaultRetentionDays.ToString(),
            _opts.LegalHoldEnabled);

        // ── Phase 1: Policy evaluation (always runs, always read-only) ────────
        var result = await _retentionService.EvaluateAsync(
            new RetentionEvaluationRequest
            {
                SampleLimit = _opts.MaxDeletesPerRun > 0 ? _opts.MaxDeletesPerRun : 5_000,
            },
            ct);

        _logger.LogInformation(
            "RetentionPolicyJob evaluation: " +
            "TotalInStore={Total} Sampled={Sampled} | " +
            "Hot={Hot} Warm={Warm} Cold={Cold} Indefinite={Indefinite} LegalHold={LegalHold}",
            result.TotalRecordsInStore, result.SampleRecordsClassified,
            result.RecordsInHotTier, result.RecordsInWarmTier,
            result.RecordsInColdTier, result.RecordsIndefinite, result.RecordsOnLegalHold);

        _logger.LogInformation(
            "RetentionPolicyJob policy: {PolicySummary}", result.PolicySummary);

        if (result.RecordsExpiredInSample > 0)
        {
            _logger.LogWarning(
                "RetentionPolicyJob: {ExpiredCount} records in sample are in the Cold tier " +
                "(past their retention window). DryRun={DryRun}.",
                result.RecordsExpiredInSample, _opts.DryRun);

            foreach (var (category, count) in result.ExpiredByCategory)
            {
                _logger.LogWarning(
                    "RetentionPolicyJob: expired by category — {Category}: {Count}",
                    category, count);
            }
        }

        // ── Phase 2: Archival + deletion enforcement ──────────────────────────
        if (_opts.DryRun)
        {
            if (result.RecordsExpiredInSample > 0)
            {
                _logger.LogInformation(
                    "RetentionPolicyJob: DryRun=true — no records were archived or deleted. " +
                    "Set Retention:DryRun=false to activate enforcement.");
            }

            _logger.LogInformation("RetentionPolicyJob: run complete (dry-run).");
            return;
        }

        if (result.RecordsInColdTier == 0)
        {
            _logger.LogInformation(
                "RetentionPolicyJob: no Cold-tier records found — nothing to enforce.");
            _logger.LogInformation("RetentionPolicyJob: run complete.");
            return;
        }

        // Guard: indefinite retention (DefaultRetentionDays <= 0) must never delete.
        if (_opts.DefaultRetentionDays <= 0)
        {
            _logger.LogWarning(
                "RetentionPolicyJob: DefaultRetentionDays={Days} (indefinite) — " +
                "Phase 2 enforcement skipped for safety.",
                _opts.DefaultRetentionDays);
            return;
        }

        var cutoff    = DateTimeOffset.UtcNow.AddDays(-_opts.DefaultRetentionDays);
        var batchSize = Math.Max(1, _opts.DeleteBatchSize);
        var remaining = _opts.MaxDeletesPerRun > 0 ? _opts.MaxDeletesPerRun : int.MaxValue;

        long totalArchived = 0;
        long totalDeleted  = 0;
        long totalHeld     = 0;
        long totalFailed   = 0;

        _logger.LogWarning(
            "RetentionPolicyJob: ENFORCEMENT ACTIVE — DryRun=false. " +
            "Cutoff={Cutoff:o} MaxDeletesPerRun={Max} BatchSize={Batch} " +
            "ArchiveBeforeDelete={Archive} Provider={Provider}",
            cutoff, _opts.MaxDeletesPerRun, batchSize,
            _opts.ArchiveBeforeDelete, _archival.ProviderName);

        while (remaining > 0 && !ct.IsCancellationRequested)
        {
            var thisBatch  = Math.Min(remaining, batchSize);
            var candidates = await _recordRepository.GetOldestEligibleAsync(cutoff, thisBatch, ct);

            if (candidates.Count == 0)
                break;

            // ── Legal hold pre-check ──────────────────────────────────────────
            HashSet<Guid> heldAuditIds = [];
            if (_opts.LegalHoldEnabled)
            {
                var ids = candidates.Select(r => r.AuditId).ToList();
                heldAuditIds = await _holdRepository.GetActiveHoldAuditIdsAsync(ids, ct);
                totalHeld += heldAuditIds.Count;

                if (heldAuditIds.Count > 0)
                {
                    _logger.LogWarning(
                        "RetentionPolicyJob: {HeldCount}/{Total} records in batch skipped — " +
                        "active legal hold prevents deletion.",
                        heldAuditIds.Count, candidates.Count);
                }
            }

            var toProcess = candidates
                .Where(r => !heldAuditIds.Contains(r.AuditId))
                .ToList();

            if (toProcess.Count == 0)
            {
                // All candidates in this batch are held — stop to avoid an infinite loop.
                _logger.LogWarning(
                    "RetentionPolicyJob: all remaining candidates are on legal hold. Stopping enforcement.");
                break;
            }

            // ── Archive before delete ─────────────────────────────────────────
            List<long> idsToDelete;

            if (_opts.ArchiveBeforeDelete)
            {
                var archiveContext = new ArchivalContext
                {
                    ArchiveJobId    = Guid.NewGuid().ToString("N"),
                    WindowFrom      = toProcess.Min(r => r.RecordedAtUtc),
                    WindowTo        = toProcess.Max(r => r.RecordedAtUtc).AddTicks(1),
                    InitiatedBy     = "RetentionPolicyJob",
                    InitiatedAtUtc  = DateTimeOffset.UtcNow,
                };

                ArchivalResult archivalResult;
                try
                {
                    archivalResult = await _archival.ArchiveAsync(
                        ToAsyncEnumerable(toProcess, ct),
                        archiveContext,
                        ct);
                }
                catch (Exception ex)
                {
                    totalFailed += toProcess.Count;
                    _logger.LogError(ex,
                        "RetentionPolicyJob: archival threw for batch of {Count} records — " +
                        "records NOT deleted. ArchiveJobId={JobId}",
                        toProcess.Count, archiveContext.ArchiveJobId);
                    break;
                }

                if (!archivalResult.IsSuccess)
                {
                    totalFailed += toProcess.Count;
                    _logger.LogError(
                        "RetentionPolicyJob: archival reported failure for batch of {Count} records — " +
                        "records NOT deleted. Provider={Provider} Error={Error}",
                        toProcess.Count, archivalResult.ProviderName, archivalResult.ErrorMessage);
                    break;
                }

                totalArchived += archivalResult.RecordsArchived;
                idsToDelete    = toProcess.Select(r => r.Id).ToList();

                _logger.LogInformation(
                    "RetentionPolicyJob: archived {Count} records to {Provider} → {Ref}",
                    archivalResult.RecordsArchived, archivalResult.ProviderName,
                    archivalResult.DestinationReference ?? "(none)");
            }
            else
            {
                idsToDelete = toProcess.Select(r => r.Id).ToList();
            }

            // ── Delete confirmed records ──────────────────────────────────────
            if (idsToDelete.Count > 0)
            {
                var deleted = await _recordRepository.DeleteBatchAsync(idsToDelete, ct);
                totalDeleted += deleted;
                remaining    -= deleted;
            }
        }

        _logger.LogWarning(
            "RetentionPolicyJob: ENFORCEMENT COMPLETE — " +
            "Archived={Archived} Deleted={Deleted} Held={Held} Failed={Failed}",
            totalArchived, totalDeleted, totalHeld, totalFailed);

        _logger.LogInformation("RetentionPolicyJob: run complete.");
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Wraps a synchronous list as an <see cref="IAsyncEnumerable{T}"/> for use with
    /// <see cref="IArchivalProvider.ArchiveAsync"/>. Respects cancellation between items.
    /// </summary>
    private static async IAsyncEnumerable<AuditEventRecord> ToAsyncEnumerable(
        IEnumerable<AuditEventRecord>          records,
        [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var record in records)
        {
            ct.ThrowIfCancellationRequested();
            yield return record;
            await Task.Yield();
        }
    }
}
