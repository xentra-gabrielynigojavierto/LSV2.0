using CareConnect.Application.DTOs;
using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

public interface IProviderRepository
{
    Task<(List<Provider> Items, int TotalCount)> SearchAsync(Guid tenantId, GetProvidersQuery query, CancellationToken ct = default);
    Task<List<Provider>> GetMarkersAsync(Guid tenantId, GetProvidersQuery query, CancellationToken ct = default);

    /// <summary>Cross-tenant read — used for discovery (detail page, availability). No ownership check.</summary>
    Task<Provider?> GetByIdCrossAsync(Guid id, CancellationToken ct = default);

    /// <summary>Tenant-scoped read — used for write operations (update, link). Returns null if provider belongs to a different tenant.</summary>
    Task<Provider?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task AddAsync(Provider provider, CancellationToken ct = default);
    Task UpdateAsync(Provider provider, CancellationToken ct = default);
    Task SyncCategoriesAsync(Guid providerId, List<Guid> categoryIds, CancellationToken ct = default);

    /// <summary>
    /// LSCC-002-01: Returns all active providers in the tenant that have no OrganizationId set.
    /// Used for backfill identification and admin reporting.
    /// </summary>
    Task<List<Provider>> GetUnlinkedAsync(Guid tenantId, CancellationToken ct = default);

    Task<Provider?> GetByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// CC2-INT-B09: Looks up a provider by their Identity user ID (sub claim).
    /// Cross-tenant — used during provider self-onboarding to find the provider record
    /// for the authenticated COMMON_PORTAL user without knowing the tenantId.
    /// </summary>
    Task<Provider?> GetByIdentityUserIdAsync(Guid identityUserId, CancellationToken ct = default);
}
