using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-023: Resolves effective governance rule packs and rules for a specific tenant
/// by combining global packs, tenant rule-pack assignments, and tenant overlays.
///
/// Resolution order when tenant scoping is enabled:
///   1. Tenant active assignments  → load referenced global packs for that tenant
///   2. Rollout-specific assignments (mode = rollout_canary / rollout_stage)
///   3. Tenant overlays (disable/override/add rules on top of resolved set)
///   4. Global active packs (when mode = tenant_inherited or no assignments)
///   5. Platform default behaviour (when no packs at any level)
///
/// When tenantId = null or Enabled = false, global behaviour is preserved unchanged.
/// All failures fail-open per FailOpenOnResolutionError config.
/// No raw phones, credentials, or message bodies are stored or returned.
/// </summary>
public sealed class SmsGovernanceTenantResolutionService : ISmsGovernanceTenantResolutionService
{
    private readonly NotificationsDbContext                     _db;
    private readonly SmsGovernanceTenantScopingOptions          _opts;
    private readonly ILogger<SmsGovernanceTenantResolutionService> _logger;

    public SmsGovernanceTenantResolutionService(
        NotificationsDbContext                                  db,
        IOptions<SmsGovernanceTenantScopingOptions>            options,
        ILogger<SmsGovernanceTenantResolutionService>          logger)
    {
        _db     = db;
        _opts   = options.Value;
        _logger = logger;
    }

    // ── ResolveEffectiveRulePacksAsync ────────────────────────────────────────

