using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Workers;

/// <summary>
/// LS-NOTIF-SMS-010: Background worker that periodically evaluates SMS operational
/// alert threshold rules and creates/updates persisted alerts.
///
/// This worker never sends SMS, triggers retries, or calls external providers.
/// It only reads ntf_NotificationAttempts and writes ntf_SmsOperationalAlerts.
///
/// Configuration (via environment variables or appsettings):
///   SMS_ALERTS_ENABLED                   = false  (default — must explicitly enable)
///   SMS_ALERTS_EVALUATION_INTERVAL_MINUTES = 15
///   SMS_ALERTS_WINDOW_MINUTES             = 60     (how far back each evaluation looks)
///
/// All threshold rules have independent configuration — see SmsOperationalAlertEvaluator
/// for the full list of SMS_ALERT_* environment variables.
/// </summary>
public class SmsOperationalAlertWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SmsOperationalAlertWorker> _logger;
    private readonly bool _enabled;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _windowSize;

    public SmsOperationalAlertWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<SmsOperationalAlertWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;

        _enabled    = configuration.GetValue("SMS_ALERTS_ENABLED", false);
        _interval   = TimeSpan.FromMinutes(
            configuration.GetValue("SMS_ALERTS_EVALUATION_INTERVAL_MINUTES", 15));
        _windowSize = TimeSpan.FromMinutes(
            configuration.GetValue("SMS_ALERTS_WINDOW_MINUTES", 60));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation(
                "SmsOperationalAlertWorker: disabled (SMS_ALERTS_ENABLED=false). " +
                "Set to true to enable periodic SMS operational alert evaluation.");
            return;
        }

        _logger.LogInformation(
            "SmsOperationalAlertWorker started: interval={Interval}m, windowSize={Window}m",
            _interval.TotalMinutes, _windowSize.TotalMinutes);

        // Stagger startup to avoid competing with other background workers at boot.
        try { await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EvaluateCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SmsOperationalAlertWorker: unhandled error in evaluation cycle");
            }

            try { await Task.Delay(_interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("SmsOperationalAlertWorker stopped");
    }

    private async Task EvaluateCycleAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var evaluator = scope.ServiceProvider.GetRequiredService<ISmsOperationalAlertEvaluator>();

        var windowEnd   = DateTime.UtcNow;
        var windowStart = windowEnd - _windowSize;

        _logger.LogDebug(
            "SmsOperationalAlertWorker: evaluating window={Start:u}..{End:u}",
            windowStart, windowEnd);

        var result = await evaluator.EvaluateAsync(windowStart, windowEnd, stoppingToken);

        _logger.LogInformation(
            "SmsOperationalAlertWorker: cycle complete — " +
            "attempts={Attempts}, created={Created}, updated={Updated}, suppressed={Suppressed}, duration={Ms}ms",
            result.AttemptsSampled, result.AlertsCreated, result.AlertsUpdated,
            result.AlertsSuppressed, result.DurationMs);
    }
}
