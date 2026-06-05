using PlatformAuditEventService.DTOs.LegalHold;

namespace PlatformAuditEventService.Services;

/// <summary>
/// Domain service for placing and releasing legal holds on audit event records.
///
/// Legal holds prevent the retention pipeline from archiving or deleting held records,
/// regardless of their age or configured retention policy.
///
/// All operations log an audit trail via structured logging for compliance traceability.
/// </summary>
public interface ILegalHoldService
{
    /// <summary>
    /// Place a legal hold on the specified audit event record.
    ///
    /// Throws <see cref="InvalidOperationException"/> if the record does not exist.
    /// Returns the created hold.
    /// </summary>
    Task<LegalHoldResponse> CreateHoldAsync(
        Guid                    auditId,
        string                  requestedByUserId,
        CreateLegalHoldRequest  request,
        CancellationToken       ct = default);

    /// <summary>
    /// Release an active legal hold.
    ///
    /// Throws <see cref="InvalidOperationException"/> if the hold is not found or is already released.
    /// Returns the updated hold.
    /// </summary>
    Task<LegalHoldResponse> ReleaseHoldAsync(
        Guid                    holdId,
        string                  releasedByUserId,
        ReleaseLegalHoldRequest request,
        CancellationToken       ct = default);

    /// <summary>
    /// List all holds (active and released) for a given audit record.
    /// </summary>
    Task<IReadOnlyList<LegalHoldResponse>> ListByAuditIdAsync(
        Guid              auditId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns true when the given audit record has at least one active hold.
    /// Used by the retention pipeline pre-check.
    /// </summary>
    Task<bool> HasActiveHoldAsync(Guid auditId, CancellationToken ct = default);
}
