using PlatformAuditEventService.Entities;

namespace PlatformAuditEventService.Repositories;

/// <summary>
/// Persistence contract for legal hold records.
///
/// Legal holds are mutable (created active, released explicitly) but their creation
/// and release metadata is immutable after each state transition.
///
/// Active hold semantics: a hold is active when <c>ReleasedAtUtc</c> is null.
/// </summary>
public interface ILegalHoldRepository
{
    /// <summary>
    /// Persist a new legal hold.
    /// </summary>
    Task<LegalHold> CreateAsync(LegalHold hold, CancellationToken ct = default);

    /// <summary>
    /// Retrieve a hold by its public <see cref="LegalHold.HoldId"/>.
    /// Returns null if not found.
    /// </summary>
    Task<LegalHold?> GetByHoldIdAsync(Guid holdId, CancellationToken ct = default);

    /// <summary>
    /// List all holds (active and released) for a given <see cref="LegalHold.AuditId"/>.
    /// Newest first.
    /// </summary>
    Task<IReadOnlyList<LegalHold>> ListByAuditIdAsync(Guid auditId, CancellationToken ct = default);

    /// <summary>
    /// Returns true when the given audit record has at least one active legal hold
    /// (i.e. a hold with <c>ReleasedAtUtc == null</c>).
    ///
    /// Used by the retention pipeline to prevent deletion of held records.
    /// </summary>
    Task<bool> HasActiveHoldAsync(Guid auditId, CancellationToken ct = default);

    /// <summary>
    /// Persist the release of an existing hold (sets ReleasedAtUtc and ReleasedByUserId).
    /// The hold must have been retrieved first (EF change-tracking baseline).
    /// </summary>
    Task<LegalHold> UpdateAsync(LegalHold hold, CancellationToken ct = default);

    /// <summary>
    /// List all active holds for a given legal authority string.
    /// Used by compliance dashboards and bulk release workflows.
    /// </summary>
    Task<IReadOnlyList<LegalHold>> ListActiveByAuthorityAsync(
        string legalAuthority,
        CancellationToken ct = default);

    /// <summary>
    /// From the provided set of audit record identifiers, return the subset that has
    /// at least one active legal hold. Returned as a HashSet for O(1) membership lookup.
    ///
    /// Used by <see cref="Services.RetentionService"/> to batch-check holds on a sample
    /// of records without issuing one query per record.
    ///
    /// Returns an empty set when no records in the input have active holds, or when the
    /// input is empty.
    /// </summary>
    Task<HashSet<Guid>> GetActiveHoldAuditIdsAsync(
        IReadOnlyList<Guid> auditIds,
        CancellationToken   ct = default);
}
