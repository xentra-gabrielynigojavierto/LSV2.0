// LSCC-009: Admin activation service interface.
using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

public interface IActivationRequestService
{
    /// <summary>
    /// Idempotent upsert: creates a new ActivationRequest for the given referral/provider pair
    /// or updates the existing one's requester details if it already exists.
    /// Called from TrackFunnelEventAsync when eventType == "ActivationStarted".
    /// </summary>
    Task UpsertAsync(
        Guid    referralId,
        Guid    providerId,
        Guid    tenantId,
        string  providerName,
        string  providerEmail,
        string? requesterName,
        string? requesterEmail,
        string? clientName,
        string? referringFirmName,
        string? requestedService,
        CancellationToken ct = default);

    /// <summary>Returns all pending activation requests (admin queue list).</summary>
    Task<List<ActivationRequestSummary>> GetPendingAsync(CancellationToken ct = default);

    /// <summary>Returns full detail for one activation request.</summary>
    Task<ActivationRequestDetail?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Approves an activation request:
    ///   1. Validates request exists and is Pending
    ///   2. Validates provider exists
    ///   3. Links provider to organizationId (idempotent if already linked)
    ///   4. Marks request Approved
    ///   5. Emits audit event
    /// Returns a summary of what was done. Safe to call twice (idempotent).
    /// </summary>
    Task<ApproveActivationResponse> ApproveAsync(
        Guid  activationRequestId,
        Guid  organizationId,
        Guid? approvedByUserId,
        CancellationToken ct = default);
}
