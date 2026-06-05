using Microsoft.Extensions.Logging;
using Reports.Contracts.Queue;

namespace Reports.Infrastructure.Queue;

public sealed class MockJobProcessor : IJobProcessor
{
    private readonly ILogger<MockJobProcessor> _log;

    public MockJobProcessor(ILogger<MockJobProcessor> log) => _log = log;

    public Task ProcessAsync(ReportJob job, CancellationToken ct)
    {
        _log.LogInformation("MockJobProcessor: Processing job {JobId} — report type {ReportType}",
            job.JobId, job.ReportTypeCode);
        return Task.CompletedTask;
    }
}
