using Microsoft.EntityFrameworkCore;
using Comms.Application.DTOs;
using Comms.Application.Repositories;
using Comms.Infrastructure.Persistence;

namespace Comms.Infrastructure.Repositories;

public class OperationalConversationQueryRepository : IOperationalConversationQueryRepository
{
    private readonly CommsDbContext _db;

    public OperationalConversationQueryRepository(CommsDbContext db)
    {
        _db = db;
    }

    public async Task<(List<ConversationOperationalListItemResponse> Items, int TotalCount)> QueryAsync(
        Guid tenantId,
        OperationalQueryRequest request,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var query = from c in _db.Conversations.AsNoTracking()
                    where c.TenantId == tenantId
                    join a in _db.ConversationAssignments.AsNoTracking()
                        on new { c.TenantId, ConversationId = c.Id }
                        equals new { a.TenantId, a.ConversationId }
                        into assignments
                    from a in assignments.DefaultIfEmpty()
                    join s in _db.ConversationSlaStates.AsNoTracking()
                        on new { c.TenantId, ConversationId = c.Id }
                        equals new { s.TenantId, s.ConversationId }
                        into slaStates
                    from s in slaStates.DefaultIfEmpty()
                    join q in _db.ConversationQueues.AsNoTracking()
                        on new { TenantId = a != null ? a.TenantId : Guid.Empty, QueueId = a != null ? a.QueueId ?? Guid.Empty : Guid.Empty }
                        equals new { q.TenantId, QueueId = q.Id }
                        into queues
                    from q in queues.DefaultIfEmpty()
                    join rs in _db.ConversationReadStates.AsNoTracking()
                        on new { c.TenantId, ConversationId = c.Id, UserId = currentUserId }
                        equals new { rs.TenantId, rs.ConversationId, rs.UserId }
                        into readStates
                    from rs in readStates.DefaultIfEmpty()
                    select new
                    {
                        Conversation = c,
                        Assignment = a,
                        Sla = s,
                        Queue = q,
                        ReadState = rs,
                        MentionCount = _db.MessageMentions
                            .Count(m => m.TenantId == tenantId
                                     && m.ConversationId == c.Id
                                     && m.MentionedUserId == currentUserId),
                        HasMentionsForFilter = request.MentionedUserId != null
                            ? _db.MessageMentions
                                .Any(m => m.TenantId == tenantId
                                       && m.ConversationId == c.Id
                                       && m.MentionedUserId == request.MentionedUserId)
                            : true,
                        LastMessageBody = _db.Messages
                            .Where(msg => msg.TenantId == tenantId && msg.ConversationId == c.Id)
                            .OrderByDescending(msg => msg.SentAtUtc)
                            .Select(msg => msg.Body)
                            .FirstOrDefault()
                    };

        if (request.QueueId.HasValue)
            query = query.Where(x => x.Assignment != null && x.Assignment.QueueId == request.QueueId.Value);

        if (request.AssignedUserId.HasValue)
            query = query.Where(x => x.Assignment != null && x.Assignment.AssignedUserId == request.AssignedUserId.Value);

        if (!string.IsNullOrWhiteSpace(request.AssignmentStatus))
            query = query.Where(x => x.Assignment != null && x.Assignment.AssignmentStatus == request.AssignmentStatus);

        if (!string.IsNullOrWhiteSpace(request.Priority))
            query = query.Where(x => x.Sla != null && x.Sla.Priority == request.Priority);

        if (!string.IsNullOrWhiteSpace(request.OperationalStatus))
            query = query.Where(x => x.Conversation.Status == request.OperationalStatus);

        if (!string.IsNullOrWhiteSpace(request.WaitingState))
            query = query.Where(x => x.Sla != null && x.Sla.WaitingOn == request.WaitingState);

        if (request.BreachedFirstResponse.HasValue)
            query = query.Where(x => x.Sla != null && x.Sla.BreachedFirstResponse == request.BreachedFirstResponse.Value);

        if (request.BreachedResolution.HasValue)
            query = query.Where(x => x.Sla != null && x.Sla.BreachedResolution == request.BreachedResolution.Value);

        if (request.MentionedUserId.HasValue)
            query = query.Where(x => x.HasMentionsForFilter);

        if (request.UnreadOnly == true)
            query = query.Where(x => x.ReadState == null || x.ReadState.LastReadAtUtc == null || x.ReadState.LastReadAtUtc < x.Conversation.LastActivityAtUtc);

        if (request.UpdatedSince.HasValue)
            query = query.Where(x => x.Conversation.UpdatedAtUtc >= request.UpdatedSince.Value);

        if (request.CreatedSince.HasValue)
            query = query.Where(x => x.Conversation.CreatedAtUtc >= request.CreatedSince.Value);

        var totalCount = await query.CountAsync(ct);

        var sortBy = (request.SortBy ?? "lastActivityAtUtc").ToLowerInvariant();
        var descending = string.Equals(request.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        query = sortBy switch
        {
            "firstresponsedueatutc" =>
                descending
                    ? query.OrderByDescending(x => x.Sla != null ? x.Sla.FirstResponseDueAtUtc : (DateTime?)null).ThenByDescending(x => x.Conversation.Id)
                    : query.OrderBy(x => x.Sla != null ? x.Sla.FirstResponseDueAtUtc : (DateTime?)null).ThenBy(x => x.Conversation.Id),
            "resolutiondueatutc" =>
                descending
                    ? query.OrderByDescending(x => x.Sla != null ? x.Sla.ResolutionDueAtUtc : (DateTime?)null).ThenByDescending(x => x.Conversation.Id)
                    : query.OrderBy(x => x.Sla != null ? x.Sla.ResolutionDueAtUtc : (DateTime?)null).ThenBy(x => x.Conversation.Id),
            "priority" =>
                descending
                    ? query.OrderByDescending(x => x.Sla != null ? x.Sla.Priority : null).ThenByDescending(x => x.Conversation.Id)
                    : query.OrderBy(x => x.Sla != null ? x.Sla.Priority : null).ThenBy(x => x.Conversation.Id),
            "createdatutc" =>
                descending
                    ? query.OrderByDescending(x => x.Conversation.CreatedAtUtc).ThenByDescending(x => x.Conversation.Id)
                    : query.OrderBy(x => x.Conversation.CreatedAtUtc).ThenBy(x => x.Conversation.Id),
            _ =>
                descending
                    ? query.OrderByDescending(x => x.Conversation.LastActivityAtUtc).ThenByDescending(x => x.Conversation.Id)
                    : query.OrderBy(x => x.Conversation.LastActivityAtUtc).ThenBy(x => x.Conversation.Id),
        };

        var skip = (request.Page - 1) * request.PageSize;

        var items = await query
            .Skip(skip)
            .Take(request.PageSize)
            .Select(x => new ConversationOperationalListItemResponse(
                x.Conversation.Id,
                x.Conversation.Subject,
                x.Conversation.Status,
                x.Assignment != null ? x.Assignment.QueueId : null,
                x.Queue != null ? x.Queue.Name : null,
                x.Assignment != null ? x.Assignment.AssignedUserId : null,
                x.Assignment != null ? x.Assignment.AssignmentStatus : null,
                x.Sla != null ? x.Sla.Priority : null,
                x.Sla != null ? x.Sla.WaitingOn : null,
                x.Sla != null && x.Sla.BreachedFirstResponse,
                x.Sla != null && x.Sla.BreachedResolution,
                x.Sla != null ? x.Sla.FirstResponseDueAtUtc : null,
                x.Sla != null ? x.Sla.ResolutionDueAtUtc : null,
                x.Conversation.LastActivityAtUtc,
                x.Conversation.CreatedAtUtc,
                x.ReadState == null || x.ReadState.LastReadAtUtc == null || x.ReadState.LastReadAtUtc < x.Conversation.LastActivityAtUtc,
                x.MentionCount,
                x.LastMessageBody != null
                    ? (x.LastMessageBody.Length > 120 ? x.LastMessageBody.Substring(0, 120) : x.LastMessageBody)
                    : null))
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
