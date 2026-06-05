using Microsoft.Extensions.Options;

namespace Support.Api.Notifications;

/// <summary>
/// No-op publisher used when notifications are disabled or unconfigured.
/// Logs the would-be dispatch as structured JSON for traceability.
/// </summary>
public sealed class NoOpNotificationPublisher : INotificationPublisher
{
    private readonly ILogger<NoOpNotificationPublisher> _log;
    private readonly IOptionsMonitor<NotificationOptions> _options;

    public NoOpNotificationPublisher(
        ILogger<NoOpNotificationPublisher> log,
        IOptionsMonitor<NotificationOptions> options)
    {
        _log = log;
        _options = options;
    }

    public Task PublishAsync(SupportNotification notification, CancellationToken ct = default)
    {
        if (!_options.CurrentValue.Enabled)
        {
            _log.LogDebug(
                "Notifications disabled; skipping dispatch event={EventType} ticket={TicketNumber}",
                notification.EventType, notification.TicketNumber);
            return Task.CompletedTask;
        }

        _log.LogInformation(
            "[NoOpPublisher] event={EventType} tenant={TenantId} ticket={TicketNumber} recipients={RecipientCount}",
            notification.EventType,
            notification.TenantId,
            notification.TicketNumber,
            notification.Recipients.Count);
        return Task.CompletedTask;
    }
}
