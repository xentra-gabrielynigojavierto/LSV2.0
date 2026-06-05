using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Repositories;

public class BillOfSaleRepository : IBillOfSaleRepository
{
    private readonly LiensDbContext _db;

    public BillOfSaleRepository(LiensDbContext db)
    {
        _db = db;
    }

    public async Task<BillOfSale?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.BillsOfSale
            .Where(b => b.TenantId == tenantId && b.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<BillOfSale?> GetByBillOfSaleNumberAsync(Guid tenantId, string billOfSaleNumber, CancellationToken ct = default)
    {
        return await _db.BillsOfSale
            .Where(b => b.TenantId == tenantId && b.BillOfSaleNumber == billOfSaleNumber)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<BillOfSale?> GetByLienOfferIdAsync(Guid tenantId, Guid lienOfferId, CancellationToken ct = default)
    {
        return await _db.BillsOfSale
            .Where(b => b.TenantId == tenantId && b.LienOfferId == lienOfferId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<BillOfSale>> GetByLienIdAsync(Guid tenantId, Guid lienId, CancellationToken ct = default)
    {
        return await _db.BillsOfSale
            .Where(b => b.TenantId == tenantId && b.LienId == lienId)
            .OrderByDescending(b => b.IssuedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<(List<BillOfSale> Items, int TotalCount)> SearchAsync(
        Guid tenantId, Guid? lienId, string? status,
        Guid? buyerOrgId, Guid? sellerOrgId, string? search,
        int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.BillsOfSale.Where(b => b.TenantId == tenantId);

        if (lienId.HasValue)
            q = q.Where(b => b.LienId == lienId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(b => b.Status == status);

        if (buyerOrgId.HasValue)
            q = q.Where(b => b.BuyerOrgId == buyerOrgId.Value);

        if (sellerOrgId.HasValue)
            q = q.Where(b => b.SellerOrgId == sellerOrgId.Value);

        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(b => b.BillOfSaleNumber.Contains(search));

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(b => b.IssuedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(BillOfSale entity, CancellationToken ct = default)
    {
        await _db.BillsOfSale.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(BillOfSale entity, CancellationToken ct = default)
    {
        _db.BillsOfSale.Update(entity);
        await _db.SaveChangesAsync(ct);
    }
}
