namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-023: Per-tenant assignment of an existing governance rule pack.
/// Allows a global rule pack to be enforced for a specific tenant in isolation,
/// without duplicating the pack. Supports rollout-scoped canary assignments.
/// No raw phones, credentials, or message bodies stored.
/// </summary>
public class SmsGovernanceTenantRulePackAssignment
{
    public Guid Id { get; set; }

    /// <summary>Target tenant (opaque Guid — no PII).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The global rule pack being assigned to this tenant.</summary>
    public Guid RulePackId { get; set; }

    /// <summary>Assignment lifecycle state.</summary>
    public string AssignmentState { get; set; } = AssignmentStates.Draft;

    /// <summary>How this assignment interacts with global/other packs.</summary>
    public string AssignmentMode { get; set; } = AssignmentModes.Inherited;

    /// <summary>Lower = higher priority when multiple assignments exist.</summary>
    public int Priority { get; set; } = 100;

    /// <summary>UTC time from which this assignment is effective. Null = immediate.</summary>
    public DateTime? EffectiveFrom { get; set; }

    /// <summary>UTC time at which this assignment expires. Null = no expiry.</summary>
    public DateTime? EffectiveTo { get; set; }

    /// <summary>Optional rollout plan that created this assignment (LS-022 integration).</summary>
    public Guid? RolloutPlanId { get; set; }

    /// <summary>Optional rollout stage that created this assignment.</summary>
    public Guid? RolloutStageId { get; set; }

    /// <summary>Optional release package for traceability (LS-021).</summary>
    public Guid? ReleasePackageId { get; set; }

    public string? AssignedBy { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime? DeactivatedAt { get; set; }
    public DateTime? SupersededAt { get; set; }
    public string? DeactivationReason { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // ── State constants ───────────────────────────────────────────────────────

    public static class AssignmentStates
    {
        public const string Draft       = "draft";
        public const string Scheduled   = "scheduled";
        public const string Active      = "active";
        public const string Inactive    = "inactive";
        public const string Superseded  = "superseded";
        public const string Expired     = "expired";
        public const string RolledBack  = "rolled_back";

        public static readonly IReadOnlySet<string> Terminal = new HashSet<string>
            { Superseded, Expired, RolledBack };
    }

    // ── Mode constants ────────────────────────────────────────────────────────

    public static class AssignmentModes
    {
        /// <summary>Tenant inherits global packs AND has this pack (additive).</summary>
        public const string Inherited       = "inherited";
        /// <summary>Only this assigned pack applies; global packs excluded for tenant.</summary>
        public const string Isolated        = "isolated";
        /// <summary>This pack overlays global behaviour (priority-controlled merge).</summary>
        public const string Overlay         = "overlay";
        /// <summary>Created by canary rollout; applies only during rollout window.</summary>
        public const string RolloutCanary   = "rollout_canary";
        /// <summary>Created by a specific rollout stage.</summary>
        public const string RolloutStage    = "rollout_stage";
    }
}
