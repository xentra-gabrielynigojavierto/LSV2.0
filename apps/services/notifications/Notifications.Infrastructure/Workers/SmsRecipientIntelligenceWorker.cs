using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;

namespace Notifications.Infrastructure.Workers;

/// <summary>
/// LS-NOTIF-SMS-016: Background worker that periodically calculates SMS recipient reputation snapshots.
///
/// Reads SmsRecipientIntelligence config to determine if enabled and at what interval.
/// Safe-off by default (SmsRecipientIntelligence:Enabled = false).
/// Does not call external providers. Does not send SMS. Does not affect delivery.
/// Failures are logged and swallowed — worker never crashes the host.
/// </summary>
public class SmsRecipientIntelligenceWorker : BackgroundService
{
    private readonly IServiceScopeFactory                      _scopeFactory;
    private readonly ILogger<SmsRecipientIntelligenceWorker>   _logger;
    private readonly SmsRecipientIntelligenceOptions           _opts;

    // Stagger startup to avoid resource contention during boot
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(120);

    public SmsRecipientIntelligenceWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<SmsRecipientIntelligenceOptions> opts,
        ILogger<SmsRecipientIntelligenceWorker> logger)
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
                "SmsRecipientIntelligenceWorker: disabled (SmsRecipientIntelligence:Enabled = false) — not starting");
            return;
        }

        _logger.LogInformation(
            "SmsRecipientIntelligenceWorker: starting — interval={Interval}min, window={Window}days",
            _opts.CalculationIntervalMinutes, _opts.ReputationWindowDays);

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
            var service = scope.ServiceProvider.GetRequiredService<ISmsRecipientIntelligenceService>();

            var windowEnd   = DateTime.UtcNow;
            var windowStart = windowEnd.AddDays(-_opts.ReputationWindowDays);

            _logger.LogDebug(
                "SmsRecipientIntelligenceWorker: calculating snapshots [{Start} → {End}]",
                windowStart, windowEnd);

            await service.CalculateSnapshotsAsync(windowStart, windowEnd, ct);

            _logger.LogDebug("SmsRecipientIntelligenceWorker: cycle complete");
        }
        catch (OperationCanceledException) when (true)
        {
            // Graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SmsRecipientIntelligenceWorker: unhandled exception in cycle — worker will retry next interval");
        }
    }
}
