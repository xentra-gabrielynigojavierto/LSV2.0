using Comms.Application.DTOs;

namespace Comms.Application.Interfaces;

public interface IEmailTemplateService
{
    Task<EmailTemplateConfigResponse> CreateAsync(
        CreateEmailTemplateConfigRequest request, Guid tenantId, Guid userId,
        CancellationToken ct = default);

    Task<EmailTemplateConfigResponse> UpdateAsync(
        Guid id, UpdateEmailTemplateConfigRequest request, Guid tenantId, Guid userId,
        CancellationToken ct = default);

    Task<EmailTemplateConfigResponse?> GetByIdAsync(
        Guid id, Guid tenantId, CancellationToken ct = default);

    Task<List<EmailTemplateConfigResponse>> ListAsync(
        Guid tenantId, CancellationToken ct = default);
}
