// LSCC-009: Activation request repository interface.
using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

public interface IActivationRequestRepository
{
    /// <summary>
    /// Returns the existing pending request for the given referral+provider pair, or null.
    /// Used for upsert logic in TrackFunnelEventAsync.
    /// </summary>
    Task<ActivationRequest?> GetByReferralAndProviderAsync(
        Guid referralId,
        Guid providerId,
        CancellationToken ct = default);

    /// <summary>Returns a single activation request by its primary key.</summary>
    Task<ActivationRequest?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns all pending activation requests, newest first.
    /// Includes Provider and Referral navigation properties.
    /// </summary>
    Task<List<ActivationRequest>> GetPendingAsync(CancellationToken ct = default);

    /// <summary>Adds a new activation request.</summary>
    Task AddAsync(ActivationRequest request, CancellationToken ct = default);

    /// <summary>Persists changes to tracked entities.</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
