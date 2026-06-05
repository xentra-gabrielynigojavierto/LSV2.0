using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Repositories;

public sealed class LienCaseNoteRepository : ILienCaseNoteRepository
{
    private readonly LiensDbContext _db;

    public LienCaseNoteRepository(LiensDbContext db)
    {
        _db = db;
    }

    public async Task<List<LienCaseNote>> GetByCaseIdAsync(Guid tenantId, Guid caseId, CancellationToken ct = default)
    {
        return await _db.LienCaseNotes
            .Where(n => n.TenantId == tenantId && n.CaseId == caseId && !n.IsDeleted)
            .OrderBy(n => n.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<LienCaseNote?> GetByIdAsync(Guid tenantId, Guid noteId, CancellationToken ct = default)
    {
        return await _db.LienCaseNotes
            .FirstOrDefaultAsync(n => n.TenantId == tenantId && n.Id == noteId, ct);
    }

    public async Task AddAsync(LienCaseNote note, CancellationToken ct = default)
    {
        await _db.LienCaseNotes.AddAsync(note, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(LienCaseNote note, CancellationToken ct = default)
    {
        _db.LienCaseNotes.Update(note);
        await _db.SaveChangesAsync(ct);
    }
}
