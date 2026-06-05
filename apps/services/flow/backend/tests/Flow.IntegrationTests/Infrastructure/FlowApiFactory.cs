using Flow.Application.Interfaces;
using Flow.Domain.Interfaces;
using Flow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flow.IntegrationTests.Infrastructure;

/// <summary>
/// LS-FLOW-HARDEN-A1.1 — boots Flow.Api in-process with a SQLite-in-memory
/// database (single shared keep-alive connection so all DbContext scopes
/// see the same data) and the <see cref="TestAuthHandler"/> swapped in for
/// the production multi-scheme auth.
///
/// Critically, <c>ClaimsTenantProvider</c>, <c>CallerContextAccessor</c>,
/// the capability policies, the tenant query filter, the
/// <c>TenantValidationMiddleware</c> and the real
/// <c>ProductWorkflowExecutionController</c> + <c>WorkflowEngine</c> all
/// run as in production.
/// </summary>
public sealed class FlowApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public FlowApiFactory()
    {
        // Shared keep-alive connection — closed when the factory disposes.
        // SQLite drops the in-memory db when the last connection closes, so
        // we keep this one open for the lifetime of the test class.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development (NOT "Testing") mirrors how the unit-tests + dev stack ran in A1.
        // Keeping Development bypasses the service-token startup guard (which
        // is exhaustively covered in Flow.UnitTests.ServiceTokenStartupGuardTests)
        // — the integration suite focuses on controller + engine behaviour.
        // Note: the "no permissions at all" capability dev-fallback only
        // engages when a user carries ZERO permission claims; every test
        // caller that should pass capability either carries the permission
        // or the matching product role, and capability-denial tests carry
        // an unrelated permission so the fallback never triggers.
        builder.UseEnvironment("Development");

        // Configuration overrides keep production guards happy without
        // requiring real secrets — TestAuth is the actual authority.
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // No JWT signing key → production JwtBearer self-disables;
                // TestAuth handles every request via the policy scheme below.
                ["Jwt:SigningKey"] = "",
                // Long enough to satisfy the A1 service-token startup guard
                // when the host is forced into a non-Development environment.
                ["ServiceTokens:SigningKey"] =
                    "integration-test-service-token-signing-key-do-not-use-elsewhere-0123456789abcdef",
                ["ServiceTokens:Issuer"] = "ls.flow.tests",
                ["ServiceTokens:Audience"] = "ls.flow",
                // Force the LoggingAuditAdapter / LoggingNotificationAdapter
                // (no HTTP fan-out from inside tests).
                ["Audit:BaseUrl"] = "",
                ["Notifications:BaseUrl"] = "",
            });
        });

        builder.ConfigureServices(services =>
        {
            // ---- Replace FlowDbContext with SQLite-in-memory --------------
            ReplaceDbContext(services);

            // ---- Replace auth pipeline with TestAuth ----------------------
            // We add the TestAuth scheme and override the
            // AuthenticationOptions defaults so [Authorize(...)] resolves it.
            services.AddAuthentication(TestAuthDefaults.Scheme)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthDefaults.Scheme, _ => { });

            services.PostConfigure<AuthenticationOptions>(opt =>
            {
                opt.DefaultAuthenticateScheme = TestAuthDefaults.Scheme;
                opt.DefaultChallengeScheme    = TestAuthDefaults.Scheme;
                opt.DefaultScheme             = TestAuthDefaults.Scheme;
                opt.DefaultForbidScheme       = TestAuthDefaults.Scheme;
                opt.DefaultSignInScheme       = TestAuthDefaults.Scheme;
                opt.DefaultSignOutScheme      = TestAuthDefaults.Scheme;
            });
        });
    }

    private void ReplaceDbContext(IServiceCollection services)
    {
        // Strip every prior FlowDbContext/IFlowDbContext/options registration.
        var doomed = services
            .Where(s =>
                s.ServiceType == typeof(DbContextOptions<FlowDbContext>) ||
                s.ServiceType == typeof(FlowDbContext) ||
                s.ServiceType == typeof(IFlowDbContext))
            .ToList();
        foreach (var d in doomed) services.Remove(d);

        services.AddDbContext<FlowDbContext>(opts =>
        {
            opts.UseSqlite(_connection);
            opts.EnableSensitiveDataLogging();
        });
        services.AddScoped<IFlowDbContext>(sp => sp.GetRequiredService<FlowDbContext>());

        // EnsureCreated runs the EF model + HasData seed against SQLite.
        // We then layer the test fixture's seed on top in SeedFixture.
        using var scope = services.BuildServiceProvider().CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowDbContext>();
        db.Database.EnsureCreated();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection.Dispose();
        }
        base.Dispose(disposing);
    }
}
