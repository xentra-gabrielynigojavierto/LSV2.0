using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Workers;

public class StatusSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StatusSyncWorker> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);

    public StatusSyncWorker(IServiceScopeFactory scopeFactory, ILogger<StatusSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("StatusSyncWorker started, interval={Interval}s", _interval.TotalSeconds);

        // Stagger startup so the worker doesn't compete immediately with NotificationWorker
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StatusSyncWorker unhandled error in reconciliation cycle");
            }

            try { await Task.Delay(_interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("StatusSyncWorker stopped");
    }

    private async Task ReconcileAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var notifService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        _logger.LogDebug("StatusSyncWorker: running stalled-processing reconciliation");
        await notifService.ReconcileStalledAsync();
    }
}
