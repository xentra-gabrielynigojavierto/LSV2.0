using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monitoring.Application.Scheduling;

namespace Monitoring.Infrastructure.Scheduling;

/// <summary>
/// Background scheduler foundation. Ticks on a configurable interval and
/// invokes the registered <see cref="IMonitoringCycleExecutor"/> for each
/// cycle. Lives in Infrastructure because it is a hosting concern; the
/// executor abstraction lives in Application so future cycle work can be
/// introduced without inverting the dependency.
///
/// <para>This class deliberately knows nothing about monitored entities,
/// checks, statuses, or alerts — it is purely the engine loop.</para>
///
/// <para>Failure isolation: each cycle is wrapped in a try/catch so a
/// single faulty cycle never takes the host down or stops subsequent
/// cycles. The wait between cycles is cancellable so host shutdown
/// unwinds promptly.</para>
/// </summary>
public sealed class MonitoringSchedulerHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<SchedulerOptions> _options;
    private readonly ILogger<MonitoringSchedulerHostedService> _logger;

    public MonitoringSchedulerHostedService(
        IServiceProvider serviceProvider,
        IOptions<SchedulerOptions> options,
        ILogger<MonitoringSchedulerHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = _options.Value;

        if (!opts.Enabled)
        {
            _logger.LogInformation(
                "Monitoring scheduler is disabled (Monitoring:Scheduler:Enabled=false). " +
                "No cycles will run.");
            return;
        }

        var interval = TimeSpan.FromSeconds(opts.IntervalSeconds);
        _logger.LogInformation(
            "Monitoring scheduler started. IntervalSeconds={IntervalSeconds}.",
            opts.IntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCycleAsync(stoppingToken).ConfigureAwait(false);

            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Shutdown requested during the inter-cycle wait — exit cleanly.
                break;
            }
        }

        _logger.LogInformation("Monitoring scheduler is stopping.");
    }

    private async Task RunCycleAsync(CancellationToken stoppingToken)
    {
        var cycleId = Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Monitoring cycle {CycleId} started.", cycleId);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var executor = scope.ServiceProvider.GetRequiredService<IMonitoringCycleExecutor>();
            await executor.ExecuteCycleAsync(stoppingToken).ConfigureAwait(false);

            stopwatch.Stop();
            _logger.LogInformation(
                "Monitoring cycle {CycleId} completed in {ElapsedMs} ms.",
                cycleId, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host is shutting down mid-cycle. Not a failure.
            stopwatch.Stop();
            _logger.LogInformation(
                "Monitoring cycle {CycleId} cancelled after {ElapsedMs} ms (host shutdown).",
                cycleId, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            // Failure isolation: log the failure and let the loop continue.
            // The next cycle will run on the configured interval.
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Monitoring cycle {CycleId} failed after {ElapsedMs} ms. " +
                "The scheduler will continue with the next cycle.",
                cycleId, stopwatch.ElapsedMilliseconds);
        }
    }
}
