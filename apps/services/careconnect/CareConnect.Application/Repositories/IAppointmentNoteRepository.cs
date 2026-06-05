using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

public interface IAppointmentNoteRepository
{
    Task<AppointmentNote?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<List<AppointmentNote>> GetByAppointmentAsync(Guid tenantId, Guid appointmentId, CancellationToken ct = default);
    Task AddAsync(AppointmentNote note, CancellationToken ct = default);
    Task UpdateAsync(AppointmentNote note, CancellationToken ct = default);
}
