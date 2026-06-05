using Flow.Application.DTOs;
using Flow.Application.Exceptions;
using Flow.Application.Interfaces;
using Flow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Flow.Application.Services;

public interface INotificationService
{
    Task<PagedResponse<NotificationResponse>> ListAsync(NotificationListQuery query, CancellationToken cancellationToken = default);
    Task<NotificationResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<NotificationResponse> MarkReadAsync(Guid id, CancellationToken cancellationToken = default);
    Task<NotificationResponse> MarkUnreadAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> MarkAllReadAsync(string? targetUserId, string? targetRoleKey, string? targetOrgId, CancellationToken cancellationToken = default);
    Task<NotificationSummaryResponse> GetSummaryAsync(string? targetUserId, string? targetRoleKey, string? targetOrgId, CancellationToken cancellationToken = default);
    Task CreateNotificationAsync(
        string type,
        string sourceType,
        string title,
        string message,
        Guid? taskId = null,
        Guid? workflowDefinitionId = null,
        string? targetUserId = null,
        string? targetRoleKey = null,
        string? targetOrgId = null,
        CancellationToken cancellationToken = default);
}

public class NotificationService : INotificationService
{
    private readonly IFlowDbContext _db;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(IFlowDbContext db, ILogger<NotificationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PagedResponse<NotificationResponse>> ListAsync(NotificationListQuery query, CancellationToken cancellationToken = default)
    {
        var q = _db.Notifications.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status))
            q = q.Where(n => n.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.TargetUserId))
            q = q.Where(n => n.TargetUserId == query.TargetUserId);
        if (!string.IsNullOrWhiteSpace(query.TargetRoleKey))
            q = q.Where(n => n.TargetRoleKey == query.TargetRoleKey);
        if (!string.IsNullOrWhiteSpace(query.TargetOrgId))
            q = q.Where(n => n.TargetOrgId == query.TargetOrgId);
        if (query.TaskId.HasValue)
            q = q.Where(n => n.TaskId == query.TaskId.Value);
        if (!string.IsNullOrWhiteSpace(query.Type))
            q = q.Where(n => n.Type == query.Type);
        if (!string.IsNullOrWhiteSpace(query.SourceType))
            q = q.Where(n => n.SourceType == query.SourceType);

        var totalCount = await q.CountAsync(cancellationToken);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var items = await q
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<NotificationResponse>
        {
            Items = items.Select(MapResponse).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<NotificationResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Notifications.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
        if (entity is null)
            throw new NotFoundException("Notification", id);
        return MapResponse(entity);
    }

    public async Task<NotificationResponse> MarkReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
        if (entity is null)
            throw new NotFoundException("Notification", id);

        entity.Status = NotificationStatus.Read;
        entity.ReadAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return MapResponse(entity);
    }

    public async Task<NotificationResponse> MarkUnreadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
        if (entity is null)
            throw new NotFoundException("Notification", id);

        entity.Status = NotificationStatus.Unread;
        entity.ReadAt = null;
        await _db.SaveChangesAsync(cancellationToken);
        return MapResponse(entity);
    }

    public async Task<int> MarkAllReadAsync(string? targetUserId, string? targetRoleKey, string? targetOrgId, CancellationToken cancellationToken = default)
    {
        var q = _db.Notifications.Where(n => n.Status == NotificationStatus.Unread);

        if (!string.IsNullOrWhiteSpace(targetUserId))
            q = q.Where(n => n.TargetUserId == targetUserId);
        if (!string.IsNullOrWhiteSpace(targetRoleKey))
            q = q.Where(n => n.TargetRoleKey == targetRoleKey);
        if (!string.IsNullOrWhiteSpace(targetOrgId))
            q = q.Where(n => n.TargetOrgId == targetOrgId);

        var unread = await q.ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var n in unread)
        {
            n.Status = NotificationStatus.Read;
            n.ReadAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return unread.Count;
    }

    public async Task<NotificationSummaryResponse> GetSummaryAsync(string? targetUserId, string? targetRoleKey, string? targetOrgId, CancellationToken cancellationToken = default)
    {
        var q = _db.Notifications.AsNoTracking()
            .Where(n => n.Status == NotificationStatus.Unread);

        if (!string.IsNullOrWhiteSpace(targetUserId))
            q = q.Where(n => n.TargetUserId == targetUserId);
        if (!string.IsNullOrWhiteSpace(targetRoleKey))
            q = q.Where(n => n.TargetRoleKey == targetRoleKey);
        if (!string.IsNullOrWhiteSpace(targetOrgId))
            q = q.Where(n => n.TargetOrgId == targetOrgId);

        var count = await q.CountAsync(cancellationToken);
        return new NotificationSummaryResponse { UnreadCount = count };
    }

    public async Task CreateNotificationAsync(
        string type,
        string sourceType,
        string title,
        string message,
        Guid? taskId = null,
        Guid? workflowDefinitionId = null,
        string? targetUserId = null,
        string? targetRoleKey = null,
        string? targetOrgId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var notification = new Notification
            {
                Type = type,
                SourceType = sourceType,
                Title = title,
                Message = message,
                TaskId = taskId,
                WorkflowDefinitionId = workflowDefinitionId,
                TargetUserId = targetUserId,
                TargetRoleKey = targetRoleKey,
                TargetOrgId = targetOrgId,
                Status = NotificationStatus.Unread,
                CreatedAt = DateTime.UtcNow
            };

            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create notification: {Type} for task {TaskId}", type, taskId);
        }
    }

    private static NotificationResponse MapResponse(Notification n) => new()
    {
        Id = n.Id,
        TaskId = n.TaskId,
        WorkflowDefinitionId = n.WorkflowDefinitionId,
        Type = n.Type,
        Title = n.Title,
        Message = n.Message,
        TargetUserId = n.TargetUserId,
        TargetRoleKey = n.TargetRoleKey,
        TargetOrgId = n.TargetOrgId,
        Status = n.Status,
        SourceType = n.SourceType,
        CreatedAt = n.CreatedAt,
        ReadAt = n.ReadAt
    };
}
