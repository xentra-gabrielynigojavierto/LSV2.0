using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

public interface IAppointmentService
{
    Task<PagedResponse<SlotResponse>> SearchSlotsAsync(Guid tenantId, SlotSearchParams query, CancellationToken ct = default);
    Task<AppointmentResponse> CreateAppointmentAsync(Guid tenantId, Guid? userId, CreateAppointmentRequest request, CancellationToken ct = default, string? actorName = null);
    // LSCC-002: referringOrgId/receivingOrgId added for org-participant scoping
    Task<PagedResponse<AppointmentResponse>> SearchAppointmentsAsync(Guid tenantId, AppointmentSearchParams query, Guid? referringOrgId = null, Guid? receivingOrgId = null, CancellationToken ct = default);
    Task<AppointmentResponse> GetAppointmentByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<AppointmentResponse> UpdateAppointmentAsync(Guid tenantId, Guid id, Guid? userId, UpdateAppointmentRequest request, CancellationToken ct = default);
    Task<AppointmentResponse> ConfirmAppointmentAsync(Guid tenantId, Guid id, Guid? userId, ConfirmAppointmentRequest request, CancellationToken ct = default);
    Task<AppointmentResponse> CompleteAppointmentAsync(Guid tenantId, Guid id, Guid? userId, CompleteAppointmentRequest request, CancellationToken ct = default);
    Task<AppointmentResponse> CancelAppointmentAsync(Guid tenantId, Guid id, Guid? userId, CancelAppointmentRequest request, CancellationToken ct = default);
    Task<AppointmentResponse> RescheduleAppointmentAsync(Guid tenantId, Guid id, Guid? userId, RescheduleAppointmentRequest request, CancellationToken ct = default);
    Task<List<AppointmentStatusHistoryResponse>> GetAppointmentHistoryAsync(Guid tenantId, Guid id, CancellationToken ct = default);
}
