namespace Notifications.Application.Interfaces;

/// <summary>
/// LS-NOTIF-SMS-023: Resolves the effective governance rule set for a specific tenant,
/// incorporating tenant rule-pack assignments and overlays on top of global governance.
/// </summary>
public interface ISmsGovernanceTenantResolutionService
{
    /// <summary>
    /// Resolves effective rule pack IDs for a tenant, incorporating assignments and overlays.
    /// Returns global pack IDs when tenant has no assignments (backward compatible).
    /// </summary>
    Task<TenantResolutionResult> ResolveEffectiveRulePacksAsync(
        Guid? tenantId,
        GovernanceResolutionContext context,
        CancellationToken ct = default);

    /// <summary>
    /// Resolves the full effective rule set for a tenant as a flat list of effective rule DTOs
    /// after applying overlays (disable, override-severity, override-pattern, add-rule, etc.).
    /// </summary>
    Task<EffectiveRuleSetResult> ResolveEffectiveRulesAsync(
        Guid? tenantId,
        GovernanceResolutionContext context,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the full effective governance graph for a tenant (for admin/diagnostic use).
    /// Shows all resolution layers: global packs, tenant assignments, rollout assignments, overlays.
    /// </summary>
    Task<EffectiveGovernanceGraphDto> GetEffectiveGovernanceGraphAsync(
        Guid tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns a human-readable explanation of how the effective rule set was resolved for a tenant.
    /// Safe diagnostic output — no secrets or raw phones.
    /// </summary>
    Task<ResolutionExplanationDto> ExplainResolutionAsync(
        Guid tenantId,
        CancellationToken ct = default);
}

// ── Resolution context ────────────────────────────────────────────────────────

public record GovernanceResolutionContext(
    Guid?   NotificationId      = null,
    Guid?   TemplateId          = null,
    Guid?   RolloutPlanId       = null,
    Guid?   ReleasePackageId    = null,
    string? EvaluationContext   = null,
    DateTime? NowUtc            = null);

// ── Result types ──────────────────────────────────────────────────────────────

public record TenantResolutionResult(
    Guid?               TenantId,
    IReadOnlyList<Guid> GlobalPackIds,
    IReadOnlyList<Guid> AssignedPackIds,
    IReadOnlyList<Guid> RolloutPackIds,
    string              ResolutionMode,
    bool                HasTenantAssignments,
    bool                FellBackToGlobal,
    string?             Warning = null);

public record EffectiveRuleSetResult(
    Guid?                         TenantId,
    IReadOnlyList<EffectiveRuleDto> Rules,
    IReadOnlyList<Guid>           DisabledRuleIds,
    IReadOnlyList<string>         OverlaysSummary,
    string                        ResolutionMode,
    bool                          FellBackToGlobal);

public record EffectiveRuleDto(
    Guid    RuleId,
    Guid    RulePackId,
    string  RuleType,
    string  Pattern,
    string  Severity,
    string? MetadataJson,
    int     Priority,
    bool    IsFromAssignment,
    bool    IsFromOverlay,
    string? OverlayType,
    string? OriginalSeverity);

public record EffectiveGovernanceGraphDto(
    Guid                            TenantId,
    string                          ResolutionMode,
    IReadOnlyList<PackSummaryDto>   InheritedGlobalPacks,
    IReadOnlyList<PackSummaryDto>   TenantAssignedPacks,
    IReadOnlyList<PackSummaryDto>   RolloutAssignedPacks,
    IReadOnlyList<OverlaySummaryDto> OverlaysApplied,
    IReadOnlyList<Guid>             DisabledRuleIds,
    int                             FinalRuleCount,
    IReadOnlyList<string>           Warnings);

public record PackSummaryDto(
    Guid    PackId,
    string  PackName,
    string? AssignmentMode,
    int     RuleCount,
    int     Priority);

public record OverlaySummaryDto(
    Guid    OverlayId,
    string  OverlayType,
    Guid?   RuleId,
    Guid?   RulePackId,
    int     Priority,
    string  Summary);

public record ResolutionExplanationDto(
    Guid                      TenantId,
    string                    ResolutionMode,
    int                       TotalAssignments,
    int                       ActiveAssignments,
    int                       TotalOverlays,
    int                       ActiveOverlays,
    int                       DisabledRulesCount,
    int                       FinalRuleCount,
    bool                      HasTenantAssignments,
    bool                      UsesGlobalFallback,
    IReadOnlyList<string>     Steps,
    IReadOnlyList<string>     Warnings);
