using Tenant.Domain;

namespace Tenant.Application.Interfaces;

public interface IDomainRepository
{
    Task<TenantDomain?>      GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<TenantDomain>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantDomain?>      GetActiveByHostAsync(string normalizedHost, CancellationToken ct = default);
    Task<TenantDomain?>      GetActivePrimarySubdomainByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<bool>               ActiveHostExistsAsync(string normalizedHost, Guid? excludeId, CancellationToken ct = default);

    /// <summary>
    /// Returns all active Subdomain-type records for the given tenant — used for primary demotion.
    /// </summary>
    Task<List<TenantDomain>> GetActiveSubdomainsForTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Resolves an active Subdomain-type record by subdomain label.
    /// Matches records where:
    ///   Host == label (bare subdomain stored), OR
    ///   Host starts with "{label}." (leftmost label of dotted host).
    /// Prefers IsPrimary = true when multiple matches exist.
    /// </summary>
    Task<TenantDomain?> GetActiveSubdomainByLabelAsync(string label, CancellationToken ct = default);

    Task AddAsync(TenantDomain domain, CancellationToken ct = default);
    Task UpdateAsync(TenantDomain domain, CancellationToken ct = default);
    Task UpdateRangeAsync(IEnumerable<TenantDomain> domains, CancellationToken ct = default);
}
