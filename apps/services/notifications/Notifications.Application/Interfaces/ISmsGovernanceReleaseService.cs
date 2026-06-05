namespace Notifications.Application.Interfaces;

// ── Request / Response types ─────────────────────────────────────────────────

public record CreateReleaseRequest(
    string  Name,
    string? Description,
    string  ReleaseType,
    Guid?   TenantId,
    string? RequestedBy);

public record AddReleaseItemRequest(
    string  EntityType,
    Guid    EntityId,
    int?    EntityVersionNumber,
    string  ActionType,
    string? EntitySnapshotJson,
    string? RequestedBy);

public record ReleaseListQuery(
    Guid?   TenantId    = null,
    string? State       = null,
    string? ReleaseType = null,
    int     Page        = 1,
    int     PageSize    = 50);

public record ReleasePackageDto(
    Guid     Id,
    Guid?    TenantId,
    string   Name,
    string?  Description,
    string   ReleaseState,
    string   ReleaseType,
    DateTime? ScheduledActivationAt,
    DateTime? ActivatedAt,
    DateTime? RejectedAt,
    DateTime? ArchivedAt,
    DateTime  CreatedAt,
    DateTime  UpdatedAt,
    string?  CreatedBy,
    string?  UpdatedBy,
    int      ItemCount);

public record ReleaseItemDto(
    Guid    Id,
    Guid    ReleasePackageId,
    string  EntityType,
    Guid    EntityId,
    int?    EntityVersionNumber,
    string  ActionType,
    DateTime CreatedAt,
    string?  CreatedBy);

public record ReleaseDetailDto(
    ReleasePackageDto                  Package,
    IReadOnlyList<ReleaseItemDto>      Items,
    IReadOnlyList<ApprovalRequestDto>  ApprovalRequests);

public record ApprovalRequestDto(
    Guid     Id,
    Guid     ReleasePackageId,
    int      ApprovalStage,
    string   ApproverRole,
    int      RequiredApprovals,
    string   Status,
    DateTime RequestedAt,
    DateTime? ResolvedAt,
    int      ApprovalCount,
    IReadOnlyList<ApprovalDecisionDto> Decisions);

public record ApprovalDecisionDto(
    Guid    Id,
    string  Decision,
    string? DecisionReason,
    string? DecidedBy,
    string? DecidedByRole,
    DateTime CreatedAt);

public record ReleaseAuditEventDto(
    Guid    Id,
    Guid    ReleasePackageId,
    string  EventType,
    string? PreviousState,
    string? NewState,
    string? Actor,
    string? Reason,
    string? MetadataJson,
    DateTime CreatedAt);

public record PaginatedReleaseResult(
    IReadOnlyList<ReleasePackageDto> Items,
    int Total,
    int Page,
    int PageSize);

public record ReleaseOperationResult(bool Success, string? ErrorMessage = null);

// ── Service interface ────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-021: Central release orchestration service.
/// All state transitions are validated and audited.
/// Activation is transactional and failure-safe — existing governance is never corrupted.
/// </summary>
public interface ISmsGovernanceReleaseService
{
    Task<ReleasePackageDto>    CreateReleaseAsync(CreateReleaseRequest request, CancellationToken ct = default);
    Task<ReleaseDetailDto?>    GetReleaseAsync(Guid releaseId, CancellationToken ct = default);
    Task<PaginatedReleaseResult> ListReleasesAsync(ReleaseListQuery query, CancellationToken ct = default);

    Task<ReleaseItemDto>        AddReleaseItemAsync(Guid releaseId, AddReleaseItemRequest request, CancellationToken ct = default);
    Task<ReleaseOperationResult> RemoveReleaseItemAsync(Guid releaseId, Guid itemId, string requestedBy, CancellationToken ct = default);

    Task<ReleaseOperationResult> SubmitForReviewAsync(Guid releaseId, string requestedBy, CancellationToken ct = default);
    Task<ReleaseOperationResult> ScheduleActivationAsync(Guid releaseId, DateTime activateAtUtc, string requestedBy, CancellationToken ct = default);
    Task<ReleaseOperationResult> ActivateAsync(Guid releaseId, string requestedBy, CancellationToken ct = default);
    Task<ReleaseOperationResult> ArchiveAsync(Guid releaseId, string requestedBy, string? reason, CancellationToken ct = default);

    Task<IReadOnlyList<ReleaseAuditEventDto>> GetAuditTrailAsync(Guid releaseId, CancellationToken ct = default);
}
