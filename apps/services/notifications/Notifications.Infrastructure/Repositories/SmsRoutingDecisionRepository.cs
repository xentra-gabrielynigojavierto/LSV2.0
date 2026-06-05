using Microsoft.EntityFrameworkCore;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Repositories;

public class SmsRoutingDecisionRepository : ISmsRoutingDecisionRepository
{
    private readonly NotificationsDbContext _db;

    public SmsRoutingDecisionRepository(NotificationsDbContext db) => _db = db;

    public async Task<SmsRoutingDecision> CreateAsync(SmsRoutingDecision decision, CancellationToken ct = default)
    {
        _db.SmsRoutingDecisions.Add(decision);
        await _db.SaveChangesAsync(ct);
        return decision;
    }

    public async Task UpdateAttemptIdAsync(Guid decisionId, Guid attemptId, CancellationToken ct = default)
    {
        var decision = await _db.SmsRoutingDecisions.FindAsync(new object[] { decisionId }, ct);
        if (decision != null)
        {
            decision.AttemptId = attemptId;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<(IReadOnlyList<SmsRoutingDecision> Items, int Total)> ListAsync(
        SmsRoutingDecisionQuery query, CancellationToken ct = default)
    {
        var q = _db.SmsRoutingDecisions.AsNoTracking().AsQueryable();

        if (query.TenantId.HasValue)
            q = q.Where(d => d.TenantId == query.TenantId);
        if (query.NotificationId.HasValue)
            q = q.Where(d => d.NotificationId == query.NotificationId);
        if (!string.IsNullOrEmpty(query.Provider))
            q = q.Where(d => d.SelectedProvider == query.Provider);
        if (!string.IsNullOrEmpty(query.RoutingMode))
            q = q.Where(d => d.RoutingMode == query.RoutingMode);
        if (query.PolicyId.HasValue)
            q = q.Where(d => d.RoutingPolicyId == query.PolicyId);
        if (query.From.HasValue)
            q = q.Where(d => d.CreatedAt >= query.From.Value);
        if (query.To.HasValue)
            q = q.Where(d => d.CreatedAt <= query.To.Value);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(d => d.CreatedAt)
            .Skip(query.Offset)
            .Take(Math.Max(1, Math.Min(query.Limit, 200)))
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<SmsRoutingDecisionSummaryDto> GetSummaryAsync(
        SmsRoutingDecisionQuery query, CancellationToken ct = default)
    {
        var q = _db.SmsRoutingDecisions.AsNoTracking().AsQueryable();

        if (query.TenantId.HasValue)   q = q.Where(d => d.TenantId       == query.TenantId);
        if (query.From.HasValue)       q = q.Where(d => d.CreatedAt       >= query.From.Value);
        if (query.To.HasValue)         q = q.Where(d => d.CreatedAt       <= query.To.Value);
        if (!string.IsNullOrEmpty(query.Provider))
            q = q.Where(d => d.SelectedProvider == query.Provider);

        var all = await q.ToListAsync(ct);

        return new SmsRoutingDecisionSummaryDto
        {
            TotalDecisions     = all.Count,
            ByMode             = all.GroupBy(d => d.RoutingMode).ToDictionary(g => g.Key, g => (long)g.Count()),
            ByProvider         = all.GroupBy(d => d.SelectedProvider).ToDictionary(g => g.Key, g => (long)g.Count()),
            PriorityModeCount  = all.Count(d => d.RoutingMode == "priority"),
            CostOptimizedCount = all.Count(d => d.RoutingMode == "cost_optimized"),
            HealthOptimizedCount = all.Count(d => d.RoutingMode == "health_optimized"),
            HybridCount        = all.Count(d => d.RoutingMode == "hybrid"),
            RegionalCount      = all.Count(d => d.RoutingMode == "regional"),
            NoRouteCount       = all.Count(d => d.SelectedProvider == "no_route"),
        };
    }

    // ── Static mapper ─────────────────────────────────────────────────────────

    public static SmsRoutingDecisionDto ToDto(SmsRoutingDecision d) => new()
    {
        Id                       = d.Id,
        TenantId                 = d.TenantId,
        NotificationId           = d.NotificationId,
        AttemptId                = d.AttemptId,
        RoutingPolicyId          = d.RoutingPolicyId,
        RoutingMode              = d.RoutingMode,
        SelectedProvider         = d.SelectedProvider,
        SelectedProviderConfigId = d.SelectedProviderConfigId,
        ProviderOwnershipMode    = d.ProviderOwnershipMode,
        CandidateProvidersJson   = d.CandidateProvidersJson,
        ExcludedProvidersJson    = d.ExcludedProvidersJson,
        DecisionReason           = d.DecisionReason,
        EstimatedCostAmount      = d.EstimatedCostAmount,
        CostCurrency             = d.CostCurrency,
        Region                   = d.Region,
        CountryCode              = d.CountryCode,
        CreatedAt                = d.CreatedAt,
    };
}
