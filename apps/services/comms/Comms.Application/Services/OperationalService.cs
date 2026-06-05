using Microsoft.Extensions.Logging;
using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Application.Repositories;
using Comms.Domain.Entities;
using Comms.Domain.Enums;

namespace Comms.Application.Services;

public class OperationalService : IOperationalService
{
    private readonly IConversationSlaStateRepository _slaRepo;
    private readonly IConversationAssignmentRepository _assignmentRepo;
    private readonly IConversationQueueRepository _queueRepo;
    private readonly IConversationRepository _conversationRepo;
    private readonly IConversationTimelineService _timeline;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<OperationalService> _logger;

    public OperationalService(
        IConversationSlaStateRepository slaRepo,
        IConversationAssignmentRepository assignmentRepo,
        IConversationQueueRepository queueRepo,
        IConversationRepository conversationRepo,
        IConversationTimelineService timeline,
        IAuditPublisher audit,
        ILogger<OperationalService> logger)
    {
        _slaRepo = slaRepo;
        _assignmentRepo = assignmentRepo;
        _queueRepo = queueRepo;
        _conversationRepo = conversationRepo;
        _timeline = timeline;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ConversationSlaStateResponse?> GetSlaStateAsync(
        Guid tenantId, Guid conversationId, CancellationToken ct = default)
    {
        var sla = await _slaRepo.GetByConversationAsync(tenantId, conversationId, ct);
        if (sla is null) return null;

        sla.EvaluateBreaches(DateTime.UtcNow);
        await _slaRepo.UpdateAsync(sla, ct);

        return ToSlaResponse(sla);
    }

    public async Task<ConversationSlaStateResponse> UpdatePriorityAsync(
        Guid tenantId, Guid conversationId, Guid userId, UpdateConversationPriorityRequest request, CancellationToken ct = default)
    {
        var sla = await _slaRepo.GetByConversationAsync(tenantId, conversationId, ct)
            ?? throw new KeyNotFoundException($"No SLA state found for conversation '{conversationId}'.");

        var oldPriority = sla.Priority;
        var now = DateTime.UtcNow;

        sla.UpdatePriority(request.Priority, now, userId);
        await _slaRepo.UpdateAsync(sla, ct);

        _logger.LogInformation("Priority changed for conversation {ConversationId}: {OldPriority} -> {NewPriority}",
            conversationId, oldPriority, request.Priority);

        _audit.Publish("PriorityChanged", "Updated",
            $"Priority changed from {oldPriority} to {request.Priority}",
            tenantId, userId, "ConversationSlaState", sla.Id.ToString(),
            metadata: $"{{\"conversationId\":\"{conversationId}\",\"oldPriority\":\"{oldPriority}\",\"newPriority\":\"{request.Priority}\",\"firstResponseDueAtUtc\":\"{sla.FirstResponseDueAtUtc:O}\",\"resolutionDueAtUtc\":\"{sla.ResolutionDueAtUtc:O}\"}}");

        try
        {
            await _timeline.RecordAsync(
                tenantId, conversationId,
                Domain.Constants.TimelineEventTypes.PriorityChanged,
                Domain.Constants.TimelineActorType.User,
                $"Priority changed from {oldPriority} to {request.Priority}",
                Domain.Constants.TimelineVisibility.InternalOnly,
                now,
                actorId: userId,
                relatedSlaId: sla.Id,
                metadataJson: $"{{\"oldPriority\":\"{oldPriority}\",\"newPriority\":\"{request.Priority}\"}}",
                ct: ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to record timeline for priority change on {ConversationId}", conversationId); }

        return ToSlaResponse(sla);
    }

    public async Task<ConversationOperationalSummaryResponse?> GetOperationalSummaryAsync(
        Guid tenantId, Guid conversationId, CancellationToken ct = default)
    {
        var conversation = await _conversationRepo.GetByIdAsync(tenantId, conversationId, ct);
        if (conversation is null) return null;

        var assignment = await _assignmentRepo.GetByConversationAsync(tenantId, conversationId, ct);
        var sla = await _slaRepo.GetByConversationAsync(tenantId, conversationId, ct);

        if (sla is not null)
        {
            sla.EvaluateBreaches(DateTime.UtcNow);
            await _slaRepo.UpdateAsync(sla, ct);
        }

        ConversationQueueResponse? queueResponse = null;
        string? queueName = null;
        if (assignment?.QueueId is not null)
        {
            var queue = await _queueRepo.GetByIdAsync(tenantId, assignment.QueueId.Value, ct);
            if (queue is not null)
            {
                queueResponse = ToQueueResponse(queue);
                queueName = queue.Name;
            }
        }

        return new ConversationOperationalSummaryResponse(
            conversation.Id,
            conversation.Status,
            conversation.Subject,
            queueResponse,
            assignment is not null ? ToAssignmentResponse(assignment, queueName) : null,
            sla is not null ? ToSlaResponse(sla) : null,
            conversation.LastActivityAtUtc,
            conversation.CreatedAtUtc);
    }

    public async Task<List<ConversationOperationalSummaryResponse>> ListOperationalAsync(
        Guid tenantId, OperationalListQuery query, CancellationToken ct = default)
    {
        var conversations = await _conversationRepo.ListByTenantAsync(tenantId, ct);

        var assignmentMap = new Dictionary<Guid, ConversationAssignment>();
        var slaMap = new Dictionary<Guid, ConversationSlaState>();
        var queueMap = new Dictionary<Guid, ConversationQueue>();

        foreach (var conv in conversations)
        {
            var assignment = await _assignmentRepo.GetByConversationAsync(tenantId, conv.Id, ct);
            if (assignment is not null)
                assignmentMap[conv.Id] = assignment;

            var sla = await _slaRepo.GetByConversationAsync(tenantId, conv.Id, ct);
            if (sla is not null)
            {
                sla.EvaluateBreaches(DateTime.UtcNow);
                slaMap[conv.Id] = sla;
            }
        }

        var queues = await _queueRepo.ListByTenantAsync(tenantId, ct);
        foreach (var q in queues)
            queueMap[q.Id] = q;

        var results = new List<ConversationOperationalSummaryResponse>();

        foreach (var conv in conversations)
        {
            assignmentMap.TryGetValue(conv.Id, out var assignment);
            slaMap.TryGetValue(conv.Id, out var sla);

            if (query.QueueId.HasValue && assignment?.QueueId != query.QueueId) continue;
            if (query.AssignedUserId.HasValue && assignment?.AssignedUserId != query.AssignedUserId) continue;
            if (!string.IsNullOrWhiteSpace(query.AssignmentStatus) && assignment?.AssignmentStatus != query.AssignmentStatus) continue;
            if (!string.IsNullOrWhiteSpace(query.Priority) && sla?.Priority != query.Priority) continue;
            if (query.BreachedFirstResponse.HasValue && sla?.BreachedFirstResponse != query.BreachedFirstResponse) continue;
            if (query.BreachedResolution.HasValue && sla?.BreachedResolution != query.BreachedResolution) continue;
            if (!string.IsNullOrWhiteSpace(query.ConversationStatus) && conv.Status != query.ConversationStatus) continue;

            ConversationQueueResponse? queueResponse = null;
            string? queueName = null;
            if (assignment?.QueueId is not null && queueMap.TryGetValue(assignment.QueueId.Value, out var queue))
            {
                queueResponse = ToQueueResponse(queue);
                queueName = queue.Name;
            }

            results.Add(new ConversationOperationalSummaryResponse(
                conv.Id,
                conv.Status,
                conv.Subject,
                queueResponse,
                assignment is not null ? ToAssignmentResponse(assignment, queueName) : null,
                sla is not null ? ToSlaResponse(sla) : null,
                conv.LastActivityAtUtc,
                conv.CreatedAtUtc));
        }

        return results;
    }

    public async Task InitializeSlaAsync(
        Guid tenantId, Guid conversationId, string priority, DateTime startAtUtc, Guid userId, CancellationToken ct = default)
    {
        var existing = await _slaRepo.GetByConversationAsync(tenantId, conversationId, ct);
        if (existing is not null) return;

        var sla = ConversationSlaState.Initialize(tenantId, conversationId, priority, startAtUtc, userId);
        await _slaRepo.AddAsync(sla, ct);

        _logger.LogInformation("SLA initialized for conversation {ConversationId}: Priority={Priority}", conversationId, priority);

        _audit.Publish("SlaInitialized", "Created",
            $"SLA initialized with priority {priority}",
            tenantId, userId, "ConversationSlaState", sla.Id.ToString(),
            metadata: $"{{\"conversationId\":\"{conversationId}\",\"priority\":\"{priority}\",\"firstResponseDueAtUtc\":\"{sla.FirstResponseDueAtUtc:O}\",\"resolutionDueAtUtc\":\"{sla.ResolutionDueAtUtc:O}\"}}");

        try
        {
            await _timeline.RecordAsync(
                tenantId, conversationId,
                Domain.Constants.TimelineEventTypes.SlaStarted,
                Domain.Constants.TimelineActorType.System,
                $"SLA started with priority {priority}",
                Domain.Constants.TimelineVisibility.InternalOnly,
                startAtUtc,
                relatedSlaId: sla.Id,
                metadataJson: $"{{\"priority\":\"{priority}\",\"firstResponseDueAtUtc\":\"{sla.FirstResponseDueAtUtc:O}\",\"resolutionDueAtUtc\":\"{sla.ResolutionDueAtUtc:O}\"}}",
                ct: ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to record timeline for SLA initialization on {ConversationId}", conversationId); }
    }

    public async Task SatisfyFirstResponseAsync(
        Guid tenantId, Guid conversationId, DateTime respondedAtUtc, Guid userId, CancellationToken ct = default)
    {
        var sla = await _slaRepo.GetByConversationAsync(tenantId, conversationId, ct);
        if (sla is null || sla.FirstResponseAtUtc.HasValue) return;

        sla.EvaluateBreaches(respondedAtUtc);
        sla.SatisfyFirstResponse(respondedAtUtc, userId);
        await _slaRepo.UpdateAsync(sla, ct);

        _logger.LogInformation("First response SLA satisfied for conversation {ConversationId}", conversationId);

        _audit.Publish("FirstResponseSlaSatisfied", "Updated",
            "First response SLA satisfied",
            tenantId, userId, "ConversationSlaState", sla.Id.ToString(),
            metadata: $"{{\"conversationId\":\"{conversationId}\",\"respondedAtUtc\":\"{respondedAtUtc:O}\",\"breached\":{sla.BreachedFirstResponse.ToString().ToLower()}}}");

        try
        {
            await _timeline.RecordAsync(
                tenantId, conversationId,
                Domain.Constants.TimelineEventTypes.FirstResponseSatisfied,
                Domain.Constants.TimelineActorType.User,
                $"First response SLA satisfied{(sla.BreachedFirstResponse ? " (after breach)" : "")}",
                Domain.Constants.TimelineVisibility.InternalOnly,
                respondedAtUtc,
                actorId: userId,
                relatedSlaId: sla.Id,
                metadataJson: $"{{\"breached\":{sla.BreachedFirstResponse.ToString().ToLower()}}}",
                ct: ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to record timeline for first response satisfaction on {ConversationId}", conversationId); }
    }

    public async Task SatisfyResolutionAsync(
        Guid tenantId, Guid conversationId, DateTime resolvedAtUtc, Guid userId, CancellationToken ct = default)
    {
        var sla = await _slaRepo.GetByConversationAsync(tenantId, conversationId, ct);
        if (sla is null || sla.ResolvedAtUtc.HasValue) return;

        sla.EvaluateBreaches(resolvedAtUtc);
        sla.SatisfyResolution(resolvedAtUtc, userId);
        await _slaRepo.UpdateAsync(sla, ct);

        _logger.LogInformation("Resolution SLA satisfied for conversation {ConversationId}", conversationId);

        _audit.Publish("ResolutionSlaSatisfied", "Updated",
            "Resolution SLA satisfied",
            tenantId, userId, "ConversationSlaState", sla.Id.ToString(),
            metadata: $"{{\"conversationId\":\"{conversationId}\",\"resolvedAtUtc\":\"{resolvedAtUtc:O}\",\"breached\":{sla.BreachedResolution.ToString().ToLower()}}}");

        try
        {
            await _timeline.RecordAsync(
                tenantId, conversationId,
                Domain.Constants.TimelineEventTypes.Resolved,
                Domain.Constants.TimelineActorType.User,
                $"Resolution SLA satisfied{(sla.BreachedResolution ? " (after breach)" : "")}",
                Domain.Constants.TimelineVisibility.InternalOnly,
                resolvedAtUtc,
                actorId: userId,
                relatedSlaId: sla.Id,
                metadataJson: $"{{\"breached\":{sla.BreachedResolution.ToString().ToLower()}}}",
                ct: ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to record timeline for resolution satisfaction on {ConversationId}", conversationId); }
    }

    public async Task UpdateWaitingStateAsync(
        Guid tenantId, Guid conversationId, string waitingState, Guid userId, CancellationToken ct = default)
    {
        var sla = await _slaRepo.GetByConversationAsync(tenantId, conversationId, ct);
        if (sla is null) return;

        var oldState = sla.WaitingOn;
        sla.SetWaitingOn(waitingState, userId);
        await _slaRepo.UpdateAsync(sla, ct);

        if (oldState != waitingState)
        {
            _audit.Publish("WaitingStateChanged", "Updated",
                $"Waiting state changed from {oldState} to {waitingState}",
                tenantId, userId, "ConversationSlaState", sla.Id.ToString(),
                metadata: $"{{\"conversationId\":\"{conversationId}\",\"oldState\":\"{oldState}\",\"newState\":\"{waitingState}\"}}");
        }
    }

    private static ConversationSlaStateResponse ToSlaResponse(ConversationSlaState s) => new(
        s.Id, s.TenantId, s.ConversationId,
        s.Priority, s.FirstResponseDueAtUtc, s.ResolutionDueAtUtc,
        s.FirstResponseAtUtc, s.ResolvedAtUtc,
        s.BreachedFirstResponse, s.BreachedResolution,
        s.WaitingOn, s.LastEvaluatedAtUtc, s.SlaStartedAtUtc,
        s.CreatedAtUtc, s.UpdatedAtUtc);

    private static ConversationAssignmentResponse ToAssignmentResponse(ConversationAssignment a, string? queueName) => new(
        a.Id, a.TenantId, a.ConversationId,
        a.QueueId, queueName,
        a.AssignedUserId, a.AssignedByUserId,
        a.AssignmentStatus,
        a.AssignedAtUtc, a.LastAssignedAtUtc,
        a.AcceptedAtUtc, a.UnassignedAtUtc,
        a.CreatedAtUtc, a.UpdatedAtUtc);

    private static ConversationQueueResponse ToQueueResponse(ConversationQueue q) => new(
        q.Id, q.TenantId, q.Name, q.Code, q.Description,
        q.IsDefault, q.IsActive,
        q.CreatedAtUtc, q.UpdatedAtUtc, q.CreatedByUserId);
}
