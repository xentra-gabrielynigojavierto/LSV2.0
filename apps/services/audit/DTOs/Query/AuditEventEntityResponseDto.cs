namespace PlatformAuditEventService.DTOs.Query;

/// <summary>Target entity context within an <see cref="AuditEventRecordResponse"/>.</summary>
public sealed class AuditEventEntityResponseDto
{
    public string? Type { get; init; }
    public string? Id { get; init; }
}
