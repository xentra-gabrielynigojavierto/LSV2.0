using Microsoft.Extensions.Logging;
using Reports.Contracts.Adapters;

namespace Reports.Infrastructure.Adapters;

public sealed class CompositeReportDataQueryAdapter : IReportDataQueryAdapter
{
    private readonly IEnumerable<IReportDataQueryAdapter> _adapters;
    private readonly ILogger<CompositeReportDataQueryAdapter> _log;

    public CompositeReportDataQueryAdapter(
        IEnumerable<IReportDataQueryAdapter> adapters,
        ILogger<CompositeReportDataQueryAdapter> log)
    {
        _adapters = adapters;
        _log = log;
    }

    public bool SupportsProduct(string productCode) =>
        _adapters.Any(a => a.SupportsProduct(productCode));

    public async Task<AdapterResult<TabularResultSet>> ExecuteQueryAsync(ReportQueryContext context, CancellationToken ct)
    {
        var adapter = _adapters.FirstOrDefault(a => a.SupportsProduct(context.ProductCode));
        if (adapter is null)
        {
            _log.LogWarning("CompositeReportDataQueryAdapter: no adapter found for product={ProductCode}", context.ProductCode);
            return AdapterResult<TabularResultSet>.Fail("UnsupportedProduct", $"No data query adapter registered for product '{context.ProductCode}'.");
        }

        _log.LogDebug("CompositeReportDataQueryAdapter: routing product={ProductCode} to {AdapterType}",
            context.ProductCode, adapter.GetType().Name);

        return await adapter.ExecuteQueryAsync(context, ct);
    }
}