    public async Task<TenantResolutionResult> ResolveEffectiveRulePacksAsync(
        Guid? tenantId,
        GovernanceResolutionContext context,
        CancellationToken ct = default)
    {
        if (!_opts.Enabled || tenantId == null)
            return NoopResult(tenantId);

        try
        {
            var nowUtc = context.NowUtc ?? DateTime.UtcNow;

            var assignments = await LoadActiveAssignmentsAsync(tenantId.Value, nowUtc, ct);

            if (assignments.Count == 0)
                return NoopResult(tenantId, fellBackToGlobal: true);

            var globalPackIds   = new List<Guid>();
            var assignedPackIds = assignments
                .Where(a => a.AssignmentMode != SmsGovernanceTenantRulePackAssignment.AssignmentModes.RolloutCanary
                         && a.AssignmentMode != SmsGovernanceTenantRulePackAssignment.AssignmentModes.RolloutStage)
                .Select(a => a.RulePackId)
                .Distinct().ToList();

            var rolloutPackIds = _opts.EnableRolloutAssignments
                ? assignments
                    .Where(a => a.AssignmentMode == SmsGovernanceTenantRulePackAssignment.AssignmentModes.RolloutCanary
                             || a.AssignmentMode == SmsGovernanceTenantRulePackAssignment.AssignmentModes.RolloutStage)
                    .Select(a => a.RulePackId)
                    .Distinct().ToList()
                : new List<Guid>();

            bool fellBack = false;

            // Inherit global packs unless mode = tenant_isolated
            if (_opts.ResolutionMode != SmsGovernanceTenantScopingOptions.ResolutionModes.TenantIsolated)
            {
                globalPackIds = await _db.SmsGovernanceRulePacks
                    .AsNoTracking()
                    .Where(p => p.TenantId == null && p.Enabled && p.Status == "active"
                             && (p.EffectiveFrom == null || p.EffectiveFrom <= nowUtc)
                             && (p.EffectiveTo   == null || p.EffectiveTo   >= nowUtc))
                    .Select(p => p.Id)
                    .ToListAsync(ct);

                fellBack = assignedPackIds.Count == 0 && rolloutPackIds.Count == 0;
            }

            return new TenantResolutionResult(
                TenantId:             tenantId,
                GlobalPackIds:        globalPackIds,
                AssignedPackIds:      assignedPackIds,
                RolloutPackIds:       rolloutPackIds,
                ResolutionMode:       _opts.ResolutionMode,
                HasTenantAssignments: assignments.Count > 0,
                FellBackToGlobal:     fellBack);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "TenantResolutionService.ResolveEffectiveRulePacksAsync failed for tenant {TenantId} — failing open",
                tenantId);

            return _opts.FailOpenOnResolutionError
                ? NoopResult(tenantId, fellBackToGlobal: true, warning: "Resolution error; fell back to global.")
                : NoopResult(tenantId);
        }
    }

    // ── ResolveEffectiveRulesAsync ────────────────────────────────────────────

    public async Task<EffectiveRuleSetResult> ResolveEffectiveRulesAsync(
        Guid? tenantId,
        GovernanceResolutionContext context,
        CancellationToken ct = default)
    {
        if (!_opts.Enabled || tenantId == null)
            return NoopRuleResult(tenantId);

        try
        {
            var packResult = await ResolveEffectiveRulePacksAsync(tenantId, context, ct);
            var allPackIds = packResult.GlobalPackIds
                .Concat(packResult.AssignedPackIds)
                .Concat(packResult.RolloutPackIds)
                .Distinct().ToList();

            if (allPackIds.Count == 0)
                return NoopRuleResult(tenantId, packResult.ResolutionMode, packResult.FellBackToGlobal);

            var baseRules = await _db.SmsGovernanceRules
                .AsNoTracking()
                .Where(r => allPackIds.Contains(r.RulePackId) && r.Enabled)
                .OrderBy(r => r.Priority)
                .ToListAsync(ct);

            if (!_opts.EnableTenantOverlays || baseRules.Count == 0)
            {
                return new EffectiveRuleSetResult(
                    TenantId:        tenantId,
                    Rules:           baseRules.Select(r => MapToEffective(r, packResult)).ToList(),
                    DisabledRuleIds: [],
                    OverlaysSummary: [],
                    ResolutionMode:  packResult.ResolutionMode,
                    FellBackToGlobal: packResult.FellBackToGlobal);
            }

            // Load active overlays for tenant
            var nowUtc = context.NowUtc ?? DateTime.UtcNow;
            var overlays = await LoadActiveOverlaysAsync(tenantId.Value, nowUtc, ct);

            return ApplyOverlays(tenantId.Value, baseRules, overlays, packResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "TenantResolutionService.ResolveEffectiveRulesAsync failed for tenant {TenantId} — failing open",
                tenantId);
            return NoopRuleResult(tenantId);
        }
    }

    // ── GetEffectiveGovernanceGraphAsync ──────────────────────────────────────

    public async Task<EffectiveGovernanceGraphDto> GetEffectiveGovernanceGraphAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var context    = new GovernanceResolutionContext();
        var ruleResult = await ResolveEffectiveRulesAsync(tenantId, context, ct);
        var packResult = await ResolveEffectiveRulePacksAsync(tenantId, context, ct);

        // Load pack summaries
        var allPackIds = packResult.GlobalPackIds
            .Concat(packResult.AssignedPackIds)
            .Concat(packResult.RolloutPackIds)
            .Distinct().ToList();

        var packs = allPackIds.Count == 0
            ? new List<SmsGovernanceRulePack>()
            : await _db.SmsGovernanceRulePacks
                .AsNoTracking()
                .Where(p => allPackIds.Contains(p.Id))
                .ToListAsync(ct);

        var rulesByPack = ruleResult.Rules.GroupBy(r => r.RulePackId)
            .ToDictionary(g => g.Key, g => g.Count());

        var nowUtc   = DateTime.UtcNow;
        var overlays = _opts.EnableTenantOverlays
            ? await LoadActiveOverlaysAsync(tenantId, nowUtc, ct)
            : new List<SmsGovernanceTenantOverlay>();

        PackSummaryDto ToSummary(SmsGovernanceRulePack p, string? mode) =>
            new(p.Id, p.Name, mode, rulesByPack.GetValueOrDefault(p.Id), p.Priority);

        return new EffectiveGovernanceGraphDto(
            TenantId:            tenantId,
            ResolutionMode:      packResult.ResolutionMode,
            InheritedGlobalPacks: packs.Where(p => packResult.GlobalPackIds.Contains(p.Id))
                                       .Select(p => ToSummary(p, "inherited")).ToList(),
            TenantAssignedPacks:  packs.Where(p => packResult.AssignedPackIds.Contains(p.Id))
                                       .Select(p => ToSummary(p, "assigned")).ToList(),
            RolloutAssignedPacks: packs.Where(p => packResult.RolloutPackIds.Contains(p.Id))
                                       .Select(p => ToSummary(p, "rollout")).ToList(),
            OverlaysApplied:      overlays.Select(o => new OverlaySummaryDto(
                                      o.Id, o.OverlayType, o.RuleId, o.RulePackId, o.Priority,
                                      $"{o.OverlayType} on {(o.RuleId.HasValue ? $"rule:{o.RuleId}" : o.RulePackId.HasValue ? $"pack:{o.RulePackId}" : "all")}"))
                                  .ToList(),
            DisabledRuleIds:      ruleResult.DisabledRuleIds,
            FinalRuleCount:       ruleResult.Rules.Count,
            Warnings:             packResult.Warning != null ? [packResult.Warning] : []);
    }

    // ── ExplainResolutionAsync ────────────────────────────────────────────────

    public async Task<ResolutionExplanationDto> ExplainResolutionAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var nowUtc      = DateTime.UtcNow;
        var context     = new GovernanceResolutionContext(NowUtc: nowUtc);
        var assignments = await LoadActiveAssignmentsAsync(tenantId, nowUtc, ct);
        var overlays    = _opts.EnableTenantOverlays
            ? await LoadActiveOverlaysAsync(tenantId, nowUtc, ct)
            : new List<SmsGovernanceTenantOverlay>();
        var ruleResult  = await ResolveEffectiveRulesAsync(tenantId, context, ct);

        var steps = new List<string>
        {
            $"Feature enabled: {_opts.Enabled}",
            $"Resolution mode: {_opts.ResolutionMode}",
            $"Active assignments: {assignments.Count}",
            assignments.Count == 0
                ? "No tenant assignments — using global governance packs"
                : $"Assigned pack IDs: {string.Join(", ", assignments.Select(a => a.RulePackId))}",
            $"Tenant overlays enabled: {_opts.EnableTenantOverlays}, active overlays: {overlays.Count}",
            $"Final effective rules: {ruleResult.Rules.Count}",
            $"Disabled rules by overlay: {ruleResult.DisabledRuleIds.Count}",
        };

        return new ResolutionExplanationDto(
            TenantId:            tenantId,
            ResolutionMode:      _opts.ResolutionMode,
            TotalAssignments:    assignments.Count,
            ActiveAssignments:   assignments.Count(a => a.AssignmentState == SmsGovernanceTenantRulePackAssignment.AssignmentStates.Active),
            TotalOverlays:       overlays.Count,
            ActiveOverlays:      overlays.Count(o => o.OverlayState == SmsGovernanceTenantOverlay.OverlayStates.Active),
            DisabledRulesCount:  ruleResult.DisabledRuleIds.Count,
            FinalRuleCount:      ruleResult.Rules.Count,
            HasTenantAssignments: assignments.Count > 0,
            UsesGlobalFallback:  ruleResult.FellBackToGlobal,
            Steps:               steps,
            Warnings:            []);
    }

    // ── Overlay application ───────────────────────────────────────────────────

    private EffectiveRuleSetResult ApplyOverlays(
        Guid tenantId,
        List<SmsGovernanceRule> baseRules,
        List<SmsGovernanceTenantOverlay> overlays,
        TenantResolutionResult packResult)
    {
        var effective      = new List<EffectiveRuleDto>();
        var disabledRuleIds = new List<Guid>();
        var overlaySummary = new List<string>();

        // Index overlays by ruleId for quick lookup
        var disableOverlays  = overlays.Where(o =>
            o.OverlayType == SmsGovernanceTenantOverlay.OverlayTypes.DisableRule ||
            o.OverlayType == SmsGovernanceTenantOverlay.OverlayTypes.SuppressRule)
            .Select(o => o.RuleId).Where(id => id.HasValue).Select(id => id!.Value)
            .ToHashSet();

        var severityOverlays = overlays
            .Where(o => o.OverlayType == SmsGovernanceTenantOverlay.OverlayTypes.OverrideSeverity && o.RuleId.HasValue)
            .ToDictionary(o => o.RuleId!.Value, o => o);

        var patternOverlays = overlays
            .Where(o => o.OverlayType == SmsGovernanceTenantOverlay.OverlayTypes.OverridePattern && o.RuleId.HasValue)
            .ToDictionary(o => o.RuleId!.Value, o => o);

        var metadataOverlays = overlays
            .Where(o => o.OverlayType == SmsGovernanceTenantOverlay.OverlayTypes.OverrideMetadata && o.RuleId.HasValue)
            .ToDictionary(o => o.RuleId!.Value, o => o);

        foreach (var rule in baseRules)
        {
            if (disableOverlays.Contains(rule.Id))
            {
                disabledRuleIds.Add(rule.Id);
                overlaySummary.Add($"disabled:{rule.Id}");
                continue;
            }

            // Check for modifications
            bool isOverlaid = false;
            string? overlayType    = null;
            string  severity       = rule.Severity;
            string  pattern        = rule.Pattern;
            string? metadataJson   = rule.MetadataJson;
            string? origSeverity   = null;

            if (severityOverlays.TryGetValue(rule.Id, out var sevOverlay) && !string.IsNullOrEmpty(sevOverlay.OverrideJson))
            {
                origSeverity = severity;
                severity     = ExtractStringField(sevOverlay.OverrideJson, "severity") ?? severity;
                isOverlaid   = true;
                overlayType  = SmsGovernanceTenantOverlay.OverlayTypes.OverrideSeverity;
                overlaySummary.Add($"severity_override:{rule.Id}:{origSeverity}->{severity}");
            }

            if (patternOverlays.TryGetValue(rule.Id, out var patOverlay) && !string.IsNullOrEmpty(patOverlay.OverrideJson))
            {
                pattern    = ExtractStringField(patOverlay.OverrideJson, "pattern") ?? pattern;
                isOverlaid = true;
                overlayType ??= SmsGovernanceTenantOverlay.OverlayTypes.OverridePattern;
                overlaySummary.Add($"pattern_override:{rule.Id}");
            }

            if (metadataOverlays.TryGetValue(rule.Id, out var metOverlay))
            {
                metadataJson = metOverlay.OverrideJson ?? metadataJson;
                isOverlaid   = true;
                overlayType ??= SmsGovernanceTenantOverlay.OverlayTypes.OverrideMetadata;
                overlaySummary.Add($"metadata_override:{rule.Id}");
            }

            effective.Add(new EffectiveRuleDto(
                RuleId:           rule.Id,
                RulePackId:       rule.RulePackId,
                RuleType:         rule.RuleType,
                Pattern:          pattern,
                Severity:         severity,
                MetadataJson:     metadataJson,
                Priority:         rule.Priority,
                IsFromAssignment: packResult.AssignedPackIds.Contains(rule.RulePackId) ||
                                  packResult.RolloutPackIds.Contains(rule.RulePackId),
                IsFromOverlay:    isOverlaid,
                OverlayType:      overlayType,
                OriginalSeverity: origSeverity));
        }

        // add_rule overlays: synthesize tenant-specific rules from overlay JSON
        var addRuleOverlays = overlays.Where(o =>
            o.OverlayType == SmsGovernanceTenantOverlay.OverlayTypes.AddRule &&
            !string.IsNullOrEmpty(o.OverrideJson));

        foreach (var overlay in addRuleOverlays.OrderBy(o => o.Priority))
        {
            var synth = TrySynthesizeRule(overlay);
            if (synth == null) continue;
            effective.Add(synth);
            overlaySummary.Add($"add_rule:{overlay.Id}");
        }

        // Sort final effective rules by priority
        effective = effective.OrderBy(r => r.Priority).ToList();

        return new EffectiveRuleSetResult(
            TenantId:         tenantId,
            Rules:            effective,
            DisabledRuleIds:  disabledRuleIds,
            OverlaysSummary:  overlaySummary,
            ResolutionMode:   packResult.ResolutionMode,
            FellBackToGlobal: packResult.FellBackToGlobal);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<List<SmsGovernanceTenantRulePackAssignment>> LoadActiveAssignmentsAsync(
        Guid tenantId, DateTime nowUtc, CancellationToken ct)
    {
        return await _db.SmsGovernanceTenantRulePackAssignments
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId
                     && a.AssignmentState == SmsGovernanceTenantRulePackAssignment.AssignmentStates.Active
                     && (a.EffectiveFrom == null || a.EffectiveFrom <= nowUtc)
                     && (a.EffectiveTo   == null || a.EffectiveTo   >= nowUtc))
            .OrderBy(a => a.Priority)
            .ToListAsync(ct);
    }

    private async Task<List<SmsGovernanceTenantOverlay>> LoadActiveOverlaysAsync(
        Guid tenantId, DateTime nowUtc, CancellationToken ct)
    {
        return await _db.SmsGovernanceTenantOverlays
            .AsNoTracking()
            .Where(o => o.TenantId == tenantId
                     && o.Enabled
                     && o.OverlayState == SmsGovernanceTenantOverlay.OverlayStates.Active
                     && (o.EffectiveFrom == null || o.EffectiveFrom <= nowUtc)
                     && (o.EffectiveTo   == null || o.EffectiveTo   >= nowUtc))
            .OrderBy(o => o.Priority)
            .ToListAsync(ct);
    }

    private static EffectiveRuleDto MapToEffective(SmsGovernanceRule r, TenantResolutionResult packResult) =>
        new(r.Id, r.RulePackId, r.RuleType, r.Pattern, r.Severity, r.MetadataJson, r.Priority,
            IsFromAssignment: packResult.AssignedPackIds.Contains(r.RulePackId) ||
                              packResult.RolloutPackIds.Contains(r.RulePackId),
            IsFromOverlay: false, OverlayType: null, OriginalSeverity: null);

    private static EffectiveRuleDto? TrySynthesizeRule(SmsGovernanceTenantOverlay overlay)
    {
        if (string.IsNullOrEmpty(overlay.OverrideJson)) return null;
        try
        {
            var ruleType = ExtractStringField(overlay.OverrideJson, "ruleType");
            var pattern  = ExtractStringField(overlay.OverrideJson, "pattern");
            var severity = ExtractStringField(overlay.OverrideJson, "severity");
            if (string.IsNullOrEmpty(ruleType) || string.IsNullOrEmpty(pattern)) return null;

            return new EffectiveRuleDto(
                RuleId:           overlay.Id, // synthetic ID = overlay ID
                RulePackId:       overlay.RulePackId ?? Guid.Empty,
                RuleType:         ruleType,
                Pattern:          pattern,
                Severity:         severity ?? "warn",
                MetadataJson:     ExtractStringField(overlay.OverrideJson, "metadataJson"),
                Priority:         overlay.Priority,
                IsFromAssignment: false,
                IsFromOverlay:    true,
                OverlayType:      SmsGovernanceTenantOverlay.OverlayTypes.AddRule,
                OriginalSeverity: null);
        }
        catch { return null; }
    }

    private static string? ExtractStringField(string json, string field)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(field, out var el) ? el.GetString() : null;
        }
        catch { return null; }
    }

    private static TenantResolutionResult NoopResult(
        Guid? tenantId, bool fellBackToGlobal = false, string? warning = null) =>
        new(tenantId, [], [], [], "global_only", false, fellBackToGlobal, warning);

    private static EffectiveRuleSetResult NoopRuleResult(
        Guid? tenantId, string resolutionMode = "global_only", bool fellBack = false) =>
        new(tenantId, [], [], [], resolutionMode, fellBack);
}
