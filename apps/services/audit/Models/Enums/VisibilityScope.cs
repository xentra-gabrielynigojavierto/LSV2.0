namespace PlatformAuditEventService.Enums;

/// <summary>
/// Defines which principals may retrieve an audit event through the query API.
/// Enforced by the QueryAuth middleware against the caller's identity claims.
/// </summary>
public enum VisibilityScope
{
    /// <summary>
    /// Visible only to platform super-admins. Tenant and user roles cannot see these records.
    /// Use for cross-tenant infrastructure events, billing, and platform security events.
    /// </summary>
    Platform = 1,

    /// <summary>
    /// Visible to tenant admins and compliance officers scoped to the matching tenantId.
    /// Platform admins also have access. Use for most multi-tenant operational events.
    /// </summary>
    Tenant = 2,

    /// <summary>
    /// Visible to organization-level roles within the same tenantId + organizationId scope.
    /// Suitable for department/org-level operational events.
    /// </summary>
    Organization = 3,

    /// <summary>
    /// Visible to the individual user identified by actorId, in addition to admins.
    /// Suitable for self-service audit trails (e.g. "your login history").
    /// </summary>
    User = 4,

    /// <summary>
    /// Not exposed through the query API regardless of caller role.
    /// Internal-only: integrity checks, system probes, debug traces.
    /// </summary>
    Internal = 5
}
