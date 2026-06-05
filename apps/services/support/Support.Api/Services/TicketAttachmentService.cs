using Microsoft.Extensions.Options;
using Support.Api.Audit;
using Support.Api.Auth;
using Support.Api.Configuration;
using Support.Api.Data;
using Support.Api.Domain;
using Support.Api.Dtos;
using Support.Api.Files;
using Support.Api.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Support.Api.Services;

public class DuplicateAttachmentException : Exception { }

/// <summary>
/// Validation failure raised by the upload pipeline (file required, too big,
/// disallowed content type, etc.). The endpoint maps this to a 400 with the
/// supplied <see cref="Errors"/> dictionary so it shows up as a validation
/// problem on the client.
/// </summary>
public class AttachmentUploadValidationException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }
    public AttachmentUploadValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("Attachment upload validation failed.")
    {
        Errors = errors;
    }
}

public interface ITicketAttachmentService
{
    Task<TicketAttachmentResponse> AddAsync(Guid ticketId, CreateTicketAttachmentRequest req, CancellationToken ct = default);

    /// <summary>
    /// Validates the file, hands it to the configured
    /// <see cref="ISupportFileStorageProvider"/>, then persists an attachment
    /// row referencing the returned document id. Throws on validation, tenant
    /// mismatch, ticket-not-found, duplicate, or storage failures (the
    /// endpoint translates each into the appropriate HTTP status).
    /// </summary>
    Task<TicketAttachmentResponse> UploadAndAttachAsync(
        Guid ticketId,
        IFormFile file,
        string? displayName,
        CancellationToken ct = default);

    Task<List<TicketAttachmentResponse>> ListAsync(Guid ticketId, CancellationToken ct = default);

    /// <summary>Returns a single attachment record, or null if not found / not owned by the current tenant.</summary>
    Task<SupportTicketAttachment?> GetByIdAsync(Guid ticketId, Guid attachmentId, CancellationToken ct = default);

    /// <summary>
    /// Emits a compliance-grade audit event recording that <paramref name="attachment"/>
    /// was downloaded by the current actor. Fire-and-observe: never throws.
    /// </summary>
    Task AuditDownloadAsync(SupportTicketAttachment attachment, CancellationToken ct = default);
}

public class TicketAttachmentService : ITicketAttachmentService
{
    private readonly SupportDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IEventLogger _events;
    private readonly ILogger<TicketAttachmentService> _log;
    private readonly IAuditPublisher _audit;
    private readonly IActorAccessor _actor;
    private readonly ISupportFileStorageProvider _storage;
    private readonly IOptionsMonitor<FileStorageOptions> _storageOptions;

    public TicketAttachmentService(SupportDbContext db, ITenantContext tenant, IEventLogger events,
        ILogger<TicketAttachmentService> log, IAuditPublisher audit, IActorAccessor actor,
        ISupportFileStorageProvider storage, IOptionsMonitor<FileStorageOptions> storageOptions)
    {
        _db = db;
        _tenant = tenant;
        _events = events;
        _log = log;
        _audit = audit;
        _actor = actor;
        _storage = storage;
        _storageOptions = storageOptions;
    }

    private bool IsPlatformAdmin =>
        _actor.Actor.Roles.Contains(SupportRoles.PlatformAdmin, StringComparer.OrdinalIgnoreCase);

    private string RequireTenant()
    {
        if (!_tenant.IsResolved) throw new TenantMissingException();
        return _tenant.TenantId!;
    }

    /// <summary>
    /// Resolves the real tenant ID for an attachment operation.
    ///
    /// - PlatformAdmin: looks up the ticket by ID only (cross-tenant access) and
    ///   returns the ticket's own TenantId so attachments are stored under the
    ///   correct tenant — not the admin's synthetic placeholder claim.
    /// - Tenant-scoped users: enforces ownership (ticketId + tenantId from claim).
    /// - Unauthenticated / no tenant claim: throws TenantMissingException.
    ///
    /// Throws TicketNotFoundException if the ticket does not exist or is not
    /// accessible to the caller.
    /// </summary>
    private async Task<string> ResolveTicketTenantAsync(Guid ticketId, CancellationToken ct)
    {
        if (IsPlatformAdmin)
        {
            var ticket = await _db.Tickets.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == ticketId, ct)
                ?? throw new TicketNotFoundException();
            return ticket.TenantId;
        }

