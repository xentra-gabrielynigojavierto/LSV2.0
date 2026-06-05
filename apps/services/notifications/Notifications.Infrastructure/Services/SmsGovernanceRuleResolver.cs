using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-019: Rule pack / compliance profile inheritance resolver.
/// LS-NOTIF-SMS-023: Extended with per-tenant assignment and overlay resolution.
///
/// Resolution order:
/// 1. Load global active packs (TenantId = null, status = active, not expired, enabled).
/// 2. Load tenant-specific active packs (status = active, not expired, enabled).
/// 3. Apply InheritanceMode from each tenant pack:
///    - merge       → global rules + tenant rules, all sorted by priority
///    - override    → tenant rules replace global rules with same RuleType
///    - append_only → global rules first (intact), tenant rules appended
/// 4. [LS-023] If tenant scoping enabled: inject rules from active tenant assignments
///    and apply tenant overlays (disable/override/add rules).
/// 5. Resolve compliance profile assignment for tenant → apply EnforcementMode.
/// 6. Trim to MaxRulesPerEvaluation.
///
/// Failures fail-open: returns empty resolution (no rules evaluated).
/// Existing global-only tenants are unaffected when LS-023 scoping is disabled.
/// </summary>
public sealed class SmsGovernanceRuleResolver : ISmsGovernanceRuleResolver
{
    private readonly NotificationsDbContext              _db;
    private readonly SmsGovernanceDynamicOptions         _options;
    private readonly SmsGovernanceTenantScopingOptions   _scopingOptions;
    private readonly ISmsGovernanceTenantResolutionService _tenantResolution;
    private readonly ILogger<SmsGovernanceRuleResolver>  _logger;

    public SmsGovernanceRuleResolver(
        NotificationsDbContext                                   db,
        Microsoft.Extensions.Options.IOptions<SmsGovernanceDynamicOptions>        options,
        Microsoft.Extensions.Options.IOptions<SmsGovernanceTenantScopingOptions>  scopingOptions,
        ISmsGovernanceTenantResolutionService                    tenantResolution,
        ILogger<SmsGovernanceRuleResolver>                       logger)
    {
        _db               = db;
        _options          = options.Value;
        _scopingOptions   = scopingOptions.Value;
        _tenantResolution = tenantResolution;
        _logger           = logger;
    }

    public async Task<SmsGovernanceRuleResolution> ResolveRulesAsync(
        Guid? tenantId,
        string? context    = null,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return Empty();

        try
        {
            var nowUtc   = DateTime.UtcNow;
            var packList = await LoadActivePacksAsync(tenantId, nowUtc, ct);

            // ── Existing LS-019 resolution ────────────────────────────────────

            List<SmsGovernanceRule> resolvedRules;

            if (packList.Count == 0)
            {
                resolvedRules = [];
            }
            else
            {
                var packIds = packList.Select(p => p.Id).ToList();
                var allRules = await _db.SmsGovernanceRules
                    .AsNoTracking()
                    .Where(r => packIds.Contains(r.RulePackId) && r.Enabled)
                    .OrderBy(r => r.Priority)
                    .Take(_options.MaxRulesPerEvaluation)
                    .ToListAsync(ct);

                if (allRules.Count == 0)
                {
                    resolvedRules = [];
                }
                else
                {
                    var globalPacks = packList.Where(p => p.TenantId == null).ToList();
                    var tenantPacks = packList.Where(p => p.TenantId != null).ToList();

                    if (tenantPacks.Count == 0)
                    {
                        resolvedRules = allRules;
                    }
                    else
                    {
                        var globalRules = allRules.Where(r => globalPacks.Any(p => p.Id == r.RulePackId)).ToList();
                        var tenantRules = allRules.Where(r => tenantPacks.Any(p => p.Id == r.RulePackId)).ToList();

                        var primaryMode = tenantPacks.OrderBy(p => p.Priority).First().InheritanceMode;
                        resolvedRules = primaryMode switch
                        {
                            "override"    => ResolveOverride(globalRules, tenantRules),
                            "append_only" => [.. globalRules.OrderBy(r => r.Priority),
                                              .. tenantRules.OrderBy(r => r.Priority)],
                            _             => [.. globalRules.Concat(tenantRules).OrderBy(r => r.Priority)],
                        };
                    }
                }
            }

            // ── LS-023: Tenant scoping extension ─────────────────────────────
            // Only applied when enabled and tenantId is present.
            // Tenant scoping is additive — it injects additional assigned-pack rules
            // and applies overlays on top of the LS-019 resolved set.
            // Global-only tenants (no assignments) are completely unaffected.

            if (_scopingOptions.Enabled && tenantId.HasValue)
            {
                var resCtx = new GovernanceResolutionContext(
                    EvaluationContext: context, NowUtc: nowUtc);

                var scopedResult = await _tenantResolution.ResolveEffectiveRulesAsync(
                    tenantId, resCtx, ct);

                if (scopedResult.Rules.Count > 0 && !scopedResult.FellBackToGlobal)
                {
                    // Merge: scoped rules supplement LS-019 resolved rules.
                    // In isolated mode, scoped result REPLACES the global resolution.
                    bool isolated = _scopingOptions.ResolutionMode ==
                        SmsGovernanceTenantScopingOptions.ResolutionModes.TenantIsolated;

                    resolvedRules = BuildFinalRuleSet(
                        resolvedRules, scopedResult, isolated, _options.MaxRulesPerEvaluation);
                }
            }

            // ── Compliance profile / EnforcementMode ─────────────────────────

            var (enforcementMode, profileAssigned) = await ResolveEnforcementModeAsync(tenantId, ct);

            return new SmsGovernanceRuleResolution
            {
                Rules            = resolvedRules,
                Packs            = packList,
                EnforcementMode  = enforcementMode,
                ProfileAssigned  = profileAssigned,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SmsGovernanceRuleResolver: ResolveRulesAsync failed for tenant {TenantId} — failing open",
                tenantId);
            return Empty();
        }
    }

