using Notifications.Domain;

namespace Notifications.Application.Interfaces;

public interface IDeliveryIssueRepository
{
    Task<DeliveryIssue?> CreateIfNotExistsAsync(DeliveryIssue issue);
    Task<List<DeliveryIssue>> GetByTenantAsync(Guid tenantId, int limit = 50, int offset = 0);
    Task<List<DeliveryIssue>> GetByNotificationIdAsync(Guid notificationId);
}
