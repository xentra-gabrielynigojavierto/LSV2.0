using CareConnect.Application.Repositories;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Infrastructure.Repositories;

public class AppointmentNoteRepository : IAppointmentNoteRepository
{
    private readonly CareConnectDbContext _db;

    public AppointmentNoteRepository(CareConnectDbContext db)
    {
        _db = db;
    }

    public async Task<AppointmentNote?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => await _db.AppointmentNotes
            .Where(n => n.TenantId == tenantId && n.Id == id)
            .FirstOrDefaultAsync(ct);

    public async Task<List<AppointmentNote>> GetByAppointmentAsync(Guid tenantId, Guid appointmentId, CancellationToken ct = default)
        => await _db.AppointmentNotes
            .Where(n => n.TenantId == tenantId && n.AppointmentId == appointmentId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task AddAsync(AppointmentNote note, CancellationToken ct = default)
    {
        await _db.AppointmentNotes.AddAsync(note, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(AppointmentNote note, CancellationToken ct = default)
    {
        _db.AppointmentNotes.Update(note);
        await _db.SaveChangesAsync(ct);
    }
}
