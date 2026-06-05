using BuildingBlocks.Context;
using Flow.Domain.Interfaces;

namespace Flow.Api.Services;

/// <summary>
/// LS-FLOW-MERGE-P3 — adapts the platform <see cref="ICurrentRequestContext"/>
/// into the Flow.Domain-defined <see cref="IFlowUserContext"/>.
///
/// <para>
/// LS-FLOW-E14.2 added the role / org / platform-admin projections so
/// the application layer can do queue-eligibility and admin-gate
/// checks without referencing <c>BuildingBlocks</c> directly.
/// </para>
/// </summary>
public sealed class FlowUserContext : IFlowUserContext
{
    private static readonly IReadOnlyCollection<string> EmptyRoles = Array.Empty<string>();

    private readonly ICurrentRequestContext _ctx;

    public FlowUserContext(ICurrentRequestContext ctx)
    {
        _ctx = ctx;
    }

    public string? TenantId => _ctx.TenantId?.ToString("D").ToLowerInvariant();
    public string? UserId => _ctx.UserId?.ToString("D");

    public string? OrgId => _ctx.OrgId?.ToString("D");

    public IReadOnlyCollection<string> Roles
    {
        get
        {
            // Union of platform roles + product roles. The assignment
            // service treats both buckets equally for queue-eligibility
            // purposes (a CARECONNECT_RECEIVER product-role and a
            // platform "Reviewer" role both confer the right to claim
            // a same-named RoleQueue task). Materialised once per call
            // because the service may iterate twice (role + admin
            // checks).
            var platform = _ctx.Roles ?? EmptyRoles;
            var product = _ctx.ProductRoles ?? EmptyRoles;

            if (platform.Count == 0 && product.Count == 0) return EmptyRoles;
            if (product.Count == 0) return platform;
            if (platform.Count == 0) return product;

            var combined = new List<string>(platform.Count + product.Count);
            combined.AddRange(platform);
            combined.AddRange(product);
            return combined;
        }
    }

    public bool IsPlatformAdmin => _ctx.IsPlatformAdmin;
}
