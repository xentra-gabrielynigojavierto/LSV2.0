using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

// CC2-INT-B06 / CC2-INT-B06-01
public interface INetworkRepository
{
    Task<List<ProviderNetwork>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct = default);

    // BLK-PERF-01: Single-query alternative to GetAllByTenantAsync + N×GetWithProvidersAsync.
    // Returns each network with its provider count without loading full provider entities.
    Task<List<(Guid Id, string Name, string? Description, int ProviderCount)>> GetAllWithProviderCountAsync(Guid tenantId, CancellationToken ct = default);

    Task<ProviderNetwork?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<ProviderNetwork?> GetWithProvidersAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<bool> NameExistsAsync(Guid tenantId, string name, Guid? excludeId = null, CancellationToken ct = default);
    Task AddAsync(ProviderNetwork network, CancellationToken ct = default);
    Task AddProviderAsync(NetworkProvider entry, CancellationToken ct = default);
    Task<NetworkProvider?> GetMembershipAsync(Guid networkId, Guid providerId, CancellationToken ct = default);
    Task RemoveProviderAsync(NetworkProvider entry, CancellationToken ct = default);
    Task<List<Provider>> GetNetworkProvidersAsync(Guid tenantId, Guid networkId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);

    // CC2-INT-B06-01: Shared provider registry — global (cross-tenant) lookups
    Task<List<Provider>> SearchProvidersGlobalAsync(string? name, string? phone, string? npi, string? city, int limit = 20, CancellationToken ct = default);
    Task<Provider?> GetProviderByIdGlobalAsync(Guid id, CancellationToken ct = default);
    Task<Provider?> GetProviderByNpiAsync(string npi, CancellationToken ct = default);
    Task AddProviderToRegistryAsync(Provider provider, CancellationToken ct = default);

    /// <summary>
    /// Replaces all category associations for a provider with the supplied list.
    /// Order is preserved: the first ID is treated as primary by convention.
    /// Does NOT call SaveChanges — caller is responsible.
    /// </summary>
    Task SyncProviderCategoriesAsync(Guid providerId, List<Guid> categoryIds, CancellationToken ct = default);
}
