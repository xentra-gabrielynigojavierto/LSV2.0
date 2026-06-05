using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;

namespace Notifications.Infrastructure.Workers;

/// <summary>
/// LS-NOTIF-SMS-015: Background worker that periodically calculates SMS provider quality snapshots.
/// Reads SmsProviderQuality config to determine if enabled and at what interval.
///
/// Safe-off by default (SmsProviderQuality:Enabled = false).
/// Does not call external providers. Does not send SMS. Does not affect delivery.
/// Failures are logged and swallowed — worker never crashes the host.
/// </summary>
public class SmsProviderQualityWorker : BackgroundService
{
    private readonly IServiceScopeFactory             _scopeFactory;
    private readonly ILogger<SmsProviderQualityWorker> _logger;
    private readonly SmsProviderQualityOptions         _opts;

    // Stagger startup to avoid resource contention during boot
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(90);

    public SmsProviderQualityWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<SmsProviderQualityOptions> opts,
        ILogger<SmsProviderQualityWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _opts         = opts.Value;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opts.Enabled)
        {
            _logger.LogInformation(
                "SmsProviderQualityWorker: disabled (SmsProviderQuality:Enabled = false) — not starting");
            return;
        }

        _logger.LogInformation(
            "SmsProviderQualityWorker: starting — interval={Interval}min, window={Window}min",
            _opts.CalculationIntervalMinutes, _opts.SnapshotWindowMinutes);

        await Task.Delay(StartupDelay, stoppingToken);

        var interval = TimeSpan.FromMinutes(Math.Max(1, _opts.CalculationIntervalMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCycleAsync(stoppingToken);
            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        try
        {
            using var scope   = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ISmsProviderQualityService>();

            var windowEnd   = DateTime.UtcNow;
            var windowStart = windowEnd.AddMinutes(-_opts.SnapshotWindowMinutes);

            _logger.LogDebug(
                "SmsProviderQualityWorker: calculating snapshots [{Start} → {End}]",
                windowStart, windowEnd);

            await service.CalculateSnapshotsAsync(windowStart, windowEnd, ct);

            _logger.LogDebug("SmsProviderQualityWorker: cycle complete");
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SmsProviderQualityWorker: unhandled error in calculation cycle — will retry at next interval");
        }
    }
}
