using Microsoft.EntityFrameworkCore;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Repositories;

/// <summary>
/// LS-NOTIF-SMS-008: Read-only EF Core aggregation repository for SMS dashboard.
///
/// All queries:
///  - Filter Channel = "sms" as the primary predicate.
///  - Never project CredentialsJson, SettingsJson, RecipientJson, or phone numbers.
///  - Never trigger sends, retries, reconciliation, or provider calls.
///  - Fetch a minimal column projection then aggregate in-memory
///    (consistent with SmsActivityRepository.SummarizeAsync pattern).
/// </summary>
public sealed class SmsDashboardRepository : ISmsDashboardRepository
{
    private readonly NotificationsDbContext _db;

    public SmsDashboardRepository(NotificationsDbContext db) => _db = db;

    // ── Outcome classification sets (mirror SmsActivityRepository) ────────────

    private static readonly HashSet<string> SkippedOutcomes = new(StringComparer.OrdinalIgnoreCase)
    {
        "skipped_missing_provider_message_id",
        "skipped_not_sms",
        "skipped_unsupported_provider",
        "provider_message_not_found",
    };

    private static readonly HashSet<string> ProviderConfigFailedOutcomes = new(StringComparer.OrdinalIgnoreCase)
    {
        "missing_provider_config_context",
        "provider_config_not_found",
        "provider_config_inactive",
        "provider_config_invalid",
        "provider_runtime_resolution_failed",
    };

    // ── Summary ───────────────────────────────────────────────────────────────

    public async Task<SmsDashboardSummaryDto> GetSummaryAsync(SmsDashboardQuery query, CancellationToken ct = default)
    {
        var rows = await BuildBaseQuery(query)
            .Select(a => new
            {
                a.TenantId,
                a.Provider,
                a.ProviderConfigId,
                OwnershipMode           = a.ProviderOwnershipMode,
                a.Status,
                a.LastReconciliationOutcome,
                a.ReconciliationAttemptCount,
                a.CreatedAt,
            })
            .ToListAsync(ct);

        if (rows.Count == 0)
            return new SmsDashboardSummaryDto();

        int sent = 0, delivered = 0, failed = 0, deadLetter = 0,
            pending = 0, processing = 0, sending = 0, retrying = 0;
        int tenantOwned = 0, platformOwned = 0, unknownOwnership = 0;
        int reconciledTotal = 0, neverReconciled = 0,
            reconUpdated = 0, reconNoChange = 0, reconLookupFailed = 0,
            reconSkipped = 0, reconProviderConfig = 0;

        var tenantIds       = new HashSet<Guid?>();
        var providers       = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var providerConfigs = new HashSet<Guid?>();

        foreach (var r in rows)
        {
            // Delivery status
            switch (r.Status)
            {
                case "sent":        sent++;        break;
                case "delivered":   delivered++;   break;
                case "failed":      failed++;      break;
                case "dead_letter": deadLetter++;  break;
                case "pending":     pending++;     break;
                case "processing":
                case "queued":      processing++;  break;
                case "sending":     sending++;     break;
                case "retrying":    retrying++;    break;
                // unknown status — counted in inProgress but not a named bucket
            }

            // Ownership
            switch (r.OwnershipMode)
            {
                case "tenant":   tenantOwned++;   break;
                case "platform": platformOwned++; break;
                default:         unknownOwnership++; break;
            }

            // Reconciliation
            if (r.ReconciliationAttemptCount == 0)
            {
                neverReconciled++;
            }
            else
            {
                reconciledTotal++;
                var outcome = r.LastReconciliationOutcome;
                if (string.Equals(outcome, "updated", StringComparison.OrdinalIgnoreCase))
                    reconUpdated++;
                else if (string.Equals(outcome, "no_change", StringComparison.OrdinalIgnoreCase))
                    reconNoChange++;
                else if (string.Equals(outcome, "vendor_lookup_failed", StringComparison.OrdinalIgnoreCase))
                    reconLookupFailed++;
                else if (!string.IsNullOrEmpty(outcome) && SkippedOutcomes.Contains(outcome))
                    reconSkipped++;
                else if (!string.IsNullOrEmpty(outcome) && ProviderConfigFailedOutcomes.Contains(outcome))
                    reconProviderConfig++;
                else
                    reconSkipped++; // unknown or null outcome — count as skipped
            }

            // Cardinality
            tenantIds.Add(r.TenantId);
            if (!string.IsNullOrEmpty(r.Provider)) providers.Add(r.Provider);
            providerConfigs.Add(r.ProviderConfigId);
        }

        return new SmsDashboardSummaryDto
        {
            TotalAttempts                   = rows.Count,
            SentCount                       = sent,
            DeliveredCount                  = delivered,
            FailedCount                     = failed,
            DeadLetterCount                 = deadLetter,
            PendingCount                    = pending,
            ProcessingCount                 = processing,
            SendingCount                    = sending,
            RetryingCount                   = retrying,
            TenantOwnedCount                = tenantOwned,
            PlatformOwnedCount              = platformOwned,
            UnknownOwnershipCount           = unknownOwnership,
            ReconciledTotal                 = reconciledTotal,
            NeverReconciled                 = neverReconciled,
            ReconciliationUpdated           = reconUpdated,
            ReconciliationNoChange          = reconNoChange,
            ReconciliationLookupFailed      = reconLookupFailed,
            ReconciliationSkipped           = reconSkipped,
            ReconciliationProviderConfigFailed = reconProviderConfig,
            UniqueTenantCount               = tenantIds.Count,
            UniqueProviderCount             = providers.Count,
            UniqueProviderConfigCount       = providerConfigs.Count,
            EarliestAt                      = rows.Min(r => r.CreatedAt),
            LatestAt                        = rows.Max(r => r.CreatedAt),
        };
    }

