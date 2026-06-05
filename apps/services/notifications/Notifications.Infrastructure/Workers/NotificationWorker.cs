using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Workers;

public class NotificationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationWorker> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);

    public NotificationWorker(IServiceScopeFactory scopeFactory, ILogger<NotificationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NotificationWorker started, interval={Interval}s", _interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NotificationWorker unhandled error in dispatch cycle");
            }

            try { await Task.Delay(_interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("NotificationWorker stopped");
    }

    private async Task ProcessBatchAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var notifRepo = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
        var notifService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var eligible = await notifRepo.GetEligibleForRetryAsync(batchSize: 10);

        if (eligible.Count == 0)
        {
            _logger.LogDebug("NotificationWorker: no notifications eligible for retry");
            return;
        }

        _logger.LogInformation("NotificationWorker: processing {Count} notifications eligible for retry", eligible.Count);

        foreach (var notification in eligible)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                _logger.LogInformation(
                    "NotificationWorker: dispatching auto-retry for notification {Id} (retryCount={RetryCount}, nextRetryAt={NextRetryAt})",
                    notification.Id, notification.RetryCount, notification.NextRetryAt);

                await notifService.ProcessAutoRetryAsync(notification.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NotificationWorker: error processing retry for notification {Id}", notification.Id);
            }
        }
    }
}
