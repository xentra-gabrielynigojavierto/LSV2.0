namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-020: Immutable snapshot of a governance rule pack at a point in time.
///
/// Created automatically on every pack create/update/disable/rollback/import.
/// Versions are never deleted or mutated — append-only audit trail.
///
/// VersionNumber increments per RulePackId (1, 2, 3 ...).
/// PackSnapshotJson contains all pack fields needed to restore the pack.
/// IncludedRulesSnapshotJson optionally captures the rule set at snapshot time.
/// No secrets, credentials, or raw phone numbers are stored.
///
/// ChangeType values: created | updated | disabled | rollback | imported
/// </summary>
public sealed class SmsGovernanceRulePackVersion
{
    public Guid    Id                        { get; set; } = Guid.NewGuid();
    public Guid    RulePackId                { get; set; }

    /// <summary>Monotonically increasing per RulePackId. Starts at 1.</summary>
    public int     VersionNumber             { get; set; }

    /// <summary>JSON snapshot of the pack fields only. No secrets.</summary>
    public string  PackSnapshotJson          { get; set; } = string.Empty;

    /// <summary>Optional JSON array of rule snapshots included in this pack at snapshot time.</summary>
    public string? IncludedRulesSnapshotJson { get; set; }

    /// <summary>created | updated | disabled | rollback | imported</summary>
    public string  ChangeType                { get; set; } = string.Empty;

    public string? ChangeReason              { get; set; }
    public DateTime CreatedAt               { get; set; } = DateTime.UtcNow;
    public string? CreatedBy                { get; set; }
}
