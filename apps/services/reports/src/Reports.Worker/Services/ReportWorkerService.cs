using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Reports.Contracts.Queue;

namespace Reports.Worker.Services;

public sealed class ReportWorkerService : BackgroundService
{
    private readonly IJobQueue _queue;
    private readonly IJobProcessor _processor;
    private readonly ILogger<ReportWorkerService> _log;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(10);

    public ReportWorkerService(IJobQueue queue, IJobProcessor processor, ILogger<ReportWorkerService> log)
    {
        _queue     = queue;
        _processor = processor;
        _log       = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("ReportWorkerService started — polling every {Interval}s", _pollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await _queue.DequeueAsync(stoppingToken);
                if (job is not null)
                {
                    _log.LogInformation("Dequeued job {JobId}", job.JobId);
                    await _processor.ProcessAsync(job, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error processing report job");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _log.LogInformation("ReportWorkerService stopping");
    }
}
