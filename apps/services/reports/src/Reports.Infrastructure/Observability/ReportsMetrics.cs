using System.Collections.Concurrent;
using Reports.Contracts.Observability;

namespace Reports.Infrastructure.Observability;

public sealed class ReportsMetrics : IReportsMetrics
{
    private long _totalExecutions;
    private long _totalExports;
    private long _totalScheduleRuns;
    private long _totalDeliveries;
    private long _totalFailures;

    private readonly ConcurrentDictionary<string, long> _executionsByProduct = new();
    private readonly ConcurrentDictionary<string, long> _exportsByFormat = new();
    private readonly ConcurrentDictionary<string, long> _deliveriesByMethod = new();

    public void IncrementExecutionCount(string tenantId, string productCode, string status)
    {
        Interlocked.Increment(ref _totalExecutions);
        _executionsByProduct.AddOrUpdate(productCode, 1, (_, v) => v + 1);
        if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
            Interlocked.Increment(ref _totalFailures);
    }

    public void IncrementExportCount(string tenantId, string format, string status)
    {
        Interlocked.Increment(ref _totalExports);
        _exportsByFormat.AddOrUpdate(format, 1, (_, v) => v + 1);
        if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
            Interlocked.Increment(ref _totalFailures);
    }

    public void IncrementScheduleRunCount(string tenantId, string status)
    {
        Interlocked.Increment(ref _totalScheduleRuns);
        if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
            Interlocked.Increment(ref _totalFailures);
    }

    public void IncrementDeliveryCount(string method, string status)
    {
        Interlocked.Increment(ref _totalDeliveries);
        _deliveriesByMethod.AddOrUpdate(method, 1, (_, v) => v + 1);
        if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
            Interlocked.Increment(ref _totalFailures);
    }

    public void RecordExecutionDuration(string tenantId, string productCode, long durationMs)
    {
    }

    public void RecordExportDuration(string tenantId, string format, long durationMs)
    {
    }

    public ReportsMetricsSnapshot GetSnapshot()
    {
        return new ReportsMetricsSnapshot
        {
            TotalExecutions = Interlocked.Read(ref _totalExecutions),
            TotalExports = Interlocked.Read(ref _totalExports),
            TotalScheduleRuns = Interlocked.Read(ref _totalScheduleRuns),
            TotalDeliveries = Interlocked.Read(ref _totalDeliveries),
            TotalFailures = Interlocked.Read(ref _totalFailures),
            ExecutionsByProduct = new Dictionary<string, long>(_executionsByProduct),
            ExportsByFormat = new Dictionary<string, long>(_exportsByFormat),
            DeliveriesByMethod = new Dictionary<string, long>(_deliveriesByMethod),
        };
    }
}
