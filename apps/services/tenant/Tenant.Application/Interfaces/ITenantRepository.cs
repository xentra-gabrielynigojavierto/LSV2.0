using Tenant.Domain;

namespace Tenant.Application.Interfaces;

public interface ITenantRepository
{
    Task<Domain.Tenant?>                              GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Domain.Tenant?>                              GetByCodeAsync(string code, CancellationToken ct = default);
    Task<Domain.Tenant?>                              GetBySubdomainAsync(string subdomain, CancellationToken ct = default);
    Task<bool>                                        ExistsByCodeAsync(string code, CancellationToken ct = default);
    Task<bool>                                        ExistsBySubdomainAsync(string subdomain, Guid? excludeId, CancellationToken ct = default);
    Task<(List<Domain.Tenant> Items, int Total)>      ListAsync(int page, int pageSize, CancellationToken ct = default);
    Task                                              AddAsync(Domain.Tenant tenant, CancellationToken ct = default);
    Task                                              UpdateAsync(Domain.Tenant tenant, CancellationToken ct = default);
}
