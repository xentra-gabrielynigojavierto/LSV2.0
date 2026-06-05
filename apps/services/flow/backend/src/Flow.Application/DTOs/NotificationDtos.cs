namespace Flow.Application.DTOs;

public record NotificationResponse
{
    public Guid Id { get; init; }
    public Guid? TaskId { get; init; }
    public Guid? WorkflowDefinitionId { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? TargetUserId { get; init; }
    public string? TargetRoleKey { get; init; }
    public string? TargetOrgId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string SourceType { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? ReadAt { get; init; }
}

public record NotificationListQuery
{
    public string? Status { get; init; }
    public string? TargetUserId { get; init; }
    public string? TargetRoleKey { get; init; }
    public string? TargetOrgId { get; init; }
    public Guid? TaskId { get; init; }
    public string? Type { get; init; }
    public string? SourceType { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}

public record NotificationSummaryResponse
{
    public int UnreadCount { get; init; }
}
