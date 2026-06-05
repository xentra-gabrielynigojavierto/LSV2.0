// BLK-PERF-01: Read-only queries use AsNoTracking() to avoid EF Core change-tracking overhead.
using CareConnect.Application.Repositories;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Infrastructure.Repositories;

public class AppointmentRepository : IAppointmentRepository
{
    private readonly CareConnectDbContext _db;

    public AppointmentRepository(CareConnectDbContext db)
    {
        _db = db;
    }

    public async Task SaveBookingAsync(AppointmentSlot slot, Appointment appointment, CancellationToken ct = default)
    {
        _db.AppointmentSlots.Update(slot);
        await _db.Appointments.AddAsync(appointment, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveStatusUpdateAsync(Appointment appointment, AppointmentStatusHistory? history, CancellationToken ct = default)
    {
        _db.Appointments.Update(appointment);
        if (history is not null)
            await _db.AppointmentStatusHistories.AddAsync(history, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveCancellationAsync(Appointment appointment, AppointmentSlot? slot, AppointmentStatusHistory history, CancellationToken ct = default)
    {
        _db.Appointments.Update(appointment);
        if (slot is not null)
            _db.AppointmentSlots.Update(slot);
        await _db.AppointmentStatusHistories.AddAsync(history, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveRescheduleAsync(Appointment appointment, AppointmentSlot? oldSlot, AppointmentSlot newSlot, AppointmentStatusHistory? history = null, CancellationToken ct = default)
    {
        _db.Appointments.Update(appointment);
        if (oldSlot is not null)
            _db.AppointmentSlots.Update(oldSlot);
        _db.AppointmentSlots.Update(newSlot);
        if (history is not null)
            await _db.AppointmentStatusHistories.AddAsync(history, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Appointment?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        // BLK-PERF-01: AsNoTracking — read-only detail; mutations go through SaveStatusUpdateAsync etc.
        return await _db.Appointments
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.Id == id)
            .Include(a => a.Provider)
            .Include(a => a.Facility)
            .Include(a => a.ServiceOffering)
            .FirstOrDefaultAsync(ct);
    }

    // LSCC-002: referringOrgId/receivingOrgId added for org-participant scoping.
    // BLK-PERF-01: AsNoTracking applied — list is read-only; filters applied before materialization.
    public async Task<(List<Appointment> Items, int TotalCount)> SearchAsync(
        Guid tenantId,
        Guid? referralId,
        Guid? providerId,
        string? status,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        Guid? referringOrgId = null,
        Guid? receivingOrgId = null,
        CancellationToken ct = default)
    {
        var query = _db.Appointments
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .Include(a => a.Provider)
            .Include(a => a.Facility)
            .Include(a => a.ServiceOffering)
            .AsQueryable();

        if (referralId.HasValue)
            query = query.Where(a => a.ReferralId == referralId.Value);

        if (providerId.HasValue)
            query = query.Where(a => a.ProviderId == providerId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(a => a.Status == status);

        if (from.HasValue)
            query = query.Where(a => a.ScheduledStartAtUtc >= from.Value);

        if (to.HasValue)
            query = query.Where(a => a.ScheduledStartAtUtc <= to.Value);

        // LSCC-002: Org-participant filters — applied independently so admins passing
        // null for both skip the filter entirely (no narrowing without org context).
        if (referringOrgId.HasValue)
            query = query.Where(a => a.ReferringOrganizationId == referringOrgId.Value);

        if (receivingOrgId.HasValue)
            query = query.Where(a => a.ReceivingOrganizationId == receivingOrgId.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(a => a.ScheduledStartAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}
