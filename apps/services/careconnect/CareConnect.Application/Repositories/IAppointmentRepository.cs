using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

public interface IAppointmentRepository
{
    Task SaveBookingAsync(AppointmentSlot slot, Appointment appointment, CancellationToken ct = default);
    Task SaveStatusUpdateAsync(Appointment appointment, AppointmentStatusHistory? history, CancellationToken ct = default);
    Task SaveCancellationAsync(Appointment appointment, AppointmentSlot? slot, AppointmentStatusHistory history, CancellationToken ct = default);
    Task SaveRescheduleAsync(Appointment appointment, AppointmentSlot? oldSlot, AppointmentSlot newSlot, AppointmentStatusHistory? history = null, CancellationToken ct = default);
    Task<Appointment?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    // LSCC-002: referringOrgId/receivingOrgId added for org-participant scoping
    Task<(List<Appointment> Items, int TotalCount)> SearchAsync(Guid tenantId, Guid? referralId, Guid? providerId, string? status, DateTime? from, DateTime? to, int page, int pageSize, Guid? referringOrgId = null, Guid? receivingOrgId = null, CancellationToken ct = default);
}
