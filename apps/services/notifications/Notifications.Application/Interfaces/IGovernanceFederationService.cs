namespace Notifications.Application.Interfaces;

// ---------------------------------------------------------------------------
// Request / Query / DTO types for IGovernanceFederationService
// ---------------------------------------------------------------------------

public record CreateChannelScopeRequest(
    string  ChannelType,
    string  ScopeMode,
    bool    Enabled,
    int     Priority,
    string? Description,
    string? RequestedBy);

public record UpdateChannelScopeRequest(
    string? ScopeMode,
    bool?   Enabled,
    int?    Priority,
    string? Description,
    string? RequestedBy);

public record FederateRulePackRequest(
    Guid    RulePackId,
    string  ChannelType,
    string? FederationGroup,
    Guid?   TenantId,
    int     Priority,
    DateTime? EffectiveFrom,
    DateTime? EffectiveTo,
    string? RequestedBy);

public record CreateFederationOverlayRequest(
    Guid?   TenantId,
    string  ChannelType,
    Guid?   RulePackId,
    Guid?   RuleId,
    string  OverlayType,
    string? OverlayJson,
    int     Priority,
    DateTime? EffectiveFrom,
    DateTime? EffectiveTo,
    string? RequestedBy);

public record ChannelScopeQuery(
    string? ChannelType  = null,
    string? ScopeMode    = null,
    bool?   Enabled      = null,
    int     Page         = 1,
    int     PageSize     = 50);

public record FederatedRulePackQuery(
    string? ChannelType     = null,
    Guid?   RulePackId      = null,
    Guid?   TenantId        = null,
    string? FederationGroup = null,
    bool?   Enabled         = null,
    int     Page            = 1,
    int     PageSize        = 50);

public record FederationOverlayQuery(
    string? ChannelType  = null,
    Guid?   TenantId     = null,
    Guid?   RulePackId   = null,
    string? OverlayType  = null,
    string? OverlayState = null,
    bool?   Enabled      = null,
    int     Page         = 1,
    int     PageSize     = 50);

public record ChannelScopeDto(
    Guid    Id,
    string  ChannelType,
    string  ScopeMode,
    bool    Enabled,
    int     Priority,
    string? Description,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string?  CreatedBy);

public record FederatedRulePackDto(
    Guid     Id,
    Guid     RulePackId,
    string   ChannelType,
    string?  FederationGroup,
    Guid?    TenantId,
    bool     Enabled,
    int      Priority,
    DateTime? EffectiveFrom,
    DateTime? EffectiveTo,
    DateTime  CreatedAt,
    string?   CreatedBy);

public record FederationOverlayDto(
    Guid     Id,
    Guid?    TenantId,
    string   ChannelType,
    Guid?    RulePackId,
    Guid?    RuleId,
    string   OverlayType,
    string   OverlayState,
    int      Priority,
    bool     Enabled,
    DateTime? EffectiveFrom,
    DateTime? EffectiveTo,
    DateTime  CreatedAt,
    string?   CreatedBy);

public record PaginatedFederationResult<T>(
    int    Total,
    int    Page,
    int    PageSize,
    IReadOnlyList<T> Items);

public record FederationOperationResult(bool Success, string? ErrorMessage = null);

// ---------------------------------------------------------------------------
// Interface
// ---------------------------------------------------------------------------

public interface IGovernanceFederationService
{
    Task<ChannelScopeDto>         CreateChannelScopeAsync(CreateChannelScopeRequest request, CancellationToken ct = default);
    Task<ChannelScopeDto>         UpdateChannelScopeAsync(Guid scopeId, UpdateChannelScopeRequest request, CancellationToken ct = default);
    Task<FederatedRulePackDto>    FederateRulePackAsync(FederateRulePackRequest request, CancellationToken ct = default);
    Task<FederationOperationResult> DisableFederatedRulePackAsync(Guid mappingId, string requestedBy, string? reason, CancellationToken ct = default);
    Task<FederationOverlayDto>    CreateFederationOverlayAsync(CreateFederationOverlayRequest request, CancellationToken ct = default);
    Task<FederationOperationResult> ActivateFederationOverlayAsync(Guid overlayId, string requestedBy, CancellationToken ct = default);
    Task<FederationOperationResult> DisableFederationOverlayAsync(Guid overlayId, string requestedBy, string? reason, CancellationToken ct = default);
    Task<PaginatedFederationResult<ChannelScopeDto>>      ListChannelScopesAsync(ChannelScopeQuery query, CancellationToken ct = default);
    Task<PaginatedFederationResult<FederatedRulePackDto>>  ListFederatedRulePacksAsync(FederatedRulePackQuery query, CancellationToken ct = default);
    Task<PaginatedFederationResult<FederationOverlayDto>>  ListFederationOverlaysAsync(FederationOverlayQuery query, CancellationToken ct = default);
}
