using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

public interface IAvailabilityTemplateService
{
    Task<List<AvailabilityTemplateResponse>> GetByProviderAsync(Guid tenantId, Guid providerId, CancellationToken ct = default);
    Task<AvailabilityTemplateResponse> CreateAsync(Guid tenantId, Guid providerId, Guid? userId, CreateAvailabilityTemplateRequest request, CancellationToken ct = default);
    Task<AvailabilityTemplateResponse> UpdateAsync(Guid tenantId, Guid id, Guid? userId, UpdateAvailabilityTemplateRequest request, CancellationToken ct = default);
}
