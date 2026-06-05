using Microsoft.Extensions.Options;
using PlatformAuditEventService.Configuration;

namespace PlatformAuditEventService.Jobs;

/// <summary>
/// Periodic <see cref="BackgroundService"/> that drives <see cref="IntegrityCheckpointJob"/>
/// on a configured interval.
///
/// Behaviour:
///   1. On each tick, acquires a scoped DI container and creates <see cref="IntegrityCheckpointJob"/>.
///   2. Computes the next checkpoint window as:
///      [lastCheckpointEnd, now)  — fetched from the IntegrityCheckpointRepository if available.
///      Falls back to (now - interval, now) for the very first run.
///   3. Executes the job. Logs errors but does NOT suppress them — a missed checkpoint is
///      observable as a gap in the checkpoint timeline. Operators must review gaps manually.
///   4. Waits for the next period via <see cref="PeriodicTimer"/> (no drift accumulation).
///
/// Configuration: Integrity:CheckpointIntervalMinutes (default: 60)
/// Can be disabled entirely by setting Integrity:AutoCheckpointEnabled=false.
///
/// No-overlap guarantee: PeriodicTimer fires only once the previous tick's awaitable completes,
/// so concurrent executions cannot occur within a single process.
/// </summary>
public sealed class IntegrityCheckpointHostedService : BackgroundService
{
    private readonly IServiceScopeFactory                        _scopeFactory;
    private readonly IntegrityOptions                            _opts;
    private readonly ILogger<IntegrityCheckpointHostedService>   _logger;

    public IntegrityCheckpointHostedService(
        IServiceScopeFactory                       scopeFactory,
        IOptions<IntegrityOptions>                 opts,
        ILogger<IntegrityCheckpointHostedService>  logger)
    {
        _scopeFactory = scopeFactory;
        _opts         = opts.Value;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opts.AutoCheckpointEnabled)
        {
            _logger.LogInformation(
                "IntegrityCheckpointHostedService: disabled (Integrity:AutoCheckpointEnabled=false). " +
                "Set to true to enable automatic checkpoint generation.");
            return;
        }

        var interval = TimeSpan.FromMinutes(
            _opts.CheckpointIntervalMinutes > 0 ? _opts.CheckpointIntervalMinutes : 60);

        _logger.LogInformation(
            "IntegrityCheckpointHostedService: starting. Interval={Interval:g}", interval);

        using var timer = new PeriodicTimer(interval);

        // Run once immediately at startup, then follow the interval
        await RunOnceAsync(interval, stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(interval, stoppingToken);
        }
    }

    private async Task RunOnceAsync(TimeSpan interval, CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var job = scope.ServiceProvider.GetRequiredService<IntegrityCheckpointJob>();

            var toUtc   = DateTimeOffset.UtcNow;
            var fromUtc = toUtc - interval;

            await job.ExecuteAsync("hourly", fromUtc, toUtc, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Host shutdown — expected, no action.
        }
        catch (Exception ex)
        {
            // Log but continue — a failed tick does NOT suppress subsequent ticks.
            // The gap in checkpoint timeline is the observable signal for operators.
            _logger.LogError(ex,
                "IntegrityCheckpointHostedService: unhandled error in checkpoint run. " +
                "The window gap will be visible in checkpoint continuity verification.");
        }
    }
}
