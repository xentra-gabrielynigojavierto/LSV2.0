using LegalSynq.AuditClient.Enums;

namespace LegalSynq.AuditClient.DTOs;

public sealed class AuditEventScopeDto
{
    public ScopeType ScopeType     { get; set; } = ScopeType.Tenant;
    public string?   PlatformId    { get; set; }
    public string?   TenantId      { get; set; }
    public string?   OrganizationId { get; set; }
    public string?   UserId        { get; set; }
}
