namespace Notifications.Application.Interfaces;

// ---------------------------------------------------------------------------
// Request / Result types for IGovernanceTopologyResolver
// ---------------------------------------------------------------------------

public record GovernanceTopologyRequest(
    Guid?    TenantId,
    string   ChannelType,
    Guid?    RolloutPlanId       = null,
    Guid?    ReleasePackageId    = null,
    string?  EvaluationContext   = null,
    DateTime? NowUtc             = null);

public record ChannelPackSummary(
    Guid    RulePackId,
    string  PackName,
    string  Source,
    string? FederationGroup,
    int     Priority,
    bool    IsGlobal,
    bool    IsChannelFederated,
    bool    IsTenantAssigned);

public record FederationOverlaySummary(
    Guid    OverlayId,
    string  OverlayType,
    string  ChannelType,
    Guid?   TenantId,
    Guid?   RulePackId,
    Guid?   RuleId,
    int     Priority);

public record GovernanceTopologyGraph(
    string   ChannelType,
    Guid?    TenantId,
    string   ScopeMode,
    IReadOnlyList<ChannelPackSummary>        GlobalPacks,
    IReadOnlyList<ChannelPackSummary>        ChannelPacks,
    IReadOnlyList<ChannelPackSummary>        TenantPacks,
    IReadOnlyList<ChannelPackSummary>        FederatedPacks,
    IReadOnlyList<FederationOverlaySummary>  TenantOverlays,
    IReadOnlyList<FederationOverlaySummary>  FederationOverlays,
    IReadOnlyList<string>                    RolloutOverrides,
    int      FinalRuleCount,
    IReadOnlyList<string>                    Warnings);

public record TopologyEffectiveRule(
    Guid    RuleId,
    string  RuleName,
    string  RuleType,
    string  Severity,
    string  ChannelType,
    string  Source,
    string? OverrideApplied);

public record TopologyEffectiveRules(
    string   ChannelType,
    Guid?    TenantId,
    IReadOnlyList<TopologyEffectiveRule> Rules,
    IReadOnlyList<string> Warnings,
    bool     FederationEnabled,
    bool     ChannelScopeFound);

public record TopologyExplanationStep(
    int     StepNumber,
    string  StepName,
    string  Description,
    int     RulesContributed,
    int     RulesFiltered,
    IReadOnlyList<string> Details);

public record TopologyExplanation(
    string   ChannelType,
    Guid?    TenantId,
    IReadOnlyList<TopologyExplanationStep> Steps,
    int      TotalFinalRules,
    bool     FederationEnabled,
    bool     ChannelScopeFound,
    string   ResolutionMode);

// ---------------------------------------------------------------------------
// Interface
// ---------------------------------------------------------------------------

public interface IGovernanceTopologyResolver
{
    Task<GovernanceTopologyGraph>     ResolveTopologyAsync(GovernanceTopologyRequest request, CancellationToken ct = default);
    Task<TopologyEffectiveRules>      ResolveEffectiveRulesAsync(GovernanceTopologyRequest request, CancellationToken ct = default);
    Task<TopologyExplanation>         ExplainTopologyAsync(GovernanceTopologyRequest request, CancellationToken ct = default);
}
