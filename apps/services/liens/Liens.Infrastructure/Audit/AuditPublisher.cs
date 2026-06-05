using Liens.Application.Interfaces;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using Microsoft.Extensions.Logging;

namespace Liens.Infrastructure.Audit;

public sealed class AuditPublisher : IAuditPublisher
{
    private readonly IAuditEventClient _client;
    private readonly ILogger<AuditPublisher> _logger;

    public AuditPublisher(IAuditEventClient client, ILogger<AuditPublisher> logger)
    {
        _client = client;
        _logger = logger;
    }

    public void Publish(
        string eventType,
        string action,
        string description,
        Guid tenantId,
        Guid? actorUserId = null,
        string? entityType = null,
        string? entityId = null,
        string? before = null,
        string? after = null,
        string? metadata = null)
    {
        var now = DateTimeOffset.UtcNow;
        var request = new IngestAuditEventRequest
        {
            EventType = eventType,
            EventCategory = EventCategory.Business,
            SourceSystem = "liens-service",
            SourceService = "liens-api",
            Visibility = VisibilityScope.Tenant,
            Severity = SeverityLevel.Info,
            OccurredAtUtc = now,
            Scope = new AuditEventScopeDto
            {
                ScopeType = ScopeType.Tenant,
                TenantId = tenantId.ToString(),
            },
            Actor = new AuditEventActorDto
            {
                Type = actorUserId.HasValue ? ActorType.User : ActorType.System,
                Id = actorUserId?.ToString(),
            },
            Entity = entityType != null
                ? new AuditEventEntityDto { Type = entityType, Id = entityId }
                : null,
            Action = action,
            Description = description,
            Before = before,
            After = after,
            Metadata = metadata,
            IdempotencyKey = IdempotencyKey.For(
                "liens-service", eventType, entityId ?? tenantId.ToString(), now.UtcTicks.ToString()),
            Tags = ["liens"],
        };

        _ = _client.IngestAsync(request).ContinueWith(t =>
        {
            if (t.IsFaulted)
                _logger.LogWarning(t.Exception, "Audit publish failed for {EventType}", eventType);
        }, TaskContinuationOptions.OnlyOnFaulted);
    }
}
