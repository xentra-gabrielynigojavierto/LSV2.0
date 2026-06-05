using Fund.Application.DTOs;

namespace Fund.Application.Interfaces;

public interface IApplicationService
{
    Task<ApplicationResponse> CreateAsync(
        Guid tenantId,
        Guid userId,
        CreateApplicationRequest request,
        CancellationToken ct = default);

    Task<ApplicationResponse> UpdateAsync(
        Guid tenantId,
        Guid id,
        Guid updatedByUserId,
        UpdateApplicationRequest request,
        CancellationToken ct = default);

    Task<List<ApplicationResponse>> GetAllAsync(Guid tenantId, CancellationToken ct = default);
    Task<ApplicationResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<ApplicationResponse> SubmitAsync(
        Guid tenantId,
        Guid id,
        Guid userId,
        SubmitApplicationRequest request,
        CancellationToken ct = default);

    Task<ApplicationResponse> BeginReviewAsync(
        Guid tenantId,
        Guid id,
        Guid userId,
        CancellationToken ct = default);

    Task<ApplicationResponse> ApproveAsync(
        Guid tenantId,
        Guid id,
        Guid userId,
        ApproveApplicationRequest request,
        CancellationToken ct = default);

    Task<ApplicationResponse> DenyAsync(
        Guid tenantId,
        Guid id,
        Guid userId,
        DenyApplicationRequest request,
        CancellationToken ct = default);
}
