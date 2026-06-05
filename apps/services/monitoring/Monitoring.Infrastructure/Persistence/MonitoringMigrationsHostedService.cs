using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Monitoring.Infrastructure.Persistence;

/// <summary>
/// One-shot startup service that applies pending EF Core migrations to the
/// monitoring database before any other hosted services access it.
///
/// <para>Registration order matters: this service must be registered
/// <em>before</em> <c>MonitoringEntityBootstrap</c> in
/// <c>DependencyInjection.AddInfrastructure</c> so that the schema is
/// up-to-date before the seed check runs.</para>
///
/// <para>Failure handling: a migration failure is logged as a critical error
/// but does not crash the host. The service degrades gracefully (schema
/// queries will fail at the ORM layer with clear exceptions).</para>
/// </summary>
public sealed class MonitoringMigrationsHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MonitoringMigrationsHostedService> _logger;

    public MonitoringMigrationsHostedService(
        IServiceProvider serviceProvider,
        ILogger<MonitoringMigrationsHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger          = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MonitoringDbContext>();

            _logger.LogInformation("MonitoringMigrations: applying pending EF Core migrations.");
            await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("MonitoringMigrations: migrations applied successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(
                ex,
                "MonitoringMigrations: failed to apply migrations. " +
                "The service will continue but persistence operations may fail.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
