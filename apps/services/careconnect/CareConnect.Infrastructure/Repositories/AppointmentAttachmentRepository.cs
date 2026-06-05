using CareConnect.Application.Repositories;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Infrastructure.Repositories;

public class AppointmentAttachmentRepository : IAppointmentAttachmentRepository
{
    private readonly CareConnectDbContext _db;

    public AppointmentAttachmentRepository(CareConnectDbContext db)
    {
        _db = db;
    }

    public async Task<List<AppointmentAttachment>> GetByAppointmentAsync(Guid tenantId, Guid appointmentId, CancellationToken ct = default)
        => await _db.AppointmentAttachments
            .Where(a => a.TenantId == tenantId && a.AppointmentId == appointmentId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task AddAsync(AppointmentAttachment attachment, CancellationToken ct = default)
    {
        await _db.AppointmentAttachments.AddAsync(attachment, ct);
        await _db.SaveChangesAsync(ct);
    }
}
