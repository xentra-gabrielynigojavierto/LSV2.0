using PlatformAuditEventService.Enums;

namespace PlatformAuditEventService.DTOs.Query;

/// <summary>Tenancy scope context within an <see cref="AuditEventRecordResponse"/>.</summary>
public sealed class AuditEventScopeResponseDto
{
    public ScopeType ScopeType { get; init; }
    public string? PlatformId { get; init; }
    public string? TenantId { get; init; }
    public string? OrganizationId { get; init; }

    /// <summary>
    /// UserScopeId from the record. May differ from Actor.Id in impersonation scenarios.
    /// </summary>
    public string? UserScopeId { get; init; }
}
