using Microsoft.EntityFrameworkCore;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Repositories;

/// <summary>
/// LS-NOTIF-SMS-013: Read-only EF Core aggregation repository for SMS cost analytics.
///
/// All queries:
///  - Filter Channel = "sms" as the primary predicate.
///  - Never project CredentialsJson, SettingsJson, RecipientJson, or phone numbers.
///  - Never trigger sends, retries, reconciliation, or provider calls.
///  - Follow the SmsDashboardRepository pattern: minimal projection then in-memory aggregation.
///
/// Effective cost rule: ActualCostAmount ?? EstimatedCostAmount (prefer actual if available).
/// </summary>
public sealed class SmsCostAnalyticsRepository : ISmsCostAnalyticsRepository
{
    private readonly NotificationsDbContext _db;

    public SmsCostAnalyticsRepository(NotificationsDbContext db) => _db = db;

    // ── Summary ───────────────────────────────────────────────────────────────

    public async Task<SmsCostSummaryDto> GetSummaryAsync(SmsCostQuery query, CancellationToken ct = default)
    {
        var rows = await BuildBaseQuery(query)
            .Select(a => new
            {
                a.TenantId,
                a.Status,
                OwnershipMode         = a.ProviderOwnershipMode,
                a.IsFailover,
                a.AttemptNumber,
                a.EstimatedCostAmount,
                a.ActualCostAmount,
                a.CostCurrency,
                a.CostSource,
                a.CreatedAt,
            })
            .ToListAsync(ct);

        if (rows.Count == 0)
            return new SmsCostSummaryDto();

        var defaultCurrency = DominantCurrency(rows.Select(r => r.CostCurrency));

        decimal totalEst = 0m, totalActual = 0m, totalEffective = 0m;
        decimal deliveredCost = 0m, sentCost = 0m, failedCost = 0m, deadLetterCost = 0m,
                retryCost = 0m, tenantOwnedCost = 0m, platformOwnedCost = 0m;
        int deliveredCount = 0, failedCount = 0;
        int costed = 0, uncosted = 0, estimatedCount = 0, reconciledCount = 0, unavailableCount = 0;
        var earliest = DateTime.MaxValue;
        var latest   = DateTime.MinValue;

        foreach (var r in rows)
        {
            var eff = Effective(r.EstimatedCostAmount, r.ActualCostAmount);
            var est = r.EstimatedCostAmount ?? 0m;
            var act = r.ActualCostAmount ?? 0m;

            if (r.CostSource != null && r.CostSource != "unavailable")
            {
                costed++;
                totalEst       += est;
                totalActual    += act;
                totalEffective += eff;

                switch (r.CostSource)
                {
                    case "estimated":            estimatedCount++;  break;
                    case "provider_reconciled":  reconciledCount++; break;
                }
            }
            else
            {
                uncosted++;
                if (r.CostSource == "unavailable") unavailableCount++;
            }

            // by delivery status
            switch (r.Status)
            {
                case "delivered":   deliveredCost   += eff; deliveredCount++; break;
                case "sent":        sentCost        += eff; break;
                case "failed":      failedCost      += eff; failedCount++;    break;
                case "dead_letter": deadLetterCost  += eff; failedCount++;    break;
            }

            // retry hops
            if (r.IsFailover || r.AttemptNumber > 1) retryCost += eff;

            // ownership
            switch (r.OwnershipMode)
            {
                case "tenant":   tenantOwnedCost   += eff; break;
                case "platform": platformOwnedCost += eff; break;
            }

            if (r.CreatedAt < earliest) earliest = r.CreatedAt;
            if (r.CreatedAt > latest)   latest   = r.CreatedAt;
        }

        return new SmsCostSummaryDto
        {
            TotalAttempts             = rows.Count,
            CostedAttempts            = costed,
            UncostedAttempts          = uncosted,
            TotalEffectiveCost        = totalEffective,
            TotalEstimatedCost        = totalEst,
            TotalActualCost           = totalActual,
            DeliveredCost             = deliveredCost,
            SentCost                  = sentCost,
            FailedCost                = failedCost,
            DeadLetterCost            = deadLetterCost,
            RetryCost                 = retryCost,
            TenantOwnedCost           = tenantOwnedCost,
            PlatformOwnedCost         = platformOwnedCost,
            CostPerDeliveredMessage   = deliveredCount > 0 ? deliveredCost / deliveredCount : null,
            DeliveredCount            = deliveredCount,
            FailedCount               = failedCount,
            Currency                  = defaultCurrency,
            EstimatedCostCount        = estimatedCount,
            ProviderReconciledCount   = reconciledCount,
            UnavailableCount          = unavailableCount,
            EarliestAt                = earliest == DateTime.MaxValue ? null : earliest,
            LatestAt                  = latest   == DateTime.MinValue ? null : latest,
        };
    }

