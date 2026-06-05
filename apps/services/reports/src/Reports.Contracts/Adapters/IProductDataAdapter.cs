using Reports.Contracts.Context;

namespace Reports.Contracts.Adapters;

public sealed class ProductDataQuery
{
    public string QueryKey { get; init; } = string.Empty;
    public IDictionary<string, string>? Parameters { get; init; }
}

public sealed class ProductDataResult
{
    public string ProductCode { get; init; } = string.Empty;
    public string QueryKey { get; init; } = string.Empty;
    public object? Data { get; init; }
}

public interface IProductDataAdapter
{
    Task<AdapterResult<IReadOnlyList<ProductContext>>> GetAvailableProductsAsync(RequestContext ctx, TenantContext tenant, CancellationToken ct = default);
    Task<AdapterResult<ProductDataResult>> QueryProductDataAsync(RequestContext ctx, TenantContext tenant, ProductContext product, ProductDataQuery query, CancellationToken ct = default);
}
