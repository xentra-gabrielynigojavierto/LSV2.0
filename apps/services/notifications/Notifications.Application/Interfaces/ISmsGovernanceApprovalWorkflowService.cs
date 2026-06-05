namespace Notifications.Application.Interfaces;

// ── Request types ────────────────────────────────────────────────────────────

public record ApproveReleaseRequest(
    string? DecidedBy,
    string? DecidedByRole,
    string? Reason);

public record RejectReleaseRequest(
    string? DecidedBy,
    string? DecidedByRole,
    string  Reason);

public record ApprovalQuery(
    string? ApproverRole = null,
    int     Page         = 1,
    int     PageSize     = 50);

public record PendingApprovalDto(
    Guid     ReleasePackageId,
    string   ReleaseName,
    string?  ReleaseDescription,
    Guid?    TenantId,
    int      ApprovalStage,
    string   ApproverRole,
    int      RequiredApprovals,
    int      ApprovalCount,
    DateTime RequestedAt);

// ── Service interface ────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-021: Approval workflow orchestration.
/// Supports ordered multi-stage approval. Stage N starts only after Stage N-1 resolves.
/// Rejecting any stage rejects the release. Final-stage approval moves release to approved.
/// All decisions are append-only.
/// </summary>
public interface ISmsGovernanceApprovalWorkflowService
{
    Task CreateApprovalRequestsAsync(Guid releaseId, CancellationToken ct = default);
    Task<ReleaseOperationResult> ApproveAsync(Guid releaseId, ApproveReleaseRequest request, CancellationToken ct = default);
    Task<ReleaseOperationResult> RejectAsync(Guid releaseId, RejectReleaseRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<PendingApprovalDto>> GetPendingApprovalsAsync(ApprovalQuery query, CancellationToken ct = default);
}
