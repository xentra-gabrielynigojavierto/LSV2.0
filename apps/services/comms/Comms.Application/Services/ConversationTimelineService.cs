using Microsoft.Extensions.Logging;
using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Application.Repositories;
using Comms.Domain.Entities;

namespace Comms.Application.Services;

public class ConversationTimelineService : IConversationTimelineService
{
    private readonly IConversationTimelineRepository _repo;
    private readonly ILogger<ConversationTimelineService> _logger;

    public ConversationTimelineService(
        IConversationTimelineRepository repo,
        ILogger<ConversationTimelineService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task RecordAsync(
        Guid tenantId, Guid conversationId,
        string eventType, string actorType, string summary, string visibility,
        DateTime occurredAtUtc,
        string? eventSubType = null,
        Guid? actorId = null,
        string? actorDisplayName = null,
        string? metadataJson = null,
        Guid? relatedMessageId = null,
        Guid? relatedAssignmentId = null,
        Guid? relatedSlaId = null,
        CancellationToken ct = default)
    {
        var entry = ConversationTimelineEntry.Create(
            tenantId, conversationId,
            eventType, actorType, summary, visibility,
            occurredAtUtc,
            eventSubType, actorId, actorDisplayName,
            metadataJson, relatedMessageId, relatedAssignmentId, relatedSlaId);

        await _repo.AddAsync(entry, ct);

        _logger.LogDebug(
            "Timeline entry recorded: {EventType} for conversation {ConversationId}",
            eventType, conversationId);
    }

    public async Task<TimelinePageResponse> GetTimelineAsync(
        Guid tenantId, Guid conversationId,
        TimelineQuery query,
        CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var skip = (page - 1) * pageSize;

        var items = await _repo.QueryAsync(
            tenantId, conversationId,
            query.FromDate, query.ToDate,
            query.EventTypes, query.IncludeInternal,
            skip, pageSize,
            ct);

        var totalCount = await _repo.CountAsync(
            tenantId, conversationId,
            query.FromDate, query.ToDate,
            query.EventTypes, query.IncludeInternal,
            ct);

        var responses = items.Select(e => new TimelineEntryResponse(
            e.Id, e.ConversationId, e.EventType, e.EventSubType,
            e.ActorType, e.ActorId, e.ActorDisplayName,
            e.OccurredAtUtc, e.Summary, e.MetadataJson,
            e.RelatedMessageId, e.RelatedAssignmentId, e.RelatedSlaId,
            e.Visibility, e.CreatedAtUtc)).ToList();

        return new TimelinePageResponse(responses, totalCount, page, pageSize, skip + pageSize < totalCount);
    }
}
