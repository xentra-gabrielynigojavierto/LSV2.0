using Comms.Domain.Entities;

namespace Comms.Application.Repositories;

public interface IEmailTemplateConfigRepository
{
    Task<EmailTemplateConfig?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<EmailTemplateConfig>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<EmailTemplateConfig?> GetByKeyAsync(Guid tenantId, string templateKey, CancellationToken ct = default);
    Task<EmailTemplateConfig?> GetGlobalByKeyAsync(string templateKey, CancellationToken ct = default);
    Task<EmailTemplateConfig?> GetDefaultAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(EmailTemplateConfig config, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
