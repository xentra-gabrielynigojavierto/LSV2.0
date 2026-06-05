using Notifications.Application.DTOs;

namespace Notifications.Application.Interfaces;

public interface INotificationService
{
    // ── Tenant-scoped operations ──────────────────────────────────────────────

    Task<NotificationResultDto> SubmitAsync(Guid tenantId, SubmitNotificationDto request);
    Task<NotificationDto?> GetByIdAsync(Guid tenantId, Guid id);
    Task<List<NotificationDto>> ListAsync(Guid tenantId, int limit = 50, int offset = 0);

    Task<PagedNotificationsResponse> ListPagedAsync(Guid tenantId, NotificationListQuery query);
    Task<NotificationStatsDto> GetStatsAsync(Guid tenantId, NotificationStatsQuery query);
    Task<List<NotificationEventDto>> GetEventsAsync(Guid tenantId, Guid id);
    Task<List<NotificationIssueDto>> GetIssuesAsync(Guid tenantId, Guid id);

    /// <param name="actorUserId">JWT subject of the operator triggering the retry; used in audit log.</param>
    Task<RetryResultDto?> RetryAsync(Guid tenantId, Guid id, string? actorUserId = null);

    /// <param name="actorUserId">JWT subject of the operator triggering the resend; used in audit log.</param>
    Task<ResendResultDto?> ResendAsync(Guid tenantId, Guid id, string? actorUserId = null);

    // ── Platform-admin cross-tenant operations ────────────────────────────────

    /// <param name="tenantId">When null, queries across all tenants.</param>
    Task<PagedNotificationsResponse> AdminListPagedAsync(Guid? tenantId, NotificationListQuery query, string actorUserId);

    /// <summary>Returns a single notification by ID regardless of tenant (platform-admin use).</summary>
    Task<NotificationDto?> AdminGetByIdAsync(Guid notificationId, string actorUserId);

    /// <param name="tenantId">When null, aggregates across all tenants.</param>
    Task<NotificationStatsDto> AdminGetStatsAsync(Guid? tenantId, NotificationStatsQuery query, string actorUserId);

    Task<List<NotificationEventDto>> AdminGetEventsAsync(Guid notificationId, string actorUserId);
    Task<List<NotificationIssueDto>> AdminGetIssuesAsync(Guid notificationId, string actorUserId);

    Task<RetryResultDto?>  AdminRetryAsync(Guid notificationId, string actorUserId);
    Task<ResendResultDto?> AdminResendAsync(Guid notificationId, string actorUserId);

    // ── Background worker operations ─────────────────────────────────────────

    /// <summary>Called by NotificationWorker. Re-attempts a notification in 'retrying' status.</summary>
    Task ProcessAutoRetryAsync(Guid notificationId);

    /// <summary>Called by StatusSyncWorker. Reconciles stalled 'processing' notifications.</summary>
    Task ReconcileStalledAsync();
}
