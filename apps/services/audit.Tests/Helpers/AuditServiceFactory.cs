using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlatformAuditEventService.Configuration;
using PlatformAuditEventService.Data;
using PlatformAuditEventService.Repositories;
using PlatformAuditEventService.Services;
using Serilog;

namespace PlatformAuditEventService.Tests.Helpers;

/// <summary>
/// Base integration test factory.
///
/// Overrides:
///   - Serilog            → isolated non-reloadable logger per factory (prevents
///                          "logger already frozen" when multiple factories run in the same session)
///   - EF Core DbContext  → fresh isolated InMemory database per factory instance
///   - Export:Provider    → "None" (prevents filesystem writes during tests)
///   - Logging providers  → cleared (keeps test output clean)
///   - Environment        → "Development" (loads appsettings.Development.json)
///
/// Auth defaults inherited from Development appsettings:
///   - IngestAuth:Mode = "None"  → ingest endpoints accept all requests unauthenticated
///   - QueryAuth:Mode  = "None"  → query endpoints resolve all callers as PlatformAdmin
/// </summary>
public class AuditServiceFactory : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Install a fresh, non-reloadable Serilog logger for this factory instance.
        // This prevents "The logger is already frozen" errors that occur when multiple
        // WebApplicationFactory instances are created in the same test run, because
        // Program.cs registers a global ReloadableLogger that can only be frozen once.
        // By registering a plain Logger AFTER the entry point, DI uses our registration
        // (last wins) and the ReloadableLogger.Freeze() is never invoked.
        builder.UseSerilog(
            new LoggerConfiguration()
                .MinimumLevel.Fatal()
                .CreateLogger(),
            dispose: true);

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Override Database:Provider to "InMemory" so that Program.cs registers
        // InMemoryAuditEventRepository (Singleton) rather than EfAuditEventRepository
        // (Scoped). This matches the pre-SQLite test baseline and keeps tests isolated
        // from the dev-only appsettings.Development.json Sqlite configuration.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "InMemory",
            });
        });

        var dbName = $"AuditEventDb-Test-{Guid.NewGuid():N}";

        builder.ConfigureServices(services =>
        {
            // Replace EF Core factory with a fresh isolated InMemory database so that
            // each factory instance (and thus each test class) starts with an empty store.
            //
            // Problem: AddDbContextFactory uses TryAddSingleton for DbContextOptions<T>,
            // meaning the FIRST registration wins. When appsettings.Development.json has
            // Provider=Sqlite, Program.cs registers Sqlite options first. A second call to
            // AddDbContextFactory for InMemory is a no-op for the options descriptor, so
            // the underlying DbContext still uses Sqlite despite the factory being replaced.
            //
            // Fix: Remove ALL EF Core descriptors related to AuditEventDbContext (both
            // the factory AND the options) so that our InMemory re-registration takes full
            // effect and every repository that uses IDbContextFactory<AuditEventDbContext>
            // gets a genuine in-process, isolated store.
            var dbContextTypes = new HashSet<Type>
            {
                typeof(IDbContextFactory<AuditEventDbContext>),
                typeof(DbContextOptions<AuditEventDbContext>),
                typeof(DbContextOptions),
            };
            var existing = services
                .Where(d =>
                    dbContextTypes.Contains(d.ServiceType) ||
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.GetGenericArguments().Any(a => a == typeof(AuditEventDbContext))))
                .ToList();
            foreach (var d in existing) services.Remove(d);

            services.AddDbContextFactory<AuditEventDbContext>(
                opts => opts.UseInMemoryDatabase(dbName));

            // When appsettings.Development.json uses Provider=Sqlite, Program.cs registers
            // EfAuditEventRepository (Scoped) as IAuditEventRepository. Replace it with the
            // thread-safe InMemoryAuditEventRepository (Singleton) so that tests always run
            // against an in-process store — exactly the same baseline as Provider=InMemory.
            var repoDescriptors = services
                .Where(d => d.ServiceType == typeof(IAuditEventRepository))
                .ToList();
            foreach (var d in repoDescriptors) services.Remove(d);
            services.AddSingleton<IAuditEventRepository, InMemoryAuditEventRepository>();

            // Override Export:Provider → "None" so tests don't write to filesystem.
            services.Configure<ExportOptions>(opts => opts.Provider = "None");
        });

        builder.ConfigureLogging(logging => logging.ClearProviders());
    }
}

/// <summary>
/// Integration test factory with <c>IngestAuth:Mode = "ServiceToken"</c> active.
///
/// Exposes <see cref="ValidToken"/> — the pre-shared secret callers must send
/// in the <c>x-service-token</c> header to authenticate ingest requests.
///
/// Uses <c>Configure&lt;IngestAuthOptions&gt;</c> (options post-configuration) rather than
/// <c>ConfigureAppConfiguration</c> to guarantee the override wins over appsettings.Development.json.
///
/// Inherits all other overrides from <see cref="AuditServiceFactory"/>.
/// </summary>
public class ServiceTokenAuditFactory : AuditServiceFactory
{
    public const string ValidToken  = "test-service-token-abc123-integration";
    public const string ServiceName = "test-service";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            // ── Options layer override ────────────────────────────────────────
            // Makes IngestAuthOptions.Mode and ServiceTokens visible to any code
            // that reads via IOptions<IngestAuthOptions> (e.g. ServiceTokenAuthenticator).
            services.Configure<IngestAuthOptions>(opts =>
            {
                opts.Mode = "ServiceToken";
                opts.ServiceTokens =
                [
                    new ServiceTokenEntry
                    {
                        Token       = ValidToken,
                        ServiceName = ServiceName,
                        Enabled     = true,
                    },
                ];
            });

            // ── IIngestAuthenticator DI override ─────────────────────────────
            // Program.cs registers IIngestAuthenticator using a factory lambda that captures
            // the raw configuration value of IngestAuth:Mode AT STARTUP (before ConfigureWebHost
            // runs). This means options-layer overrides are too late to affect which
            // IIngestAuthenticator implementation the factory picks.
            //
            // Fix: remove the existing singleton factory and register ServiceTokenAuthenticator
            // directly (it is already registered as a concrete singleton by Program.cs, so
            // sp.GetRequiredService<ServiceTokenAuthenticator>() is safe here).
            var existing = services
                .Where(d => d.ServiceType == typeof(IIngestAuthenticator))
                .ToList();
            foreach (var d in existing) services.Remove(d);

            services.AddSingleton<IIngestAuthenticator>(
                sp => sp.GetRequiredService<ServiceTokenAuthenticator>());
        });
    }
}
