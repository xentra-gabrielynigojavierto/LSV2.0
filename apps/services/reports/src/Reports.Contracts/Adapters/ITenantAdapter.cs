using Reports.Contracts.Context;

namespace Reports.Contracts.Adapters;

public interface ITenantAdapter
{
    Task<AdapterResult<TenantContext>> ResolveTenantAsync(RequestContext ctx, string tenantCode, CancellationToken ct = default);
    Task<AdapterResult<bool>> IsTenantActiveAsync(RequestContext ctx, string tenantId, CancellationToken ct = default);
}
