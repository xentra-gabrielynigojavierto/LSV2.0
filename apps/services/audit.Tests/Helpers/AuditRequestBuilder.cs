using PlatformAuditEventService.DTOs.Ingest;
using PlatformAuditEventService.Enums;

namespace PlatformAuditEventService.Tests.Helpers;

/// <summary>
/// Builds structurally valid <see cref="IngestAuditEventRequest"/> objects for integration tests.
///
/// All factory methods produce requests that pass every rule in
/// <c>IngestAuditEventRequestValidator</c> by default. Named parameters allow
/// individual fields to be overridden to exercise validation or routing logic.
/// </summary>
public static class AuditRequestBuilder
{
    /// <summary>
    /// Returns a minimal request that satisfies all required validator rules.
    ///
    /// Required fields populated:
    ///   EventType, SourceSystem, SourceService, Visibility, Severity,
    ///   OccurredAtUtc, Scope.ScopeType, Scope.TenantId, Actor.Type
    /// </summary>
    public static IngestAuditEventRequest MinimalValid(
        string? eventType      = null,
        string? tenantId       = null,
        string? idempotencyKey = null,
        string? sourceSystem   = null,
        string? actorId        = null) =>
        new()
        {
            EventType      = eventType    ?? "user.login.succeeded",
            EventCategory  = EventCategory.Security,
            SourceSystem   = sourceSystem ?? "identity-service",
            SourceService  = "auth-module",
            Visibility     = VisibilityScope.Tenant,
            Severity       = SeverityLevel.Info,
            OccurredAtUtc  = DateTimeOffset.UtcNow.AddSeconds(-1),
            IdempotencyKey = idempotencyKey,
            Scope = new AuditEventScopeDto
            {
                ScopeType = ScopeType.Tenant,
                TenantId  = tenantId ?? "tenant-001",
            },
            Actor = new AuditEventActorDto
            {
                Type = ActorType.User,
                Id   = actorId ?? "user-abc",
                Name = "Test User",
            },
        };

    /// <summary>
    /// Returns a <see cref="BatchIngestRequest"/> with <paramref name="count"/> valid events.
    /// Each event gets a unique IdempotencyKey and an indexed EventType.
    /// </summary>
    public static BatchIngestRequest ValidBatch(int count = 3, string? tenantId = null) =>
        new()
        {
            Events = Enumerable.Range(0, count)
                .Select(i => MinimalValid(
                    eventType:      $"document.uploaded.v{i}",
                    tenantId:       tenantId,
                    idempotencyKey: $"batch-key-{i}-{Guid.NewGuid():N}"))
                .ToList(),
        };
}
