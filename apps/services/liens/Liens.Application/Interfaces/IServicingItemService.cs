using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface IServicingItemService
{
    Task<PaginatedResult<ServicingItemResponse>> SearchAsync(
        Guid tenantId, string? search, string? status, string? priority, string? assignedTo,
        Guid? caseId, Guid? lienId, int page, int pageSize,
        CancellationToken ct = default);

    Task<ServicingItemResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<ServicingItemResponse> CreateAsync(
        Guid tenantId, Guid orgId, Guid actingUserId,
        CreateServicingItemRequest request, CancellationToken ct = default);

    Task<ServicingItemResponse> UpdateAsync(
        Guid tenantId, Guid id, Guid actingUserId,
        UpdateServicingItemRequest request, CancellationToken ct = default);

    Task<ServicingItemResponse> UpdateStatusAsync(
        Guid tenantId, Guid id, Guid actingUserId,
        string status, string? resolution = null, CancellationToken ct = default);
}
