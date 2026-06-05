using Microsoft.Extensions.Logging;
using Reports.Contracts.Adapters;
using Reports.Contracts.Context;

namespace Reports.Infrastructure.Adapters;

public sealed class MockTenantAdapter : ITenantAdapter
{
    private readonly ILogger<MockTenantAdapter> _log;

    public MockTenantAdapter(ILogger<MockTenantAdapter> log) => _log = log;

    public Task<AdapterResult<TenantContext>> ResolveTenantAsync(RequestContext ctx, string tenantCode, CancellationToken ct)
    {
        _log.LogDebug("MockTenantAdapter: ResolveTenant for {Code} [Correlation={CorrelationId}]", tenantCode, ctx.CorrelationId);
        var tenant = new TenantContext
        {
            TenantId = "mock-tenant-id",
            TenantName = "Mock Tenant",
            OrganizationType = "LienCompany",
            IsActive = true,
        };
        return Task.FromResult(AdapterResult<TenantContext>.Ok(tenant));
    }

    public Task<AdapterResult<bool>> IsTenantActiveAsync(RequestContext ctx, string tenantId, CancellationToken ct)
    {
        _log.LogDebug("MockTenantAdapter: IsTenantActive for {TenantId} [Correlation={CorrelationId}]", tenantId, ctx.CorrelationId);
        return Task.FromResult(AdapterResult<bool>.Ok(true));
    }
}
