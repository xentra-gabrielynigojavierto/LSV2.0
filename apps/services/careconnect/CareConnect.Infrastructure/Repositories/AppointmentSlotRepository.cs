using CareConnect.Application.Repositories;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Infrastructure.Repositories;

public class AppointmentSlotRepository : IAppointmentSlotRepository
{
    private readonly CareConnectDbContext _db;

    public AppointmentSlotRepository(CareConnectDbContext db)
    {
        _db = db;
    }

    public async Task<HashSet<DateTime>> GetExistingStartTimesAsync(Guid tenantId, Guid providerId, Guid templateId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var times = await _db.AppointmentSlots
            .Where(s => s.TenantId == tenantId
                     && s.ProviderId == providerId
                     && s.ProviderAvailabilityTemplateId == templateId
                     && s.StartAtUtc >= from
                     && s.StartAtUtc < to)
            .Select(s => s.StartAtUtc)
            .ToListAsync(ct);

        return new HashSet<DateTime>(times);
    }

    public async Task AddRangeAsync(IEnumerable<AppointmentSlot> slots, CancellationToken ct = default)
    {
        await _db.AppointmentSlots.AddRangeAsync(slots, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<AppointmentSlot?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.AppointmentSlots
            .Where(s => s.TenantId == tenantId && s.Id == id)
            .Include(s => s.Provider)
            .Include(s => s.Facility)
            .Include(s => s.ServiceOffering)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(List<AppointmentSlot> Items, int TotalCount)> SearchAsync(
        Guid tenantId,
        Guid? providerId,
        Guid? facilityId,
        Guid? serviceOfferingId,
        DateTime? from,
        DateTime? to,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.AppointmentSlots
            .Where(s => s.TenantId == tenantId)
            .Include(s => s.Provider)
            .Include(s => s.Facility)
            .Include(s => s.ServiceOffering)
            .AsQueryable();

        if (providerId.HasValue)
            query = query.Where(s => s.ProviderId == providerId.Value);

        if (facilityId.HasValue)
            query = query.Where(s => s.FacilityId == facilityId.Value);

        if (serviceOfferingId.HasValue)
            query = query.Where(s => s.ServiceOfferingId == serviceOfferingId.Value);

        if (from.HasValue)
            query = query.Where(s => s.StartAtUtc >= from.Value);

        if (to.HasValue)
            query = query.Where(s => s.StartAtUtc <= to.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(s => s.Status == status);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(s => s.StartAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task UpdateAsync(AppointmentSlot slot, CancellationToken ct = default)
    {
        _db.AppointmentSlots.Update(slot);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<AppointmentSlot>> GetOpenByProviderInRangeAsync(Guid tenantId, Guid providerId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        return await _db.AppointmentSlots
            .Where(s => s.TenantId == tenantId
                     && s.ProviderId == providerId
                     && s.Status == SlotStatus.Open
                     && s.StartAtUtc < to
                     && s.EndAtUtc > from)
            .Include(s => s.Facility)
            .Include(s => s.ServiceOffering)
            .OrderBy(s => s.StartAtUtc)
            .ToListAsync(ct);
    }

    public async Task UpdateRangeAsync(IEnumerable<AppointmentSlot> slots, CancellationToken ct = default)
    {
        _db.AppointmentSlots.UpdateRange(slots);
        await _db.SaveChangesAsync(ct);
    }
}
