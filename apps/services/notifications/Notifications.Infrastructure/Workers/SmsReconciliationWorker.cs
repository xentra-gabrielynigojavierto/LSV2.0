using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Workers;

/// <summary>
/// Background worker that periodically reconciles stale/pending outbound SMS attempts
/// by querying the SMS provider (Twilio) for the current delivery status.
///
/// This is a backstop for missed or delayed webhook delivery updates.
/// It never sends or resends SMS — only updates local delivery tracking state.
///
/// Configuration (via environment variables or appsettings):
///   SMS_RECONCILIATION_ENABLED          = false (default — must explicitly enable)
///   SMS_RECONCILIATION_INTERVAL_MINUTES = 15
///   SMS_RECONCILIATION_STALE_AFTER_MINUTES = 30
///   SMS_RECONCILIATION_BATCH_SIZE       = 50
///
/// Webhooks remain the primary real-time status source.
/// Reconciliation is a fallback for missed webhook events.
/// </summary>
public class SmsReconciliationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SmsReconciliationWorker> _logger;
    private readonly bool _enabled;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _staleAfter;
    private readonly int _batchSize;

    public SmsReconciliationWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<SmsReconciliationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;

        _enabled    = configuration.GetValue("SMS_RECONCILIATION_ENABLED", false);
        _interval   = TimeSpan.FromMinutes(configuration.GetValue("SMS_RECONCILIATION_INTERVAL_MINUTES", 15));
        _staleAfter = TimeSpan.FromMinutes(configuration.GetValue("SMS_RECONCILIATION_STALE_AFTER_MINUTES", 30));
        _batchSize  = Math.Min(configuration.GetValue("SMS_RECONCILIATION_BATCH_SIZE", 50), 200);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation(
                "SmsReconciliationWorker: disabled (SMS_RECONCILIATION_ENABLED=false). " +
                "Set to true to enable periodic SMS vendor status reconciliation.");
            return;
        }

        _logger.LogInformation(
            "SmsReconciliationWorker started: interval={Interval}m, staleAfter={StaleAfter}m, batchSize={Batch}",
            _interval.TotalMinutes, _staleAfter.TotalMinutes, _batchSize);

        // Stagger startup to avoid competing with NotificationWorker and StatusSyncWorker.
        try { await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SmsReconciliationWorker: unhandled error in reconciliation cycle");
            }

            try { await Task.Delay(_interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("SmsReconciliationWorker stopped");
    }

    private async Task ReconcileCycleAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ISmsReconciliationService>();

        _logger.LogDebug(
            "SmsReconciliationWorker: starting batch — staleAfter={StaleAfter}m, limit={Limit}",
            _staleAfter.TotalMinutes, _batchSize);

        var batch = await svc.ReconcileStalePendingAsync(_batchSize, _staleAfter, stoppingToken);

        _logger.LogInformation(
            "SmsReconciliationWorker: batch complete — total={Total}, updated={Updated}, noChange={NoChange}, skipped={Skipped}, failed={Failed}, duration={Ms}ms",
            batch.Total, batch.Updated, batch.NoChange, batch.Skipped, batch.Failed,
            (int)batch.Duration.TotalMilliseconds);
    }
}
