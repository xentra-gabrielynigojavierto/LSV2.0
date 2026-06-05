using Microsoft.Extensions.Logging;
using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Application.Repositories;
using Comms.Domain.Entities;
using Comms.Domain.Enums;

namespace Comms.Application.Services;

public class AssignmentService : IAssignmentService
{
    private readonly IConversationAssignmentRepository _assignmentRepo;
    private readonly IConversationQueueRepository _queueRepo;
    private readonly IConversationRepository _conversationRepo;
    private readonly IConversationSlaStateRepository _slaRepo;
    private readonly IConversationTimelineService _timeline;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<AssignmentService> _logger;

    public AssignmentService(
        IConversationAssignmentRepository assignmentRepo,
        IConversationQueueRepository queueRepo,
        IConversationRepository conversationRepo,
        IConversationSlaStateRepository slaRepo,
        IConversationTimelineService timeline,
        IAuditPublisher audit,
        ILogger<AssignmentService> logger)
    {
        _assignmentRepo = assignmentRepo;
        _queueRepo = queueRepo;
        _conversationRepo = conversationRepo;
        _slaRepo = slaRepo;
        _timeline = timeline;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ConversationAssignmentResponse> AssignAsync(
        Guid tenantId, Guid conversationId, Guid userId, AssignConversationRequest request, CancellationToken ct = default)
    {
        var conversation = await _conversationRepo.GetByIdAsync(tenantId, conversationId, ct)
            ?? throw new KeyNotFoundException($"Conversation '{conversationId}' not found.");

        string? queueName = null;
        if (request.QueueId.HasValue)
        {
            var queue = await _queueRepo.GetByIdAsync(tenantId, request.QueueId.Value, ct)
                ?? throw new KeyNotFoundException($"Queue '{request.QueueId}' not found.");
            if (!queue.IsActive)
                throw new InvalidOperationException("Cannot assign to an inactive queue.");
            queueName = queue.Name;
        }

        var existing = await _assignmentRepo.GetByConversationAsync(tenantId, conversationId, ct);
        if (existing is not null)
            throw new InvalidOperationException("Conversation already has an assignment. Use reassign instead.");

        var assignment = ConversationAssignment.Create(
            tenantId, conversationId, request.QueueId, request.AssignedUserId, userId, userId);

        await _assignmentRepo.AddAsync(assignment, ct);

        if (!string.IsNullOrWhiteSpace(request.Priority))
        {
            var sla = await _slaRepo.GetByConversationAsync(tenantId, conversationId, ct);
            if (sla is not null)
            {
                sla.UpdatePriority(request.Priority, DateTime.UtcNow, userId);
                await _slaRepo.UpdateAsync(sla, ct);
            }
            else
            {
                var newSla = ConversationSlaState.Initialize(tenantId, conversationId, request.Priority, DateTime.UtcNow, userId);
                await _slaRepo.AddAsync(newSla, ct);
            }
        }

        _logger.LogInformation("Conversation {ConversationId} assigned: Queue={QueueId}, User={UserId}",
            conversationId, request.QueueId, request.AssignedUserId);

        _audit.Publish("ConversationAssigned", "Assigned",
            $"Conversation assigned to queue/user",
            tenantId, userId, "ConversationAssignment", assignment.Id.ToString(),
            metadata: $"{{\"conversationId\":\"{conversationId}\",\"queueId\":\"{request.QueueId}\",\"assignedUserId\":\"{request.AssignedUserId}\",\"status\":\"{assignment.AssignmentStatus}\"}}");

        try
        {
            await _timeline.RecordAsync(
                tenantId, conversationId,
                Domain.Constants.TimelineEventTypes.Assigned,
                Domain.Constants.TimelineActorType.User,
                $"Case assigned{(queueName != null ? $" to queue {queueName}" : "")}",
                Domain.Constants.TimelineVisibility.InternalOnly,
                DateTime.UtcNow,
                actorId: userId,
                relatedAssignmentId: assignment.Id,
                metadataJson: $"{{\"queueId\":\"{request.QueueId}\",\"assignedUserId\":\"{request.AssignedUserId}\",\"queueName\":\"{queueName}\"}}",
                ct: ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to record timeline for assignment on {ConversationId}", conversationId); }

        return ToResponse(assignment, queueName);
    }

    public async Task<ConversationAssignmentResponse> ReassignAsync(
        Guid tenantId, Guid conversationId, Guid userId, ReassignConversationRequest request, CancellationToken ct = default)
    {
        var assignment = await _assignmentRepo.GetByConversationAsync(tenantId, conversationId, ct)
            ?? throw new KeyNotFoundException($"No assignment found for conversation '{conversationId}'.");

        string? queueName = null;
        if (request.QueueId.HasValue)
        {
            var queue = await _queueRepo.GetByIdAsync(tenantId, request.QueueId.Value, ct)
                ?? throw new KeyNotFoundException($"Queue '{request.QueueId}' not found.");
            if (!queue.IsActive)
                throw new InvalidOperationException("Cannot reassign to an inactive queue.");
            queueName = queue.Name;
        }

        var oldUserId = assignment.AssignedUserId;
        var oldQueueId = assignment.QueueId;

        assignment.Reassign(request.QueueId, request.AssignedUserId, userId, userId);
        await _assignmentRepo.UpdateAsync(assignment, ct);

        _logger.LogInformation("Conversation {ConversationId} reassigned: Queue={QueueId}, User={UserId}",
            conversationId, request.QueueId, request.AssignedUserId);

        _audit.Publish("ConversationReassigned", "Reassigned",
            $"Conversation reassigned",
            tenantId, userId, "ConversationAssignment", assignment.Id.ToString(),
            metadata: $"{{\"conversationId\":\"{conversationId}\",\"oldQueueId\":\"{oldQueueId}\",\"newQueueId\":\"{request.QueueId}\",\"oldAssignedUserId\":\"{oldUserId}\",\"newAssignedUserId\":\"{request.AssignedUserId}\"}}");

        try
        {
            await _timeline.RecordAsync(
                tenantId, conversationId,
                Domain.Constants.TimelineEventTypes.Reassigned,
                Domain.Constants.TimelineActorType.User,
                $"Case reassigned{(queueName != null ? $" to queue {queueName}" : "")}",
                Domain.Constants.TimelineVisibility.InternalOnly,
                DateTime.UtcNow,
                actorId: userId,
                relatedAssignmentId: assignment.Id,
                metadataJson: $"{{\"oldQueueId\":\"{oldQueueId}\",\"newQueueId\":\"{request.QueueId}\",\"oldAssignedUserId\":\"{oldUserId}\",\"newAssignedUserId\":\"{request.AssignedUserId}\"}}",
                ct: ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to record timeline for reassignment on {ConversationId}", conversationId); }

        return ToResponse(assignment, queueName);
    }

    public async Task<ConversationAssignmentResponse> UnassignAsync(
        Guid tenantId, Guid conversationId, Guid userId, CancellationToken ct = default)
    {
        var assignment = await _assignmentRepo.GetByConversationAsync(tenantId, conversationId, ct)
            ?? throw new KeyNotFoundException($"No assignment found for conversation '{conversationId}'.");

        var oldUserId = assignment.AssignedUserId;
        assignment.Unassign(userId);
        await _assignmentRepo.UpdateAsync(assignment, ct);

        string? queueName = null;
        if (assignment.QueueId.HasValue)
        {
            var queue = await _queueRepo.GetByIdAsync(tenantId, assignment.QueueId.Value, ct);
            queueName = queue?.Name;
        }

        _logger.LogInformation("Conversation {ConversationId} unassigned", conversationId);

        _audit.Publish("ConversationUnassigned", "Unassigned",
            $"Conversation user unassigned",
            tenantId, userId, "ConversationAssignment", assignment.Id.ToString(),
            metadata: $"{{\"conversationId\":\"{conversationId}\",\"previousAssignedUserId\":\"{oldUserId}\"}}");

        try
        {
            await _timeline.RecordAsync(
                tenantId, conversationId,
                Domain.Constants.TimelineEventTypes.Unassigned,
                Domain.Constants.TimelineActorType.User,
                "Case unassigned",
                Domain.Constants.TimelineVisibility.InternalOnly,
                DateTime.UtcNow,
                actorId: userId,
                relatedAssignmentId: assignment.Id,
                metadataJson: $"{{\"previousAssignedUserId\":\"{oldUserId}\"}}",
                ct: ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to record timeline for unassignment on {ConversationId}", conversationId); }

        return ToResponse(assignment, queueName);
    }

    public async Task<ConversationAssignmentResponse> AcceptAsync(
        Guid tenantId, Guid conversationId, Guid userId, CancellationToken ct = default)
    {
        var assignment = await _assignmentRepo.GetByConversationAsync(tenantId, conversationId, ct)
            ?? throw new KeyNotFoundException($"No assignment found for conversation '{conversationId}'.");

        if (assignment.AssignedUserId != userId)
            throw new UnauthorizedAccessException("Only the assigned user can accept the assignment.");

        assignment.Accept(userId);
        await _assignmentRepo.UpdateAsync(assignment, ct);

        string? queueName = null;
        if (assignment.QueueId.HasValue)
        {
            var queue = await _queueRepo.GetByIdAsync(tenantId, assignment.QueueId.Value, ct);
            queueName = queue?.Name;
        }

        _logger.LogInformation("Conversation {ConversationId} assignment accepted by user {UserId}", conversationId, userId);

        _audit.Publish("AssignmentAccepted", "Accepted",
            $"Assignment accepted",
            tenantId, userId, "ConversationAssignment", assignment.Id.ToString(),
            metadata: $"{{\"conversationId\":\"{conversationId}\"}}");

        return ToResponse(assignment, queueName);
    }

    public async Task<ConversationAssignmentResponse?> GetByConversationAsync(
        Guid tenantId, Guid conversationId, CancellationToken ct = default)
    {
        var assignment = await _assignmentRepo.GetByConversationAsync(tenantId, conversationId, ct);
        if (assignment is null) return null;

        string? queueName = null;
        if (assignment.QueueId.HasValue)
        {
            var queue = await _queueRepo.GetByIdAsync(tenantId, assignment.QueueId.Value, ct);
            queueName = queue?.Name;
        }

        return ToResponse(assignment, queueName);
    }

    private static ConversationAssignmentResponse ToResponse(ConversationAssignment a, string? queueName) => new(
        a.Id, a.TenantId, a.ConversationId,
        a.QueueId, queueName,
        a.AssignedUserId, a.AssignedByUserId,
        a.AssignmentStatus,
        a.AssignedAtUtc, a.LastAssignedAtUtc,
        a.AcceptedAtUtc, a.UnassignedAtUtc,
        a.CreatedAtUtc, a.UpdatedAtUtc);
}
