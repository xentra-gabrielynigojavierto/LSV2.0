namespace Flow.Domain.Interfaces;

/// <summary>
/// LS-FLOW-MERGE-P3 — Application-level access to the current authenticated
/// user / tenant. Defined in Flow.Domain so application services can be
/// independent of the BuildingBlocks shared library; implemented in Flow.Api
/// on top of <c>BuildingBlocks.Context.ICurrentRequestContext</c>.
///
/// <para>
/// LS-FLOW-E14.2 widened this seam with role / org / platform-admin
/// projections so the new <c>WorkflowTaskAssignmentService</c> can
/// run its queue-eligibility and supervisor-authority checks without
/// taking a direct reference on <c>BuildingBlocks</c> from
/// <c>Flow.Application</c>. All four extensions are read-only
/// projections of values the platform already exposes via
/// <c>ICurrentRequestContext</c>; no new identity surface is
/// introduced.
/// </para>
/// </summary>
public interface IFlowUserContext
{
    /// <summary>Tenant id formatted exactly as <see cref="ITenantProvider.GetTenantId"/> returns it.</summary>
    string? TenantId { get; }

    /// <summary>Authenticated user id (string form), or null when anonymous.</summary>
    string? UserId { get; }

    /// <summary>
    /// LS-FLOW-E14.2 — caller's organization id (string form), or
    /// null when the request has no org context. Used by the
    /// assignment service to validate <c>OrgQueue</c> claim
    /// eligibility.
    /// </summary>
    string? OrgId { get; }

    /// <summary>
    /// LS-FLOW-E14.2 — union of platform roles
    /// (<c>ICurrentRequestContext.Roles</c>) and product roles
    /// (<c>ICurrentRequestContext.ProductRoles</c>). Used by the
    /// assignment service to validate <c>RoleQueue</c> claim
    /// eligibility and the supervisor-only reassign gate. Always
    /// non-null; empty when the caller has no roles.
    /// </summary>
    IReadOnlyCollection<string> Roles { get; }

    /// <summary>
    /// LS-FLOW-E14.2 — convenience projection of
    /// <c>ICurrentRequestContext.IsPlatformAdmin</c>. Platform admins
    /// are explicitly allowed to act as eligible for any queue (for
    /// support / on-call work) and to perform reassign without an
    /// explicit tenant-admin role.
    /// </summary>
    bool IsPlatformAdmin { get; }
}
