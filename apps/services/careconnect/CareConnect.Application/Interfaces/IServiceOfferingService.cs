using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

public interface IServiceOfferingService
{
    Task<List<ServiceOfferingResponse>> GetAllAsync(Guid tenantId, CancellationToken ct = default);
    Task<ServiceOfferingResponse> CreateAsync(Guid tenantId, Guid? userId, CreateServiceOfferingRequest request, CancellationToken ct = default);
    Task<ServiceOfferingResponse> UpdateAsync(Guid tenantId, Guid id, Guid? userId, UpdateServiceOfferingRequest request, CancellationToken ct = default);
}
