using CareConnect.Application.DTOs;
using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

public interface IReferralRepository
{
    Task<(List<Referral> Items, int TotalCount)> SearchAsync(Guid tenantId, GetReferralsQuery query, CancellationToken ct = default);
    Task<Referral?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    /// <summary>
    /// LSCC-005: Loads a referral by ID without tenant scoping.
    /// Used only by public token-based endpoints where no tenant context is available.
    /// </summary>
    Task<Referral?> GetByIdGlobalAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Referral referral, CancellationToken ct = default);
    Task UpdateAsync(Referral referral, ReferralStatusHistory? history = null, ReferralProviderReassignment? providerReassignment = null, CancellationToken ct = default);
    Task<List<ReferralStatusHistory>> GetHistoryByReferralAsync(Guid tenantId, Guid referralId, CancellationToken ct = default);
    Task AddProviderReassignmentAsync(ReferralProviderReassignment reassignment, CancellationToken ct = default);
    Task<List<ReferralProviderReassignment>> GetProviderReassignmentsByReferralAsync(Guid tenantId, Guid referralId, CancellationToken ct = default);
    /// <summary>Returns a map of ProviderId → first network name for the given provider IDs.</summary>
    Task<Dictionary<Guid, string>> GetProviderNetworkNamesAsync(IEnumerable<Guid> providerIds, CancellationToken ct = default);
}
