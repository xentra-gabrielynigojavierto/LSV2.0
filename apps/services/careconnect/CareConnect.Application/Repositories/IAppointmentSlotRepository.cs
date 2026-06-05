using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

public interface IAppointmentSlotRepository
{
    Task<HashSet<DateTime>> GetExistingStartTimesAsync(Guid tenantId, Guid providerId, Guid templateId, DateTime from, DateTime to, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<AppointmentSlot> slots, CancellationToken ct = default);
    Task<AppointmentSlot?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<(List<AppointmentSlot> Items, int TotalCount)> SearchAsync(Guid tenantId, Guid? providerId, Guid? facilityId, Guid? serviceOfferingId, DateTime? from, DateTime? to, string? status, int page, int pageSize, CancellationToken ct = default);
    Task UpdateAsync(AppointmentSlot slot, CancellationToken ct = default);
    Task<List<AppointmentSlot>> GetOpenByProviderInRangeAsync(Guid tenantId, Guid providerId, DateTime from, DateTime to, CancellationToken ct = default);
    Task UpdateRangeAsync(IEnumerable<AppointmentSlot> slots, CancellationToken ct = default);
}
