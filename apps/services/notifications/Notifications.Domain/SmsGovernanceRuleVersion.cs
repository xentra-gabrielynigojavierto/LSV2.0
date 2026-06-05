namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-020: Immutable snapshot of a governance rule at a point in time.
///
/// Created automatically on every rule create/update/disable/rollback/import.
/// Versions are never deleted or mutated — they form an append-only audit trail.
///
/// VersionNumber increments per RuleId (1, 2, 3 ...).
/// RuleSnapshotJson contains all rule fields needed to restore the rule.
/// No secrets, credentials, or raw phone numbers are stored.
///
/// ChangeType values: created | updated | disabled | rollback | imported
/// </summary>
public sealed class SmsGovernanceRuleVersion
{
    public Guid    Id                { get; set; } = Guid.NewGuid();
    public Guid    RuleId            { get; set; }

    /// <summary>Denormalised for efficient pack-level version history queries.</summary>
    public Guid?   RulePackId        { get; set; }

    /// <summary>Monotonically increasing per RuleId. Starts at 1.</summary>
    public int     VersionNumber     { get; set; }

    /// <summary>JSON snapshot of the rule at the time of this version. No secrets.</summary>
    public string  RuleSnapshotJson  { get; set; } = string.Empty;

    /// <summary>created | updated | disabled | rollback | imported</summary>
    public string  ChangeType        { get; set; } = string.Empty;

    public string? ChangeReason      { get; set; }
    public DateTime CreatedAt        { get; set; } = DateTime.UtcNow;
    public string? CreatedBy         { get; set; }
}