    // ── Trends ────────────────────────────────────────────────────────────────

    public async Task<SmsCostTrendResult> GetTrendsAsync(
        SmsCostQuery query,
        DateTime windowFrom,
        DateTime windowTo,
        CancellationToken ct = default)
    {
        var rows = await BuildBaseQuery(query)
            .Where(a => a.CreatedAt >= windowFrom && a.CreatedAt <= windowTo)
            .Select(a => new
            {
                a.Status,
                a.IsFailover,
                a.AttemptNumber,
                a.EstimatedCostAmount,
                a.ActualCostAmount,
                a.CostSource,
                a.CostCurrency,
                a.CreatedAt,
            })
            .ToListAsync(ct);

        var bucket          = query.Bucket;
        var defaultCurrency = DominantCurrency(rows.Select(r => r.CostCurrency));

        var grouped = rows
            .GroupBy(r => TruncateToBucket(r.CreatedAt, bucket))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                decimal eff = 0m, dlv = 0m, fail = 0m, retry = 0m;
                int total = 0, costedC = 0;
                foreach (var r in g)
                {
                    total++;
                    var e = Effective(r.EstimatedCostAmount, r.ActualCostAmount);
                    if (r.CostSource != null && r.CostSource != "unavailable") { eff += e; costedC++; }

                    switch (r.Status)
                    {
                        case "delivered": dlv  += e; break;
                        case "failed":
                        case "dead_letter": fail += e; break;
                    }
                    if (r.IsFailover || r.AttemptNumber > 1) retry += e;
                }
                var start = g.Key;
                return new SmsCostTrendPointDto
                {
                    BucketStart         = start,
                    BucketEnd           = BucketEnd(start, bucket),
                    TotalAttempts       = total,
                    CostedAttempts      = costedC,
                    TotalEffectiveCost  = eff,
                    DeliveredCost       = dlv,
                    FailedCost          = fail,
                    RetryCost           = retry,
                    Currency            = defaultCurrency,
                };
            })
            .ToList();

        return new SmsCostTrendResult
        {
            Bucket     = bucket,
            WindowFrom = windowFrom,
            WindowTo   = windowTo,
            Points     = grouped,
            Currency   = defaultCurrency,
        };
    }

    // ── Provider breakdown ────────────────────────────────────────────────────

    public async Task<SmsCostProviderResult> GetProviderBreakdownAsync(SmsCostQuery query, CancellationToken ct = default)
    {
        var rows = await BuildBaseQuery(query)
            .Select(a => new
            {
                a.Provider,
                a.ProviderConfigId,
                OwnershipMode         = a.ProviderOwnershipMode,
                a.Status,
                a.EstimatedCostAmount,
                a.ActualCostAmount,
                a.CostSource,
                a.CostCurrency,
                a.CreatedAt,
            })
            .ToListAsync(ct);

        if (rows.Count == 0)
            return new SmsCostProviderResult();

        var defaultCurrency = DominantCurrency(rows.Select(r => r.CostCurrency));
        decimal grandTotal = 0m;

        var items = rows
            .GroupBy(r => (Provider: r.Provider, ConfigId: r.ProviderConfigId, OwnershipMode: r.OwnershipMode))
            .OrderByDescending(g => g.Sum(r => Effective(r.EstimatedCostAmount, r.ActualCostAmount)))
            .Take(query.ProviderBreakdownLimit)
            .Select(g =>
            {
                int total = 0, dlv = 0, fail = 0, costedC = 0;
                decimal eff = 0m;
                DateTime? latest = null;

                foreach (var r in g)
                {
                    total++;
                    var e = Effective(r.EstimatedCostAmount, r.ActualCostAmount);
                    if (r.CostSource != null && r.CostSource != "unavailable") { eff += e; costedC++; grandTotal += e; }

                    switch (r.Status)
                    {
                        case "delivered": dlv++;  break;
                        case "failed":
                        case "dead_letter": fail++; break;
                    }
                    if (!latest.HasValue || r.CreatedAt > latest.Value) latest = r.CreatedAt;
                }

                return new SmsCostProviderItemDto
                {
                    Provider                 = g.Key.Provider,
                    ProviderConfigId         = g.Key.ConfigId,
                    ProviderOwnershipMode    = g.Key.OwnershipMode ?? "unknown",
                    TotalAttempts            = total,
                    DeliveredAttempts        = dlv,
                    FailedAttempts           = fail,
                    CostedAttempts           = costedC,
                    TotalEffectiveCost       = eff,
                    CostPerDeliveredMessage  = dlv > 0 ? eff / dlv : null,
                    Currency                 = defaultCurrency,
                    LatestActivityAt         = latest,
                };
            })
            .ToList();

        return new SmsCostProviderResult
        {
            Items                    = items,
            TotalProviderConfigs     = rows.Select(r => r.ProviderConfigId).Distinct().Count(),
            GrandTotalEffectiveCost  = grandTotal,
            Currency                 = defaultCurrency,
        };
    }

    // ── Tenant breakdown ──────────────────────────────────────────────────────

    public async Task<SmsCostTenantResult> GetTenantBreakdownAsync(SmsCostQuery query, CancellationToken ct = default)
    {
        var rows = await BuildBaseQuery(query)
            .Select(a => new
            {
                a.TenantId,
                a.Status,
                a.EstimatedCostAmount,
                a.ActualCostAmount,
                a.CostSource,
                a.CostCurrency,
                a.CreatedAt,
            })
            .ToListAsync(ct);

        if (rows.Count == 0)
            return new SmsCostTenantResult();

        var defaultCurrency = DominantCurrency(rows.Select(r => r.CostCurrency));
        decimal grandTotal = 0m;

        var items = rows
            .GroupBy(r => r.TenantId)
            .OrderByDescending(g => g.Sum(r => Effective(r.EstimatedCostAmount, r.ActualCostAmount)))
            .Take(query.TenantBreakdownLimit)
            .Select(g =>
            {
                int total = 0, dlv = 0, fail = 0, costedC = 0;
                decimal eff = 0m;
                DateTime? latest = null;

                foreach (var r in g)
                {
                    total++;
                    var e = Effective(r.EstimatedCostAmount, r.ActualCostAmount);
                    if (r.CostSource != null && r.CostSource != "unavailable") { eff += e; costedC++; grandTotal += e; }

                    switch (r.Status)
                    {
                        case "delivered": dlv++;  break;
                        case "failed":
                        case "dead_letter": fail++; break;
                    }
                    if (!latest.HasValue || r.CreatedAt > latest.Value) latest = r.CreatedAt;
                }

                return new SmsCostTenantItemDto
                {
                    TenantId                = g.Key,
                    TotalAttempts           = total,
                    DeliveredAttempts       = dlv,
                    FailedAttempts          = fail,
                    CostedAttempts          = costedC,
                    TotalEffectiveCost      = eff,
                    CostPerDeliveredMessage = dlv > 0 ? eff / dlv : null,
                    Currency                = defaultCurrency,
                    LatestActivityAt        = latest,
                };
            })
            .ToList();

        return new SmsCostTenantResult
        {
            Items                   = items,
            TotalTenants            = rows.Select(r => r.TenantId).Distinct().Count(),
            GrandTotalEffectiveCost = grandTotal,
            Currency                = defaultCurrency,
        };
    }

    // ── Failure / retry cost breakdown ────────────────────────────────────────

    public async Task<SmsCostFailureResult> GetFailureCostBreakdownAsync(SmsCostQuery query, CancellationToken ct = default)
    {
        var rows = await BuildBaseQuery(query)
            .Where(a => a.Status == "failed" || a.Status == "dead_letter" || a.FailureCategory != null)
            .Select(a => new
            {
                FailureCategory       = a.FailureCategory,
                a.IsFailover,
                a.AttemptNumber,
                a.Status,
                a.EstimatedCostAmount,
                a.ActualCostAmount,
                a.CostSource,
                a.CostCurrency,
                UpdatedAt             = a.UpdatedAt,
            })
            .ToListAsync(ct);

        if (rows.Count == 0)
            return new SmsCostFailureResult();

        var defaultCurrency = DominantCurrency(rows.Select(r => r.CostCurrency));
        decimal totalFailed = 0m, totalRetry = 0m;

        var items = rows
            .GroupBy(r => (Cat: r.FailureCategory ?? "unknown", IsRetry: r.IsFailover || r.AttemptNumber > 1))
            .OrderByDescending(g => g.Count())
            .Take(query.FailureBreakdownLimit)
            .Select(g =>
            {
                int count = 0, costedC = 0;
                decimal eff = 0m;
                DateTime? latest = null;

                foreach (var r in g)
                {
                    count++;
                    var e = Effective(r.EstimatedCostAmount, r.ActualCostAmount);
                    if (r.CostSource != null && r.CostSource != "unavailable") { eff += e; costedC++; }
                    if (g.Key.IsRetry) totalRetry += e; else totalFailed += e;
                    if (!latest.HasValue || r.UpdatedAt > latest.Value) latest = r.UpdatedAt;
                }

                return new SmsCostFailureItemDto
                {
                    FailureCategory      = g.Key.Cat,
                    IsRetry              = g.Key.IsRetry,
                    Count                = count,
                    CostedCount          = costedC,
                    TotalEffectiveCost   = eff,
                    Currency             = defaultCurrency,
                    LatestOccurrenceAt   = latest,
                };
            })
            .ToList();

        return new SmsCostFailureResult
        {
            Items               = items,
            TotalFailedAttempts = rows.Count,
            TotalFailedCost     = totalFailed,
            TotalRetryCost      = totalRetry,
            Currency            = defaultCurrency,
        };
    }

    // ── Export ────────────────────────────────────────────────────────────────

    public async Task<SmsCostExportResult> ExportAsync(SmsCostQuery query, CancellationToken ct = default)
    {
        var limit = Math.Clamp(query.ExportLimit, 1, 10_000);

        var rows = await BuildBaseQuery(query)
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit + 1)
            .Select(a => new SmsCostExportRowDto
            {
                AttemptId             = a.Id,
                NotificationId        = a.NotificationId,
                TenantId              = a.TenantId,
                Provider              = a.Provider,
                ProviderConfigId      = a.ProviderConfigId,
                ProviderOwnershipMode = a.ProviderOwnershipMode,
                Status                = a.Status,
                FailureCategory       = a.FailureCategory,
                AttemptNumber         = a.AttemptNumber,
                IsRetry               = a.IsFailover || a.AttemptNumber > 1,
                EstimatedCostAmount   = a.EstimatedCostAmount,
                ActualCostAmount      = a.ActualCostAmount,
                EffectiveCostAmount   = a.ActualCostAmount ?? a.EstimatedCostAmount,
                CostCurrency          = a.CostCurrency,
                CostSource            = a.CostSource,
                CostRecordedAt        = a.CostRecordedAt,
                CreatedAt             = a.CreatedAt,
                CompletedAt           = a.CompletedAt,
            })
            .ToListAsync(ct);

        var truncated = rows.Count > limit;
        if (truncated) rows.RemoveAt(rows.Count - 1);

        var currency = DominantCurrency(rows.Select(r => r.CostCurrency));

        return new SmsCostExportResult
        {
            Rows        = rows,
            TotalRows   = rows.Count,
            Truncated   = truncated,
            Limit       = limit,
            Currency    = currency,
            GeneratedAt = DateTime.UtcNow,
        };
    }

    // ── Base query builder ────────────────────────────────────────────────────

    private IQueryable<Domain.NotificationAttempt> BuildBaseQuery(SmsCostQuery query)
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

        if (!string.IsNullOrWhiteSpace(query.CostSource))
        {
            var cs = query.CostSource.Trim().ToLowerInvariant();
            q = q.Where(a => a.CostSource == cs);
        }

        if (!string.IsNullOrWhiteSpace(query.Currency))
        {
            var cur = query.Currency.Trim().ToUpperInvariant();
            q = q.Where(a => a.CostCurrency == cur);
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static decimal Effective(decimal? estimated, decimal? actual)
        => actual ?? estimated ?? 0m;

    private static string DominantCurrency(IEnumerable<string?> currencies)
    {
        var dominant = currencies
            .Where(c => !string.IsNullOrEmpty(c))
            .GroupBy(c => c!)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key;
        return dominant ?? "USD";
    }

    private static DateTime TruncateToBucket(DateTime utc, string bucket)
        => bucket switch
        {
            "hour" => new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc),
            "week" => TruncateToWeekStart(utc),
            _      => new DateTime(utc.Year, utc.Month, utc.Day, 0, 0, 0, DateTimeKind.Utc),
        };

    private static DateTime TruncateToWeekStart(DateTime utc)
    {
        var day = utc.Date;
        var daysSinceMonday = ((int)day.DayOfWeek + 6) % 7;
        return DateTime.SpecifyKind(day.AddDays(-daysSinceMonday), DateTimeKind.Utc);
    }

    private static DateTime BucketEnd(DateTime start, string bucket)
        => bucket switch
        {
            "hour" => start.AddHours(1).AddTicks(-1),
            "week" => start.AddDays(7).AddTicks(-1),
            _      => start.AddDays(1).AddTicks(-1),
        };
}
