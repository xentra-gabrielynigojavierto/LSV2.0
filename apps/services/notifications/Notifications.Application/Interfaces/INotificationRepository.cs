using Notifications.Application.DTOs;
using Notifications.Domain;

namespace Notifications.Application.Interfaces;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid id);
    Task<Notification?> GetByIdAndTenantAsync(Guid id, Guid tenantId);
    Task<Notification?> FindByIdempotencyKeyAsync(Guid tenantId, string idempotencyKey);
    Task<List<Notification>> GetByTenantAsync(Guid tenantId, int limit = 50, int offset = 0);
    Task<Notification> CreateAsync(Notification notification);
    Task UpdateAsync(Notification notification);
    Task UpdateStatusAsync(Guid id, string status, string? providerUsed = null, string? failureCategory = null, string? lastErrorMessage = null);

    // Tenant-scoped paged queries
    Task<(List<Notification> Items, int Total)> GetPagedAsync(Guid tenantId, NotificationListQuery query);
    Task<NotificationStatsData> GetStatsAsync(Guid tenantId, NotificationStatsQuery query);

    // Admin cross-tenant queries (tenantId == null → all tenants)
    Task<(List<Notification> Items, int Total)> GetPagedAdminAsync(Guid? tenantId, NotificationListQuery query);
    Task<NotificationStatsData> GetStatsAdminAsync(Guid? tenantId, NotificationStatsQuery query);

    // Worker queries
    Task<List<Notification>> GetEligibleForRetryAsync(int batchSize = 10);
    Task<List<Notification>> GetStalledProcessingAsync(TimeSpan threshold, int batchSize = 20);
}
