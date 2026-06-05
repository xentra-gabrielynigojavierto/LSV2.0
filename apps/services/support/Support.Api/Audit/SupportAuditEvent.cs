namespace Support.Api.Audit;

/// <summary>
/// Canonical Support audit event names. Stable wire identifiers consumed by
/// the Audit Service for compliance reporting.
/// </summary>
public static class SupportAuditEventTypes
{
    public const string TicketCreated = "support.ticket.created";
    public const string TicketUpdated = "support.ticket.updated";
    public const string TicketStatusChanged = "support.ticket.status_changed";
    public const string TicketAssignmentChanged = "support.ticket.assignment_changed";
    public const string TicketCommentAdded = "support.ticket.comment_added";
    public const string TicketAttachmentAdded = "support.ticket.attachment_added";
    public const string TicketAttachmentDownloaded = "support.ticket.attachment_downloaded";
    public const string TicketProductRefLinked = "support.ticket.product_ref_linked";
    public const string TicketProductRefRemoved = "support.ticket.product_ref_removed";
    public const string QueueCreated = "support.queue.created";
    public const string QueueUpdated = "support.queue.updated";
    public const string QueueMemberAdded = "support.queue.member_added";
    public const string QueueMemberRemoved = "support.queue.member_removed";
    public const string TenantSettingsChanged = "support.tenant_settings.changed";
}

public static class SupportAuditResourceTypes
{
    public const string SupportTicket = "support_ticket";
    public const string SupportQueue = "support_queue";
    public const string SupportQueueMember = "support_queue_member";
    public const string SupportTenantSettings = "support_tenant_settings";
}

public static class SupportAuditActions
{
    public const string Create = "create";
    public const string Update = "update";
    public const string StatusChange = "status_change";
    public const string Assign = "assign";
    public const string CommentAdd = "comment_add";
    public const string AttachmentLink = "attachment_link";
    public const string AttachmentDownload = "attachment_download";
    public const string ProductRefLink = "product_ref_link";
    public const string ProductRefRemove = "product_ref_remove";
    public const string MemberAdd = "member_add";
    public const string MemberRemove = "member_remove";
    public const string SettingsUpdate = "settings_update";
}

public static class SupportAuditOutcomes
{
    public const string Success = "success";
    public const string Failure = "failure";
}

/// <summary>
/// Compliance-grade audit record for a single Support Service action.
/// Emitted to the Audit Service via <see cref="IAuditPublisher"/>.
///
/// IMPORTANT: do not place sensitive content (full comment bodies, file
/// contents, tokens) in <see cref="Metadata"/>. Length / id summaries only.
/// </summary>
public sealed record SupportAuditEvent(
    string EventType,
    string TenantId,
    string? ActorUserId,
    string? ActorEmail,
    IReadOnlyList<string> ActorRoles,
    string ResourceType,
    string ResourceId,
    string? ResourceNumber,
    string Action,
    string Outcome,
    DateTime OccurredAt,
    string? CorrelationId,
    string? IpAddress,
    string? UserAgent,
    IReadOnlyDictionary<string, object?> Metadata);
