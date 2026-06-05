using System.Collections.Concurrent;
using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.Models;

namespace PlatformAuditEventService.Repositories;

/// <summary>
/// Thread-safe in-memory audit event repository.
/// For development, testing, and initial scaffolding only.
/// Replace with a durable persistence adapter (e.g. PostgreSQL + EF Core, Cosmos DB)
/// before production deployment.
/// </summary>
public sealed class InMemoryAuditEventRepository : IAuditEventRepository
{
    private readonly ConcurrentDictionary<Guid, AuditEvent> _store = new();

    public Task<AuditEvent> AppendAsync(AuditEvent auditEvent, CancellationToken ct = default)
    {
        _store[auditEvent.Id] = auditEvent;
        return Task.FromResult(auditEvent);
    }

    public Task<AuditEvent?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        _store.TryGetValue(id, out var evt);
        return Task.FromResult(evt);
    }

    public Task<PagedResult<AuditEvent>> QueryAsync(AuditEventQueryRequest q, CancellationToken ct = default)
    {
        var query = _store.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(q.Source))
            query = query.Where(e => e.Source.Equals(q.Source, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(q.EventType))
            query = query.Where(e => e.EventType.Equals(q.EventType, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(q.Category))
            query = query.Where(e => e.Category.Equals(q.Category, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(q.Severity))
            query = query.Where(e => e.Severity.Equals(q.Severity, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(q.TenantId))
            query = query.Where(e => e.TenantId == q.TenantId);

        if (!string.IsNullOrWhiteSpace(q.ActorId))
            query = query.Where(e => e.ActorId == q.ActorId);

        if (!string.IsNullOrWhiteSpace(q.TargetType))
            query = query.Where(e => string.Equals(e.TargetType, q.TargetType, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(q.TargetId))
            query = query.Where(e => e.TargetId == q.TargetId);

        if (!string.IsNullOrWhiteSpace(q.Outcome))
            query = query.Where(e => e.Outcome.Equals(q.Outcome, StringComparison.OrdinalIgnoreCase));

        if (q.From.HasValue)
            query = query.Where(e => e.OccurredAtUtc >= q.From.Value);

        if (q.To.HasValue)
            query = query.Where(e => e.OccurredAtUtc <= q.To.Value);

        var ordered = query.OrderByDescending(e => e.OccurredAtUtc).ToList();
        var total   = ordered.Count;
        var pageSize = Math.Max(1, Math.Min(q.PageSize, 500));
        var page     = Math.Max(1, q.Page);
        var items    = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Task.FromResult(new PagedResult<AuditEvent>
        {
            Items      = items,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize,
        });
    }

    public Task<long> CountAsync(CancellationToken ct = default) =>
        Task.FromResult((long)_store.Count);
}
