using Microsoft.Extensions.Options;
using PlatformAuditEventService.Configuration;

namespace PlatformAuditEventService.Jobs;

/// <summary>
/// Periodic <see cref="BackgroundService"/> that drives <see cref="RetentionPolicyJob"/>
/// on a configured daily schedule.
///
/// Behaviour:
///   1. Waits until the next configured run time (Retention:JobCronUtc default = "0 2 * * *" → 02:00 UTC daily).
///      For simplicity, this implementation uses a 24-hour PeriodicTimer. For cron-exact scheduling,
///      replace with a cron library such as Quartz.NET.
///   2. Creates a DI scope, resolves <see cref="RetentionPolicyJob"/>, and executes it.
///   3. Any error is logged but does NOT crash the service or prevent the next run.
///
/// No-overlap guarantee: PeriodicTimer fires only after the previous tick completes.
///
/// Configuration:
///   Retention:JobEnabled         — must be true for this service to do anything (default: false).
///   Retention:RetentionIntervalHours — polling interval in hours (default: 24).
/// </summary>
public sealed class RetentionHostedService : BackgroundService
{
    private readonly IServiceScopeFactory               _scopeFactory;
    private readonly RetentionOptions                   _opts;
    private readonly ILogger<RetentionHostedService>    _logger;

    public RetentionHostedService(
        IServiceScopeFactory            scopeFactory,
        IOptions<RetentionOptions>      opts,
        ILogger<RetentionHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _opts         = opts.Value;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opts.JobEnabled)
        {
            _logger.LogInformation(
                "RetentionHostedService: disabled (Retention:JobEnabled=false). " +
                "Set to true to enable automatic retention enforcement.");
            return;
        }

        var intervalHours = _opts.RetentionIntervalHours > 0 ? _opts.RetentionIntervalHours : 24;
        var interval      = TimeSpan.FromHours(intervalHours);

        _logger.LogInformation(
            "RetentionHostedService: starting. Interval={Interval:g} DryRun={DryRun}",
            interval, _opts.DryRun);

        using var timer = new PeriodicTimer(interval);

        // First run immediately on startup
        await RunOnceAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var job = scope.ServiceProvider.GetRequiredService<RetentionPolicyJob>();
            await job.ExecuteAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Host shutdown — expected, no action.
        }
        catch (Exception ex)
        {
            // Log but continue — a failed run does NOT suppress subsequent scheduled runs.
            _logger.LogError(ex,
                "RetentionHostedService: unhandled error in retention run. Will retry next interval.");
        }
    }
}
