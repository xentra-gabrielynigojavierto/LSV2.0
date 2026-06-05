using Microsoft.EntityFrameworkCore;
using Comms.Application.Repositories;
using Comms.Domain.Constants;
using Comms.Domain.Entities;
using Comms.Infrastructure.Persistence;

namespace Comms.Infrastructure.Repositories;

public class ConversationTimelineRepository : IConversationTimelineRepository
{
    private readonly CommsDbContext _db;

    public ConversationTimelineRepository(CommsDbContext db) => _db = db;

    public async Task AddAsync(ConversationTimelineEntry entry, CancellationToken ct = default)
    {
        _db.ConversationTimelineEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<ConversationTimelineEntry>> QueryAsync(
        Guid tenantId, Guid conversationId,
        DateTime? fromDate, DateTime? toDate,
        List<string>? eventTypes, bool includeInternal,
        int skip, int take,
        CancellationToken ct = default)
    {
        var query = BuildQuery(tenantId, conversationId, fromDate, toDate, eventTypes, includeInternal);

        return await query
            .OrderByDescending(e => e.OccurredAtUtc)
            .ThenByDescending(e => e.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<int> CountAsync(
        Guid tenantId, Guid conversationId,
        DateTime? fromDate, DateTime? toDate,
        List<string>? eventTypes, bool includeInternal,
        CancellationToken ct = default)
    {
        var query = BuildQuery(tenantId, conversationId, fromDate, toDate, eventTypes, includeInternal);
        return await query.CountAsync(ct);
    }

    private IQueryable<ConversationTimelineEntry> BuildQuery(
        Guid tenantId, Guid conversationId,
        DateTime? fromDate, DateTime? toDate,
        List<string>? eventTypes, bool includeInternal)
    {
        var query = _db.ConversationTimelineEntries
            .Where(e => e.TenantId == tenantId && e.ConversationId == conversationId);

        if (!includeInternal)
            query = query.Where(e => e.Visibility == TimelineVisibility.SharedExternalSafe);

        if (fromDate.HasValue)
            query = query.Where(e => e.OccurredAtUtc >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(e => e.OccurredAtUtc <= toDate.Value);

        if (eventTypes is { Count: > 0 })
            query = query.Where(e => eventTypes.Contains(e.EventType));

        return query;
    }
}
