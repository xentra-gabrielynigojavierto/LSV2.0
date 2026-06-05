using Support.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Support.Tests;

public class SupportApiFactory : WebApplicationFactory<Program>
{
    public string DbName { get; } = $"support-tests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.AddDbContext<SupportDbContext>(o => o.UseInMemoryDatabase(DbName));
        });
    }
}

/// <summary>
/// Production-like factory: uses the "Production" environment so the real JWT
/// auth pipeline is wired and tenant resolution requires a JWT claim only.
/// Used to verify 401 / 403 / header-rejection behavior outside Dev/Testing.
/// </summary>
public class SupportApiProdFactory : WebApplicationFactory<Program>
{
    public string DbName { get; } = $"support-tests-prod-{Guid.NewGuid()}";

    static SupportApiProdFactory()
    {
        // Env vars are read by the default config providers BEFORE
        // Program.cs queries `builder.Configuration`, so this is the
        // only reliable way to inject JWT settings into a
        // WebApplication.CreateBuilder host under WebApplicationFactory.
        // Maps to Jwt:Issuer / Jwt:Audience / Jwt:SigningKey (platform-standard section).
        Environment.SetEnvironmentVariable("Jwt__Issuer",
            "https://test-issuer.local");
        Environment.SetEnvironmentVariable("Jwt__Audience",
            "support-api-tests");
        Environment.SetEnvironmentVariable("Jwt__SigningKey",
            "test-only-symmetric-signing-key-for-prod-like-tests-32+chars!!");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        builder.ConfigureServices(services =>
        {
            // Replace MySQL with InMemory for this prod-like profile.
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<SupportDbContext>) ||
                d.ServiceType == typeof(SupportDbContext)).ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<SupportDbContext>(o => o.UseInMemoryDatabase(DbName));
        });
    }
}
