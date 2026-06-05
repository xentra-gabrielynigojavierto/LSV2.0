namespace Notifications.Application.Interfaces;

// ---------------------------------------------------------------------------
// Query / Result types for IGovernanceFederationAnalyticsService
// ---------------------------------------------------------------------------

public record FederationAnalyticsQuery(
    string?   ChannelType     = null,
    string?   FederationGroup = null,
    Guid?     TenantId        = null,
    DateTime? FromUtc         = null,
    DateTime? ToUtc           = null);

public record ChannelGovernanceStats(
    string ChannelType,
    int    ActiveChannelScopes,
    int    FederatedPackCount,
    int    ActiveOverlayCount,
    int    TenantCoverageCount,
    string ScopeMode,
    bool   Enabled);

public record FederatedPackStats(
    Guid   RulePackId,
    string PackName,
    int    ChannelCount,
    int    TenantSpecificCount,
    IReadOnlyList<string> Channels);

public record TopologyAnalyticsResult(
    int    TotalChannelScopes,
    int    EnabledChannelScopes,
    int    TotalFederatedPacks,
    int    EnabledFederatedPacks,
    int    TotalFederationOverlays,
    int    ActiveFederationOverlays,
    int    TotalAuditEvents,
    IReadOnlyList<ChannelGovernanceStats> ByChannel,
    IReadOnlyList<string> Warnings);

public record ChannelGovernanceAnalyticsResult(
    string   ChannelType,
    int      FederatedPackCount,
    int      GlobalPackCount,
    int      TenantSpecificPackCount,
    int      ActiveOverlayCount,
    int      TenantCoverageCount,
    IReadOnlyList<FederatedPackStats> TopPacks);

public record FederatedPackAnalyticsResult(
    Guid     RulePackId,
    int      TotalChannelMappings,
    int      EnabledMappings,
    int      TenantSpecificMappings,
    IReadOnlyList<string> ActiveChannels,
    IReadOnlyList<string> FederationGroups);

public record CrossChannelRolloutAnalyticsResult(
    int    TotalRolloutPlans,
    int    ActiveRolloutPlans,
    IReadOnlyList<string> ChannelsCovered,
    int    FederationAuditEventsCount);

// ---------------------------------------------------------------------------
// Interface
// ---------------------------------------------------------------------------

public interface IGovernanceFederationAnalyticsService
{
    Task<TopologyAnalyticsResult>          GetTopologyAnalyticsAsync(FederationAnalyticsQuery query, CancellationToken ct = default);
    Task<ChannelGovernanceAnalyticsResult> GetChannelGovernanceAnalyticsAsync(FederationAnalyticsQuery query, CancellationToken ct = default);
    Task<FederatedPackAnalyticsResult>     GetFederatedRulePackAnalyticsAsync(Guid rulePackId, CancellationToken ct = default);
    Task<CrossChannelRolloutAnalyticsResult> GetCrossChannelRolloutAnalyticsAsync(FederationAnalyticsQuery query, CancellationToken ct = default);
}
