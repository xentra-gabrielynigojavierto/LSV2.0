using BuildingBlocks.Context;
using Microsoft.Extensions.Logging;
using Reports.Contracts.Adapters;
using Reports.Contracts.Context;

namespace Reports.Infrastructure.Adapters;

public sealed class ClaimsEntitlementAdapter : IEntitlementAdapter
{
    private readonly ICurrentRequestContext _ctx;
    private readonly ILogger<ClaimsEntitlementAdapter> _log;

    public ClaimsEntitlementAdapter(ICurrentRequestContext ctx, ILogger<ClaimsEntitlementAdapter> log)
    {
        _ctx = ctx;
        _log = log;
    }

    public Task<AdapterResult<bool>> CanAccessReportsAsync(RequestContext ctx, TenantContext tenant, UserContext user, CancellationToken ct)
    {
        var canAccess = _ctx.IsAuthenticated && _ctx.TenantId is not null;

        _log.LogDebug("ClaimsEntitlementAdapter: CanAccessReports for {TenantId}/{UserId} = {CanAccess} [Correlation={CorrelationId}]",
            tenant.TenantId, user.UserId, canAccess, ctx.CorrelationId);

        return Task.FromResult(AdapterResult<bool>.Ok(canAccess));
    }

    public Task<AdapterResult<bool>> CanExecuteReportAsync(RequestContext ctx, TenantContext tenant, UserContext user, string reportTypeCode, CancellationToken ct)
    {
        var canExecute = _ctx.IsAuthenticated && _ctx.TenantId is not null;

        _log.LogDebug("ClaimsEntitlementAdapter: CanExecuteReport {ReportType} for {TenantId}/{UserId} = {CanExecute} [Correlation={CorrelationId}]",
            reportTypeCode, tenant.TenantId, user.UserId, canExecute, ctx.CorrelationId);

        return Task.FromResult(AdapterResult<bool>.Ok(canExecute));
    }
}
