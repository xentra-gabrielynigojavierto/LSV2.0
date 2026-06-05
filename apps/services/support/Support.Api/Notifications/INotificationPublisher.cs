namespace Support.Api.Notifications;

/// <summary>
/// Abstraction over outbound notification dispatch. Implementations MUST NOT
/// throw on transport failures — Support Service write paths rely on
/// notification dispatch being best-effort.
/// </summary>
public interface INotificationPublisher
{
    Task PublishAsync(SupportNotification notification, CancellationToken ct = default);
}
