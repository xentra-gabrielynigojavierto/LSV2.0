using Support.Api.Audit;
using Support.Api.Auth;
using Support.Api.Data;
using Support.Api.Domain;
using Support.Api.Dtos;
using Support.Api.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Support.Api.Services;

public class DuplicateProductReferenceException : Exception { }
public class ProductReferenceNotFoundException : Exception { }

public interface ITicketProductReferenceService
{
    Task<ProductReferenceResponse> AddAsync(Guid ticketId, CreateProductReferenceRequest req, CancellationToken ct = default);
    Task<List<ProductReferenceResponse>> ListAsync(Guid ticketId, string? productCode, string? entityType, CancellationToken ct = default);
    Task DeleteAsync(Guid ticketId, Guid refId, CancellationToken ct = default);
}

public class TicketProductReferenceService : ITicketProductReferenceService
{
    private readonly SupportDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IEventLogger _events;
    private readonly ILogger<TicketProductReferenceService> _log;
    private readonly IAuditPublisher _audit;
    private readonly IActorAccessor _actor;

    public TicketProductReferenceService(SupportDbContext db, ITenantContext tenant, IEventLogger events,
        ILogger<TicketProductReferenceService> log, IAuditPublisher audit, IActorAccessor actor)
    {
        _db = db;
        _tenant = tenant;
        _events = events;
        _log = log;
        _audit = audit;
        _actor = actor;
    }

    private async Task TryAuditProductRefAsync(string eventType, string action, SupportTicketProductRef pref, CancellationToken ct)
    {
        try
        {
            var ticketNumber = await _db.Tickets.AsNoTracking()
                .Where(t => t.Id == pref.TicketId && t.TenantId == pref.TenantId)
                .Select(t => t.TicketNumber)
                .FirstOrDefaultAsync(ct);

            var actor = _actor.Actor;
            var req = _actor.Request;
            var meta = new Dictionary<string, object?>
            {
                ["product_ref_id"] = pref.Id,
                ["product_code"] = pref.ProductCode,
                ["entity_type"] = pref.EntityType,
                ["entity_id"] = pref.EntityId,
            };
            if (eventType == SupportAuditEventTypes.TicketProductRefLinked)
            {
                meta["display_label"] = pref.DisplayLabel;
            }

            var evt = new SupportAuditEvent(
                EventType: eventType,
                TenantId: pref.TenantId,
                ActorUserId: actor.UserId ?? pref.CreatedByUserId,
                ActorEmail: actor.Email,
                ActorRoles: actor.Roles,
                ResourceType: SupportAuditResourceTypes.SupportTicket,
                ResourceId: pref.TicketId.ToString(),
                ResourceNumber: ticketNumber,
                Action: action,
                Outcome: SupportAuditOutcomes.Success,
                OccurredAt: DateTime.UtcNow,
                CorrelationId: req.CorrelationId,
                IpAddress: req.IpAddress,
                UserAgent: req.UserAgent,
                Metadata: meta);
            await _audit.PublishAsync(evt, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "Audit dispatch threw event={EventType} product_ref={RefId}",
                eventType, pref.Id);
        }
    }

    private string RequireTenant()
    {
        if (!_tenant.IsResolved) throw new TenantMissingException();
        return _tenant.TenantId!;
    }

    private async Task RequireOwnedTicketAsync(Guid ticketId, string tenantId, CancellationToken ct)
    {
        var exists = await _db.Tickets.AsNoTracking()
            .AnyAsync(x => x.Id == ticketId && x.TenantId == tenantId, ct);
        if (!exists) throw new TicketNotFoundException();
    }

    public async Task<ProductReferenceResponse> AddAsync(Guid ticketId, CreateProductReferenceRequest req, CancellationToken ct = default)
    {
        var tenantId = RequireTenant();
        await RequireOwnedTicketAsync(ticketId, tenantId, ct);

        var productCode = req.ProductCode.Trim().ToUpperInvariant();
        var entityType = req.EntityType.Trim();
        var entityId = req.EntityId.Trim();

        var dup = await _db.TicketProductRefs.AsNoTracking().AnyAsync(r =>
            r.TenantId == tenantId &&
            r.TicketId == ticketId &&
            r.ProductCode == productCode &&
            r.EntityType == entityType &&
            r.EntityId == entityId, ct);
        if (dup) throw new DuplicateProductReferenceException();

        var pref = new SupportTicketProductRef
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            TenantId = tenantId,
            ProductCode = productCode,
            EntityType = entityType,
            EntityId = entityId,
            DisplayLabel = req.DisplayLabel,
            MetadataJson = req.MetadataJson,
            CreatedByUserId = req.CreatedByUserId ?? _tenant.UserId,
            CreatedAt = DateTime.UtcNow,
        };
        _db.TicketProductRefs.Add(pref);

        _events.Log(ticketId, tenantId, "product_ref_linked", "Product reference linked",
            metadata: new
            {
                product_ref_id = pref.Id,
                product_code = pref.ProductCode,
                entity_type = pref.EntityType,
                entity_id = pref.EntityId,
                display_label = pref.DisplayLabel,
            },
            actorUserId: pref.CreatedByUserId);

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Product ref {RefId} linked to ticket {TicketId} tenant={TenantId} product={Product}",
            pref.Id, ticketId, tenantId, pref.ProductCode);

        await TryAuditProductRefAsync(
            SupportAuditEventTypes.TicketProductRefLinked,
            SupportAuditActions.ProductRefLink,
            pref, ct);

        return ProductReferenceResponse.From(pref);
    }

    public async Task<List<ProductReferenceResponse>> ListAsync(Guid ticketId, string? productCode, string? entityType, CancellationToken ct = default)
    {
        var tenantId = RequireTenant();
        await RequireOwnedTicketAsync(ticketId, tenantId, ct);

        var q = _db.TicketProductRefs.AsNoTracking()
            .Where(r => r.TicketId == ticketId && r.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(productCode))
        {
            var pc = productCode.Trim().ToUpperInvariant();
            q = q.Where(r => r.ProductCode == pc);
        }
        if (!string.IsNullOrWhiteSpace(entityType))
        {
            var et = entityType.Trim();
            q = q.Where(r => r.EntityType == et);
        }
        var items = await q.OrderBy(r => r.CreatedAt).ToListAsync(ct);
        return items.Select(ProductReferenceResponse.From).ToList();
    }

    public async Task DeleteAsync(Guid ticketId, Guid refId, CancellationToken ct = default)
    {
        var tenantId = RequireTenant();
        await RequireOwnedTicketAsync(ticketId, tenantId, ct);

        var pref = await _db.TicketProductRefs.FirstOrDefaultAsync(r =>
            r.Id == refId && r.TicketId == ticketId && r.TenantId == tenantId, ct);
        if (pref is null) throw new ProductReferenceNotFoundException();

        _db.TicketProductRefs.Remove(pref);
        _events.Log(ticketId, tenantId, "product_ref_removed", "Product reference removed",
            metadata: new
            {
                product_ref_id = pref.Id,
                product_code = pref.ProductCode,
                entity_type = pref.EntityType,
                entity_id = pref.EntityId,
            },
            actorUserId: _tenant.UserId);

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Product ref {RefId} removed from ticket {TicketId} tenant={TenantId}", refId, ticketId, tenantId);

        await TryAuditProductRefAsync(
            SupportAuditEventTypes.TicketProductRefRemoved,
            SupportAuditActions.ProductRefRemove,
            pref, ct);
    }
}
