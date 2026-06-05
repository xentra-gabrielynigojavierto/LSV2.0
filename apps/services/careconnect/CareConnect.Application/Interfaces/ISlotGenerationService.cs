using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

public interface ISlotGenerationService
{
    Task<GenerateSlotsResponse> GenerateSlotsAsync(Guid tenantId, Guid providerId, Guid? userId, GenerateSlotsRequest request, CancellationToken ct = default);
}
