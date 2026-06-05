using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

public interface IAppointmentAttachmentRepository
{
    Task<List<AppointmentAttachment>> GetByAppointmentAsync(Guid tenantId, Guid appointmentId, CancellationToken ct = default);
    Task AddAsync(AppointmentAttachment attachment, CancellationToken ct = default);
}
