using Liens.Domain.Entities;

namespace Liens.Application.Repositories;

public interface IBillOfSaleRepository
{
    Task<BillOfSale?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<BillOfSale?> GetByBillOfSaleNumberAsync(Guid tenantId, string billOfSaleNumber, CancellationToken ct = default);
    Task<BillOfSale?> GetByLienOfferIdAsync(Guid tenantId, Guid lienOfferId, CancellationToken ct = default);
    Task<List<BillOfSale>> GetByLienIdAsync(Guid tenantId, Guid lienId, CancellationToken ct = default);
    Task<(List<BillOfSale> Items, int TotalCount)> SearchAsync(
        Guid tenantId, Guid? lienId, string? status,
        Guid? buyerOrgId, Guid? sellerOrgId, string? search,
        int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(BillOfSale entity, CancellationToken ct = default);
    Task UpdateAsync(BillOfSale entity, CancellationToken ct = default);
}
