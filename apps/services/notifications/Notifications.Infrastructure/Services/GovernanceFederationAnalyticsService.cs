using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Services;

public sealed class GovernanceFederationAnalyticsService : IGovernanceFederationAnalyticsService
{
    private readonly NotificationsDbContext                       _db;
    private readonly ILogger<GovernanceFederationAnalyticsService> _logger;

    public GovernanceFederationAnalyticsService(
        NotificationsDbContext db,
        ILogger<GovernanceFederationAnalyticsService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<TopologyAnalyticsResult> GetTopologyAnalyticsAsync(
        FederationAnalyticsQuery query, CancellationToken ct = default)
    {
        var warnings = new List<string>();

        var totalScopes   = await _db.GovernanceChannelScopes.CountAsync(ct);
        var enabledScopes = await _db.GovernanceChannelScopes.CountAsync(s => s.Enabled, ct);
        var totalFedPacks  = await _db.GovernanceFederatedRulePacks.CountAsync(ct);
        var enabledFedPacks = await _db.GovernanceFederatedRulePacks.CountAsync(s => s.Enabled, ct);
        var totalOverlays  = await _db.GovernanceFederationOverlays.CountAsync(ct);
        var activeOverlays = await _db.GovernanceFederationOverlays
            .CountAsync(o => o.Enabled && o.OverlayState == GovernanceFederationOverlay.OverlayStates.Active, ct);
        var totalAuditEvents = await _db.GovernanceFederationAuditEvents.CountAsync(ct);

        // Per-channel breakdown
        var scopes = await _db.GovernanceChannelScopes.AsNoTracking().ToListAsync(ct);
        var byChannel = new List<ChannelGovernanceStats>();
        foreach (var scope in scopes)
        {
            var fedPackCount = await _db.GovernanceFederatedRulePacks
                .CountAsync(fp => fp.ChannelType == scope.ChannelType && fp.Enabled, ct);
            var overlayCount = await _db.GovernanceFederationOverlays
                .CountAsync(o => o.ChannelType == scope.ChannelType && o.Enabled &&
                                 o.OverlayState == GovernanceFederationOverlay.OverlayStates.Active, ct);
            var tenantCount = await _db.GovernanceFederatedRulePacks
                .Where(fp => fp.ChannelType == scope.ChannelType && fp.Enabled && fp.TenantId != null)
                .Select(fp => fp.TenantId)
                .Distinct()
                .CountAsync(ct);

            byChannel.Add(new ChannelGovernanceStats(
                scope.ChannelType, enabledScopes, fedPackCount, overlayCount, tenantCount,
                scope.ScopeMode, scope.Enabled));
        }

        if (!byChannel.Any())
            warnings.Add("No channel scopes configured — federation is not yet active");

        return new TopologyAnalyticsResult(
            totalScopes, enabledScopes, totalFedPacks, enabledFedPacks,
            totalOverlays, activeOverlays, totalAuditEvents, byChannel, warnings);
    }

    public async Task<ChannelGovernanceAnalyticsResult> GetChannelGovernanceAnalyticsAsync(
        FederationAnalyticsQuery query, CancellationToken ct = default)
    {
        var channel = query.ChannelType ?? "sms";

        var fedPackCount   = await _db.GovernanceFederatedRulePacks.CountAsync(fp => fp.ChannelType == channel && fp.Enabled && fp.TenantId == null, ct);
        var tenantPackCount = await _db.GovernanceFederatedRulePacks.CountAsync(fp => fp.ChannelType == channel && fp.Enabled && fp.TenantId != null, ct);
        var overlayCount   = await _db.GovernanceFederationOverlays.CountAsync(o => o.ChannelType == channel && o.Enabled, ct);
        var tenantCoverage = await _db.GovernanceFederatedRulePacks
            .Where(fp => fp.ChannelType == channel && fp.Enabled && fp.TenantId != null)
            .Select(fp => fp.TenantId)
            .Distinct()
            .CountAsync(ct);
        var globalPackCount = await _db.SmsGovernanceRulePacks.CountAsync(p => p.Enabled && p.Status == "active", ct);

        var topPacks = await _db.GovernanceFederatedRulePacks
            .AsNoTracking()
            .Where(fp => fp.ChannelType == channel && fp.Enabled)
            .GroupBy(fp => fp.RulePackId)
            .Select(g => new { RulePackId = g.Key, Count = g.Count(), Groups = g.Select(fp => fp.FederationGroup).Distinct().ToList() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(ct);

        var topPackStats = topPacks.Select(p => new FederatedPackStats(
            p.RulePackId,
            p.RulePackId.ToString("N"),
            1,
            p.Count,
            new[] { channel })).ToList();

        return new ChannelGovernanceAnalyticsResult(
            channel, fedPackCount, globalPackCount, tenantPackCount, overlayCount, tenantCoverage, topPackStats);
    }

    public async Task<FederatedPackAnalyticsResult> GetFederatedRulePackAnalyticsAsync(
        Guid rulePackId, CancellationToken ct = default)
    {
        var mappings = await _db.GovernanceFederatedRulePacks
            .AsNoTracking()
            .Where(fp => fp.RulePackId == rulePackId)
            .ToListAsync(ct);

        var total         = mappings.Count;
        var enabled       = mappings.Count(m => m.Enabled);
        var tenantSpecific = mappings.Count(m => m.TenantId != null);
        var channels      = mappings.Select(m => m.ChannelType).Distinct().ToList();
        var groups        = mappings.Where(m => m.FederationGroup != null)
                                    .Select(m => m.FederationGroup!).Distinct().ToList();

        return new FederatedPackAnalyticsResult(rulePackId, total, enabled, tenantSpecific, channels, groups);
    }

    public async Task<CrossChannelRolloutAnalyticsResult> GetCrossChannelRolloutAnalyticsAsync(
        FederationAnalyticsQuery query, CancellationToken ct = default)
    {
        var totalRollouts  = await _db.SmsGovernanceRolloutPlans.CountAsync(ct);
        var activeRollouts = await _db.SmsGovernanceRolloutPlans
            .CountAsync(r => RolloutStates.ActiveStates.Contains(r.RolloutState), ct);
        var channels       = await _db.GovernanceChannelScopes
            .Where(s => s.Enabled)
            .Select(s => s.ChannelType)
            .Distinct()
            .ToListAsync(ct);
        var auditCount = await _db.GovernanceFederationAuditEvents
            .CountAsync(e => e.EventType == GovernanceFederationAuditEvent.EventTypes.RolloutFederationStarted ||
                             e.EventType == GovernanceFederationAuditEvent.EventTypes.RolloutFederationCompleted ||
                             e.EventType == GovernanceFederationAuditEvent.EventTypes.RolloutFederationFailed, ct);

        return new CrossChannelRolloutAnalyticsResult(totalRollouts, activeRollouts, channels, auditCount);
    }
}
