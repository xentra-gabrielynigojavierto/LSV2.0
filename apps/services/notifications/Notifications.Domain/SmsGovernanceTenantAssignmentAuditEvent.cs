namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-023: Append-only audit trail for tenant governance assignment
/// and overlay lifecycle events. No raw phones, credentials, or message bodies.
/// </summary>
public class SmsGovernanceTenantAssignmentAuditEvent
{
    public Guid Id { get; set; }

    /// <summary>Target tenant (opaque Guid).</summary>
    public Guid TenantId { get; set; }

    public Guid? AssignmentId { get; set; }
    public Guid? OverlayId { get; set; }

    /// <summary>Specific event type — see <see cref="EventTypes"/>.</summary>
    public string EventType { get; set; } = string.Empty;

    public string? PreviousState { get; set; }
    public string? NewState { get; set; }

    /// <summary>User/service that triggered the event.</summary>
    public string? Actor { get; set; }

    public string? Reason { get; set; }

    /// <summary>
    /// Safe JSON metadata (pack/rule IDs, rollout context, etc.).
    /// Must not contain secrets, phone numbers, or message bodies.
    /// </summary>
    public string? MetadataJson { get; set; }

    public DateTime CreatedAt { get; set; }

    // ── Event type constants ──────────────────────────────────────────────────

    public static class EventTypes
    {
        public const string AssignmentCreated            = "assignment_created";
        public const string AssignmentActivated          = "assignment_activated";
        public const string AssignmentDeactivated        = "assignment_deactivated";
        public const string AssignmentSuperseded         = "assignment_superseded";
        public const string AssignmentRolledBack         = "assignment_rolled_back";
        public const string AssignmentExpired            = "assignment_expired";

        public const string OverlayCreated               = "overlay_created";
        public const string OverlayActivated             = "overlay_activated";
        public const string OverlayDisabled              = "overlay_disabled";
        public const string OverlayExpired               = "overlay_expired";

        public const string ResolutionEvaluated          = "resolution_evaluated";
        public const string IsolationValidationFailed    = "isolation_validation_failed";
        public const string RolloutAssignmentCreated     = "rollout_assignment_created";
        public const string RolloutAssignmentRolledBack  = "rollout_assignment_rolled_back";
    }
}
