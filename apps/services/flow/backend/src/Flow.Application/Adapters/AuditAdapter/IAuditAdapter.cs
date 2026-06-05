namespace Flow.Application.Adapters.AuditAdapter;

/// <summary>
/// Platform audit seam for Flow. Implementations are expected to be
/// fire-and-forget safe — failures must not impact the originating
/// workflow/task operation.
/// </summary>
public interface IAuditAdapter
{
    Task WriteEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}

/// <summary>
/// Minimal audit envelope — intentionally narrow until the platform
/// AuditClient contract is wired in Phase 3.
/// </summary>
public sealed record AuditEvent(
    string Action,
    string EntityType,
    string EntityId,
    string? TenantId,
    string? UserId,
    string? Description,
    IReadOnlyDictionary<string, string?>? Metadata = null,
    DateTime? OccurredAtUtc = null);
