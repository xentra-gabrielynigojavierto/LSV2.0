using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Services;

public sealed class GovernanceTopologyResolver : IGovernanceTopologyResolver
{
    private readonly NotificationsDbContext                    _db;
    private readonly GovernanceFederationOptions               _opts;
    private readonly ISmsGovernanceTenantResolutionService     _tenantResolution;
    private readonly ILogger<GovernanceTopologyResolver>       _logger;

    public GovernanceTopologyResolver(
        NotificationsDbContext db,
        IOptions<GovernanceFederationOptions> opts,
        ISmsGovernanceTenantResolutionService tenantResolution,
        ILogger<GovernanceTopologyResolver> logger)
    {
        _db               = db;
        _opts             = opts.Value;
        _tenantResolution = tenantResolution;
        _logger           = logger;
    }

    // -----------------------------------------------------------------------
    // ResolveTopologyAsync
    // -----------------------------------------------------------------------

    public async Task<GovernanceTopologyGraph> ResolveTopologyAsync(
        GovernanceTopologyRequest request, CancellationToken ct = default)
    {
        var now = request.NowUtc ?? DateTime.UtcNow;
        var warnings = new List<string>();

        if (!_opts.Enabled)
        {
            warnings.Add("Federation disabled — global-only resolution");
            return EmptyTopology(request, warnings);
        }

        var scope = await LoadChannelScopeAsync(request.ChannelType, ct);
        if (scope == null)
        {
            warnings.Add($"No active channel scope for '{request.ChannelType}' — global-only resolution");
            return EmptyTopology(request, warnings);
        }

        // Step 1: Global packs
        var globalPacks = await LoadActiveGlobalPacksAsync(ct);
        var globalSummaries = globalPacks.Select(p => new ChannelPackSummary(
            p.Id, p.Name, "global", null, p.Priority, true, false, false)).ToList();

        // Step 2: Channel-federated packs (global, no tenant scope)
        var channelFedPacks = await LoadChannelFederatedPacksAsync(request.ChannelType, null, now, ct);
        var channelSummaries = channelFedPacks.Select(fp => new ChannelPackSummary(
            fp.RulePackId, fp.RulePackId.ToString("N"), "channel_federated",
            fp.FederationGroup, fp.Priority, false, true, false)).ToList();

        // Step 3: Tenant-assigned packs from LS-023 (SMS only) + tenant-federated (all channels)
        var tenantPacks    = new List<ChannelPackSummary>();
        var federatedPacks = new List<ChannelPackSummary>(channelSummaries);

        if (request.TenantId.HasValue)
        {
            if (request.ChannelType.Equals("sms", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var ctx = new GovernanceResolutionContext();
                    var tenantResult = await _tenantResolution.ResolveEffectiveRulePacksAsync(
                        request.TenantId.Value, ctx, ct);
                    foreach (var packId in tenantResult.AssignedPackIds)
                    {
                        tenantPacks.Add(new ChannelPackSummary(
                            packId, packId.ToString("N"), "tenant_assigned",
                            null, 100, false, false, true));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "GovernanceTopologyResolver: LS-023 tenant resolution failed — continuing");
                    warnings.Add("Tenant resolution failed; tenant packs excluded from topology");
                }
            }

            // Tenant-specific federated packs (any channel)
            var tenantFedPacks = await LoadChannelFederatedPacksAsync(request.ChannelType, request.TenantId, now, ct);
            foreach (var fp in tenantFedPacks)
            {
                var summary = new ChannelPackSummary(
                    fp.RulePackId, fp.RulePackId.ToString("N"), "tenant_federated",
                    fp.FederationGroup, fp.Priority, false, true, true);
                tenantPacks.Add(summary);
                federatedPacks.Add(summary);
            }
        }

        // Step 4: Overlays
        var tenantOverlays = request.TenantId.HasValue
            ? await LoadOverlaysAsync(request.ChannelType, request.TenantId, now, ct)
            : new List<GovernanceFederationOverlay>();

        var globalOverlays = await LoadOverlaysAsync(request.ChannelType, null, now, ct);

        // Step 5: Rollout override labels
        var rolloutOverrides = request.RolloutPlanId.HasValue
            ? new List<string> { $"rollout:{request.RolloutPlanId}" }
            : new List<string>();

        // Approximate rule count: sum rules across global packs
        var ruleCount = 0;
        foreach (var p in globalPacks)
            ruleCount += await _db.SmsGovernanceRules.CountAsync(r => r.RulePackId == p.Id && r.Enabled, ct);
        foreach (var fp in channelFedPacks)
            ruleCount += await _db.SmsGovernanceRules.CountAsync(r => r.RulePackId == fp.RulePackId && r.Enabled, ct);

        return new GovernanceTopologyGraph(
            ChannelType:       request.ChannelType,
            TenantId:          request.TenantId,
            ScopeMode:         scope.ScopeMode,
            GlobalPacks:       globalSummaries,
            ChannelPacks:      channelSummaries,
            TenantPacks:       tenantPacks.Where(t => t.IsTenantAssigned).ToList(),
            FederatedPacks:    federatedPacks,
            TenantOverlays:    tenantOverlays.Select(MapOverlaySummary).ToList(),
            FederationOverlays: globalOverlays.Select(MapOverlaySummary).ToList(),
            RolloutOverrides:  rolloutOverrides,
            FinalRuleCount:    ruleCount,
            Warnings:          warnings);
    }

