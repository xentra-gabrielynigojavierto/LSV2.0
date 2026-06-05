using PlatformAuditEventService.Enums;

namespace PlatformAuditEventService.DTOs.Query;

/// <summary>Actor context within an <see cref="AuditEventRecordResponse"/>.</summary>
public sealed class AuditEventActorResponseDto
{
    public string? Id { get; init; }
    public ActorType Type { get; init; }
    public string? Name { get; init; }

    /// <summary>
    /// Only present when the caller has sufficient role to see network identifiers.
    /// Redacted to null by the query layer for User-scoped callers.
    /// </summary>
    public string? IpAddress { get; init; }

    /// <summary>
    /// Only present when caller has sufficient role.
    /// </summary>
    public string? UserAgent { get; init; }
}
