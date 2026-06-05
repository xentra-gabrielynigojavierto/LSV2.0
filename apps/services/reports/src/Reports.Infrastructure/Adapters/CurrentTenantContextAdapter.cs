using BuildingBlocks.Context;
using Reports.Contracts.Context;

namespace Reports.Infrastructure.Adapters;

/// <summary>
/// Implements ICurrentTenantContext by reading JWT claims from the scoped ICurrentRequestContext.
/// This is the canonical source of tenant and user identity — never trust client-supplied values.
/// </summary>
public sealed class CurrentTenantContextAdapter : ICurrentTenantContext
{
    private readonly ICurrentRequestContext _ctx;

    public CurrentTenantContextAdapter(ICurrentRequestContext ctx) => _ctx = ctx;

    public string? TenantId => _ctx.TenantId?.ToString();
    public string? UserId => _ctx.UserId?.ToString();
}
