using Microsoft.EntityFrameworkCore;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly NotificationsDbContext _db;
    public NotificationRepository(NotificationsDbContext db) => _db = db;

    public async Task<Notification?> GetByIdAsync(Guid id)
        => await _db.Notifications.FindAsync(id);

    public async Task<Notification?> GetByIdAndTenantAsync(Guid id, Guid tenantId)
        => await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.TenantId == tenantId);

    public async Task<Notification?> FindByIdempotencyKeyAsync(Guid tenantId, string idempotencyKey)
        => await _db.Notifications.FirstOrDefaultAsync(n => n.TenantId == tenantId && n.IdempotencyKey == idempotencyKey);

    public async Task<List<Notification>> GetByTenantAsync(Guid tenantId, int limit = 50, int offset = 0)
        => await _db.Notifications.Where(n => n.TenantId == tenantId)
            .OrderByDescending(n => n.CreatedAt).Skip(offset).Take(limit).ToListAsync();

    public async Task<Notification> CreateAsync(Notification notification)
    {
        notification.Id = notification.Id == Guid.Empty ? Guid.NewGuid() : notification.Id;
        notification.CreatedAt = DateTime.UtcNow;
        notification.UpdatedAt = DateTime.UtcNow;
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();
        return notification;
    }

    public async Task UpdateAsync(Notification notification)
    {
        notification.UpdatedAt = DateTime.UtcNow;
        _db.Notifications.Update(notification);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateStatusAsync(Guid id, string status, string? providerUsed = null, string? failureCategory = null, string? lastErrorMessage = null)
    {
        var n = await _db.Notifications.FindAsync(id);
        if (n == null) return;
        n.Status = status;
        if (providerUsed != null) n.ProviderUsed = providerUsed;
        if (failureCategory != null) n.FailureCategory = failureCategory;
        if (lastErrorMessage != null) n.LastErrorMessage = lastErrorMessage;
        n.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<(List<Notification> Items, int Total)> GetPagedAsync(Guid tenantId, NotificationListQuery query)
    {
        var q = _db.Notifications.Where(n => n.TenantId == tenantId);

        if (!string.IsNullOrEmpty(query.Status))
            q = q.Where(n => n.Status == query.Status);

        if (!string.IsNullOrEmpty(query.Channel))
            q = q.Where(n => n.Channel == query.Channel);

        if (!string.IsNullOrEmpty(query.Provider))
            q = q.Where(n => n.ProviderUsed == query.Provider);

        if (!string.IsNullOrEmpty(query.ProductKey))
            q = q.Where(n => n.Category == query.ProductKey);

        if (!string.IsNullOrEmpty(query.Recipient))
            q = q.Where(n => n.RecipientJson.Contains(query.Recipient));

        if (query.From.HasValue)
            q = q.Where(n => n.CreatedAt >= query.From.Value);

        if (query.To.HasValue)
            q = q.Where(n => n.CreatedAt <= query.To.Value);

        var sortField = query.SortBy?.ToLowerInvariant() ?? "created_at";
        var sortDesc = !string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        q = (sortField, sortDesc) switch
        {
            ("status", true)     => q.OrderByDescending(n => n.Status),
            ("status", false)    => q.OrderBy(n => n.Status),
            ("channel", true)    => q.OrderByDescending(n => n.Channel),
            ("channel", false)   => q.OrderBy(n => n.Channel),
            ("updated_at", true) => q.OrderByDescending(n => n.UpdatedAt),
            ("updated_at", false)=> q.OrderBy(n => n.UpdatedAt),
            (_, true)            => q.OrderByDescending(n => n.CreatedAt),
            (_, false)           => q.OrderBy(n => n.CreatedAt),
        };

        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var page     = Math.Max(1, query.Page);
        var offset   = (page - 1) * pageSize;

        var total = await q.CountAsync();
        var items = await q.Skip(offset).Take(pageSize).ToListAsync();

        return (items, total);
    }

    public async Task<(List<Notification> Items, int Total)> GetPagedAdminAsync(Guid? tenantId, NotificationListQuery query)
    {
        var q = tenantId.HasValue
            ? _db.Notifications.Where(n => n.TenantId == tenantId.Value)
            : _db.Notifications.AsQueryable();

        if (!string.IsNullOrEmpty(query.Status))
            q = q.Where(n => n.Status == query.Status);

        if (!string.IsNullOrEmpty(query.Channel))
            q = q.Where(n => n.Channel == query.Channel);

        if (!string.IsNullOrEmpty(query.Provider))
            q = q.Where(n => n.ProviderUsed == query.Provider);

        if (!string.IsNullOrEmpty(query.ProductKey))
            q = q.Where(n => n.Category == query.ProductKey);

        if (!string.IsNullOrEmpty(query.Recipient))
            q = q.Where(n => n.RecipientJson.Contains(query.Recipient));

        if (query.From.HasValue)
            q = q.Where(n => n.CreatedAt >= query.From.Value);

        if (query.To.HasValue)
            q = q.Where(n => n.CreatedAt <= query.To.Value);

        var sortField = query.SortBy?.ToLowerInvariant() ?? "created_at";
        var sortDesc  = !string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        q = (sortField, sortDesc) switch
        {
            ("status",     true)  => q.OrderByDescending(n => n.Status),
            ("status",     false) => q.OrderBy(n => n.Status),
            ("channel",    true)  => q.OrderByDescending(n => n.Channel),
            ("channel",    false) => q.OrderBy(n => n.Channel),
            ("updated_at", true)  => q.OrderByDescending(n => n.UpdatedAt),
            ("updated_at", false) => q.OrderBy(n => n.UpdatedAt),
            (_,            true)  => q.OrderByDescending(n => n.CreatedAt),
            (_,            false) => q.OrderBy(n => n.CreatedAt),
        };

        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var page     = Math.Max(1, query.Page);
        var offset   = (page - 1) * pageSize;

        var total = await q.CountAsync();
        var items = await q.Skip(offset).Take(pageSize).ToListAsync();

        return (items, total);
    }

    public async Task<NotificationStatsData> GetStatsAdminAsync(Guid? tenantId, NotificationStatsQuery query)
    {
        var q = tenantId.HasValue
            ? _db.Notifications.Where(n => n.TenantId == tenantId.Value)
            : _db.Notifications.AsQueryable();

        if (!string.IsNullOrEmpty(query.Channel))
            q = q.Where(n => n.Channel == query.Channel);

        if (!string.IsNullOrEmpty(query.Status))
            q = q.Where(n => n.Status == query.Status);

        if (!string.IsNullOrEmpty(query.Provider))
            q = q.Where(n => n.ProviderUsed == query.Provider);

        if (!string.IsNullOrEmpty(query.ProductKey))
            q = q.Where(n => n.Category == query.ProductKey);

        if (query.From.HasValue)
            q = q.Where(n => n.CreatedAt >= query.From.Value);

        if (query.To.HasValue)
            q = q.Where(n => n.CreatedAt <= query.To.Value);

        var items = await q
            .Select(n => new { n.TenantId, n.Status, n.Channel, n.ProviderUsed, n.BlockedByPolicy, n.CreatedAt })
            .ToListAsync();

        var statusCounts   = items.GroupBy(x => x.Status).ToDictionary(g => g.Key, g => g.Count());
        var channelCounts  = items.GroupBy(x => x.Channel).ToDictionary(g => g.Key, g => g.Count());
        var providerCounts = items.Where(x => !string.IsNullOrEmpty(x.ProviderUsed))
                                  .GroupBy(x => x.ProviderUsed!).ToDictionary(g => g.Key, g => g.Count());

        var trendFrom  = query.From ?? DateTime.UtcNow.AddDays(-7);
        var trendTo    = query.To   ?? DateTime.UtcNow;
        var trendItems = items.Where(x => x.CreatedAt >= trendFrom && x.CreatedAt <= trendTo);

        var trendByDay = trendItems
            .GroupBy(x => x.CreatedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyTrendPoint
            {
                Date    = g.Key.ToString("yyyy-MM-dd"),
                Total   = g.Count(),
                Sent    = g.Count(x => x.Status == "sent"),
                Failed  = g.Count(x => x.Status == "failed"),
                Blocked = g.Count(x => x.Status == "blocked" || x.BlockedByPolicy),
            })
            .ToList();

        var deliveredQuery = tenantId.HasValue
            ? _db.NotificationEvents.Where(e => e.TenantId == tenantId.Value && e.NormalizedEventType == "delivered")
            : _db.NotificationEvents.Where(e => e.NormalizedEventType == "delivered");

        var deliveredCount = await deliveredQuery.CountAsync();

        return new NotificationStatsData
        {
            TotalCount     = items.Count,
            StatusCounts   = statusCounts,
            ChannelCounts  = channelCounts,
            ProviderCounts = providerCounts,
            DeliveredCount = deliveredCount,
            Trend          = trendByDay,
        };
    }

    public async Task<List<Notification>> GetEligibleForRetryAsync(int batchSize = 10)
        => await _db.Notifications
            .Where(n => n.Status == "retrying" && n.NextRetryAt != null && n.NextRetryAt <= DateTime.UtcNow)
            .OrderBy(n => n.NextRetryAt)
            .Take(batchSize)
            .ToListAsync();

    public async Task<List<Notification>> GetStalledProcessingAsync(TimeSpan threshold, int batchSize = 20)
    {
        var cutoff = DateTime.UtcNow - threshold;
        return await _db.Notifications
            .Where(n => n.Status == "processing" && n.UpdatedAt < cutoff)
            .OrderBy(n => n.UpdatedAt)
            .Take(batchSize)
            .ToListAsync();
    }

    public async Task<NotificationStatsData> GetStatsAsync(Guid tenantId, NotificationStatsQuery query)
    {
        var q = _db.Notifications.Where(n => n.TenantId == tenantId);

        if (!string.IsNullOrEmpty(query.Channel))
            q = q.Where(n => n.Channel == query.Channel);

        if (!string.IsNullOrEmpty(query.Status))
            q = q.Where(n => n.Status == query.Status);

        if (!string.IsNullOrEmpty(query.Provider))
            q = q.Where(n => n.ProviderUsed == query.Provider);

        if (!string.IsNullOrEmpty(query.ProductKey))
            q = q.Where(n => n.Category == query.ProductKey);

        if (query.From.HasValue)
            q = q.Where(n => n.CreatedAt >= query.From.Value);

        if (query.To.HasValue)
            q = q.Where(n => n.CreatedAt <= query.To.Value);

        var items = await q
            .Select(n => new { n.Status, n.Channel, n.ProviderUsed, n.BlockedByPolicy, n.CreatedAt })
            .ToListAsync();

        var statusCounts = items
            .GroupBy(x => x.Status)
            .ToDictionary(g => g.Key, g => g.Count());

        var channelCounts = items
            .GroupBy(x => x.Channel)
            .ToDictionary(g => g.Key, g => g.Count());

        var providerCounts = items
            .Where(x => !string.IsNullOrEmpty(x.ProviderUsed))
            .GroupBy(x => x.ProviderUsed!)
            .ToDictionary(g => g.Key, g => g.Count());

        var trendFrom = query.From ?? DateTime.UtcNow.AddDays(-7);
        var trendTo   = query.To   ?? DateTime.UtcNow;
        var trendItems = items.Where(x => x.CreatedAt >= trendFrom && x.CreatedAt <= trendTo);

        var trendByDay = trendItems
            .GroupBy(x => x.CreatedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyTrendPoint
            {
                Date    = g.Key.ToString("yyyy-MM-dd"),
                Total   = g.Count(),
                Sent    = g.Count(x => x.Status == "sent"),
                Failed  = g.Count(x => x.Status == "failed"),
                Blocked = g.Count(x => x.Status == "blocked" || x.BlockedByPolicy),
            })
            .ToList();

        var deliveredCount = await _db.NotificationEvents
            .Where(e => e.TenantId == tenantId && e.NormalizedEventType == "delivered")
            .CountAsync();

        return new NotificationStatsData
        {
            TotalCount     = items.Count,
            StatusCounts   = statusCounts,
            ChannelCounts  = channelCounts,
            ProviderCounts = providerCounts,
            DeliveredCount = deliveredCount,
            Trend          = trendByDay,
        };
    }
}
