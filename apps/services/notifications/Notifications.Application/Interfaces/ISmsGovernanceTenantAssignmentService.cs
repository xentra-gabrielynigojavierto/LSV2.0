namespace Notifications.Application.Interfaces;

/// <summary>
/// LS-NOTIF-SMS-023: Orchestrates tenant rule-pack assignment and overlay lifecycle.
/// All assignment changes are audited. No raw phones or credentials stored.
/// </summary>
public interface ISmsGovernanceTenantAssignmentService
{
    // ── Assignments ───────────────────────────────────────────────────────────

    Task<AssignmentOperationResult> AssignRulePackAsync(
        AssignRulePackRequest request, CancellationToken ct = default);

    Task<AssignmentOperationResult> ActivateAssignmentAsync(
        Guid assignmentId, string requestedBy, CancellationToken ct = default);

    Task<AssignmentOperationResult> DeactivateAssignmentAsync(
        Guid assignmentId, string requestedBy, string? reason, CancellationToken ct = default);

    Task<AssignmentOperationResult> RollbackAssignmentAsync(
        Guid assignmentId, string requestedBy, string? reason, CancellationToken ct = default);

    Task<PaginatedAssignmentResult> ListAssignmentsAsync(
        TenantAssignmentQuery query, CancellationToken ct = default);

    Task<TenantAssignmentDto?> GetAssignmentAsync(
        Guid assignmentId, CancellationToken ct = default);

    // ── Overlays ──────────────────────────────────────────────────────────────

    Task<OverlayOperationResult> CreateOverlayAsync(
        CreateTenantOverlayRequest request, CancellationToken ct = default);

    Task<OverlayOperationResult> ActivateOverlayAsync(
        Guid overlayId, string requestedBy, CancellationToken ct = default);

    Task<OverlayOperationResult> DisableOverlayAsync(
        Guid overlayId, string requestedBy, string? reason, CancellationToken ct = default);

    Task<PaginatedOverlayResult> ListOverlaysAsync(
        TenantOverlayQuery query, CancellationToken ct = default);

    Task<TenantOverlayDto?> GetOverlayAsync(
        Guid overlayId, CancellationToken ct = default);

    // ── Audit ─────────────────────────────────────────────────────────────────

    Task<IReadOnlyList<TenantAssignmentAuditEventDto>> GetAuditTrailAsync(
        TenantAuditQuery query, CancellationToken ct = default);
}

// ── Request types ─────────────────────────────────────────────────────────────

public record AssignRulePackRequest(
    Guid    TenantId,
    Guid    RulePackId,
    string  AssignmentMode,
    int     Priority        = 100,
    DateTime? EffectiveFrom = null,
    DateTime? EffectiveTo   = null,
    Guid?   RolloutPlanId   = null,
    Guid?   RolloutStageId  = null,
    Guid?   ReleasePackageId = null,
    string? AssignedBy      = null);

public record CreateTenantOverlayRequest(
    Guid    TenantId,
    string  OverlayType,
    Guid?   RulePackId    = null,
    Guid?   RuleId        = null,
    string? OverrideJson  = null,
    int     Priority      = 100,
    DateTime? EffectiveFrom = null,
    DateTime? EffectiveTo   = null,
    string? CreatedBy     = null);

// ── Query types ───────────────────────────────────────────────────────────────

public record TenantAssignmentQuery(
    Guid?   TenantId        = null,
    Guid?   RulePackId      = null,
    string? AssignmentState = null,
    string? AssignmentMode  = null,
    Guid?   RolloutPlanId   = null,
    int     Page            = 1,
    int     PageSize        = 50);

public record TenantOverlayQuery(
    Guid?   TenantId        = null,
    Guid?   RulePackId      = null,
    Guid?   RuleId          = null,
    string? OverlayType     = null,
    string? OverlayState    = null,
    bool?   Enabled         = null,
    int     Page            = 1,
    int     PageSize        = 50);

public record TenantAuditQuery(
    Guid?   TenantId        = null,
    Guid?   AssignmentId    = null,
    Guid?   OverlayId       = null,
    string? EventType       = null,
    int     Page            = 1,
    int     PageSize        = 100);

// ── Result types ──────────────────────────────────────────────────────────────

public record AssignmentOperationResult(
    bool    Success,
    Guid?   AssignmentId    = null,
    string? ErrorMessage    = null,
    string? ErrorCode       = null);

public record OverlayOperationResult(
    bool    Success,
    Guid?   OverlayId       = null,
    string? ErrorMessage    = null,
    string? ErrorCode       = null);

// ── DTO types ─────────────────────────────────────────────────────────────────

public record TenantAssignmentDto(
    Guid    Id,
    Guid    TenantId,
    Guid    RulePackId,
    string  AssignmentState,
    string  AssignmentMode,
    int     Priority,
    DateTime? EffectiveFrom,
    DateTime? EffectiveTo,
    Guid?   RolloutPlanId,
    Guid?   RolloutStageId,
    Guid?   ReleasePackageId,
    string? AssignedBy,
    DateTime? ActivatedAt,
    DateTime? DeactivatedAt,
    DateTime? SupersededAt,
    string? DeactivationReason,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record TenantOverlayDto(
    Guid    Id,
    Guid    TenantId,
    Guid?   RulePackId,
    Guid?   RuleId,
    string  OverlayType,
    string  OverlayState,
    string? OverrideJson,
    int     Priority,
    bool    Enabled,
    DateTime? EffectiveFrom,
    DateTime? EffectiveTo,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? CreatedBy,
    string? UpdatedBy);

public record TenantAssignmentAuditEventDto(
    Guid    Id,
    Guid    TenantId,
    Guid?   AssignmentId,
    Guid?   OverlayId,
    string  EventType,
    string? PreviousState,
    string? NewState,
    string? Actor,
    string? Reason,
    string? MetadataJson,
    DateTime CreatedAt);

public record PaginatedAssignmentResult(
    IReadOnlyList<TenantAssignmentDto> Items,
    int Total, int Page, int PageSize);

public record PaginatedOverlayResult(
    IReadOnlyList<TenantOverlayDto> Items,
    int Total, int Page, int PageSize);
