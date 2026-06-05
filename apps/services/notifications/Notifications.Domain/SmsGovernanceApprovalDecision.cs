namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-021: An individual approver's decision for an approval request.
/// Decisions are append-only — records are never deleted or mutated.
/// Must not store secrets, credentials, raw phones, or message content.
/// </summary>
public class SmsGovernanceApprovalDecision
{
    public Guid    Id                { get; set; }
    public Guid    ApprovalRequestId { get; set; }
    public Guid    ReleasePackageId  { get; set; }

    /// <summary>approve | reject</summary>
    public string  Decision          { get; set; } = string.Empty;
    public string? DecisionReason    { get; set; }
    public string? DecidedBy         { get; set; }
    public string? DecidedByRole     { get; set; }

    public DateTime CreatedAt { get; set; }
}

public static class ApprovalDecisions
{
    public const string Approve = "approve";
    public const string Reject  = "reject";
}
