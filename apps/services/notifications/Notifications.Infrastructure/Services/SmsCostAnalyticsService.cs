using Microsoft.Extensions.Logging;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-013: Orchestrates SMS cost analytics queries.
///
/// Responsibilities:
///  - Validates and normalises bucket parameter (invalid values default to "day").
///  - Applies default date window (last 30 days) when From/To are not supplied for trends.
///  - Clamps breakdown limits to safe bounds.
///  - Delegates all aggregation to <see cref="ISmsCostAnalyticsRepository"/>.
///  - Read-only — never triggers sends, retries, reconciliation, or provider calls.
///  - Never records, modifies, or deletes cost data.
/// </summary>
public sealed class SmsCostAnalyticsService : ISmsCostAnalyticsService
{
    private readonly ISmsCostAnalyticsRepository _repo;
    private readonly ILogger<SmsCostAnalyticsService> _logger;

    private static readonly HashSet<string> ValidBuckets =
        new(StringComparer.OrdinalIgnoreCase) { "hour", "day", "week" };

    private const int DefaultTrendDays = 30;

    public SmsCostAnalyticsService(ISmsCostAnalyticsRepository repo, ILogger<SmsCostAnalyticsService> logger)
    {
        _repo   = repo;
        _logger = logger;
    }

    public Task<SmsCostSummaryDto> GetSummaryAsync(SmsCostQuery query, CancellationToken ct = default)
    {
        _logger.LogDebug("SMS cost analytics: summary tenantId={TenantId}", query.TenantId);
        return _repo.GetSummaryAsync(query, ct);
    }

    public async Task<SmsCostTrendResult> GetTrendsAsync(SmsCostQuery query, CancellationToken ct = default)
    {
        query.Bucket = ValidBuckets.Contains(query.Bucket ?? "")
            ? query.Bucket!.Trim().ToLowerInvariant()
            : "day";

        var now        = DateTime.UtcNow;
        var windowTo   = query.To.HasValue   ? query.To.Value.ToUniversalTime()   : now;
        var windowFrom = query.From.HasValue  ? query.From.Value.ToUniversalTime() : now.AddDays(-DefaultTrendDays);

        if (windowFrom > windowTo) windowFrom = windowTo.AddDays(-DefaultTrendDays);

        _logger.LogDebug(
            "SMS cost analytics: trends tenantId={TenantId} bucket={Bucket} from={From:O} to={To:O}",
            query.TenantId, query.Bucket, windowFrom, windowTo);

        return await _repo.GetTrendsAsync(query, windowFrom, windowTo, ct);
    }

    public Task<SmsCostProviderResult> GetProviderBreakdownAsync(SmsCostQuery query, CancellationToken ct = default)
    {
        query.ProviderBreakdownLimit = Math.Clamp(query.ProviderBreakdownLimit, 1, 500);
        _logger.LogDebug("SMS cost analytics: provider breakdown tenantId={TenantId}", query.TenantId);
        return _repo.GetProviderBreakdownAsync(query, ct);
    }

    public Task<SmsCostTenantResult> GetTenantBreakdownAsync(SmsCostQuery query, CancellationToken ct = default)
    {
        query.TenantBreakdownLimit = Math.Clamp(query.TenantBreakdownLimit, 1, 500);
        _logger.LogDebug("SMS cost analytics: tenant breakdown tenantId={TenantId}", query.TenantId);
        return _repo.GetTenantBreakdownAsync(query, ct);
    }

    public Task<SmsCostFailureResult> GetFailureCostBreakdownAsync(SmsCostQuery query, CancellationToken ct = default)
    {
        query.FailureBreakdownLimit = Math.Clamp(query.FailureBreakdownLimit, 1, 200);
        _logger.LogDebug("SMS cost analytics: failure cost breakdown tenantId={TenantId}", query.TenantId);
        return _repo.GetFailureCostBreakdownAsync(query, ct);
    }

    public Task<SmsCostExportResult> ExportAsync(SmsCostQuery query, CancellationToken ct = default)
    {
        query.ExportLimit = Math.Clamp(query.ExportLimit, 1, 10_000);
        _logger.LogDebug("SMS cost analytics: export tenantId={TenantId} limit={Limit}", query.TenantId, query.ExportLimit);
        return _repo.ExportAsync(query, ct);
    }
}
