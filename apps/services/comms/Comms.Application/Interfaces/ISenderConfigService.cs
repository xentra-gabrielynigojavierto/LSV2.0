using Comms.Application.DTOs;

namespace Comms.Application.Interfaces;

public interface ISenderConfigService
{
    Task<TenantEmailSenderConfigResponse> CreateAsync(
        CreateTenantEmailSenderConfigRequest request, Guid tenantId, Guid userId,
        CancellationToken ct = default);

    Task<TenantEmailSenderConfigResponse> UpdateAsync(
        Guid id, UpdateTenantEmailSenderConfigRequest request, Guid tenantId, Guid userId,
        CancellationToken ct = default);

    Task<TenantEmailSenderConfigResponse?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default);

    Task<List<TenantEmailSenderConfigResponse>> ListAsync(
        Guid tenantId, CancellationToken ct = default);
}
