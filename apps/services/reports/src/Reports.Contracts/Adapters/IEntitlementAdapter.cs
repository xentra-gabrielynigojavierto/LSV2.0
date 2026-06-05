using Reports.Contracts.Context;

namespace Reports.Contracts.Adapters;

public interface IEntitlementAdapter
{
    Task<AdapterResult<bool>> CanAccessReportsAsync(RequestContext ctx, TenantContext tenant, UserContext user, CancellationToken ct = default);
    Task<AdapterResult<bool>> CanExecuteReportAsync(RequestContext ctx, TenantContext tenant, UserContext user, string reportTypeCode, CancellationToken ct = default);
}
