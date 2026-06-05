using PlatformAuditEventService.Enums;

namespace PlatformAuditEventService.DTOs.Ingest;

/// <summary>
/// Identity of the principal that performed the audited action.
/// </summary>
public sealed class AuditEventActorDto
{
    /// <summary>
    /// Stable identifier of the actor in the source system.
    /// Null for anonymous or unidentifiable actors.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Kind of principal. Defaults to User for human-initiated actions.
    /// </summary>
    public ActorType Type { get; set; } = ActorType.User;

    /// <summary>
    /// Display name or label of the actor at the time of the event.
    /// Snapshot — persisted as-is and not updated if the actor's name changes later.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Client IP address. Supports IPv4 (15 chars) and IPv6 (max 45 chars).
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// HTTP User-Agent string from the originating request, if available.
    /// </summary>
    public string? UserAgent { get; set; }
}
