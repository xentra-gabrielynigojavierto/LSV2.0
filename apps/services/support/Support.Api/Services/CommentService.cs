using Support.Api.Audit;
using Support.Api.Auth;
using Support.Api.Data;
using Support.Api.Domain;
using Support.Api.Dtos;
using Support.Api.Notifications;
using Support.Api.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Support.Api.Services;

public class TimelineQuery
{
    public CommentVisibility? Visibility { get; set; }
    public CommentType? CommentType { get; set; }
}

public interface ICommentService
{
    Task<CommentResponse> AddAsync(Guid ticketId, CreateCommentRequest req, CancellationToken ct = default);
    Task<List<CommentResponse>> ListAsync(Guid ticketId, CommentVisibility? visibility, CommentType? commentType, CancellationToken ct = default);
    Task<List<TimelineItem>> TimelineAsync(Guid ticketId, CancellationToken ct = default);

    /// <summary>
    /// Customer-safe comment — verifies tenantId + externalCustomerId + VisibilityScope=CustomerVisible
    /// before creating the comment. Always creates as CommentType=CustomerReply, Visibility=CustomerVisible.
    /// Throws TicketNotFoundException if any ownership constraint fails.
    /// Used exclusively by the CustomerAccess-protected endpoints.
    /// </summary>
    Task<CommentResponse> AddCustomerCommentAsync(
        string tenantId,
        Guid externalCustomerId,
        Guid ticketId,
        string body,
        string? authorEmail = null,
        string? authorName = null,
        CancellationToken ct = default);
}

public class CommentService : ICommentService
{
    private readonly SupportDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IEventLogger _events;
    private readonly ILogger<CommentService> _log;
    private readonly INotificationPublisher _notifications;
    private readonly IAuditPublisher _audit;
    private readonly IActorAccessor _actor;
    private readonly IUserEmailResolver _emailResolver;
    private readonly IPlatformSettingStore _platformSettings;

    public CommentService(SupportDbContext db, ITenantContext tenant, IEventLogger events,
        ILogger<CommentService> log, INotificationPublisher notifications,
        IAuditPublisher audit, IActorAccessor actor, IUserEmailResolver emailResolver,
        IPlatformSettingStore platformSettings)
    {
        _db = db;
        _tenant = tenant;
        _events = events;
        _log = log;
        _notifications = notifications;
        _audit = audit;
        _actor = actor;
        _emailResolver = emailResolver;
        _platformSettings = platformSettings;
    }

    private string RequireTenant()
    {
        if (!_tenant.IsResolved) throw new TenantMissingException();
        return _tenant.TenantId!;
    }

