using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Repositories;

public sealed class LienTaskNoteRepository : ILienTaskNoteRepository
{
    private readonly LiensDbContext _db;

    public LienTaskNoteRepository(LiensDbContext db)
    {
        _db = db;
    }

    public async Task<List<LienTaskNote>> GetByTaskIdAsync(Guid tenantId, Guid taskId, CancellationToken ct = default)
    {
        return await _db.LienTaskNotes
            .Where(n => n.TenantId == tenantId && n.TaskId == taskId && !n.IsDeleted)
            .OrderBy(n => n.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<LienTaskNote?> GetByIdAsync(Guid tenantId, Guid noteId, CancellationToken ct = default)
    {
        return await _db.LienTaskNotes
            .FirstOrDefaultAsync(n => n.TenantId == tenantId && n.Id == noteId, ct);
    }

    public async Task AddAsync(LienTaskNote note, CancellationToken ct = default)
    {
        await _db.LienTaskNotes.AddAsync(note, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(LienTaskNote note, CancellationToken ct = default)
    {
        _db.LienTaskNotes.Update(note);
        await _db.SaveChangesAsync(ct);
    }
}
