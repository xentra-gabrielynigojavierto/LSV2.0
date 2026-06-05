using Comms.Domain.Entities;

namespace Comms.Application.Repositories;

public interface ITenantEmailSenderConfigRepository
{
    Task<TenantEmailSenderConfig?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<List<TenantEmailSenderConfig>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantEmailSenderConfig?> GetDefaultAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantEmailSenderConfig?> GetByFromEmailAsync(Guid tenantId, string fromEmail, CancellationToken ct = default);
    Task<List<TenantEmailSenderConfig>> GetDefaultsAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(TenantEmailSenderConfig config, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
