using Microsoft.Extensions.Options;
using PlatformAuditEventService.Configuration;
using PlatformAuditEventService.DTOs.Retention;
using PlatformAuditEventService.Entities;
using PlatformAuditEventService.Enums;
using PlatformAuditEventService.Repositories;

using AuditEventQueryRequest = PlatformAuditEventService.DTOs.Query.AuditEventQueryRequest;

namespace PlatformAuditEventService.Services;

/// <summary>
/// Default implementation of <see cref="IRetentionService"/>.
///
/// Retention resolution (highest priority first):
///   1. Per-tenant override  — <c>Retention:TenantOverrides[tenantId]</c>
///   2. Per-category override — <c>Retention:CategoryOverrides[category]</c>
///   3. Default              — <c>Retention:DefaultRetentionDays</c>
///   4. 0                    — indefinite (no purge ever)
///
/// Tier boundaries:
///   Hot      — RecordedAtUtc + HotRetentionDays  > now
///   Warm     — now is between HotRetentionDays and the full retention window
///   Cold     — now > RecordedAtUtc + retentionDays  (past full window)
///   Indefinite — retentionDays == 0
///
/// EvaluateAsync:
///   Pulls up to <c>SampleLimit</c> records ordered oldest-first from the
///   primary store and classifies each. The total record count in the store is
///   fetched via a fast aggregate query. All operations are read-only.
///
/// Legal hold enforcement (Step 23):
///   When <c>Retention:LegalHoldEnabled=true</c>, <see cref="EvaluateAsync"/> batch-checks
///   all Cold-tier records in the sample against the LegalHolds table. Records with active holds
///   are reclassified to <see cref="StorageTier.LegalHold"/> and counted separately.
///   The retention enforcement job uses the same check before archiving or deleting any record.
/// </summary>
public sealed class RetentionService : IRetentionService
{
    private readonly RetentionOptions                   _opts;
    private readonly IAuditEventRecordRepository        _recordRepository;
    private readonly ILegalHoldRepository               _holdRepository;
    private readonly ILogger<RetentionService>          _logger;

    public RetentionService(
        IOptions<RetentionOptions>          opts,
        IAuditEventRecordRepository         recordRepository,
        ILegalHoldRepository                holdRepository,
        ILogger<RetentionService>           logger)
    {
        _opts             = opts.Value;
        _recordRepository = recordRepository;
        _holdRepository   = holdRepository;
        _logger           = logger;
    }

    // ── IRetentionService ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    public int ResolveRetentionDays(AuditEventRecord record)
    {
        // 1. Per-tenant override
        if (record.TenantId is not null
            && _opts.TenantOverrides.TryGetValue(record.TenantId, out var tenantDays))
        {
            return tenantDays;
        }

        // 2. Per-category override
        var categoryKey = record.EventCategory.ToString();
        if (_opts.CategoryOverrides.TryGetValue(categoryKey, out var catDays))
            return catDays;

        // 3. Platform default
        return _opts.DefaultRetentionDays;
    }

    /// <inheritdoc/>
    public DateTimeOffset? ComputeExpirationDate(AuditEventRecord record)
    {
        var days = ResolveRetentionDays(record);
        if (days <= 0) return null;           // Indefinite — never expires
        return record.RecordedAtUtc.AddDays(days);
    }

    /// <inheritdoc/>
    public StorageTier ClassifyTier(AuditEventRecord record)
    {
        var days = ResolveRetentionDays(record);

        if (days <= 0)
            return StorageTier.Indefinite;

        var now      = DateTimeOffset.UtcNow;
        var age      = now - record.RecordedAtUtc;
        var expiry   = record.RecordedAtUtc.AddDays(days);
        var hotDays  = _opts.HotRetentionDays;

        // Past full retention window → Cold (eligible for archival + deletion)
        if (now >= expiry)
            return StorageTier.Cold;

        // Within hot window
        if (hotDays > 0 && age.TotalDays <= hotDays)
            return StorageTier.Hot;

        // Between hot window end and full retention end → Warm
        return StorageTier.Warm;
    }

