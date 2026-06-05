using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Monitoring.Domain.Monitoring;
using Monitoring.Infrastructure.Persistence;

namespace Monitoring.Infrastructure.Bootstrap;

/// <summary>
/// One-shot startup service that seeds the monitored-entity registry with the
/// LegalSynq platform's initial set of services when the DB is empty.
///
/// <para><b>Canonical path</b>: entities are created through the domain
/// constructor (<see cref="MonitoredEntity"/>), which enforces all invariants,
/// then persisted via <see cref="MonitoringDbContext"/>. This is equivalent to
/// what the admin API does — just without the RS256 token barrier that blocks
/// platform components from calling the admin API directly. Once
/// MON-INT-01-003 (auth alignment) lands, this bootstrap can be replaced
/// by a seed script that calls the real admin endpoints.</para>
///
/// <para><b>Idempotency</b>: if <em>any</em> entity row already exists, the
/// service skips seeding entirely. Safe to run on every restart.</para>
///
/// <para><b>Isolation</b>: opens its own <see cref="IServiceScope"/> so that
/// the scoped <see cref="MonitoringDbContext"/> is not shared with the request
/// pipeline. The scope is disposed when seeding completes.</para>
///
/// <para><b>Reversibility</b>: set <c>MonitoringBootstrap:Enabled=false</c> in
/// <c>appsettings.json</c> (or via env var
/// <c>MonitoringBootstrap__Enabled=false</c>) to disable. The feature is also
/// naturally retired once any entity is registered via the admin API, since the
/// non-empty check prevents re-seeding.</para>
/// </summary>
public sealed class MonitoringEntityBootstrap : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MonitoringEntityBootstrap> _logger;
    private readonly bool _enabled;

    public MonitoringEntityBootstrap(
        IServiceScopeFactory scopeFactory,
        ILogger<MonitoringEntityBootstrap> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        // Default: enabled. Set MonitoringBootstrap__Enabled=false to skip.
        _enabled = configuration.GetValue("MonitoringBootstrap:Enabled", defaultValue: true);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("MonitoringEntityBootstrap: disabled via config. Skipping seed.");
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MonitoringDbContext>();

        var alreadySeeded = await db.MonitoredEntities.AnyAsync(cancellationToken);
        if (alreadySeeded)
        {
            _logger.LogInformation(
                "MonitoringEntityBootstrap: entity registry is non-empty. Skipping seed.");
            return;
        }

        _logger.LogInformation(
            "MonitoringEntityBootstrap: entity registry is empty. Seeding {Count} entities.",
            Entities.Length);

        foreach (var seed in Entities)
        {
            var entity = new MonitoredEntity(
                id:            Guid.NewGuid(),
                name:          seed.Name,
                entityType:    seed.EntityType,
                monitoringType: seed.MonitoringType,
                target:        seed.Target,
                scope:         seed.Scope,
                impactLevel:   seed.ImpactLevel,
                isEnabled:     true);

            db.MonitoredEntities.Add(entity);

            _logger.LogDebug(
                "MonitoringEntityBootstrap: queued entity '{Name}' → {Target} " +
                "({EntityType}, {MonitoringType}, Impact={ImpactLevel}, Scope={Scope}).",
                seed.Name, seed.Target, seed.EntityType, seed.MonitoringType,
                seed.ImpactLevel, seed.Scope);
        }

        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "MonitoringEntityBootstrap: seeded {Count} entities successfully.",
            Entities.Length);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // ── Initial entity set ────────────────────────────────────────────────────
    // Mirrors the service list in apps/control-center/src/lib/system-health-store.ts.
    // Targets use 127.0.0.1 (not localhost) to avoid IPv6 resolution differences
    // on the Replit host (Node.js resolves "localhost" to ::1 first; .NET services
    // bind to 0.0.0.0 IPv4 only).

    private static readonly EntitySeed[] Entities =
    [
        new("Gateway",
            "http://127.0.0.1:5010/health",
            EntityType.InternalService, MonitoringType.Http,
            ImpactLevel.Blocking, "infrastructure"),

        new("Identity",
            "http://127.0.0.1:5001/health",
            EntityType.InternalService, MonitoringType.Http,
            ImpactLevel.Blocking, "infrastructure"),

        new("Documents",
            "http://127.0.0.1:5006/health",
            EntityType.InternalService, MonitoringType.Http,
            ImpactLevel.Degraded, "infrastructure"),

        new("Notifications",
            "http://127.0.0.1:5008/health",
            EntityType.InternalService, MonitoringType.Http,
            ImpactLevel.Degraded, "infrastructure"),

        new("Audit",
            "http://127.0.0.1:5007/health",
            EntityType.InternalService, MonitoringType.Http,
            ImpactLevel.Degraded, "infrastructure"),

        new("Reports",
            "http://127.0.0.1:5029/api/v1/health",
            EntityType.InternalService, MonitoringType.Http,
            ImpactLevel.Degraded, "infrastructure"),

        new("Workflow",
            "http://127.0.0.1:5012/health",
            EntityType.InternalService, MonitoringType.Http,
            ImpactLevel.Degraded, "infrastructure"),

        new("Synq Fund",
            "http://127.0.0.1:5002/health",
            EntityType.InternalService, MonitoringType.Http,
            ImpactLevel.Degraded, "product"),

        new("Synq CareConnect",
            "http://127.0.0.1:5003/health",
            EntityType.InternalService, MonitoringType.Http,
            ImpactLevel.Degraded, "product"),

        new("Synq Liens",
            "http://127.0.0.1:5009/health",
            EntityType.InternalService, MonitoringType.Http,
            ImpactLevel.Degraded, "product"),
    ];

    private sealed record EntitySeed(
        string Name,
        string Target,
        EntityType EntityType,
        MonitoringType MonitoringType,
        ImpactLevel ImpactLevel,
        string Scope);
}
