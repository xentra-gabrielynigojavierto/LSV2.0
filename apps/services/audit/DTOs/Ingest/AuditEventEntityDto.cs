namespace PlatformAuditEventService.DTOs.Ingest;

/// <summary>
/// The domain resource that was acted upon.
/// Both fields are optional — omit when the event is not targeted at a specific resource.
/// </summary>
public sealed class AuditEventEntityDto
{
    /// <summary>
    /// Resource type. Convention: PascalCase domain model name.
    /// Examples: "User", "Document", "Appointment", "Plan", "Role".
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Identifier of the resource within the source system.
    /// </summary>
    public string? Id { get; set; }
}
