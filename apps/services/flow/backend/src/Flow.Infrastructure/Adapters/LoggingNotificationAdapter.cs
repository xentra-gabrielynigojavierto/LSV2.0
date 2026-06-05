using Flow.Application.Adapters.NotificationAdapter;
using Microsoft.Extensions.Logging;

namespace Flow.Infrastructure.Adapters;

/// <summary>
/// Safe-baseline notification adapter. Logs notifications instead of
/// dispatching them so Flow can run without a live Notifications service.
/// </summary>
public sealed class LoggingNotificationAdapter : INotificationAdapter
{
    private readonly ILogger<LoggingNotificationAdapter> _log;

    public LoggingNotificationAdapter(ILogger<LoggingNotificationAdapter> log) => _log = log;

    public Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        _log.LogInformation(
            "[notify] event={EventKey} channel={Channel} tenant={TenantId} to-user={RecipientUserId} to-role={RecipientRoleKey} subject={Subject}",
            message.EventKey, message.Channel, message.TenantId,
            message.RecipientUserId, message.RecipientRoleKey, message.Subject);
        return Task.CompletedTask;
    }
}
