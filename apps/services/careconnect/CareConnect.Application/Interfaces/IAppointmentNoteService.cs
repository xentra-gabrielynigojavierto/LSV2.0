using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

public interface IAppointmentNoteService
{
    Task<List<AppointmentNoteResponse>> GetByAppointmentAsync(Guid tenantId, Guid appointmentId, Guid? callerOrgId, bool isAdmin, CancellationToken ct = default);
    Task<AppointmentNoteResponse> CreateAsync(Guid tenantId, Guid appointmentId, Guid? userId, Guid? callerOrgId, CreateAppointmentNoteRequest request, CancellationToken ct = default);
    Task<AppointmentNoteResponse> UpdateAsync(Guid tenantId, Guid id, Guid? userId, UpdateAppointmentNoteRequest request, CancellationToken ct = default);
}
