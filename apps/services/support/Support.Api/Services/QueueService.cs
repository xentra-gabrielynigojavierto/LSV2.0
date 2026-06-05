using Support.Api.Audit;
using Support.Api.Auth;
using Support.Api.Data;
using Support.Api.Domain;
using Support.Api.Dtos;
using Support.Api.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Support.Api.Services;

public class QueueNotFoundException : Exception { }
public class QueueNameConflictException : Exception { }
public class QueueMemberConflictException : Exception { }
public class QueueMemberNotFoundException : Exception { }
public class QueueInactiveException : Exception { }

public class QueueListQuery
{
    public string? ProductCode { get; set; }
    public bool? IsActive { get; set; }
    public string? Search { get; set; }
}

public interface IQueueService
{
    Task<QueueResponse> CreateAsync(CreateQueueRequest req, CancellationToken ct = default);
    Task<List<QueueResponse>> ListAsync(QueueListQuery query, CancellationToken ct = default);
    Task<QueueResponse?> GetAsync(Guid id, CancellationToken ct = default);
    Task<QueueResponse> UpdateAsync(Guid id, UpdateQueueRequest req, CancellationToken ct = default);
    Task<QueueMemberResponse> AddMemberAsync(Guid queueId, AddQueueMemberRequest req, CancellationToken ct = default);
    Task<List<QueueMemberResponse>> ListMembersAsync(Guid queueId, CancellationToken ct = default);
    Task RemoveMemberAsync(Guid queueId, Guid memberId, CancellationToken ct = default);
}

public class QueueService : IQueueService
{
    private readonly SupportDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IEventLogger _events;
    private readonly ILogger<QueueService> _log;
    private readonly IAuditPublisher _audit;
    private readonly IActorAccessor _actor;

    public QueueService(SupportDbContext db, ITenantContext tenant, IEventLogger events,
        ILogger<QueueService> log, IAuditPublisher audit, IActorAccessor actor)
    {
        _db = db;
        _tenant = tenant;
        _events = events;
        _log = log;
        _audit = audit;
        _actor = actor;
    }

    private async Task TryAuditQueueAsync(string eventType, string action, SupportQueue q, CancellationToken ct)
    {
        try
        {
            var actor = _actor.Actor;
            var req = _actor.Request;
            var evt = new SupportAuditEvent(
                EventType: eventType,
                TenantId: q.TenantId,
                ActorUserId: actor.UserId ?? _tenant.UserId,
                ActorEmail: actor.Email,
                ActorRoles: actor.Roles,
                ResourceType: SupportAuditResourceTypes.SupportQueue,
                ResourceId: q.Id.ToString(),
                ResourceNumber: null,
                Action: action,
                Outcome: SupportAuditOutcomes.Success,
                OccurredAt: DateTime.UtcNow,
                CorrelationId: req.CorrelationId,
                IpAddress: req.IpAddress,
                UserAgent: req.UserAgent,
                Metadata: new Dictionary<string, object?>
                {
                    ["queue_id"] = q.Id,
                    ["name"] = q.Name,
                    ["product_code"] = q.ProductCode,
                    ["is_active"] = q.IsActive,
                });
            await _audit.PublishAsync(evt, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "Audit dispatch threw event={EventType} queue={QueueId}", eventType, q.Id);
        }
    }

    private async Task TryAuditQueueMemberAsync(string eventType, string action, SupportQueueMember m, CancellationToken ct)
    {
        try
        {
            var actor = _actor.Actor;
            var req = _actor.Request;
            var evt = new SupportAuditEvent(
                EventType: eventType,
                TenantId: m.TenantId,
                ActorUserId: actor.UserId ?? _tenant.UserId,
                ActorEmail: actor.Email,
                ActorRoles: actor.Roles,
                ResourceType: SupportAuditResourceTypes.SupportQueueMember,
                ResourceId: m.Id.ToString(),
                ResourceNumber: null,
                Action: action,
                Outcome: SupportAuditOutcomes.Success,
                OccurredAt: DateTime.UtcNow,
                CorrelationId: req.CorrelationId,
                IpAddress: req.IpAddress,
                UserAgent: req.UserAgent,
                Metadata: new Dictionary<string, object?>
                {
                    ["queue_id"] = m.QueueId,
                    ["user_id"] = m.UserId,
                    ["role"] = m.Role.ToString(),
                    ["is_active"] = m.IsActive,
                });
            await _audit.PublishAsync(evt, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "Audit dispatch threw event={EventType} member={MemberId}", eventType, m.Id);
        }
    }

    private string RequireTenant()
    {
        if (!_tenant.IsResolved) throw new TenantMissingException();
        return _tenant.TenantId!;
    }

    private static string? NormalizeProductCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();