    // -----------------------------------------------------------------------
    // ResolveEffectiveRulesAsync
    // -----------------------------------------------------------------------

    public async Task<TopologyEffectiveRules> ResolveEffectiveRulesAsync(
        GovernanceTopologyRequest request, CancellationToken ct = default)
    {
        var now = request.NowUtc ?? DateTime.UtcNow;

        if (!_opts.Enabled)
            return new TopologyEffectiveRules(request.ChannelType, request.TenantId,
                new List<TopologyEffectiveRule>(), new[] { "Federation disabled" }, false, false);

        var scope = await LoadChannelScopeAsync(request.ChannelType, ct);
        if (scope == null)
            return new TopologyEffectiveRules(request.ChannelType, request.TenantId,
                new List<TopologyEffectiveRule>(),
                new[] { $"No active channel scope for '{request.ChannelType}'" }, true, false);

        var rules    = new List<TopologyEffectiveRule>();
        var warnings = new List<string>();

        // Global rules
        var globalPacks = await LoadActiveGlobalPacksAsync(ct);
        foreach (var pack in globalPacks)
        {
            var packRules = await _db.SmsGovernanceRules
                .AsNoTracking()
                .Where(r => r.RulePackId == pack.Id && r.Enabled)
                .ToListAsync(ct);
            rules.AddRange(packRules.Select(r => new TopologyEffectiveRule(
                r.Id, r.Name, r.RuleType, r.Severity, request.ChannelType, "global", null)));
        }

        // Channel-federated rules
        var channelFedPacks = await LoadChannelFederatedPacksAsync(request.ChannelType, null, now, ct);
        foreach (var fp in channelFedPacks)
        {
            var packRules = await _db.SmsGovernanceRules
                .AsNoTracking()
                .Where(r => r.RulePackId == fp.RulePackId && r.Enabled)
                .ToListAsync(ct);
            rules.AddRange(packRules.Select(r => new TopologyEffectiveRule(
                r.Id, r.Name, r.RuleType, r.Severity, request.ChannelType, "channel_federated", null)));
        }

        // Apply federation overlays in-memory (no base rule mutation)
        var overlays = await LoadOverlaysAsync(request.ChannelType, request.TenantId, now, ct);
        var globalOverlays = await LoadOverlaysAsync(request.ChannelType, null, now, ct);
        ApplyFederationOverlays(rules, overlays.Concat(globalOverlays).ToList());

        if (!rules.Any())
            warnings.Add("No effective rules resolved — check channel scope and federated pack configuration");

        return new TopologyEffectiveRules(request.ChannelType, request.TenantId,
            rules, warnings, true, true);
    }

    // -----------------------------------------------------------------------
    // ExplainTopologyAsync
    // -----------------------------------------------------------------------

