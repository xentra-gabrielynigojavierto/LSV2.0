using Notifications.Domain;

namespace Notifications.Application.Interfaces;

public interface INotificationEventRepository
{
    Task<NotificationEvent?> FindByDedupKeyAsync(string dedupKey);
    Task<NotificationEvent> CreateAsync(NotificationEvent evt);
    Task<List<NotificationEvent>> GetByNotificationIdAsync(Guid notificationId, int limit = 50);
}
