namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-023: Tenant-specific governance overlay.
/// Allows a tenant to disable, suppress, or modify the behavior of specific
/// global governance rules without altering the base rule. Overlays never
/// mutate base SmsGovernanceRule or SmsGovernanceRulePack records.
/// No raw phones, credentials, or message bodies stored.
/// </summary>
public class SmsGovernanceTenantOverlay
{
    public Guid Id { get; set; }

    /// <summary>Target tenant (opaque Guid — no PII).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Optional target rule pack (null = applies to all packs for tenant).</summary>
    public Guid? RulePackId { get; set; }

    /// <summary>Optional target rule (null = pack-level overlay).</summary>
    public Guid? RuleId { get; set; }

    /// <summary>What this overlay does to the effective rule set.</summary>
    public string OverlayType { get; set; } = OverlayTypes.DisableRule;

    /// <summary>Lifecycle state of this overlay.</summary>
    public string OverlayState { get; set; } = OverlayStates.Draft;

    /// <summary>
    /// Safe JSON blob for overlay parameters (e.g. new severity, pattern, metadata).
    /// Must not contain secrets, credentials, phone numbers, or message content.
    /// </summary>
    public string? OverrideJson { get; set; }

    /// <summary>Lower = applied first when multiple overlays of same type exist.</summary>
    public int Priority { get; set; } = 100;

    public bool Enabled { get; set; } = true;

    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    // ── Overlay type constants ────────────────────────────────────────────────

    public static class OverlayTypes
    {
        /// <summary>Remove target rule from tenant's effective rule set.</summary>
        public const string DisableRule           = "disable_rule";
        /// <summary>Same as disable_rule (alias for suppress).</summary>
        public const string SuppressRule          = "suppress_rule";
        /// <summary>Override severity (warn/review_required/block) for tenant only.</summary>
        public const string OverrideSeverity      = "override_severity";
        /// <summary>Override the rule pattern/phrase for tenant only.</summary>
        public const string OverridePattern       = "override_pattern";
        /// <summary>Merge or replace rule MetadataJson for tenant only.</summary>
        public const string OverrideMetadata      = "override_metadata";
        /// <summary>Override rule's classification behavior for tenant only.</summary>
        public const string OverrideClassification = "override_classification";
        /// <summary>Synthesize a tenant-specific rule from overlay JSON (pack-level).</summary>
        public const string AddRule               = "add_rule";
    }

    // ── Overlay state constants ───────────────────────────────────────────────

    public static class OverlayStates
    {
        public const string Draft      = "draft";
        public const string Active     = "active";
        public const string Inactive   = "inactive";
        public const string Expired    = "expired";
        public const string Superseded = "superseded";
    }
}
