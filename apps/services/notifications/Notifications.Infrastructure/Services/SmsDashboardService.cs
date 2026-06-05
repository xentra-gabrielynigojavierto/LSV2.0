using Microsoft.Extensions.Logging;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-008: Orchestrates SMS dashboard queries.
///
/// Responsibilities:
///  - Validates and normalises bucket parameter (invalid values default to "day").
///  - Applies default date window (last 30 days) when From/To are not supplied for trends.
///  - Delegates all aggregation to <see cref="ISmsDashboardRepository"/>.
///  - Read-only — never triggers sends, retries, reconciliation, or provider calls.
/// </summary>
public sealed class SmsDashboardService : ISmsDashboardService
{
    private readonly ISmsDashboardRepository _repo;
    private readonly ILogger<SmsDashboardService> _logger;

    private static readonly HashSet<string> ValidBuckets = new(StringComparer.OrdinalIgnoreCase)
        { "hour", "day", "week" };

    // Default trend window when no from/to is supplied.
    private const int DefaultTrendDays = 30;

    public SmsDashboardService(ISmsDashboardRepository repo, ILogger<SmsDashboardService> logger)
    {
        _repo   = repo;
        _logger = logger;
    }

    public Task<SmsDashboardSummaryDto> GetSummaryAsync(SmsDashboardQuery query, CancellationToken ct = default)
    {
        _logger.LogDebug("SMS dashboard: summary tenantId={TenantId} provider={Provider}", query.TenantId, query.Provider);
        return _repo.GetSummaryAsync(query, ct);
    }

    public async Task<SmsDashboardTrendResult> GetTrendsAsync(SmsDashboardQuery query, CancellationToken ct = default)
    {
        // Normalise bucket
        query.Bucket = ValidBuckets.Contains(query.Bucket ?? "")
            ? query.Bucket!.Trim().ToLowerInvariant()
            : "day";

        // Apply default bounded window
        var now       = DateTime.UtcNow;
        var windowTo  = query.To.HasValue   ? query.To.Value.ToUniversalTime()   : now;
        var windowFrom = query.From.HasValue ? query.From.Value.ToUniversalTime() : now.AddDays(-DefaultTrendDays);

        // Guard: from must not exceed to
        if (windowFrom > windowTo) windowFrom = windowTo.AddDays(-DefaultTrendDays);

        _logger.LogDebug(
            "SMS dashboard: trends tenantId={TenantId} bucket={Bucket} from={From:O} to={To:O}",
            query.TenantId, query.Bucket, windowFrom, windowTo);

        return await _repo.GetTrendsAsync(query, windowFrom, windowTo, ct);
    }

    public Task<SmsDashboardFailureResult> GetFailureBreakdownAsync(SmsDashboardQuery query, CancellationToken ct = default)
    {
        query.FailureBreakdownLimit = Math.Max(1, Math.Min(query.FailureBreakdownLimit, 200));
        _logger.LogDebug("SMS dashboard: failures tenantId={TenantId}", query.TenantId);
        return _repo.GetFailureBreakdownAsync(query, ct);
    }

    public Task<SmsDashboardTenantResult> GetTenantBreakdownAsync(SmsDashboardQuery query, CancellationToken ct = default)
    {
        query.TenantBreakdownLimit = Math.Max(1, Math.Min(query.TenantBreakdownLimit, 500));
        _logger.LogDebug("SMS dashboard: tenant breakdown tenantId={TenantId}", query.TenantId);
        return _repo.GetTenantBreakdownAsync(query, ct);
    }

    public Task<SmsDashboardProviderResult> GetProviderBreakdownAsync(SmsDashboardQuery query, CancellationToken ct = default)
    {
        query.ProviderBreakdownLimit = Math.Max(1, Math.Min(query.ProviderBreakdownLimit, 500));
        _logger.LogDebug("SMS dashboard: provider breakdown tenantId={TenantId}", query.TenantId);
        return _repo.GetProviderBreakdownAsync(query, ct);
    }
}
