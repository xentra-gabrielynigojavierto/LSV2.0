using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

public interface IAppointmentStatusHistoryRepository
{
    Task<List<AppointmentStatusHistory>> GetByAppointmentIdAsync(Guid tenantId, Guid appointmentId, CancellationToken ct = default);
}
