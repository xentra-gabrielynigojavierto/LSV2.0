namespace Notifications.Application.Interfaces;

// ── Request types ─────────────────────────────────────────────────────────────

public record CreateRolloutRequest(
    Guid    ReleasePackageId,
    string  Name,
    string? Description,
    string  RolloutStrategy,
    Guid?   TenantId,
    string? RollbackThresholdJson,
    string? RequestedBy);

public record AddRolloutStageRequest(
    int      StageNumber,
    string?  StageName,
    decimal? TenantPercentage,
    int?     DurationMinutes,
    string?  RequestedBy);

public record AddTenantCohortRequest(
    Guid?   StageId,
    Guid    TenantId,
    string  CohortName,
    string? RequestedBy);

public record RolloutListQuery(
    Guid?   ReleasePackageId = null,
    Guid?   TenantId         = null,
    string? State            = null,
    string? Strategy         = null,
    int     Page             = 1,
    int     PageSize         = 50);

// ── DTO types ─────────────────────────────────────────────────────────────────

public record RolloutPlanDto(
    Guid     Id,
    Guid     ReleasePackageId,
    Guid?    TenantId,
    string   Name,
    string?  Description,
    string   RolloutState,
    string   RolloutStrategy,
    int?     CurrentStageNumber,
    string?  RollbackThresholdJson,
    DateTime? StartedAt,
    DateTime? PausedAt,
    DateTime? ResumedAt,
    DateTime? CompletedAt,
    DateTime? RolledBackAt,
    DateTime? FailedAt,
    string?   FailureReason,
    DateTime  CreatedAt,
    DateTime  UpdatedAt,
    string?   CreatedBy,
    string?   UpdatedBy);

public record RolloutStageDto(
    Guid     Id,
    Guid     RolloutPlanId,
    int      StageNumber,
    string?  StageName,
    string   StageState,
    decimal? TenantPercentage,
    int?     DurationMinutes,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    DateTime? FailedAt,
    string?   FailureReason,
    DateTime  CreatedAt,
    DateTime  UpdatedAt);

public record TenantCohortDto(
    Guid     Id,
    Guid     RolloutPlanId,
    Guid?    StageId,
    Guid     TenantId,
    string   CohortName,
    bool     Enabled,
    DateTime? ActivatedAt,
    DateTime? RolledBackAt,
    DateTime  CreatedAt,
    DateTime  UpdatedAt);

public record RolloutDetailDto(
    RolloutPlanDto                 Plan,
    IReadOnlyList<RolloutStageDto> Stages,
    IReadOnlyList<TenantCohortDto> Cohorts);

public record RolloutAuditEventDto(
    Guid     Id,
    Guid     RolloutPlanId,
    Guid?    StageId,
    Guid?    TenantId,
    string   EventType,
    string?  PreviousState,
    string?  NewState,
    string?  Actor,
    string?  Reason,
    string?  MetadataJson,
    DateTime CreatedAt);

public record PaginatedRolloutResult(
    IReadOnlyList<RolloutPlanDto> Items,
    int Total,
    int Page,
    int PageSize);

public record RolloutOperationResult(bool Success, string? ErrorMessage = null);

// ── Interface ─────────────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-022: Central rollout orchestration service.
/// All state transitions are validated and audited.
/// Rollout failures never corrupt active governance state.
/// Full-activation strategy delegates to ISmsGovernanceReleaseService.ActivateAsync.
/// Canary/staged strategies record orchestration state only — true per-tenant
/// enforcement scoping requires LS-NOTIF-SMS-023.
/// </summary>
public interface ISmsGovernanceRolloutService
{
    Task<RolloutPlanDto>         CreateRolloutAsync(CreateRolloutRequest request, CancellationToken ct = default);
    Task<RolloutDetailDto?>      GetRolloutAsync(Guid rolloutId, CancellationToken ct = default);
    Task<PaginatedRolloutResult> ListRolloutsAsync(RolloutListQuery query, CancellationToken ct = default);

    Task<RolloutStageDto>       AddStageAsync(Guid rolloutId, AddRolloutStageRequest request, CancellationToken ct = default);
    Task<TenantCohortDto>       AddCohortTenantAsync(Guid rolloutId, AddTenantCohortRequest request, CancellationToken ct = default);

    Task<RolloutOperationResult> StartRolloutAsync(Guid rolloutId, string requestedBy, CancellationToken ct = default);
    Task<RolloutOperationResult> PauseRolloutAsync(Guid rolloutId, string requestedBy, string? reason, CancellationToken ct = default);
    Task<RolloutOperationResult> ResumeRolloutAsync(Guid rolloutId, string requestedBy, CancellationToken ct = default);
    Task<RolloutOperationResult> RollbackRolloutAsync(Guid rolloutId, string requestedBy, string? reason, CancellationToken ct = default);
    Task<RolloutOperationResult> AdvanceStageAsync(Guid rolloutId, string requestedBy, CancellationToken ct = default);
    Task<RolloutOperationResult> CompleteRolloutAsync(Guid rolloutId, string requestedBy, CancellationToken ct = default);

    Task<IReadOnlyList<RolloutAuditEventDto>> GetAuditTrailAsync(Guid rolloutId, CancellationToken ct = default);
}
