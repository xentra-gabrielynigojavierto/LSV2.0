using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

public interface INotificationService
{
    Task<PagedResponse<NotificationResponse>> SearchAsync(Guid tenantId, GetNotificationsQuery query, CancellationToken ct = default);
    Task<NotificationResponse> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task CreateReferralStatusChangedAsync(Guid tenantId, Guid referralId, Guid? userId, CancellationToken ct = default);
    Task CreateAppointmentScheduledAsync(Guid tenantId, Guid appointmentId, DateTime scheduledStartUtc, Guid? userId, CancellationToken ct = default);
    Task CreateAppointmentConfirmedAsync(Guid tenantId, Guid appointmentId, Guid? userId, CancellationToken ct = default);
    Task CreateAppointmentCancelledAsync(Guid tenantId, Guid appointmentId, Guid? userId, CancellationToken ct = default);
}
