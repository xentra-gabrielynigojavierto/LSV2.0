namespace PlatformAuditEventService.Enums;

/// <summary>
/// Describes the organizational level at which an audit event or export job is scoped.
/// Drives multi-tenancy isolation and determines which ID fields are meaningful.
/// </summary>
public enum ScopeType
{
    /// <summary>Global / cross-platform scope. No tenant or org constraint.</summary>
    Global = 1,

    /// <summary>Scoped to the platform layer (infrastructure, billing, licensing).</summary>
    Platform = 2,

    /// <summary>Scoped to a specific tenant identified by TenantId.</summary>
    Tenant = 3,

    /// <summary>Scoped to an organization within a tenant (TenantId + OrganizationId).</summary>
    Organization = 4,

    /// <summary>Scoped to a single user (TenantId + UserId/ActorId).</summary>
    User = 5,

    /// <summary>Scoped to a specific service or integration, regardless of tenant.</summary>
    Service = 6
}
