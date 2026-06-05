using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Repositories;

public class ContactRepository : IContactRepository
{
    private readonly LiensDbContext _db;

    public ContactRepository(LiensDbContext db)
    {
        _db = db;
    }

    public async Task<Contact?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.Contacts
            .Where(c => c.TenantId == tenantId && c.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(List<Contact> Items, int TotalCount)> SearchAsync(
        Guid tenantId, string? search, string? contactType, bool? isActive,
        int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.Contacts.Where(c => c.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(c =>
                c.FirstName.Contains(term) ||
                c.LastName.Contains(term) ||
                c.DisplayName.Contains(term) ||
                (c.Email != null && c.Email.Contains(term)) ||
                (c.Organization != null && c.Organization.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(contactType))
            q = q.Where(c => c.ContactType == contactType);

        if (isActive.HasValue)
            q = q.Where(c => c.IsActive == isActive.Value);

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .OrderBy(c => c.DisplayName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(Contact entity, CancellationToken ct = default)
    {
        await _db.Contacts.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Contact entity, CancellationToken ct = default)
    {
        _db.Contacts.Update(entity);
        await _db.SaveChangesAsync(ct);
    }
}
