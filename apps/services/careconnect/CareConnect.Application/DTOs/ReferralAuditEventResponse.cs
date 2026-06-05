namespace CareConnect.Application.DTOs;

/// <summary>
/// LSCC-005-02: A single item in the referral operational audit timeline.
///
/// Sourced from three local data stores:
///   1. ReferralStatusHistory — status transitions (Created, Accepted, Cancelled, etc.)
///   2. CareConnectNotifications — email delivery events (Sent, Failed, Retrying, RetryExhausted, Resent)
///   3. ReferralProviderReassignments — provider reassignment events (old provider ID → new provider ID)
///
/// Ordered chronologically (oldest first) for display in the ReferralAuditTimeline component.
/// </summary>
public class ReferralAuditEventResponse
{
    /// <summary>Machine-readable event type identifier.</summary>
    public string   EventType  { get; set; } = string.Empty;

    /// <summary>Human-readable label for the event (e.g. "Provider Notification Sent").</summary>
    public string   Label      { get; set; } = string.Empty;

    /// <summary>ISO 8601 UTC timestamp when the event occurred.</summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>
    /// Optional contextual detail (e.g. attempt number, failure reason, old → new status transition).
    /// Kept concise — no raw internal payloads.
    /// </summary>
    public string?  Detail     { get; set; }

    /// <summary>
    /// Event category for UI colour-coding.
    /// Values: info | success | warning | error | security
    /// </summary>
    public string   Category   { get; set; } = "info";
}