    public async Task<QueueResponse> CreateAsync(CreateQueueRequest req, CancellationToken ct = default)
    {
        var tenantId = RequireTenant();
        var name = req.Name.Trim();

        var exists = await _db.Queues.AsNoTracking()
            .AnyAsync(q => q.TenantId == tenantId && q.Name == name, ct);
        if (exists) throw new QueueNameConflictException();

        var now = DateTime.UtcNow;
        var queue = new SupportQueue
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Description = req.Description,
            ProductCode = NormalizeProductCode(req.ProductCode),
            IsActive = req.IsActive ?? true,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedByUserId = _tenant.UserId,
            UpdatedByUserId = _tenant.UserId,
        };
        _db.Queues.Add(queue);
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Queue {QueueId} created tenant={TenantId} name={Name}", queue.Id, tenantId, queue.Name);
        await TryAuditQueueAsync(SupportAuditEventTypes.QueueCreated, SupportAuditActions.Create, queue, ct);
        return QueueResponse.From(queue);
    }

    public async Task<List<QueueResponse>> ListAsync(QueueListQuery query, CancellationToken ct = default)
    {
        var tenantId = RequireTenant();
        var q = _db.Queues.AsNoTracking().Where(x => x.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(query.ProductCode))
        {
            var pc = query.ProductCode.Trim().ToUpperInvariant();
            q = q.Where(x => x.ProductCode == pc);
        }
        if (query.IsActive.HasValue) q = q.Where(x => x.IsActive == query.IsActive.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(x => x.Name.Contains(s) || (x.Description != null && x.Description.Contains(s)));
        }
        var items = await q.OrderBy(x => x.Name).ToListAsync(ct);
        return items.Select(QueueResponse.From).ToList();
    }

    public async Task<QueueResponse?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = RequireTenant();
        var q = await _db.Queues.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        return q is null ? null : QueueResponse.From(q);
    }

    public async Task<QueueResponse> UpdateAsync(Guid id, UpdateQueueRequest req, CancellationToken ct = default)
    {
        var tenantId = RequireTenant();
        var q = await _db.Queues.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct)
            ?? throw new QueueNotFoundException();

        if (req.Name is not null)
        {
            var newName = req.Name.Trim();
            if (newName != q.Name)
            {
                var conflict = await _db.Queues.AsNoTracking()
                    .AnyAsync(x => x.TenantId == tenantId && x.Name == newName && x.Id != id, ct);
                if (conflict) throw new QueueNameConflictException();
                q.Name = newName;
            }
        }
        if (req.Description is not null) q.Description = req.Description;
        if (req.ProductCode is not null) q.ProductCode = NormalizeProductCode(req.ProductCode);
        if (req.IsActive.HasValue) q.IsActive = req.IsActive.Value;
        q.UpdatedAt = DateTime.UtcNow;
        q.UpdatedByUserId = _tenant.UserId;

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Queue {QueueId} updated tenant={TenantId}", q.Id, tenantId);
        await TryAuditQueueAsync(SupportAuditEventTypes.QueueUpdated, SupportAuditActions.Update, q, ct);
        return QueueResponse.From(q);
    }

    private async Task<SupportQueue> RequireOwnedQueueAsync(Guid queueId, string tenantId, CancellationToken ct)
    {
        var q = await _db.Queues.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == queueId && x.TenantId == tenantId, ct);
        return q ?? throw new QueueNotFoundException();
    }

    public async Task<QueueMemberResponse> AddMemberAsync(Guid queueId, AddQueueMemberRequest req, CancellationToken ct = default)
    {
        var tenantId = RequireTenant();
        await RequireOwnedQueueAsync(queueId, tenantId, ct);

        var userId = req.UserId.Trim();
        var existing = await _db.QueueMembers
            .FirstOrDefaultAsync(m => m.QueueId == queueId && m.UserId == userId, ct);
        if (existing is not null)
        {
            if (existing.IsActive) throw new QueueMemberConflictException();
            // reactivate
            existing.IsActive = true;
            existing.Role = req.Role;
            existing.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await TryAuditQueueMemberAsync(SupportAuditEventTypes.QueueMemberAdded, SupportAuditActions.MemberAdd, existing, ct);
            return QueueMemberResponse.From(existing);
        }

        var now = DateTime.UtcNow;
        var member = new SupportQueueMember
        {
            Id = Guid.NewGuid(),
            QueueId = queueId,
            TenantId = tenantId,
            UserId = userId,
            Role = req.Role,
            IsActive = req.IsActive ?? true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.QueueMembers.Add(member);
        await _db.SaveChangesAsync(ct);
        await TryAuditQueueMemberAsync(SupportAuditEventTypes.QueueMemberAdded, SupportAuditActions.MemberAdd, member, ct);
        return QueueMemberResponse.From(member);
    }

    public async Task<List<QueueMemberResponse>> ListMembersAsync(Guid queueId, CancellationToken ct = default)
    {
        var tenantId = RequireTenant();
        await RequireOwnedQueueAsync(queueId, tenantId, ct);

        var items = await _db.QueueMembers.AsNoTracking()
            .Where(m => m.QueueId == queueId && m.TenantId == tenantId)
            .OrderBy(m => m.Role)
            .ThenBy(m => m.UserId)
            .ToListAsync(ct);
        return items.Select(QueueMemberResponse.From).ToList();
    }

    public async Task RemoveMemberAsync(Guid queueId, Guid memberId, CancellationToken ct = default)
    {
        var tenantId = RequireTenant();
        await RequireOwnedQueueAsync(queueId, tenantId, ct);

        var member = await _db.QueueMembers
            .FirstOrDefaultAsync(m => m.Id == memberId && m.QueueId == queueId && m.TenantId == tenantId, ct)
            ?? throw new QueueMemberNotFoundException();

        member.IsActive = false;
        member.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await TryAuditQueueMemberAsync(SupportAuditEventTypes.QueueMemberRemoved, SupportAuditActions.MemberRemove, member, ct);
    }
}