    private bool IsPlatformAdmin =>
        _actor.Actor.Roles.Contains(SupportRoles.PlatformAdmin, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves the ticket and returns its entity.
    /// - Tenant-scoped users: enforces tenant ownership (ticketId + tenantId).
    /// - PlatformAdmin: finds by ticketId alone (cross-tenant access).
    /// - Anyone else without a tenant claim: throws TenantMissingException.
    /// </summary>
    private async Task<SupportTicket> ResolveTicketAsync(Guid ticketId, CancellationToken ct)
    {
        SupportTicket? t;
        // PlatformAdmin check must come FIRST — the platform admin JWT carries a
        // synthetic tenant_id claim that makes _tenant.IsResolved=true. Without this
        // ordering, platform admins would be scoped to the system placeholder tenant.
        if (IsPlatformAdmin)
        {
            t = await _db.Tickets.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == ticketId, ct);
        }
        else if (_tenant.IsResolved)
        {
            t = await _db.Tickets.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == ticketId && x.TenantId == _tenant.TenantId, ct);
        }
        else
        {
            throw new TenantMissingException();
        }
        if (t is null) throw new TicketNotFoundException();
        return t;
    }

    private async Task<SupportTicket> RequireOwnedTicketAsync(Guid ticketId, string tenantId, CancellationToken ct)
    {
        var t = await _db.Tickets.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == ticketId && x.TenantId == tenantId, ct);
        if (t is null) throw new TicketNotFoundException();
        return t;
    }

    public async Task<CommentResponse> AddAsync(Guid ticketId, CreateCommentRequest req, CancellationToken ct = default)
    {
        var ticket = await ResolveTicketAsync(ticketId, ct);
        var tenantId = ticket.TenantId;

        var comment = new SupportTicketComment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            TenantId = tenantId,
            CommentType = req.CommentType ?? CommentType.CustomerReply,
            Visibility = req.Visibility ?? CommentVisibility.CustomerVisible,
            Body = req.Body,
            AuthorUserId = req.AuthorUserId ?? _tenant.UserId,
            AuthorName = req.AuthorName,
            AuthorEmail = req.AuthorEmail,
            CreatedAt = DateTime.UtcNow,
        };
        _db.TicketComments.Add(comment);

        _events.Log(ticketId, tenantId, "comment_added", "Comment added",
            metadata: new { comment_id = comment.Id, visibility = comment.Visibility.ToString() },
            actorUserId: comment.AuthorUserId);

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Comment {CommentId} added to ticket {TicketId} tenant={TenantId}", comment.Id, ticketId, tenantId);

        await TryPublishCommentNotificationAsync(ticket, comment, ct);
        await TryPublishCommentAuditAsync(ticket, comment, ct);

        return CommentResponse.From(comment);
    }

    private async Task TryPublishCommentAuditAsync(SupportTicket ticket, SupportTicketComment comment, CancellationToken ct)
    {
        try
        {
            var actor = _actor.Actor;
            var req = _actor.Request;
            // Body length only — never include body content in audit metadata.
            var bodyLen = comment.Body?.Length ?? 0;
            var evt = new SupportAuditEvent(
                EventType: SupportAuditEventTypes.TicketCommentAdded,
                TenantId: ticket.TenantId,
                ActorUserId: actor.UserId ?? comment.AuthorUserId,
                ActorEmail: actor.Email ?? comment.AuthorEmail,
                ActorRoles: actor.Roles,
                ResourceType: SupportAuditResourceTypes.SupportTicket,
                ResourceId: ticket.Id.ToString(),
                ResourceNumber: ticket.TicketNumber,
                Action: SupportAuditActions.CommentAdd,
                Outcome: SupportAuditOutcomes.Success,
                OccurredAt: DateTime.UtcNow,
                CorrelationId: req.CorrelationId,
                IpAddress: req.IpAddress,
                UserAgent: req.UserAgent,
                Metadata: new Dictionary<string, object?>
                {
                    ["comment_id"] = comment.Id,
                    ["comment_type"] = comment.CommentType.ToString(),
                    ["visibility"] = comment.Visibility.ToString(),
                    ["author_user_id"] = comment.AuthorUserId,
                    ["body_length"] = bodyLen,
                });
            await _audit.PublishAsync(evt, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "Audit dispatch threw event=support.ticket.comment_added ticket={TicketNumber}",
                ticket.TicketNumber);
        }
    }

    private async Task TryPublishCommentNotificationAsync(SupportTicket ticket, SupportTicketComment comment, CancellationToken ct)
    {
        try
        {
            var recipients = await ResolveCommentRecipientsAsync(ticket, comment, ct);
            await AddPlatformAdminRecipientsAsync(recipients, ct);
            recipients = DeduplicateRecipients(recipients);
            if (recipients.Count == 0)
            {
                // Spec: "If no recipient can be resolved: log and skip dispatch."
                _log.LogInformation(
                    "Notification skipped (no recipients) event=support.ticket.comment_added ticket={TicketNumber}",
                    ticket.TicketNumber);
                return;
            }

            var authorDisplay = !string.IsNullOrWhiteSpace(comment.AuthorName)
                ? comment.AuthorName
                : (!string.IsNullOrWhiteSpace(comment.AuthorEmail)
                    ? comment.AuthorEmail
                    : "Support Team");

            var commentTypeLabel = comment.CommentType switch
            {
                CommentType.InternalNote  => "Internal Note",
                CommentType.CustomerReply => "Reply",
                CommentType.SystemNote    => "System Note",
                _                         => comment.CommentType.ToString(),
            };

            var payload = new Dictionary<string, object?>
            {
                ["ticket_id"]          = ticket.Id,
                ["ticket_number"]      = ticket.TicketNumber,
                ["title"]              = ticket.Title,
                ["comment_id"]         = comment.Id,
                ["comment_type"]       = comment.CommentType.ToString(),
                ["comment_type_label"] = commentTypeLabel,
                ["visibility"]         = comment.Visibility.ToString(),
                ["author_user_id"]     = comment.AuthorUserId,
                ["author_name"]        = comment.AuthorName,
                ["author_email"]       = comment.AuthorEmail,
                ["author_display"]     = authorDisplay,
                ["comment_body"]       = TruncateForEmail(comment.Body, 1500),
                ["tenant_id"]          = ticket.TenantId,
            };
            var notification = new SupportNotification(
                SupportNotificationEventTypes.TicketCommentAdded,
                ticket.TenantId, ticket.Id, ticket.TicketNumber,
                recipients, payload, DateTime.UtcNow);
            await _notifications.PublishAsync(notification, ct);
        }
        catch (Exception ex)
        {
            // Notification dispatch must never break comment writes.
            _log.LogWarning(ex,
                "Notification dispatch threw event=support.ticket.comment_added ticket={TicketNumber}",
                ticket.TicketNumber);
        }
    }

    private static string? TruncateForEmail(string? body, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        return body.Length <= maxLen ? body : body[..maxLen] + "…";
    }

    private async Task<List<NotificationRecipient>> ResolveCommentRecipientsAsync(
        SupportTicket ticket, SupportTicketComment comment, CancellationToken ct)
    {
        var list = new List<NotificationRecipient>();
        var isInternal = comment.Visibility == CommentVisibility.Internal
                         || comment.CommentType == CommentType.InternalNote;
        var isCustomerReply = comment.CommentType == CommentType.CustomerReply;

        if (isInternal)
        {
            // Internal comments may notify internal support staff (assigned user)
            // only — never the requester/customer.
            if (!string.IsNullOrWhiteSpace(ticket.AssignedUserId))
                list.Add(await MakeAssignedUserRecipientAsync(ticket, ct));
            return list;
        }

        if (isCustomerReply)
        {
            // Customer replies notify support participants (assigned user).
            if (!string.IsNullOrWhiteSpace(ticket.AssignedUserId))
                list.Add(await MakeAssignedUserRecipientAsync(ticket, ct));
        }
        else
        {
            // Customer-visible support reply: notify the requester.
            // Prefer a stored email; fall back to identity-DB lookup when only
            // a RequesterUserId is present.
            if (!string.IsNullOrWhiteSpace(ticket.RequesterEmail))
            {
                list.Add(new NotificationRecipient(NotificationRecipientKind.Email, null, ticket.RequesterEmail));
            }
            else if (!string.IsNullOrWhiteSpace(ticket.RequesterUserId))
            {
                var email = await _emailResolver.ResolveAsync(ticket.RequesterUserId, ticket.TenantId, ct);
                list.Add(string.IsNullOrWhiteSpace(email)
                    ? new NotificationRecipient(NotificationRecipientKind.User, ticket.RequesterUserId, null)
                    : new NotificationRecipient(NotificationRecipientKind.Email, null, email));
            }
        }

        return list;
    }

    private async Task<NotificationRecipient> MakeAssignedUserRecipientAsync(SupportTicket ticket, CancellationToken ct)
    {
        var email = await _emailResolver.ResolveAsync(ticket.AssignedUserId!, ticket.TenantId, ct);
        return string.IsNullOrWhiteSpace(email)
            ? new NotificationRecipient(NotificationRecipientKind.User, ticket.AssignedUserId, null)
            : new NotificationRecipient(NotificationRecipientKind.Email, null, email);
    }

    private async Task AddPlatformAdminRecipientsAsync(List<NotificationRecipient> list, CancellationToken ct)
    {
        var existing = new HashSet<string>(
            list.Where(r => !string.IsNullOrWhiteSpace(r.Email)).Select(r => r.Email!),
            StringComparer.OrdinalIgnoreCase);

        // Source 1: configured admin notify email (Tenant DB — always available).
        var adminNotifyEmail = await _platformSettings.GetAsync("platform.adminNotifyEmail", ct);
        if (!string.IsNullOrWhiteSpace(adminNotifyEmail) && existing.Add(adminNotifyEmail))
            list.Add(new NotificationRecipient(NotificationRecipientKind.Email, null, adminNotifyEmail,
                null, IsAdminRecipient: true));

        // Source 2: PlatformInternal users from the Identity DB (bonus).
        var adminEmails = await _emailResolver.ResolvePlatformAdminEmailsAsync(ct);
        foreach (var email in adminEmails)
        {
            if (existing.Add(email))
                list.Add(new NotificationRecipient(NotificationRecipientKind.Email, null, email,
                    null, IsAdminRecipient: true));
        }
    }

    private static List<NotificationRecipient> DeduplicateRecipients(List<NotificationRecipient> list)
    {
        var seenEmails  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenUserIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result      = new List<NotificationRecipient>(list.Count);

        foreach (var r in list)
        {
            if (r.Kind == NotificationRecipientKind.Email && !string.IsNullOrWhiteSpace(r.Email))
            {
                if (seenEmails.Add(r.Email)) result.Add(r);
            }
            else if (r.Kind == NotificationRecipientKind.User && !string.IsNullOrWhiteSpace(r.UserId))
            {
                if (seenUserIds.Add(r.UserId)) result.Add(r);
            }
        }

        return result;
    }

    public async Task<List<CommentResponse>> ListAsync(Guid ticketId, CommentVisibility? visibility, CommentType? commentType, CancellationToken ct = default)
    {
        var ticket = await ResolveTicketAsync(ticketId, ct);
        var tenantId = ticket.TenantId;

        var q = _db.TicketComments.AsNoTracking()
            .Where(c => c.TicketId == ticketId && c.TenantId == tenantId);
        if (visibility.HasValue) q = q.Where(c => c.Visibility == visibility.Value);
        if (commentType.HasValue) q = q.Where(c => c.CommentType == commentType.Value);

        var items = await q.OrderBy(c => c.CreatedAt).ToListAsync(ct);
        return items.Select(CommentResponse.From).ToList();
    }

    public async Task<List<TimelineItem>> TimelineAsync(Guid ticketId, CancellationToken ct = default)
    {
        var ticket = await ResolveTicketAsync(ticketId, ct);
        var tenantId = ticket.TenantId;

        var comments = await _db.TicketComments.AsNoTracking()
            .Where(c => c.TicketId == ticketId && c.TenantId == tenantId)
            .ToListAsync(ct);
        var events = await _db.TicketEvents.AsNoTracking()
            .Where(e => e.TicketId == ticketId && e.TenantId == tenantId)
            .ToListAsync(ct);

        var items = new List<TimelineItem>(comments.Count + events.Count);
        items.AddRange(comments.Select(c => new TimelineItem
        {
            Type = "comment",
            CreatedAt = c.CreatedAt,
            Body = c.Body,
            Summary = null,
            CommentType = c.CommentType.ToString(),
            Visibility = c.Visibility.ToString(),
            ActorUserId = c.AuthorUserId,
            ActorName = c.AuthorName,
            ActorEmail = c.AuthorEmail,
        }));
        items.AddRange(events.Select(e => new TimelineItem
        {
            Type = "event",
            CreatedAt = e.CreatedAt,
            Summary = e.Summary,
            EventType = e.EventType,
            ActorUserId = e.ActorUserId,
            MetadataJson = e.MetadataJson,
        }));

        return items.OrderBy(i => i.CreatedAt).ToList();
    }

    public async Task<CommentResponse> AddCustomerCommentAsync(
        string tenantId,
        Guid externalCustomerId,
        Guid ticketId,
        string body,
        string? authorEmail = null,
        string? authorName = null,
        CancellationToken ct = default)
    {
        // Enforce: tenant + externalCustomerId + CustomerVisible
        var ticket = await _db.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId
                && x.Id == ticketId
                && x.ExternalCustomerId == externalCustomerId
                && x.VisibilityScope == TicketVisibilityScope.CustomerVisible, ct);

        if (ticket is null) throw new TicketNotFoundException();

        var comment = new SupportTicketComment
        {
            Id          = Guid.NewGuid(),
            TicketId    = ticketId,
            TenantId    = tenantId,
            CommentType = CommentType.CustomerReply,
            Visibility  = CommentVisibility.CustomerVisible,
            Body        = body,
            AuthorUserId = null,
            AuthorEmail  = authorEmail,
            AuthorName   = authorName,
            CreatedAt    = DateTime.UtcNow,
        };
        _db.TicketComments.Add(comment);

        _events.Log(ticketId, tenantId, "customer_comment_added", "Customer comment added",
            metadata: new { comment_id = comment.Id, external_customer_id = externalCustomerId },
            actorUserId: null);

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Customer comment {CommentId} added to ticket {TicketId} tenant={TenantId} externalCustomerId={CustomerId}",
            comment.Id, ticketId, tenantId, externalCustomerId);

        await TryPublishCommentNotificationAsync(ticket, comment, ct);

        try
        {
            var actor = _actor.Actor;
            var req = _actor.Request;
            var evt = new SupportAuditEvent(
                EventType: SupportAuditEventTypes.TicketCommentAdded,
                TenantId: ticket.TenantId,
                ActorUserId: actor.UserId,
                ActorEmail: actor.Email ?? authorEmail,
                ActorRoles: actor.Roles,
                ResourceType: SupportAuditResourceTypes.SupportTicket,
                ResourceId: ticket.Id.ToString(),
                ResourceNumber: ticket.TicketNumber,
                Action: SupportAuditActions.CommentAdd,
                Outcome: SupportAuditOutcomes.Success,
                OccurredAt: DateTime.UtcNow,
                CorrelationId: req.CorrelationId,
                IpAddress: req.IpAddress,
                UserAgent: req.UserAgent,
                Metadata: new Dictionary<string, object?>
                {
                    ["comment_id"]          = comment.Id,
                    ["comment_type"]        = comment.CommentType.ToString(),
                    ["visibility"]          = comment.Visibility.ToString(),
                    ["author_email"]        = authorEmail,
                    ["requester_type"]      = ticket.RequesterType.ToString(),
                    ["external_customer_id"] = externalCustomerId,
                    ["body_length"]         = body?.Length ?? 0,
                });
            await _audit.PublishAsync(evt, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "Audit dispatch threw event=support.ticket.comment_added ticket={TicketNumber}",
                ticket.TicketNumber);
        }

        return CommentResponse.From(comment);
    }
}
