using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Monitoring.Application.Queries;
using Monitoring.Application.Scheduling;
using Monitoring.Infrastructure.Bootstrap;
using Monitoring.Infrastructure.Http;
using Monitoring.Infrastructure.Persistence;
using Monitoring.Infrastructure.Queries;
using Monitoring.Infrastructure.Scheduling;
using Monitoring.Infrastructure.UptimeAggregation;

namespace Monitoring.Infrastructure;

public static class DependencyInjection
{
    public const string MonitoringDbConnectionStringName = "MonitoringDb";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(MonitoringDbConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string 'ConnectionStrings:{MonitoringDbConnectionStringName}' is missing. " +
                "Set it in appsettings.json or via the environment variable " +
                $"'ConnectionStrings__{MonitoringDbConnectionStringName}'.");
        }

        services.AddDbContext<MonitoringDbContext>(options =>
        {
            // ServerVersion is set explicitly (rather than AutoDetect) so that
            // DI registration never attempts a network call. This keeps service
            // startup decoupled from DB availability.
            var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));

            options.UseMySql(
                connectionString,
                serverVersion,
                mySqlOptions =>
                {
                    mySqlOptions.MigrationsAssembly(typeof(MonitoringDbContext).Assembly.GetName().Name);
                    mySqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                });
        });

        services.AddHostedService<DatabaseConnectivityHostedService>();

        // Apply EF Core migrations before any seed or query services touch the DB.
        // Must be registered before MonitoringEntityBootstrap (hosted services start
        // in registration order).
        services.AddHostedService<MonitoringMigrationsHostedService>();

        // One-shot startup seed: registers platform services if the entity
        // registry is empty. Idempotent — skips if any row already exists.
        // Disable via MonitoringBootstrap__Enabled=false.
        services.AddHostedService<MonitoringEntityBootstrap>();

        // Read service — scoped so it shares the per-request MonitoringDbContext.
        services.AddScoped<IMonitoringReadService, EfCoreMonitoringReadService>();

        // Uptime read service — scoped.
        services.AddScoped<IUptimeReadService, EfCoreUptimeReadService>();

        // Uptime aggregation engine options and hosted service.
        services.AddOptions<UptimeAggregationOptions>()
            .Bind(configuration.GetSection(UptimeAggregationOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHostedService<UptimeAggregationHostedService>();

        // Scheduler foundation. Options are validated at startup so a bad
        // interval value fails fast with a clear message.
        services.AddOptions<SchedulerOptions>()
            .Bind(configuration.GetSection(SchedulerOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHostedService<MonitoringSchedulerHostedService>();

        // Override the Application-layer no-op cycle executor with the real
        // registry-driven executor that loads enabled MonitoredEntity rows
        // from the DbContext and dispatches them to IMonitoredEntityExecutor.
        // Last-registered wins for IServiceProvider.GetRequiredService<>().
        services.AddScoped<IMonitoringCycleExecutor, MonitoredEntityRegistryCycleExecutor>();

        // Durable check-result persistence. Scoped so it shares the
        // per-cycle MonitoringDbContext with the cycle executor.
        services.AddScoped<ICheckResultWriter, EfCoreCheckResultWriter>();

        // Current-status projection writer. Scoped, sharing the same
        // per-cycle MonitoringDbContext as the history-row writer so
        // both upserts run inside one DbContext lifetime.
        services.AddScoped<IEntityStatusWriter, EfCoreEntityStatusWriter>();

        // Alert rule engine. Scoped, sharing the per-cycle
        // MonitoringDbContext so the prior-status read, the alert
        // insert/update, and the surrounding history + current-status
        // writes all run inside one DbContext lifetime.
        services.AddScoped<IAlertRuleEngine, EfCoreAlertRuleEngine>();

        // HTTP check adapter — first real per-entity executor.
        services.AddOptions<HttpCheckOptions>()
            .Bind(configuration.GetSection(HttpCheckOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Named HttpClient managed by IHttpClientFactory. The factory's own
        // timeout is set slightly above the per-call timeout so the
        // per-request linked CancellationTokenSource (in
        // HttpMonitoredEntityExecutor) is the authoritative bound.
        services.AddHttpClient(HttpMonitoredEntityExecutor.HttpClientName, (sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<HttpCheckOptions>>().Value;
                client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds + 5);
            })
            // Strip the factory's default request/response logging handlers.
            // They log the full request URL at Information level, which would
            // bypass our sanitization and leak userinfo / query-string tokens
            // for monitored targets. Our executor produces all the operator
            // visibility we need (success / non-2xx / timeout / network /
            // invalid URL) using a redacted target.
            .RemoveAllLoggers();

        // Override the Application-layer no-op per-entity executor with the
        // real HTTP-capable executor. Non-HTTP entities are skipped inside
        // the executor itself (debug log, no I/O).
        services.AddScoped<IMonitoredEntityExecutor, HttpMonitoredEntityExecutor>();

        return services;
    }
}
