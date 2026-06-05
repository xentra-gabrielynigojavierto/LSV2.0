namespace CareConnect.Application.DTOs;

public class GetNotificationsQuery
{
    public string?  Status             { get; set; }
    public string?  NotificationType   { get; set; }
    public string?  RelatedEntityType  { get; set; }
    public Guid?    RelatedEntityId    { get; set; }
    public DateTime? ScheduledFrom    { get; set; }
    public DateTime? ScheduledTo      { get; set; }
    public int      Page               { get; set; } = 1;
    public int      PageSize           { get; set; } = 20;
}

public class NotificationResponse
{
    public Guid     Id                { get; init; }
    public string   NotificationType  { get; init; } = string.Empty;
    public string   RelatedEntityType { get; init; } = string.Empty;
    public Guid     RelatedEntityId   { get; init; }
    public string   RecipientType     { get; init; } = string.Empty;
    public string?  RecipientAddress  { get; init; }
    public string?  Subject           { get; init; }
    public string?  Message           { get; init; }
    public string   Status            { get; init; } = string.Empty;
    public DateTime? ScheduledForUtc { get; init; }
    public DateTime? SentAtUtc       { get; init; }
    public DateTime? FailedAtUtc     { get; init; }
    public string?  FailureReason    { get; init; }
    public DateTime CreatedAtUtc     { get; init; }
}
