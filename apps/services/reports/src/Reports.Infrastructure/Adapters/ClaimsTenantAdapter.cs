using BuildingBlocks.Context;
using Microsoft.Extensions.Logging;
using Reports.Contracts.Adapters;
using Reports.Contracts.Context;

namespace Reports.Infrastructure.Adapters;

public sealed class ClaimsTenantAdapter : ITenantAdapter
{
    private readonly ICurrentRequestContext _ctx;
    private readonly ILogger<ClaimsTenantAdapter> _log;

    public ClaimsTenantAdapter(ICurrentRequestContext ctx, ILogger<ClaimsTenantAdapter> log)
    {
        _ctx = ctx;
        _log = log;
    }

    public Task<AdapterResult<TenantContext>> ResolveTenantAsync(RequestContext ctx, string tenantCode, CancellationToken ct)
    {
        if (_ctx.TenantId is null)
        {
            _log.LogWarning("ClaimsTenantAdapter: No tenant_id in JWT claims [Correlation={CorrelationId}]", ctx.CorrelationId);
            return Task.FromResult(AdapterResult<TenantContext>.Fail("NO_TENANT", "No tenant_id claim present in JWT."));
        }

        var tenant = new TenantContext
        {
            TenantId = _ctx.TenantId.Value.ToString(),
            TenantName = _ctx.TenantCode,
            OrganizationType = _ctx.OrgType ?? string.Empty,
            IsActive = true,
        };

        _log.LogDebug("ClaimsTenantAdapter: Resolved tenant {TenantId} org={OrgType} [Correlation={CorrelationId}]",
            tenant.TenantId, tenant.OrganizationType, ctx.CorrelationId);

        return Task.FromResult(AdapterResult<TenantContext>.Ok(tenant));
    }

    public Task<AdapterResult<bool>> IsTenantActiveAsync(RequestContext ctx, string tenantId, CancellationToken ct)
    {
        var isCurrentTenant = _ctx.TenantId?.ToString() == tenantId;
        _log.LogDebug("ClaimsTenantAdapter: IsTenantActive for {TenantId} match={IsMatch} [Correlation={CorrelationId}]",
            tenantId, isCurrentTenant, ctx.CorrelationId);
        return Task.FromResult(AdapterResult<bool>.Ok(isCurrentTenant));
    }
}
