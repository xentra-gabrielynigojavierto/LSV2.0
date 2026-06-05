namespace Fund.Application;

public interface IApplicationRepository
{
    Task<Domain.Application?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<List<Domain.Application>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(Domain.Application application, CancellationToken ct = default);
    Task UpdateAsync(Domain.Application application, CancellationToken ct = default);
}
