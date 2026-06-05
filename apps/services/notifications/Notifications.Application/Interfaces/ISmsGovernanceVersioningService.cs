namespace Notifications.Application.Interfaces;

// ─── Result types ─────────────────────────────────────────────────────────────

/// <summary>LS-NOTIF-SMS-020: Summary of a single rule version snapshot.</summary>
public sealed class RuleVersionDto
{
    public Guid    Id            { get; init; }
    public Guid    RuleId        { get; init; }
    public Guid?   RulePackId    { get; init; }
    public int     VersionNumber { get; init; }

    /// <summary>Safe snapshot JSON — no secrets.</summary>
    public string  RuleSnapshotJson { get; init; } = string.Empty;

    public string  ChangeType    { get; init; } = string.Empty;
    public string? ChangeReason  { get; init; }
    public DateTime CreatedAt    { get; init; }
    public string? CreatedBy     { get; init; }
}

/// <summary>LS-NOTIF-SMS-020: Summary of a single rule-pack version snapshot.</summary>
public sealed class RulePackVersionDto
{
    public Guid    Id                        { get; init; }
    public Guid    RulePackId                { get; init; }
    public int     VersionNumber             { get; init; }
    public string  PackSnapshotJson          { get; init; } = string.Empty;
    public string? IncludedRulesSnapshotJson { get; init; }
    public string  ChangeType                { get; init; } = string.Empty;
    public string? ChangeReason              { get; init; }
    public DateTime CreatedAt               { get; init; }
    public string? CreatedBy                { get; init; }
}

/// <summary>LS-NOTIF-SMS-020: Result of a rollback operation.</summary>
public sealed class RollbackResult
{
    public bool    Success       { get; init; }
    public string? ErrorMessage  { get; init; }
    public int     RestoredToVersion { get; init; }
    public int     NewVersionNumber  { get; init; }

    public static RollbackResult Ok(int restoredTo, int newVersion) =>
        new() { Success = true, RestoredToVersion = restoredTo, NewVersionNumber = newVersion };

    public static RollbackResult Fail(string error) =>
        new() { Success = false, ErrorMessage = error };
}

// ─── Interface ────────────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-020: Governance rule and rule-pack versioning service.
///
/// Creates immutable snapshots on every mutation and supports rollback.
/// Rollback creates a new version (ChangeType=rollback) — history is never deleted.
/// All snapshot JSON is sanitised — no secrets, no raw phone numbers.
///
/// Snapshot failures fail the calling mutation unless the caller opts out.
/// </summary>
public interface ISmsGovernanceVersioningService
{
    /// <summary>
    /// Create an immutable snapshot of the given rule's current state.
    /// Called immediately after the rule is saved to the database.
    /// </summary>
    Task SnapshotRuleAsync(
        Guid ruleId,
        string changeType,
        string? changeReason,
        string? requestedBy,
        CancellationToken ct = default);

    /// <summary>
    /// Create an immutable snapshot of the given rule pack's current state.
    /// When includeRules=true, the current rules in the pack are serialised into
    /// IncludedRulesSnapshotJson (subject to MaxSnapshotJsonBytes limit).
    /// </summary>
    Task SnapshotRulePackAsync(
        Guid rulePackId,
        string changeType,
        string? changeReason,
        string? requestedBy,
        bool includeRules = true,
        CancellationToken ct = default);

    /// <summary>Returns all version snapshots for a rule, newest first.</summary>
    Task<IReadOnlyList<RuleVersionDto>> GetRuleVersionsAsync(
        Guid ruleId,
        CancellationToken ct = default);

    /// <summary>Returns all version snapshots for a rule pack, newest first.</summary>
    Task<IReadOnlyList<RulePackVersionDto>> GetRulePackVersionsAsync(
        Guid rulePackId,
        CancellationToken ct = default);

    /// <summary>
    /// Restore a rule to the state captured in the given version snapshot.
    /// Creates a new version with ChangeType=rollback. Does NOT delete history.
    /// </summary>
    Task<RollbackResult> RollbackRuleAsync(
        Guid ruleId,
        int versionNumber,
        string? requestedBy,
        string? reason,
        CancellationToken ct = default);

    /// <summary>
    /// Restore a rule pack (pack fields only) to the state captured in the given
    /// version snapshot. Rules are NOT automatically rolled back.
    /// Creates a new pack version with ChangeType=rollback.
    /// </summary>
    Task<RollbackResult> RollbackRulePackAsync(
        Guid rulePackId,
        int versionNumber,
        string? requestedBy,
        string? reason,
        CancellationToken ct = default);
}