    public async Task<IReadOnlyList<SmsGovernanceRulePack>> ResolveRulePacksAsync(
        Guid? tenantId,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return [];

        try
        {
            return await LoadActivePacksAsync(tenantId, DateTime.UtcNow, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SmsGovernanceRuleResolver: ResolveRulePacksAsync failed for tenant {TenantId}",
                tenantId);
            return [];
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task<List<SmsGovernanceRulePack>> LoadActivePacksAsync(
        Guid? tenantId, DateTime nowUtc, CancellationToken ct)
    {
        return await _db.SmsGovernanceRulePacks
            .AsNoTracking()
            .Where(p =>
                p.Enabled &&
                p.Status == "active" &&
                (p.TenantId == null || p.TenantId == tenantId) &&
                (p.EffectiveFrom == null || p.EffectiveFrom <= nowUtc) &&
                (p.EffectiveTo   == null || p.EffectiveTo   >= nowUtc))
            .OrderBy(p => p.Priority)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Merge LS-019 resolved rules with LS-023 scoped effective rules.
    /// In isolated mode, the scoped result replaces the global set entirely.
    /// In inherited mode, scoped rules supplement (unique by RuleId, scoped wins on conflict).
    /// </summary>
    private static List<SmsGovernanceRule> BuildFinalRuleSet(
        List<SmsGovernanceRule>  ls019Rules,
        EffectiveRuleSetResult   scopedResult,
        bool                     isolated,
        int                      maxRules)
    {
        if (isolated)
        {
            // Isolated: only tenant-scoped rules apply
            return scopedResult.Rules
                .Select(ToGovernanceRule)
                .OrderBy(r => r.Priority)
                .Take(maxRules)
                .ToList();
        }

        // Inherited: merge ls019 rules + scoped rules.
        // If a scoped rule has the same Id as an ls019 rule, the scoped (possibly overlaid) version wins.
        var scopedIds  = new HashSet<Guid>(scopedResult.Rules.Select(r => r.RuleId));
        var baseRules  = ls019Rules.Where(r => !scopedIds.Contains(r.Id)).ToList();
        var scopedConverted = scopedResult.Rules.Select(ToGovernanceRule).ToList();

        return baseRules.Concat(scopedConverted)
            .OrderBy(r => r.Priority)
            .Take(maxRules)
            .ToList();
    }

    /// <summary>
    /// Convert LS-023 EffectiveRuleDto back to SmsGovernanceRule (without persisting).
    /// Used to blend scoped results into the existing rule resolution pipeline.
    /// </summary>
    private static SmsGovernanceRule ToGovernanceRule(EffectiveRuleDto r) =>
        new()
        {
            Id           = r.RuleId,
            RulePackId   = r.RulePackId,
            RuleType     = r.RuleType,
            Pattern      = r.Pattern,
            Severity     = r.Severity,
            MetadataJson = r.MetadataJson,
            Priority     = r.Priority,
            Enabled      = true,
        };

    /// <summary>
    /// override mode: tenant rules replace global rules that share the same RuleType.
    /// Global rule types not covered by any tenant rule are preserved.
    /// </summary>
    private static List<SmsGovernanceRule> ResolveOverride(
        List<SmsGovernanceRule> globalRules,
        List<SmsGovernanceRule> tenantRules)
    {
        var overriddenTypes = tenantRules.Select(r => r.RuleType).ToHashSet();
        var kept = globalRules.Where(r => !overriddenTypes.Contains(r.RuleType)).ToList();
        return [.. kept.Concat(tenantRules).OrderBy(r => r.Priority)];
    }

    private async Task<(string enforcementMode, bool profileAssigned)> ResolveEnforcementModeAsync(
        Guid? tenantId, CancellationToken ct)
    {
        if (tenantId == null) return ("standard", false);

        try
        {
            var assignment = await _db.SmsComplianceProfileAssignments
                .AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.Scope == "tenant" && a.Enabled)
                .FirstOrDefaultAsync(ct);

            if (assignment == null) return ("standard", false);

            var profile = await _db.SmsComplianceProfiles
                .AsNoTracking()
                .Where(p => p.Id == assignment.ProfileId && p.Enabled)
                .FirstOrDefaultAsync(ct);

            return profile == null
                ? ("standard", false)
                : (profile.EnforcementMode, true);
        }
        catch
        {
            return ("standard", false);
        }
    }

    private static SmsGovernanceRuleResolution Empty(
        List<SmsGovernanceRulePack>? packs = null) =>
        new()
        {
            Rules           = [],
            Packs           = packs ?? [],
            EnforcementMode = "standard",
            ProfileAssigned = false,
        };
}
