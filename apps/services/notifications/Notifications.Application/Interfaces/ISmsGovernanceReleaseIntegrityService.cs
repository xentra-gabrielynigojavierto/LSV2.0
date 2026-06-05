namespace Notifications.Application.Interfaces;

// ── LS-NOTIF-SMS-021-HARDENING ─────────────────────────────────────────────────

/// <summary>
/// Result of validating release items within a package.
/// </summary>
public sealed record ReleaseValidationReport(
    bool                    IsValid,
    IReadOnlyList<string>   Issues,
    int                     ItemCount);

/// <summary>
/// Result of an audit-trail integrity check.
/// </summary>
public sealed record ReleaseIntegrityReport(
    bool                    IsValid,
    IReadOnlyList<string>   Issues,
    int                     AuditEventCount);

/// <summary>
/// Current activation lock status for a release package.
/// </summary>
public sealed record ReleaseActivationLockStatus(
    bool      IsLocked,
    Guid?     LockId,
    DateTime? AcquiredAt,
    DateTime? ExpiresAt,
    string?   LockedBy,
    bool      IsExpired);

/// <summary>
/// LS-NOTIF-SMS-021-HARDENING: Read-only service for inspecting release integrity,
/// item validation, and concurrency lock status.
///
/// All operations are non-destructive — they never modify state.
/// </summary>
public interface ISmsGovernanceReleaseIntegrityService
{
    /// <summary>
    /// Validates all release items in the package: checks max-item cap, valid entity types,
    /// valid action types, and duplicate entity+action pairs.
    /// </summary>
    Task<ReleaseValidationReport> ValidateReleaseItemsAsync(
        Guid releaseId, CancellationToken ct = default);

    /// <summary>
    /// Validates the audit trail of a release package: checks that each observed state
    /// transition has a corresponding audit event and that terminal/active state is consistent
    /// with the audit log.
    /// </summary>
    Task<ReleaseIntegrityReport> ValidateReleaseIntegrityAsync(
        Guid releaseId, CancellationToken ct = default);

    /// <summary>
    /// Returns the current activation lock status for a release package.
    /// IsExpired is true when ActivationLockExpiresAt is non-null and in the past.
    /// </summary>
    Task<ReleaseActivationLockStatus> GetActivationLockStatusAsync(
        Guid releaseId, CancellationToken ct = default);
}
