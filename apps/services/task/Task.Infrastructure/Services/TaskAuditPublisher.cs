using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using Microsoft.Extensions.Logging;
using Task.Application.Interfaces;

namespace Task.Infrastructure.Services;

public class TaskAuditPublisher : ITaskAuditPublisher
{
    private readonly IAuditEventClient       _client;
    private readonly ILogger<TaskAuditPublisher> _logger;

    public TaskAuditPublisher(IAuditEventClient client, ILogger<TaskAuditPublisher> logger)
    {
        _client = client;
        _logger = logger;
    }

    public void Publish(
        string  eventType,
        string  action,
        string  description,
        Guid    tenantId,
        Guid?   actorUserId  = null,
        string? entityType   = null,
        string? entityId     = null,
        string? metadata     = null)
    {
        var now     = DateTimeOffset.UtcNow;
        var request = new IngestAuditEventRequest
        {
            EventType      = eventType,
            EventCategory  = EventCategory.Business,
            SourceSystem   = "task-service",
            SourceService  = "platform-tasks",
            Visibility     = VisibilityScope.Tenant,
            Severity       = SeverityLevel.Info,
            OccurredAtUtc  = now,
            Scope = new AuditEventScopeDto
            {
                ScopeType = ScopeType.Tenant,
                TenantId  = tenantId.ToString(),
            },
            Actor = new AuditEventActorDto
            {
                Type = actorUserId.HasValue ? ActorType.User : ActorType.System,
                Id   = actorUserId?.ToString(),
            },
            Entity = entityType is not null
                ? new AuditEventEntityDto { Type = entityType, Id = entityId }
                : null,
            Action      = action,
            Description = description,
            Metadata    = metadata,
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(
                now, "task-service", eventType, entityId ?? tenantId.ToString()),
            Tags = ["task-management"],
        };

        _ = _client.IngestAsync(request).ContinueWith(t =>
        {
            if (t.IsFaulted)
                _logger.LogWarning(t.Exception, "Audit publish failed for {EventType}", eventType);
        }, System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
    }
}
