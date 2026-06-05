namespace Notifications.Application.DTOs;

public class NotificationListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? Status { get; set; }
    public string? Channel { get; set; }
    public string? Provider { get; set; }
    public string? Recipient { get; set; }
    public string? ProductKey { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
}

public class NotificationStatsQuery
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Channel { get; set; }
    public string? Status { get; set; }
    public string? Provider { get; set; }
    public string? ProductKey { get; set; }
}

public class PagedNotificationsResponse
{
    public List<NotificationDto> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public AppliedFiltersDto AppliedFilters { get; set; } = new();
}

public class AppliedFiltersDto
{
    public string? Status { get; set; }
    public string? Channel { get; set; }
    public string? Provider { get; set; }
    public string? Recipient { get; set; }
    public string? ProductKey { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
}

public class NotificationStatsDto
{
    public int TotalCount { get; set; }
    public int QueuedCount { get; set; }
    public int SentCount { get; set; }
    public int DeliveredCount { get; set; }
    public int FailedCount { get; set; }
    public int SuppressedCount { get; set; }
    public int PartialCount { get; set; }
    public Dictionary<string, int> ChannelBreakdown { get; set; } = new();
    public Dictionary<string, int> ProviderBreakdown { get; set; } = new();
    public Dictionary<string, int> StatusDistribution { get; set; } = new();
    public List<DailyTrendPoint> RecentTrend { get; set; } = new();
    public AppliedFiltersDto AppliedFilters { get; set; } = new();
}

public class DailyTrendPoint
{
    public string Date { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Sent { get; set; }
    public int Failed { get; set; }
    public int Blocked { get; set; }
}

public class NotificationEventDto
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Description { get; set; }
    public string? Provider { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? MetadataJson { get; set; }
}

public class NotificationIssueDto
{
    public Guid Id { get; set; }
    public string IssueType { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? RecommendedAction { get; set; }
    public string? DetailsJson { get; set; }
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RetryResultDto
{
    public Guid NotificationId { get; set; }
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string? ProviderUsed { get; set; }
    public string? FailureCategory { get; set; }
    public string? LastErrorMessage { get; set; }
    public DateTime RetriedAt { get; set; }
}

public class ResendResultDto
{
    public Guid OriginalNotificationId { get; set; }
    public Guid NewNotificationId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class NotificationStatsData
{
    public int TotalCount { get; set; }
    public Dictionary<string, int> StatusCounts { get; set; } = new();
    public Dictionary<string, int> ChannelCounts { get; set; } = new();
    public Dictionary<string, int> ProviderCounts { get; set; } = new();
    public int DeliveredCount { get; set; }
    public List<DailyTrendPoint> Trend { get; set; } = new();
}