        var tenantId = RequireTenant();
        var exists = await _db.Tickets.AsNoTracking()
            .AnyAsync(x => x.Id == ticketId && x.TenantId == tenantId, ct);
        if (!exists) throw new TicketNotFoundException();
        return tenantId;
    }

    public async Task<TicketAttachmentResponse> AddAsync(Guid ticketId, CreateTicketAttachmentRequest req, CancellationToken ct = default)
    {
        var tenantId = await ResolveTicketTenantAsync(ticketId, ct);

        var attachment = new SupportTicketAttachment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            TenantId = tenantId,
            DocumentId = req.DocumentId,
            FileName = req.FileName,
            ContentType = req.ContentType,
            FileSizeBytes = req.FileSizeBytes,
            UploadedByUserId = req.UploadedByUserId ?? _tenant.UserId,
            CreatedAt = DateTime.UtcNow,
        };

        return await PersistAsync(attachment, ct);
    }

    public async Task<TicketAttachmentResponse> UploadAndAttachAsync(
        Guid ticketId,
        IFormFile file,
        string? displayName,
        CancellationToken ct = default)
    {
        var tenantId = await ResolveTicketTenantAsync(ticketId, ct);

        var opts = _storageOptions.CurrentValue;
        ValidateFile(file, displayName, opts);

        var effectiveName = string.IsNullOrWhiteSpace(displayName) ? file.FileName : displayName!;
        // Uploader identity is server-derived from the JWT to prevent
        // form-field spoofing of audit / timeline attribution. Any
        // `uploaded_by_user_id` form input is intentionally ignored; the
        // existing JSON link path (`AddAsync`) keeps its prior contract.
        var actorUserId = _tenant.UserId;

        SupportFileUploadResult result;
        await using (var stream = file.OpenReadStream())
        {
            var uploadRequest = new SupportFileUploadRequest(
                TenantId: tenantId,
                TicketId: ticketId,
                FileName: effectiveName,
                ContentType: file.ContentType,
                FileSizeBytes: file.Length,
                Stream: stream,
                UploadedByUserId: actorUserId);

            // Provider exceptions (NotConfigured / Remote) bubble up to the endpoint.
            result = await _storage.UploadAsync(uploadRequest, ct);
        }

        var attachment = new SupportTicketAttachment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            TenantId = tenantId,
            DocumentId = result.DocumentId,
            FileName = result.FileName,
            ContentType = result.ContentType ?? file.ContentType,
            FileSizeBytes = result.FileSizeBytes,
            UploadedByUserId = actorUserId,
            CreatedAt = DateTime.UtcNow,
        };

        return await PersistAsync(attachment, ct);
    }

    /// <summary>
    /// Shared persistence path: enforces no-duplicate, writes the row, emits the
    /// timeline event, saves, and dispatches audit. Used by both the link flow
    /// (<see cref="AddAsync"/>) and the upload flow (<see cref="UploadAndAttachAsync"/>)
    /// so that auditing/timeline behaviour is identical regardless of how the
    /// attachment came to exist.
    /// </summary>
    private async Task<TicketAttachmentResponse> PersistAsync(
        SupportTicketAttachment attachment, CancellationToken ct)
    {
        var dup = await _db.TicketAttachments.AsNoTracking().AnyAsync(x =>
            x.TenantId == attachment.TenantId
            && x.TicketId == attachment.TicketId
            && x.DocumentId == attachment.DocumentId, ct);
        if (dup) throw new DuplicateAttachmentException();

        _db.TicketAttachments.Add(attachment);

        _events.Log(attachment.TicketId, attachment.TenantId, "attachment_added", "Attachment added",
            metadata: new
            {
                attachment_id = attachment.Id,
                document_id = attachment.DocumentId,
                file_name = attachment.FileName,
            },
            actorUserId: attachment.UploadedByUserId);

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Attachment {AttachmentId} added to ticket {TicketId} tenant={TenantId}", attachment.Id, attachment.TicketId, attachment.TenantId);

        await TryAuditAttachmentAddedAsync(attachment, ct);

        return TicketAttachmentResponse.From(attachment);
    }

    private static void ValidateFile(IFormFile? file, string? displayName, FileStorageOptions opts)
    {
        var errors = new Dictionary<string, List<string>>();
        void Add(string field, string msg)
        {
            if (!errors.TryGetValue(field, out var list))
            {
                list = new List<string>();
                errors[field] = list;
            }
            list.Add(msg);
        }

        if (file is null)
        {
            Add("file", "A file is required.");
        }
        else
        {
            if (file.Length <= 0) Add("file", "File is empty.");

            var maxBytes = (long)Math.Max(1, opts.MaxFileSizeMb) * 1024L * 1024L;
            if (file.Length > maxBytes)
            {
                Add("file", $"File exceeds the maximum allowed size of {opts.MaxFileSizeMb} MB.");
            }

            var name = string.IsNullOrWhiteSpace(displayName) ? file.FileName : displayName;
            if (string.IsNullOrWhiteSpace(name))
            {
                Add("file_name", "File name is required.");
            }
            else if (name.Length > 255)
            {
                Add("file_name", "File name must be 255 characters or fewer.");
            }
            else if (HasUnsafeCharacters(name))
            {
                Add("file_name", "File name contains invalid characters.");
            }

            if (string.IsNullOrWhiteSpace(file.ContentType))
            {
                Add("content_type", "Content type is required.");
            }
            else if (opts.AllowedContentTypes.Count > 0
                && !opts.AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            {
                Add("content_type", $"Content type '{file.ContentType}' is not allowed.");
            }
        }

        if (errors.Count > 0)
        {
            throw new AttachmentUploadValidationException(
                errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray()));
        }
    }

    private static bool HasUnsafeCharacters(string name)
    {
        if (name.Contains("..", StringComparison.Ordinal)) return true;
        var invalid = Path.GetInvalidFileNameChars();
        return name.Any(c => invalid.Contains(c) || c == '/' || c == '\\');
    }

    private async Task TryAuditAttachmentAddedAsync(SupportTicketAttachment a, CancellationToken ct)
    {
        try
        {
            // Look up the ticket number for resource enrichment (best-effort).
            var ticketNumber = await _db.Tickets.AsNoTracking()
                .Where(t => t.Id == a.TicketId && t.TenantId == a.TenantId)
                .Select(t => t.TicketNumber)
                .FirstOrDefaultAsync(ct);

            var actor = _actor.Actor;
            var req = _actor.Request;
            var evt = new SupportAuditEvent(
                EventType: SupportAuditEventTypes.TicketAttachmentAdded,
                TenantId: a.TenantId,
                ActorUserId: actor.UserId ?? a.UploadedByUserId,
                ActorEmail: actor.Email,
                ActorRoles: actor.Roles,
                ResourceType: SupportAuditResourceTypes.SupportTicket,
                ResourceId: a.TicketId.ToString(),
                ResourceNumber: ticketNumber,
                Action: SupportAuditActions.AttachmentLink,
                Outcome: SupportAuditOutcomes.Success,
                OccurredAt: DateTime.UtcNow,
                CorrelationId: req.CorrelationId,
                IpAddress: req.IpAddress,
                UserAgent: req.UserAgent,
                Metadata: new Dictionary<string, object?>
                {
                    ["attachment_id"] = a.Id,
                    ["document_id"] = a.DocumentId,
                    ["file_name"] = a.FileName,
                    ["content_type"] = a.ContentType,
                    ["file_size_bytes"] = a.FileSizeBytes,
                });
            await _audit.PublishAsync(evt, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "Audit dispatch threw event=support.ticket.attachment_added attachment={AttachmentId}",
                a.Id);
        }
    }

    public async Task<List<TicketAttachmentResponse>> ListAsync(Guid ticketId, CancellationToken ct = default)
    {
        var tenantId = await ResolveTicketTenantAsync(ticketId, ct);

        var items = await _db.TicketAttachments.AsNoTracking()
            .Where(a => a.TicketId == ticketId && a.TenantId == tenantId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);
        return items.Select(TicketAttachmentResponse.From).ToList();
    }

    public async Task<SupportTicketAttachment?> GetByIdAsync(Guid ticketId, Guid attachmentId, CancellationToken ct = default)
    {
        var tenantId = await ResolveTicketTenantAsync(ticketId, ct);
        return await _db.TicketAttachments.AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.Id == attachmentId && a.TicketId == ticketId && a.TenantId == tenantId,
                ct);
    }

    /// <summary>
    /// Emits a compliance-grade audit event for an attachment download.
    /// Fire-and-observe: exceptions are swallowed and logged at Warning level
    /// so that a transient audit failure never interrupts the file stream.
    /// </summary>
    public async Task AuditDownloadAsync(SupportTicketAttachment attachment, CancellationToken ct = default)
    {
        try
        {
            var ticketNumber = await _db.Tickets.AsNoTracking()
                .Where(t => t.Id == attachment.TicketId && t.TenantId == attachment.TenantId)
                .Select(t => t.TicketNumber)
                .FirstOrDefaultAsync(ct);

            var actor = _actor.Actor;
            var req   = _actor.Request;
            var evt = new SupportAuditEvent(
                EventType:      SupportAuditEventTypes.TicketAttachmentDownloaded,
                TenantId:       attachment.TenantId,
                ActorUserId:    actor.UserId,
                ActorEmail:     actor.Email,
                ActorRoles:     actor.Roles,
                ResourceType:   SupportAuditResourceTypes.SupportTicket,
                ResourceId:     attachment.TicketId.ToString(),
                ResourceNumber: ticketNumber,
                Action:         SupportAuditActions.AttachmentDownload,
                Outcome:        SupportAuditOutcomes.Success,
                OccurredAt:     DateTime.UtcNow,
                CorrelationId:  req.CorrelationId,
                IpAddress:      req.IpAddress,
                UserAgent:      req.UserAgent,
                Metadata: new Dictionary<string, object?>
                {
                    ["attachment_id"]    = attachment.Id,
                    ["document_id"]      = attachment.DocumentId,
                    ["file_name"]        = attachment.FileName,
                    ["content_type"]     = attachment.ContentType,
                    ["file_size_bytes"]  = attachment.FileSizeBytes,
                });
            await _audit.PublishAsync(evt, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "Audit dispatch threw for event=support.ticket.attachment_downloaded attachment={AttachmentId}",
                attachment.Id);
        }
    }
}
