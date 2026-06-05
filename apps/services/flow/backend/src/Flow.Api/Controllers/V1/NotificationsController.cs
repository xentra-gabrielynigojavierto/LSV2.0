using BuildingBlocks.Authorization;
using Flow.Application.DTOs;
using Flow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flow.Api.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = Policies.AuthenticatedUser)]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] string? targetUserId,
        [FromQuery] string? targetRoleKey,
        [FromQuery] string? targetOrgId,
        [FromQuery] Guid? taskId,
        [FromQuery] string? type,
        [FromQuery] string? sourceType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var query = new NotificationListQuery
        {
            Status = status,
            TargetUserId = targetUserId,
            TargetRoleKey = targetRoleKey,
            TargetOrgId = targetOrgId,
            TaskId = taskId,
            Type = type,
            SourceType = sourceType,
            Page = page,
            PageSize = pageSize
        };
        var result = await _notificationService.ListAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _notificationService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        var result = await _notificationService.MarkReadAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/unread")]
    public async Task<IActionResult> MarkUnread(Guid id, CancellationToken cancellationToken)
    {
        var result = await _notificationService.MarkUnreadAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead(
        [FromQuery] string? targetUserId,
        [FromQuery] string? targetRoleKey,
        [FromQuery] string? targetOrgId,
        CancellationToken cancellationToken)
    {
        var count = await _notificationService.MarkAllReadAsync(targetUserId, targetRoleKey, targetOrgId, cancellationToken);
        return Ok(new { markedRead = count });
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] string? targetUserId,
        [FromQuery] string? targetRoleKey,
        [FromQuery] string? targetOrgId,
        CancellationToken cancellationToken)
    {
        var result = await _notificationService.GetSummaryAsync(targetUserId, targetRoleKey, targetOrgId, cancellationToken);
        return Ok(result);
    }
}
