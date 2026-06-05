using CareConnect.Application.Repositories;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Infrastructure.Repositories;

public class ReferralNoteRepository : IReferralNoteRepository
{
    private readonly CareConnectDbContext _db;

    public ReferralNoteRepository(CareConnectDbContext db)
    {
        _db = db;
    }

    public async Task<ReferralNote?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => await _db.ReferralNotes
            .Where(n => n.TenantId == tenantId && n.Id == id)
            .FirstOrDefaultAsync(ct);

    public async Task<List<ReferralNote>> GetByReferralAsync(Guid tenantId, Guid referralId, CancellationToken ct = default)
        => await _db.ReferralNotes
            .Where(n => n.TenantId == tenantId && n.ReferralId == referralId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task AddAsync(ReferralNote note, CancellationToken ct = default)
    {
        await _db.ReferralNotes.AddAsync(note, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ReferralNote note, CancellationToken ct = default)
    {
        _db.ReferralNotes.Update(note);
        await _db.SaveChangesAsync(ct);
    }
}
