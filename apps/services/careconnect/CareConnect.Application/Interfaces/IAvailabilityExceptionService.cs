using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

public interface IAvailabilityExceptionService
{
    Task<List<AvailabilityExceptionResponse>> GetByProviderAsync(Guid tenantId, Guid providerId, bool? isActive, CancellationToken ct = default);
    Task<AvailabilityExceptionResponse> CreateAsync(Guid tenantId, Guid providerId, Guid? userId, CreateAvailabilityExceptionRequest request, CancellationToken ct = default);
    Task<AvailabilityExceptionResponse> UpdateAsync(Guid tenantId, Guid id, Guid? userId, UpdateAvailabilityExceptionRequest request, CancellationToken ct = default);
    Task<ApplyExceptionsResponse> ApplyExceptionsToSlotsAsync(Guid tenantId, Guid providerId, Guid? userId, CancellationToken ct = default);
}
