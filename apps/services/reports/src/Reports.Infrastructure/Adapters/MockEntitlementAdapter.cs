using Microsoft.Extensions.Logging;
using Reports.Contracts.Adapters;
using Reports.Contracts.Context;

namespace Reports.Infrastructure.Adapters;

public sealed class MockEntitlementAdapter : IEntitlementAdapter
{
    private readonly ILogger<MockEntitlementAdapter> _log;

    public MockEntitlementAdapter(ILogger<MockEntitlementAdapter> log) => _log = log;

    public Task<AdapterResult<bool>> CanAccessReportsAsync(RequestContext ctx, TenantContext tenant, UserContext user, CancellationToken ct)
    {
        _log.LogDebug("MockEntitlementAdapter: CanAccessReports for {TenantId}/{UserId} [Correlation={CorrelationId}]",
            tenant.TenantId, user.UserId, ctx.CorrelationId);
        return Task.FromResult(AdapterResult<bool>.Ok(true));
    }

    public Task<AdapterResult<bool>> CanExecuteReportAsync(RequestContext ctx, TenantContext tenant, UserContext user, string reportTypeCode, CancellationToken ct)
    {
        _log.LogDebug("MockEntitlementAdapter: CanExecuteReport {ReportType} [Correlation={CorrelationId}]", reportTypeCode, ctx.CorrelationId);
        return Task.FromResult(AdapterResult<bool>.Ok(true));
    }
}
