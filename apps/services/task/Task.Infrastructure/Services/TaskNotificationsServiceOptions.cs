namespace Task.Infrastructure.Services;

/// <summary>
/// Configuration for the external Notifications service used by the Task notification client.
/// Bind from configuration section <c>"NotificationsService"</c>.
/// </summary>
public sealed class TaskNotificationsServiceOptions
{
    public const string SectionName = "NotificationsService";

    public string? BaseUrl        { get; set; }
    public int     TimeoutSeconds { get; set; } = 30;
}