    // ── Trends ────────────────────────────────────────────────────────────────

    public async Task<SmsDashboardTrendResult> GetTrendsAsync(
        SmsDashboardQuery query,
        DateTime windowFrom,
        DateTime windowTo,
        CancellationToken ct = default)
    {
        var rows = await BuildBaseQuery(query)
            .Where(a => a.CreatedAt >= windowFrom && a.CreatedAt <= windowTo)
            .Select(a => new
            {
                a.Status,
                a.ReconciliationAttemptCount,
                a.LastReconciliationOutcome,
                a.CreatedAt,
            })
            .ToListAsync(ct);

        var bucket = query.Bucket;

        // Group by bucket
        var grouped = rows
            .GroupBy(r => TruncateToBucket(r.CreatedAt, bucket))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var start = g.Key;
                int total = 0, sentC = 0, deliveredC = 0, failedC = 0, pendingC = 0,
                    reconTotal = 0, reconLookup = 0;

                foreach (var r in g)
                {
                    total++;
                    switch (r.Status)
                    {
                        case "sent":        sentC++;      break;
                        case "delivered":   deliveredC++; break;
                        case "failed":
                        case "dead_letter": failedC++;    break;
                        case "pending":
                        case "sending":
                        case "processing":
                        case "queued":
                        case "retrying":    pendingC++;   break;
                    }

                    if (r.ReconciliationAttemptCount > 0) reconTotal++;
                    if (string.Equals(r.LastReconciliationOutcome, "vendor_lookup_failed",
                        StringComparison.OrdinalIgnoreCase)) reconLookup++;
                }

                return new SmsDashboardTrendPointDto
                {
                    BucketStart              = start,
                    BucketEnd                = BucketEnd(start, bucket),
                    TotalAttempts            = total,
                    SentCount                = sentC,
                    DeliveredCount           = deliveredC,
                    FailedCount              = failedC,
                    PendingCount             = pendingC,
                    ReconciledTotal          = reconTotal,
                    ReconciliationLookupFailed = reconLookup,
                };
            })
            .ToList();

        return new SmsDashboardTrendResult
        {
            Bucket     = bucket,
            WindowFrom = windowFrom,
            WindowTo   = windowTo,
            Points     = grouped,
        };
    }

    // ── Failure breakdown ─────────────────────────────────────────────────────

    public async Task<SmsDashboardFailureResult> GetFailureBreakdownAsync(
        SmsDashboardQuery query,
        CancellationToken ct = default)
    {
        var rows = await BuildBaseQuery(query)
            .Where(a =>
                a.Status == "failed" ||
                a.Status == "dead_letter" ||
                a.FailureCategory != null)
            .Select(a => new
            {
                FailureCategory          = a.FailureCategory,
                a.LastReconciliationErrorCode,
                OccurredAt               = a.UpdatedAt,
            })
            .ToListAsync(ct);

        if (rows.Count == 0)
            return new SmsDashboardFailureResult { Items = Array.Empty<SmsDashboardFailureItemDto>(), TotalFailedAttempts = 0 };

        // Group by (FailureCategory ?? "unknown", LastReconciliationErrorCode)
        var grouped = rows
            .GroupBy(r => (
                Cat:      r.FailureCategory ?? "unknown",
                ErrCode:  r.LastReconciliationErrorCode))
            .OrderByDescending(g => g.Count())
            .Take(query.FailureBreakdownLimit)
            .Select(g => new SmsDashboardFailureItemDto
            {
                FailureCategory    = g.Key.Cat,
                ErrorCode          = string.IsNullOrEmpty(g.Key.ErrCode) ? null : g.Key.ErrCode,
                Count              = g.Count(),
                LatestOccurrenceAt = g.Max(r => r.OccurredAt),
            })
            .ToList();

        return new SmsDashboardFailureResult
        {
            Items               = grouped,
            TotalFailedAttempts = rows.Count,
        };
    }

    // ── Tenant breakdown ──────────────────────────────────────────────────────

    public async Task<SmsDashboardTenantResult> GetTenantBreakdownAsync(
        SmsDashboardQuery query,
        CancellationToken ct = default)
    {
        var rows = await BuildBaseQuery(query)
            .Select(a => new
            {
                a.TenantId,
                a.Status,
                OwnershipMode            = a.ProviderOwnershipMode,
                a.ReconciliationAttemptCount,
                a.CreatedAt,
            })
            .ToListAsync(ct);

        if (rows.Count == 0)
            return new SmsDashboardTenantResult { Items = Array.Empty<SmsDashboardTenantItemDto>(), TotalTenants = 0 };

        var grouped = rows
            .GroupBy(r => r.TenantId)
            .OrderByDescending(g => g.Count())
            .Take(query.TenantBreakdownLimit)
            .Select(g =>
            {
                int total = 0, sentC = 0, deliveredC = 0, failedC = 0, pendingC = 0,
                    reconTotal = 0, neverRecon = 0, tenantOwned = 0, platformOwned = 0;
                var latestAt = DateTime.MinValue;

                foreach (var r in g)
                {
                    total++;
                    switch (r.Status)
                    {
                        case "sent":        sentC++;      break;
                        case "delivered":   deliveredC++; break;
                        case "failed":
                        case "dead_letter": failedC++;    break;
                        default:            pendingC++;   break;
                    }
                    switch (r.OwnershipMode)
                    {
                        case "tenant":   tenantOwned++;   break;
                        case "platform": platformOwned++; break;
                    }
                    if (r.ReconciliationAttemptCount > 0) reconTotal++;
                    else neverRecon++;
                    if (r.CreatedAt > latestAt) latestAt = r.CreatedAt;
                }

                return new SmsDashboardTenantItemDto
                {
                    TenantId         = g.Key,
                    TotalAttempts    = total,
                    SentCount        = sentC,
                    DeliveredCount   = deliveredC,
                    FailedCount      = failedC,
                    PendingCount     = pendingC,
                    ReconciledTotal  = reconTotal,
                    NeverReconciled  = neverRecon,
                    TenantOwnedCount = tenantOwned,
                    PlatformOwnedCount = platformOwned,
                    LatestActivityAt = latestAt == DateTime.MinValue ? DateTime.UtcNow : latestAt,
                };
            })
            .ToList();

        return new SmsDashboardTenantResult
        {
            Items        = grouped,
            TotalTenants = rows.Select(r => r.TenantId).Distinct().Count(),
        };
    }

    // ── Provider breakdown ────────────────────────────────────────────────────

    public async Task<SmsDashboardProviderResult> GetProviderBreakdownAsync(
        SmsDashboardQuery query,
        CancellationToken ct = default)
    {
        var rows = await BuildBaseQuery(query)
            .Select(a => new
            {
                a.Provider,
                a.ProviderConfigId,
                OwnershipMode            = a.ProviderOwnershipMode,
                a.Status,
                a.ReconciliationAttemptCount,
                a.LastReconciliationOutcome,
                a.CreatedAt,
            })
            .ToListAsync(ct);

        if (rows.Count == 0)
            return new SmsDashboardProviderResult { Items = Array.Empty<SmsDashboardProviderItemDto>(), TotalProviderConfigs = 0 };

        var grouped = rows
            .GroupBy(r => (
                Provider:      r.Provider,
                ConfigId:      r.ProviderConfigId,
                OwnershipMode: r.OwnershipMode))
            .OrderByDescending(g => g.Count())
            .Take(query.ProviderBreakdownLimit)
            .Select(g =>
            {
                int total = 0, sentC = 0, deliveredC = 0, failedC = 0,
                    reconTotal = 0, reconLookup = 0;
                var latestAt = DateTime.MinValue;

                foreach (var r in g)
                {
                    total++;
                    switch (r.Status)
                    {
                        case "sent":        sentC++;      break;
                        case "delivered":   deliveredC++; break;
                        case "failed":
                        case "dead_letter": failedC++;    break;
                    }
                    if (r.ReconciliationAttemptCount > 0) reconTotal++;
                    if (string.Equals(r.LastReconciliationOutcome, "vendor_lookup_failed",
                        StringComparison.OrdinalIgnoreCase)) reconLookup++;
                    if (r.CreatedAt > latestAt) latestAt = r.CreatedAt;
                }

                return new SmsDashboardProviderItemDto
                {
                    Provider                   = g.Key.Provider,
                    ProviderConfigId           = g.Key.ConfigId,
                    ProviderOwnershipMode      = g.Key.OwnershipMode ?? "unknown",
                    TotalAttempts              = total,
                    SentCount                  = sentC,
                    DeliveredCount             = deliveredC,
                    FailedCount                = failedC,
                    ReconciledTotal            = reconTotal,
                    ReconciliationLookupFailed = reconLookup,
                    LatestActivityAt           = latestAt == DateTime.MinValue ? DateTime.UtcNow : latestAt,
                };
            })
            .ToList();

        return new SmsDashboardProviderResult
        {
            Items              = grouped,
            TotalProviderConfigs = rows.Select(r => r.ProviderConfigId).Distinct().Count(),
        };
    }

    // ── Query builder ─────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the base SMS dashboard filter query.
    /// Always filters Channel = "sms". Never touches CredentialsJson or SettingsJson.
    /// </summary>
    private IQueryable<Domain.NotificationAttempt> BuildBaseQuery(SmsDashboardQuery query)
    {
        var q = _db.NotificationAttempts.Where(a => a.Channel == "sms");

        if (query.TenantId.HasValue)
        {
            var tid = query.TenantId.Value;
            q = q.Where(a => a.TenantId == tid);
        }

        if (!string.IsNullOrWhiteSpace(query.Provider))
        {
            var prov = query.Provider.Trim().ToLowerInvariant();
            q = q.Where(a => a.Provider == prov);
        }

        if (query.ProviderConfigId.HasValue)
        {
            var pcid = query.ProviderConfigId.Value;
            q = q.Where(a => a.ProviderConfigId == pcid);
        }

        if (!string.IsNullOrWhiteSpace(query.ProviderOwnershipMode))
        {
            var mode = query.ProviderOwnershipMode.Trim().ToLowerInvariant();
            q = q.Where(a => a.ProviderOwnershipMode == mode);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim().ToLowerInvariant();
            q = q.Where(a => a.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.FailureCategory))
        {
            var cat = query.FailureCategory.Trim().ToLowerInvariant();
            q = q.Where(a => a.FailureCategory == cat);
        }

        if (query.From.HasValue)
        {
            var from = query.From.Value.ToUniversalTime();
            q = q.Where(a => a.CreatedAt >= from);
        }

        if (query.To.HasValue)
        {
            var to = query.To.Value.ToUniversalTime();
            q = q.Where(a => a.CreatedAt <= to);
        }

        return q;
    }

    // ── Trend bucketing helpers ───────────────────────────────────────────────

    private static DateTime TruncateToBucket(DateTime utc, string bucket)
        => bucket switch
        {
            "hour" => new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc),
            "week" => TruncateToWeekStart(utc),
            _      => new DateTime(utc.Year, utc.Month, utc.Day, 0, 0, 0, DateTimeKind.Utc),
        };

    /// <summary>Returns the UTC Monday of the week containing <paramref name="utc"/>.</summary>
    private static DateTime TruncateToWeekStart(DateTime utc)
    {
        var day = utc.Date;
        // DayOfWeek: Mon=1 Tue=2 ... Sun=0; shift so Mon=0 Sun=6
        var daysSinceMonday = ((int)day.DayOfWeek + 6) % 7;
        return DateTime.SpecifyKind(day.AddDays(-daysSinceMonday), DateTimeKind.Utc);
    }

    private static DateTime BucketEnd(DateTime bucketStart, string bucket)
        => bucket switch
        {
            "hour" => bucketStart.AddHours(1).AddTicks(-1),
            "week" => bucketStart.AddDays(7).AddTicks(-1),
            _      => bucketStart.AddDays(1).AddTicks(-1),
        };
}
