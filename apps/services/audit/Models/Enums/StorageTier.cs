namespace PlatformAuditEventService.Enums;

/// <summary>
/// Classifies an audit record's position in the retention lifecycle.
///
/// Tiers drive storage and access decisions:
///
///   Hot       — Record is within the primary retention window. Stored in the
///               primary database with full query, export, and integrity-check access.
///               Governed by <c>Retention:HotRetentionDays</c>.
///
///   Warm      — Record has aged out of the hot window but is still within the
///               full configured retention period. Eligible for archival to a
///               secondary store (e.g. S3, Azure Blob). Still accessible for
///               compliance queries; access latency may increase after archival.
///               Window: HotRetentionDays → DefaultRetentionDays (or per-tenant/category override).
///
///   Cold      — Record has exceeded the full retention period and is eligible
///               for permanent deletion from the primary store. Deletion requires
///               an explicit compliance workflow; never happens automatically.
///               The archival pipeline should have processed this record before deletion.
///
///   Indefinite — Record has no configured retention limit (<c>RetentionDays = 0</c>)
///               and must be retained indefinitely. Never eligible for purge.
///               Equivalent to a permanent legal hold from a platform perspective.
///
///   LegalHold — Record is explicitly placed on a legal hold that overrides
///               all retention policy rules. Must not be archived or deleted
///               while the hold is active, even if it would otherwise qualify
///               as Cold. (Future: requires per-record hold tracking.)
/// </summary>
public enum StorageTier
{
    /// <summary>Within primary retention window. Full access.</summary>
    Hot = 1,

    /// <summary>Past hot window; within full retention. Eligible for archival.</summary>
    Warm = 2,

    /// <summary>Past full retention. Eligible for deletion (requires explicit workflow).</summary>
    Cold = 3,

    /// <summary>No retention limit configured (RetentionDays=0). Never purge.</summary>
    Indefinite = 4,

    /// <summary>
    /// Explicit legal hold — overrides retention policy.
    /// (Future: requires per-record hold tracking mechanism.)
    /// </summary>
    LegalHold = 5,
}
