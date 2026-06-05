using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

public interface IFacilityService
{
    Task<List<FacilityResponse>> GetAllAsync(Guid tenantId, CancellationToken ct = default);
    Task<FacilityResponse> CreateAsync(Guid tenantId, Guid? userId, CreateFacilityRequest request, CancellationToken ct = default);
    Task<FacilityResponse> UpdateAsync(Guid tenantId, Guid id, Guid? userId, UpdateFacilityRequest request, CancellationToken ct = default);
}