    public async Task<TopologyExplanation> ExplainTopologyAsync(
        GovernanceTopologyRequest request, CancellationToken ct = default)
    {
        var now = request.NowUtc ?? DateTime.UtcNow;
        var steps = new List<TopologyExplanationStep>();
        int stepNum = 0;

        // Step 0: Federation enabled check
        stepNum++;
        if (!_opts.Enabled)
        {
            steps.Add(new TopologyExplanationStep(stepNum, "Federation Check",
                "GovernanceFederation.Enabled = false — all resolution defers to SMS global path",
                0, 0, new[] { "Set GovernanceFederation:Enabled=true to activate cross-channel federation" }));
            return new TopologyExplanation(request.ChannelType, request.TenantId,
                steps, 0, false, false, "disabled");
        }
        steps.Add(new TopologyExplanationStep(stepNum, "Federation Check",
            "Federation enabled", 0, 0, Array.Empty<string>()));

        // Step 1: Channel scope
        stepNum++;
        var scope = await LoadChannelScopeAsync(request.ChannelType, ct);
        if (scope == null)
        {
            steps.Add(new TopologyExplanationStep(stepNum, "Channel Scope",
                $"No active channel scope for '{request.ChannelType}' — only global packs apply",
                0, 0, new[] { $"Create a channel scope for '{request.ChannelType}' to enable federation" }));
            return new TopologyExplanation(request.ChannelType, request.TenantId,
                steps, 0, true, false, "no_scope");
        }
        steps.Add(new TopologyExplanationStep(stepNum, "Channel Scope",
            $"Active channel scope found: mode={scope.ScopeMode}, priority={scope.Priority}",
            0, 0, new[] { $"Channel: {scope.ChannelType}, ScopeMode: {scope.ScopeMode}" }));

        // Step 2: Global packs
        stepNum++;
        var globalPacks = await LoadActiveGlobalPacksAsync(ct);
        var globalRuleCount = 0;
        foreach (var p in globalPacks)
            globalRuleCount += await _db.SmsGovernanceRules.CountAsync(r => r.RulePackId == p.Id && r.Enabled, ct);
        steps.Add(new TopologyExplanationStep(stepNum, "Global Packs",
            $"Loaded {globalPacks.Count} active global governance packs ({globalRuleCount} rules)",
            globalRuleCount, 0,
            globalPacks.Select(p => $"Pack: {p.Name} (priority={p.Priority})").ToArray()));

        // Step 3: Channel-federated packs
        stepNum++;
        var channelFedPacks = await LoadChannelFederatedPacksAsync(request.ChannelType, null, now, ct);
        steps.Add(new TopologyExplanationStep(stepNum, "Channel-Federated Packs",
            $"Loaded {channelFedPacks.Count} federated packs for channel '{request.ChannelType}'",
            channelFedPacks.Count, 0,
            channelFedPacks.Select(fp =>
                $"FederatedPack: {fp.RulePackId} (group={fp.FederationGroup ?? "none"}, priority={fp.Priority})").ToArray()));

        // Step 4: Tenant packs
        stepNum++;
        var tenantDetails = new List<string>();
        int tenantRuleCount = 0;
        if (request.TenantId.HasValue)
        {
            if (request.ChannelType.Equals("sms", StringComparison.OrdinalIgnoreCase))
                tenantDetails.Add("LS-023 tenant resolution applied for SMS channel");

            var tenantFedPacks = await LoadChannelFederatedPacksAsync(request.ChannelType, request.TenantId, now, ct);
            tenantRuleCount = tenantFedPacks.Count;
            tenantDetails.AddRange(tenantFedPacks.Select(fp =>
                $"TenantFederatedPack: {fp.RulePackId} (channel={fp.ChannelType})"));
        }
        else
        {
            tenantDetails.Add("No tenant scoping requested — global/channel rules only");
        }
        steps.Add(new TopologyExplanationStep(stepNum, "Tenant Packs",
            "Tenant-scoped pack resolution", tenantRuleCount, 0, tenantDetails));

        // Step 5: Federation overlays
        stepNum++;
        var overlays = await LoadOverlaysAsync(request.ChannelType, request.TenantId, now, ct);
        var globalOverlays = await LoadOverlaysAsync(request.ChannelType, null, now, ct);
        var allOverlays = overlays.Concat(globalOverlays).ToList();
        var disableCount = allOverlays.Count(o =>
            o.OverlayType is GovernanceFederationOverlay.OverlayTypes.DisableRule
                          or GovernanceFederationOverlay.OverlayTypes.SuppressRule);
        steps.Add(new TopologyExplanationStep(stepNum, "Federation Overlays",
            $"Applied {allOverlays.Count} active federation overlays in-memory (base rules unchanged)",
            0, disableCount,
            allOverlays.Select(o =>
                $"Overlay: {o.OverlayType} (channel={o.ChannelType}, priority={o.Priority})").ToArray()));

        var totalRules = globalRuleCount + channelFedPacks.Count + tenantRuleCount - disableCount;

        return new TopologyExplanation(request.ChannelType, request.TenantId,
            steps, Math.Max(0, totalRules), true, true, scope.ScopeMode);
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private async Task<GovernanceChannelScope?> LoadChannelScopeAsync(string channelType, CancellationToken ct) =>
        await _db.GovernanceChannelScopes.AsNoTracking()
            .Where(s => s.ChannelType == channelType && s.Enabled)
            .OrderBy(s => s.Priority)
            .FirstOrDefaultAsync(ct);

    private async Task<List<SmsGovernanceRulePack>> LoadActiveGlobalPacksAsync(CancellationToken ct) =>
        await _db.SmsGovernanceRulePacks.AsNoTracking()
            .Where(p => p.TenantId == null && p.Enabled && p.Status == "active")
            .OrderBy(p => p.Priority)
            .ToListAsync(ct);

    private async Task<List<GovernanceFederatedRulePack>> LoadChannelFederatedPacksAsync(
        string channelType, Guid? tenantId, DateTime now, CancellationToken ct) =>
        await _db.GovernanceFederatedRulePacks.AsNoTracking()
            .Where(fp => fp.ChannelType == channelType &&
                         fp.Enabled &&
                         fp.TenantId == tenantId &&
                         (fp.EffectiveFrom == null || fp.EffectiveFrom <= now) &&
                         (fp.EffectiveTo   == null || fp.EffectiveTo   >  now))
            .OrderBy(fp => fp.Priority)
            .ToListAsync(ct);

    private async Task<List<GovernanceFederationOverlay>> LoadOverlaysAsync(
        string channelType, Guid? tenantId, DateTime now, CancellationToken ct) =>
        await _db.GovernanceFederationOverlays.AsNoTracking()
            .Where(o => o.ChannelType == channelType &&
                        o.Enabled &&
                        o.OverlayState == GovernanceFederationOverlay.OverlayStates.Active &&
                        o.TenantId == tenantId &&
                        (o.EffectiveFrom == null || o.EffectiveFrom <= now) &&
                        (o.EffectiveTo   == null || o.EffectiveTo   >  now))
            .OrderBy(o => o.Priority)
            .ToListAsync(ct);

    private static void ApplyFederationOverlays(
        List<TopologyEffectiveRule> rules,
        List<GovernanceFederationOverlay> overlays)
    {
        foreach (var overlay in overlays)
        {
            switch (overlay.OverlayType)
            {
                case GovernanceFederationOverlay.OverlayTypes.DisableRule:
                case GovernanceFederationOverlay.OverlayTypes.SuppressRule:
                    if (overlay.RuleId.HasValue)
                        rules.RemoveAll(r => r.RuleId == overlay.RuleId.Value);
                    break;

                case GovernanceFederationOverlay.OverlayTypes.OverrideSeverity:
                    if (overlay.RuleId.HasValue)
                    {
                        var idx = rules.FindIndex(r => r.RuleId == overlay.RuleId.Value);
                        if (idx >= 0)
                        {
                            var r = rules[idx];
                            rules[idx] = r with
                            {
                                Severity        = ExtractStringField(overlay.OverlayJson, "severity") ?? r.Severity,
                                OverrideApplied = overlay.OverlayType
                            };
                        }
                    }
                    break;

                case GovernanceFederationOverlay.OverlayTypes.AddRule:
                    rules.Add(new TopologyEffectiveRule(
                        overlay.RuleId ?? Guid.NewGuid(),
                        ExtractStringField(overlay.OverlayJson, "name") ?? "federation_injected",
                        ExtractStringField(overlay.OverlayJson, "ruleType") ?? "federation",
                        ExtractStringField(overlay.OverlayJson, "severity") ?? "info",
                        overlay.ChannelType,
                        "federation_overlay",
                        overlay.OverlayType));
                    break;
            }
        }
    }

    private static string? ExtractStringField(string? json, string field)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        var key = $"\"{field}\"";
        var idx = json.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var colon = json.IndexOf(':', idx + key.Length);
        if (colon < 0) return null;
        var valStart = json.IndexOf('"', colon + 1);
        if (valStart < 0) return null;
        var valEnd = json.IndexOf('"', valStart + 1);
        if (valEnd < 0) return null;
        return json.Substring(valStart + 1, valEnd - valStart - 1);
    }

    private static FederationOverlaySummary MapOverlaySummary(GovernanceFederationOverlay o) =>
        new(o.Id, o.OverlayType, o.ChannelType, o.TenantId, o.RulePackId, o.RuleId, o.Priority);

    private static GovernanceTopologyGraph EmptyTopology(GovernanceTopologyRequest request, List<string> warnings) =>
        new(request.ChannelType, request.TenantId, "global_only",
            new List<ChannelPackSummary>(), new List<ChannelPackSummary>(),
            new List<ChannelPackSummary>(), new List<ChannelPackSummary>(),
            new List<FederationOverlaySummary>(), new List<FederationOverlaySummary>(),
            new List<string>(), 0, warnings);
}
