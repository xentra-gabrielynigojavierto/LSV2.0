using CareConnect.Application.Repositories;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Infrastructure.Repositories;

public class AppointmentStatusHistoryRepository : IAppointmentStatusHistoryRepository
{
    private readonly CareConnectDbContext _db;

    public AppointmentStatusHistoryRepository(CareConnectDbContext db)
    {
        _db = db;
    }

    public async Task<List<AppointmentStatusHistory>> GetByAppointmentIdAsync(
        Guid tenantId,
        Guid appointmentId,
        CancellationToken ct = default)
    {
        return await _db.AppointmentStatusHistories
            .Where(h => h.TenantId == tenantId && h.AppointmentId == appointmentId)
            .OrderByDescending(h => h.ChangedAtUtc)
            .ToListAsync(ct);
    }
}
