namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-021: One approval stage for a release package.
/// Multi-stage approval is supported by ordering on ApprovalStage (1-based).
/// Stage N cannot start until Stage N-1 is approved.
/// </summary>
public class SmsGovernanceApprovalRequest
{
    public Guid   Id               { get; set; }
    public Guid   ReleasePackageId { get; set; }
    public int    ApprovalStage    { get; set; }
    public string ApproverRole     { get; set; } = string.Empty;
    public int    RequiredApprovals { get; set; } = 1;

    /// <summary>pending | approved | rejected | cancelled</summary>
    public string    Status      { get; set; } = ApprovalRequestStatuses.Pending;
    public DateTime  RequestedAt { get; set; }
    public DateTime? ResolvedAt  { get; set; }
    public DateTime  CreatedAt   { get; set; }
    public DateTime  UpdatedAt   { get; set; }
}

public static class ApprovalRequestStatuses
{
    public const string Pending   = "pending";
    public const string Approved  = "approved";
    public const string Rejected  = "rejected";
    public const string Cancelled = "cancelled";
}
