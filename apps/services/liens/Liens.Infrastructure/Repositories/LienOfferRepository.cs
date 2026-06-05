using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Repositories;

public class LienOfferRepository : ILienOfferRepository
{
    private readonly LiensDbContext _db;

    public LienOfferRepository(LiensDbContext db)
    {
        _db = db;
    }

    public async Task<LienOffer?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.LienOffers
            .Where(o => o.TenantId == tenantId && o.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<LienOffer>> GetByLienIdAsync(Guid tenantId, Guid lienId, CancellationToken ct = default)
    {
        return await _db.LienOffers
            .Where(o => o.TenantId == tenantId && o.LienId == lienId)
            .OrderByDescending(o => o.OfferedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<(List<LienOffer> Items, int TotalCount)> SearchAsync(
        Guid tenantId, Guid? lienId, string? status, Guid? buyerOrgId, Guid? sellerOrgId,
        int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.LienOffers.Where(o => o.TenantId == tenantId);

        if (lienId.HasValue)
            q = q.Where(o => o.LienId == lienId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(o => o.Status == status);

        if (buyerOrgId.HasValue)
            q = q.Where(o => o.BuyerOrgId == buyerOrgId.Value);

        if (sellerOrgId.HasValue)
            q = q.Where(o => o.SellerOrgId == sellerOrgId.Value);

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(o => o.OfferedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<bool> HasActiveOfferAsync(Guid tenantId, Guid lienId, Guid buyerOrgId, CancellationToken ct = default)
    {
        return await _db.LienOffers
            .AnyAsync(o => o.TenantId == tenantId
                        && o.LienId == lienId
                        && o.BuyerOrgId == buyerOrgId
                        && o.Status == Liens.Domain.Enums.OfferStatus.Pending, ct);
    }

    public async Task AddAsync(LienOffer entity, CancellationToken ct = default)
    {
        await _db.LienOffers.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(LienOffer entity, CancellationToken ct = default)
    {
        _db.LienOffers.Update(entity);
        await _db.SaveChangesAsync(ct);
    }
}
