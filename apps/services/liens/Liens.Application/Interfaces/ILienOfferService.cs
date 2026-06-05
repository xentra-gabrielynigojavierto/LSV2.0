using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface ILienOfferService
{
    Task<PaginatedResult<LienOfferResponse>> SearchAsync(
        Guid tenantId, Guid? lienId, string? status,
        Guid? buyerOrgId, Guid? sellerOrgId,
        int page, int pageSize,
        CancellationToken ct = default);

    Task<LienOfferResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<List<LienOfferResponse>> GetByLienIdAsync(Guid tenantId, Guid lienId, CancellationToken ct = default);

    Task<LienOfferResponse> CreateAsync(
        Guid tenantId, Guid buyerOrgId, Guid actingUserId,
        CreateLienOfferRequest request, CancellationToken ct = default);
}
