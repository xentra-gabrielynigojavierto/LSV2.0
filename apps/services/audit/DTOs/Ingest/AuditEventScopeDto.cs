using PlatformAuditEventService.Enums;

namespace PlatformAuditEventService.DTOs.Ingest;

/// <summary>
/// Tenancy and organizational scope for an inbound audit event.
/// The combination of ScopeType + the relevant ID fields determines the isolation boundary.
/// </summary>
public sealed class AuditEventScopeDto
{
    /// <summary>
    /// Organizational level this event is scoped to.
    /// Determines which ID fields are required:
    ///   Tenant      → TenantId required
    ///   Organization → TenantId + OrganizationId required
    ///   User        → TenantId + UserId required
    ///   Service     → SourceSystem is the scope (no ID fields needed)
    ///   Global/Platform → no ID fields required
    /// </summary>
    public ScopeType ScopeType { get; set; } = ScopeType.Tenant;

    /// <summary>
    /// Platform partition identifier. Null for single-platform deployments.
    /// </summary>
    public string? PlatformId { get; set; }

    /// <summary>
    /// Top-level tenant boundary. Required when ScopeType is Tenant, Organization, or User.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Organization within a tenant. Required when ScopeType is Organization.
    /// </summary>
    public string? OrganizationId { get; set; }

    /// <summary>
    /// User-level scope ID. Required when ScopeType is User.
    /// Typically matches Actor.Id but may differ in impersonation scenarios.
    /// </summary>
    public string? UserId { get; set; }
}