    /// <inheritdoc/>
    public async Task<RetentionEvaluationResult> EvaluateAsync(
        RetentionEvaluationRequest request,
        CancellationToken          ct = default)
    {
        var evaluatedAt = DateTimeOffset.UtcNow;

        _logger.LogInformation(
            "RetentionEvaluation: starting scan. TenantId={TenantId} Category={Category} SampleLimit={Limit}",
            request.TenantId ?? "*", request.Category ?? "*", request.SampleLimit);

        // ── Total count (fast aggregate) ──────────────────────────────────────
        var totalCount = await _recordRepository.CountAsync(ct);

        // ── Sample classification ─────────────────────────────────────────────
        long hot = 0, warm = 0, cold = 0, indefinite = 0, legalHold = 0;
        long expiredInSample = 0;
        DateTimeOffset? oldestRecordedAt = null;
        var expiredByCategory = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        long sampleCount = 0;

        if (request.SampleLimit > 0)
        {
            var sampleQuery  = BuildSampleQuery(request);
            var sampleResult = await _recordRepository.QueryAsync(sampleQuery, ct);

            // ── Batch legal hold check ────────────────────────────────────────
            // Pre-fetch active holds for Cold-tier candidates to avoid per-record queries.
            HashSet<Guid> heldAuditIds = [];
            if (_opts.LegalHoldEnabled)
            {
                var coldCandidateIds = sampleResult.Items
                    .Where(r => ClassifyTier(r) == StorageTier.Cold)
                    .Select(r => r.AuditId)
                    .ToList();

                if (coldCandidateIds.Count > 0)
                    heldAuditIds = await _holdRepository.GetActiveHoldAuditIdsAsync(coldCandidateIds, ct);
            }
            // ─────────────────────────────────────────────────────────────────

            foreach (var record in sampleResult.Items)
            {
                sampleCount++;

                // Track oldest record
                if (oldestRecordedAt is null || record.RecordedAtUtc < oldestRecordedAt)
                    oldestRecordedAt = record.RecordedAtUtc;

                var tier = ClassifyTier(record);

                // Legal hold overrides Cold → reclassify to LegalHold
                if (tier == StorageTier.Cold && _opts.LegalHoldEnabled
                    && heldAuditIds.Contains(record.AuditId))
                {
                    tier = StorageTier.LegalHold;
                }

                switch (tier)
                {
                    case StorageTier.Hot:       hot++;        break;
                    case StorageTier.Warm:      warm++;       break;
                    case StorageTier.Cold:
                        cold++;
                        expiredInSample++;
                        var catKey = record.EventCategory.ToString();
                        expiredByCategory[catKey] = expiredByCategory.GetValueOrDefault(catKey) + 1;
                        break;
                    case StorageTier.Indefinite: indefinite++; break;
                    case StorageTier.LegalHold:  legalHold++;  break;
                }
            }
        }

        var policySummary = BuildPolicySummary();

        _logger.LogInformation(
            "RetentionEvaluation: complete. Total={Total} Sampled={Sampled} " +
            "Hot={Hot} Warm={Warm} Cold={Cold} Indefinite={Indefinite} ExpiredInSample={Expired}",
            totalCount, sampleCount, hot, warm, cold, indefinite, expiredInSample);

        if (expiredInSample > 0)
        {
            _logger.LogWarning(
                "RetentionEvaluation: {Expired} records in sample are past their retention window " +
                "(Cold tier). Archival and deletion are not enabled in v1. " +
                "Set Retention:ArchiveBeforeDelete=true and configure IArchivalProvider to activate.",
                expiredInSample);
        }

        return new RetentionEvaluationResult
        {
            TotalRecordsInStore      = totalCount,
            SampleRecordsClassified  = sampleCount,
            RecordsInHotTier         = hot,
            RecordsInWarmTier        = warm,
            RecordsInColdTier        = cold,
            RecordsIndefinite        = indefinite,
            RecordsOnLegalHold       = legalHold,
            RecordsExpiredInSample   = expiredInSample,
            ExpiredByCategory        = expiredByCategory,
            OldestRecordedAtUtc      = oldestRecordedAt,
            PolicySummary            = policySummary,
            IsDryRun                 = true,
            EvaluatedAtUtc           = evaluatedAt,
        };
    }

    /// <inheritdoc/>
    public string BuildPolicySummary()
    {
        var parts = new List<string>();

        parts.Add(_opts.DefaultRetentionDays <= 0
            ? "Default: indefinite"
            : $"Default: {_opts.DefaultRetentionDays}d");

        parts.Add($"Hot window: {(_opts.HotRetentionDays > 0 ? $"{_opts.HotRetentionDays}d" : "none configured")}");

        if (_opts.CategoryOverrides.Count > 0)
        {
            var overrides = string.Join(", ", _opts.CategoryOverrides.Select(kv =>
                kv.Value <= 0 ? $"{kv.Key}=indefinite" : $"{kv.Key}={kv.Value}d"));
            parts.Add($"Category overrides: [{overrides}]");
        }

        if (_opts.TenantOverrides.Count > 0)
            parts.Add($"Tenant overrides: {_opts.TenantOverrides.Count} configured");

        if (_opts.LegalHoldEnabled)
            parts.Add("Legal hold: enabled (v1: placeholder — no records are flagged)");

        if (_opts.DryRun)
            parts.Add("DryRun: true (no records will be deleted)");

        return string.Join(". ", parts) + ".";
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private AuditEventQueryRequest BuildSampleQuery(RetentionEvaluationRequest request) =>
        new()
        {
            TenantId      = request.TenantId,
            Category      = string.IsNullOrWhiteSpace(request.Category)
                            ? null
                            : Enum.TryParse<EventCategory>(request.Category, ignoreCase: true, out var cat)
                              ? cat : null,
            // Oldest-first — maximises chance of finding expired records in the sample.
            SortBy         = "recordedAtUtc",
            SortDescending = false,
            Page           = 1,
            PageSize       = Math.Max(1, Math.Min(request.SampleLimit, 10_000)),
        };
}
