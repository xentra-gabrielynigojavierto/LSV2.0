using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Monitoring.Infrastructure.Persistence;

/// <summary>
/// Performs a one-shot, fire-and-forget DB connectivity probe shortly after the
/// host starts. Logs the outcome but never throws — DB unavailability must not
/// crash the service. The HTTP host (and <c>/health</c>) keep working regardless.
/// </summary>
public class DatabaseConnectivityHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseConnectivityHostedService> _logger;

    public DatabaseConnectivityHostedService(
        IServiceProvider serviceProvider,
        ILogger<DatabaseConnectivityHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(() => ProbeAsync(cancellationToken), cancellationToken);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task ProbeAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MonitoringDbContext>();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var canConnect = await dbContext.Database.CanConnectAsync(cts.Token).ConfigureAwait(false);

            if (canConnect)
            {
                _logger.LogInformation("Database connectivity check succeeded for MonitoringDb.");
            }
            else
            {
                _logger.LogWarning(
                    "Database connectivity check returned false for MonitoringDb. " +
                    "Service will continue running, but persistence operations will fail until the database is reachable.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Host is shutting down; nothing to log.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Database connectivity check failed for MonitoringDb. " +
                "Service will continue running, but persistence operations will fail until the database is reachable.");
        }
    }
}
