using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface ICaseService
{
    Task<PaginatedResult<CaseResponse>> SearchAsync(
        Guid tenantId, string? search, string? status, int page, int pageSize,
        CancellationToken ct = default);

    Task<CaseResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<CaseResponse?> GetByCaseNumberAsync(Guid tenantId, string caseNumber, CancellationToken ct = default);

    Task<CaseResponse> CreateAsync(
        Guid tenantId, Guid orgId, Guid actingUserId,
        CreateCaseRequest request, CancellationToken ct = default);

    Task<CaseResponse> UpdateAsync(
        Guid tenantId, Guid id, Guid actingUserId,
        UpdateCaseRequest request, CancellationToken ct = default);
}
