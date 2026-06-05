using Microsoft.Extensions.Logging;
using Reports.Contracts.Adapters;
using Reports.Contracts.Context;

namespace Reports.Infrastructure.Adapters;

public sealed class MockProductDataAdapter : IProductDataAdapter
{
    private readonly ILogger<MockProductDataAdapter> _log;

    public MockProductDataAdapter(ILogger<MockProductDataAdapter> log) => _log = log;

    public Task<AdapterResult<IReadOnlyList<ProductContext>>> GetAvailableProductsAsync(RequestContext ctx, TenantContext tenant, CancellationToken ct)
    {
        _log.LogDebug("MockProductDataAdapter: GetAvailableProducts for {TenantId} [Correlation={CorrelationId}]",
            tenant.TenantId, ctx.CorrelationId);
        IReadOnlyList<ProductContext> products = new[]
        {
            new ProductContext { ProductCode = "liens", ProductName = "Liens Management" },
            new ProductContext { ProductCode = "careconnect", ProductName = "CareConnect" },
            new ProductContext { ProductCode = "fund", ProductName = "Fund" },
        };
        return Task.FromResult(AdapterResult<IReadOnlyList<ProductContext>>.Ok(products));
    }

    public Task<AdapterResult<ProductDataResult>> QueryProductDataAsync(RequestContext ctx, TenantContext tenant, ProductContext product, ProductDataQuery query, CancellationToken ct)
    {
        _log.LogDebug("MockProductDataAdapter: QueryProductData {Product}/{QueryKey} [Correlation={CorrelationId}]",
            product.ProductCode, query.QueryKey, ctx.CorrelationId);
        var result = new ProductDataResult
        {
            ProductCode = product.ProductCode,
            QueryKey = query.QueryKey,
            Data = null,
        };
        return Task.FromResult(AdapterResult<ProductDataResult>.Ok(result));
    }
}
