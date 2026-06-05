using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Workers;

/// <summary>
/// LS-NOTIF-SMS-011: Background worker that periodically retries failed/pending
/// SMS alert escalations whose NextRetryAt timestamp has elapsed.
///
/// This worker never sends SMS, calls SMS providers, or modifies alerts.
/// It only processes ntf_SmsAlertEscalations retry records.
///
/// Configuration (via environment variables or appsettings):
///   SMS_ALERT_ESCALATION_RETRY_ENABLED          = false (default — must explicitly enable)
///   SMS_ALERT_ESCALATION_RETRY_INTERVAL_MINUTES = 5
///   SMS_ALERT_ESCALATION_RETRY_BATCH_SIZE       = 50
/// </summary>
public class SmsAlertEscalationRetryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SmsAlertEscalationRetryWorker> _logger;
    private readonly bool     _enabled;
    private readonly TimeSpan _interval;
    private readonly int      _batchSize;

    public SmsAlertEscalationRetryWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<SmsAlertEscalationRetryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;

        _enabled   = configuration.GetValue("SMS_ALERT_ESCALATION_RETRY_ENABLED", false);
        _interval  = TimeSpan.FromMinutes(
            configuration.GetValue("SMS_ALERT_ESCALATION_RETRY_INTERVAL_MINUTES", 5));
        _batchSize = Math.Min(
            configuration.GetValue("SMS_ALERT_ESCALATION_RETRY_BATCH_SIZE", 50), 200);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation(
                "SmsAlertEscalationRetryWorker: disabled " +
                "(SMS_ALERT_ESCALATION_RETRY_ENABLED=false). " +
                "Set to true to enable automatic escalation retry processing.");
            return;
        }

        _logger.LogInformation(
            "SmsAlertEscalationRetryWorker started: interval={Interval}m, batchSize={Batch}",
            _interval.TotalMinutes, _batchSize);

        // Stagger startup to avoid competing with other background workers at boot.
        try { await Task.Delay(TimeSpan.FromSeconds(90), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "SmsAlertEscalationRetryWorker: unhandled error in retry cycle");
            }

            try { await Task.Delay(_interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("SmsAlertEscalationRetryWorker stopped");
    }

    private async Task ProcessCycleAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var escalationService =
            scope.ServiceProvider.GetRequiredService<ISmsAlertEscalationService>();

        var processed = await escalationService.ProcessPendingRetriesAsync(_batchSize, stoppingToken);

        if (processed > 0)
            _logger.LogInformation(
                "SmsAlertEscalationRetryWorker: cycle complete — retried {Count} escalations",
                processed);
        else
            _logger.LogDebug("SmsAlertEscalationRetryWorker: no pending retries due");
    }
}
