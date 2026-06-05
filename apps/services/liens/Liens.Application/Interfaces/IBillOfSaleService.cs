using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface IBillOfSaleService
{
    Task<BillOfSaleResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<BillOfSaleResponse?> GetByBillOfSaleNumberAsync(Guid tenantId, string billOfSaleNumber, CancellationToken ct = default);

    Task<PaginatedResult<BillOfSaleResponse>> SearchAsync(
        Guid tenantId, Guid? lienId, string? status,
        Guid? buyerOrgId, Guid? sellerOrgId,
        string? search,
        int page, int pageSize,
        CancellationToken ct = default);

    Task<List<BillOfSaleResponse>> GetByLienIdAsync(Guid tenantId, Guid lienId, CancellationToken ct = default);

    Task<BillOfSaleResponse> SubmitForExecutionAsync(Guid tenantId, Guid id, Guid actingUserId, CancellationToken ct = default);

    Task<BillOfSaleResponse> ExecuteAsync(Guid tenantId, Guid id, Guid actingUserId, CancellationToken ct = default);

    Task<BillOfSaleResponse> CancelAsync(Guid tenantId, Guid id, Guid actingUserId, string? reason = null, CancellationToken ct = default);
}
