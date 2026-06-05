namespace Reports.Contracts.Observability;

public interface IReportsMetrics
{
    void IncrementExecutionCount(string tenantId, string productCode, string status);
    void IncrementExportCount(string tenantId, string format, string status);
    void IncrementScheduleRunCount(string tenantId, string status);
    void IncrementDeliveryCount(string method, string status);
    void RecordExecutionDuration(string tenantId, string productCode, long durationMs);
    void RecordExportDuration(string tenantId, string format, long durationMs);

    ReportsMetricsSnapshot GetSnapshot();
}

public sealed class ReportsMetricsSnapshot
{
    public long TotalExecutions { get; init; }
    public long TotalExports { get; init; }
    public long TotalScheduleRuns { get; init; }
    public long TotalDeliveries { get; init; }
    public long TotalFailures { get; init; }
    public IDictionary<string, long> ExecutionsByProduct { get; init; } = new Dictionary<string, long>();
    public IDictionary<string, long> ExportsByFormat { get; init; } = new Dictionary<string, long>();
    public IDictionary<string, long> DeliveriesByMethod { get; init; } = new Dictionary<string, long>();
}
