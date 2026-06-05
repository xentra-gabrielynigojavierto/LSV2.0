using Microsoft.EntityFrameworkCore;
using PlatformAuditEventService.Data;
using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.Models;

namespace PlatformAuditEventService.Repositories;

/// <summary>
/// EF Core / MySQL-backed audit event repository.
/// Append-only: no UPDATE or DELETE operations are exposed.
/// Used when Database:Provider = "MySQL".
/// </summary>
public sealed class EfAuditEventRepository : IAuditEventRepository
{
    private readonly IDbContextFactory<AuditEventDbContext> _contextFactory;
    private readonly ILogger<EfAuditEventRepository> _logger;

    public EfAuditEventRepository(
        IDbContextFactory<AuditEventDbContext> contextFactory,
        ILogger<EfAuditEventRepository> logger)
    {
        _contextFactory = contextFactory;
        _logger         = logger;
    }

    public async Task<AuditEvent> AppendAsync(AuditEvent auditEvent, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        db.AuditEvents.Add(auditEvent);
        await db.SaveChangesAsync(ct);

        _logger.LogDebug("AuditEvent persisted to MySQL: Id={Id}", auditEvent.Id);
        return auditEvent;
    }

    public async Task<AuditEvent?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.AuditEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task<PagedResult<AuditEvent>> QueryAsync(AuditEventQueryRequest q, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var query = db.AuditEvents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.Source))
            query = query.Where(e => e.Source == q.Source);

        if (!string.IsNullOrWhiteSpace(q.EventType))
            query = query.Where(e => e.EventType == q.EventType);

        if (!string.IsNullOrWhiteSpace(q.Category))
            query = query.Where(e => e.Category == q.Category);

        if (!string.IsNullOrWhiteSpace(q.Severity))
            query = query.Where(e => e.Severity == q.Severity);

        if (!string.IsNullOrWhiteSpace(q.TenantId))
            query = query.Where(e => e.TenantId == q.TenantId);

        if (!string.IsNullOrWhiteSpace(q.ActorId))
            query = query.Where(e => e.ActorId == q.ActorId);

        if (!string.IsNullOrWhiteSpace(q.TargetType))
            query = query.Where(e => e.TargetType == q.TargetType);

        if (!string.IsNullOrWhiteSpace(q.TargetId))
            query = query.Where(e => e.TargetId == q.TargetId);

        if (!string.IsNullOrWhiteSpace(q.Outcome))
            query = query.Where(e => e.Outcome == q.Outcome);

        if (q.From.HasValue)
            query = query.Where(e => e.OccurredAtUtc >= q.From.Value);

        if (q.To.HasValue)
            query = query.Where(e => e.OccurredAtUtc <= q.To.Value);

        var total    = await query.CountAsync(ct);
        var pageSize = Math.Max(1, Math.Min(q.PageSize, 500));
        var page     = Math.Max(1, q.Page);

        var items = await query
            .OrderByDescending(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<AuditEvent>
        {
            Items      = items,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize,
        };
    }

    public async Task<long> CountAsync(CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.AuditEvents.LongCountAsync(ct);
    }
}
