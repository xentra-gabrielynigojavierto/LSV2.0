namespace Reports.Contracts.Queue;

public interface IJobProcessor
{
    Task ProcessAsync(ReportJob job, CancellationToken ct = default);
}
