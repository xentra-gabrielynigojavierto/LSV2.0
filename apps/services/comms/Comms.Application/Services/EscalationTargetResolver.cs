using Microsoft.Extensions.Logging;
using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Application.Repositories;

namespace Comms.Application.Services;

public class EscalationTargetResolver : IEscalationTargetResolver
{
    private readonly IConversationAssignmentRepository _assignmentRepo;
    private readonly IQueueEscalationConfigRepository _escalationConfigRepo;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<EscalationTargetResolver> _logger;

    public EscalationTargetResolver(
        IConversationAssignmentRepository assignmentRepo,
        IQueueEscalationConfigRepository escalationConfigRepo,
        IAuditPublisher audit,
        ILogger<EscalationTargetResolver> logger)
    {
        _assignmentRepo = assignmentRepo;
        _escalationConfigRepo = escalationConfigRepo;
        _audit = audit;
        _logger = logger;
    }

    public async Task<EscalationTarget?> ResolveAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default)
    {
        var assignment = await _assignmentRepo.GetByConversationAsync(tenantId, conversationId, ct);

        if (assignment?.AssignedUserId is not null && assignment.AssignmentStatus != Domain.Enums.AssignmentStatus.Unassigned)
        {
            _audit.Publish("EscalationTargetResolved", "Resolved",
                "Escalation target resolved to assigned user",
                tenantId, entityType: "ConversationAssignment", entityId: assignment.Id.ToString(),
                metadata: $"{{\"conversationId\":\"{conversationId}\",\"targetUserId\":\"{assignment.AssignedUserId}\",\"source\":\"assigned_user\"}}");

            return new EscalationTarget(assignment.AssignedUserId.Value, assignment.QueueId, "assigned_user");
        }

        if (assignment?.QueueId is not null)
        {
            var config = await _escalationConfigRepo.GetActiveByQueueAsync(tenantId, assignment.QueueId.Value, ct);
            if (config?.FallbackUserId is not null)
            {
                _audit.Publish("EscalationTargetResolved", "Resolved",
                    "Escalation target resolved to queue fallback user",
                    tenantId, entityType: "QueueEscalationConfig", entityId: config.Id.ToString(),
                    metadata: $"{{\"conversationId\":\"{conversationId}\",\"targetUserId\":\"{config.FallbackUserId}\",\"queueId\":\"{assignment.QueueId}\",\"source\":\"queue_fallback\"}}");

                return new EscalationTarget(config.FallbackUserId.Value, assignment.QueueId, "queue_fallback");
            }
        }

        _logger.LogWarning("No escalation target found for conversation {ConversationId} in tenant {TenantId}",
            conversationId, tenantId);

        _audit.Publish("EscalationTargetMissing", "Skipped",
            "No escalation target available for conversation",
            tenantId, entityType: "Conversation", entityId: conversationId.ToString(),
            metadata: $"{{\"conversationId\":\"{conversationId}\",\"reason\":\"no_assigned_user_or_queue_fallback\"}}");

        return null;
    }
}
