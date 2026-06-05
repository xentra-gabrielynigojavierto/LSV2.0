namespace Flow.Application.Adapters.NotificationAdapter;

/// <summary>
/// Platform notifications seam for Flow. System notifications only
/// (task assignment, approval requests, workflow completion). Two-way
/// communications and threading are explicitly out of scope.
/// </summary>
public interface INotificationAdapter
{
    Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default);
}

public sealed record NotificationMessage(
    string Channel,
    string EventKey,
    string? TenantId,
    string? RecipientUserId,
    string? RecipientRoleKey,
    string Subject,
    string Body,
    IReadOnlyDictionary<string, string?>? Data = null);
