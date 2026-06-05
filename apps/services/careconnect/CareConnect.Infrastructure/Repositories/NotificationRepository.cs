using CareConnect.Application.DTOs;
using CareConnect.Application.Repositories;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly CareConnectDbContext _db;

    public NotificationRepository(CareConnectDbContext db)
    {
        _db = db;
    }

    public async Task<CareConnectNotification?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => await _db.CareConnectNotifications
            .Where(n => n.TenantId == tenantId && n.Id == id)
            .FirstOrDefaultAsync(ct);

    public async Task<(List<CareConnectNotification> Items, int TotalCount)> SearchAsync(
        Guid tenantId,
        GetNotificationsQuery query,
        CancellationToken ct = default)
    {
        var q = _db.CareConnectNotifications
            .Where(n => n.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(query.Status))
            q = q.Where(n => n.Status == query.Status);

        if (!string.IsNullOrWhiteSpace(query.NotificationType))
            q = q.Where(n => n.NotificationType == query.NotificationType);

        if (!string.IsNullOrWhiteSpace(query.RelatedEntityType))
            q = q.Where(n => n.RelatedEntityType == query.RelatedEntityType);

        if (query.RelatedEntityId.HasValue)
            q = q.Where(n => n.RelatedEntityId == query.RelatedEntityId.Value);

        if (query.ScheduledFrom.HasValue)
            q = q.Where(n => n.ScheduledForUtc >= query.ScheduledFrom.Value);

        if (query.ScheduledTo.HasValue)
            q = q.Where(n => n.ScheduledForUtc <= query.ScheduledTo.Value);

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task AddAsync(CareConnectNotification notification, CancellationToken ct = default)
    {
        await _db.CareConnectNotifications.AddAsync(notification, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddRangeAsync(IEnumerable<CareConnectNotification> notifications, CancellationToken ct = default)
    {
        await _db.CareConnectNotifications.AddRangeAsync(notifications, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(CareConnectNotification notification, CancellationToken ct = default)
    {
        _db.CareConnectNotifications.Update(notification);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> ExistsByDedupeKeyAsync(string dedupeKey, CancellationToken ct = default)
        => await _db.CareConnectNotifications
            .AnyAsync(n => n.DedupeKey == dedupeKey, ct);

    public async Task<bool> TryAddWithDedupeAsync(CareConnectNotification notification, CancellationToken ct = default)
    {
        try
        {
            await _db.CareConnectNotifications.AddAsync(notification, ct);
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            when (ex.InnerException is MySqlConnector.MySqlException { Number: 1062 })
        {
            _db.Entry(notification).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
            return false;
        }
    }

    // LSCC-005-01: Referral-scoped notification queries

    /// <summary>
    /// Returns the most recently created notification for the given referral, optionally
    /// filtered to a specific notification type. Used to populate the email delivery
    /// status indicator on the referral detail view.
    /// </summary>
    public async Task<CareConnectNotification?> GetLatestByReferralAsync(
        Guid tenantId,
        Guid referralId,
        string? notificationType = null,
        CancellationToken ct = default)
    {
        var q = _db.CareConnectNotifications
            .Where(n => n.TenantId == tenantId
                     && n.RelatedEntityId == referralId
                     && n.RelatedEntityType == NotificationRelatedEntityType.Referral);

        if (!string.IsNullOrWhiteSpace(notificationType))
            q = q.Where(n => n.NotificationType == notificationType);

        return await q
            .OrderByDescending(n => n.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Returns all notifications for the given referral, ordered newest-first.
    /// Used to populate the notification history panel on the referral detail view.
    /// </summary>
    public async Task<List<CareConnectNotification>> GetAllByReferralAsync(
        Guid tenantId,
        Guid referralId,
        CancellationToken ct = default)
        => await _db.CareConnectNotifications
            .Where(n => n.TenantId == tenantId
                     && n.RelatedEntityId == referralId
                     && n.RelatedEntityType == NotificationRelatedEntityType.Referral)
            .OrderByDescending(n => n.CreatedAtUtc)
            .ToListAsync(ct);

    // LSCC-005-02: retry worker query

    /// <summary>
    /// Returns failed notifications that are past their scheduled retry time and have not
    /// yet exhausted the maximum attempt count. Used exclusively by <c>ReferralEmailRetryWorker</c>.
    /// The index on (Status, NextRetryAfterUtc) makes this efficient.
    /// </summary>
    public async Task<List<CareConnectNotification>> GetRetryEligibleAsync(
        DateTime utcNow,
        int maxAttempts,
        int batchSize,
        CancellationToken ct = default)
        => await _db.CareConnectNotifications
            .Where(n => n.Status           == NotificationStatus.Failed
                     && n.NextRetryAfterUtc != null
                     && n.NextRetryAfterUtc <= utcNow
                     && n.AttemptCount      < maxAttempts)
            .OrderBy(n => n.NextRetryAfterUtc)
            .Take(batchSize)
            .ToListAsync(ct);
}
