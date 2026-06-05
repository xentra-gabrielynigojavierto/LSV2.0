namespace Notifications.Application.Interfaces;

public interface IDeliveryStatusService
{
    Task UpdateAttemptFromEventAsync(Guid attemptId, string normalizedEventType);
    Task UpdateNotificationFromEventAsync(Guid notificationId, string normalizedEventType);
}
