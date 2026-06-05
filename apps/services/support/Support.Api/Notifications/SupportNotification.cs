namespace Support.Api.Notifications;

/// <summary>
/// Canonical Support notification event names. Stable wire identifiers
/// consumed by Notifications Service for template/provider routing.
/// </summary>
public static class SupportNotificationEventTypes
{
    public const string TicketCreated = "support.ticket.created";
    public const string TicketAssigned = "support.ticket.assigned";
    public const string TicketUpdated = "support.ticket.updated";
    public const string TicketStatusChanged = "support.ticket.status_changed";
    public const string TicketCommentAdded = "support.ticket.comment_added";
}

public enum NotificationRecipientKind
{
    User,
    Email,
    QueueMember,
}

public sealed record NotificationRecipient(
    NotificationRecipientKind Kind,
    string? UserId,
    string? Email,
    Guid? QueueId = null,
    bool IsAdminRecipient = false);

/// <summary>
/// A neutral, transport-agnostic notification request emitted by Support
/// Service. The Notifications Service is responsible for templating,
/// provider routing, and delivery.
/// </summary>
public sealed record SupportNotification(
    string EventType,
    string TenantId,
    Guid TicketId,
    string TicketNumber,
    IReadOnlyList<NotificationRecipient> Recipients,
    IReadOnlyDictionary<string, object?> Payload,
    DateTime OccurredAt);
