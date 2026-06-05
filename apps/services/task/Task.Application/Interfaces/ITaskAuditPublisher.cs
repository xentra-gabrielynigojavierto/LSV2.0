namespace Task.Application.Interfaces;

/// <summary>
/// Fire-and-forget audit publisher for the Task service.
/// Matches the pattern from Identity.Infrastructure.Services.AuditPublisher.
/// Failures are logged as warnings and never propagate to callers.
/// </summary>
public interface ITaskAuditPublisher
{
    void Publish(
        string  eventType,
        string  action,
        string  description,
        Guid    tenantId,
        Guid?   actorUserId  = null,
        string? entityType   = null,
        string? entityId     = null,
        string? metadata     = null);
}
