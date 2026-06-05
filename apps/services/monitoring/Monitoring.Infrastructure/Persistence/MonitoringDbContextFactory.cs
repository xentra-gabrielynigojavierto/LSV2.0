using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Monitoring.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> to create a <see cref="MonitoringDbContext"/>
/// without spinning up the full Api host.
///
/// Resolution order for the connection string:
///   1. Environment variable <c>ConnectionStrings__MonitoringDb</c>
///   2. <c>appsettings.json</c> in the working directory (if present)
///   3. A safe local-dev fallback pointing at <c>localhost</c>
/// </summary>
public class MonitoringDbContextFactory : IDesignTimeDbContextFactory<MonitoringDbContext>
{
    private const string DesignTimeFallbackConnectionString =
        "Server=localhost;Port=3306;Database=monitoring;User=monitoring;Password=monitoring;";

    public MonitoringDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString =
            configuration.GetConnectionString("MonitoringDb")
            ?? DesignTimeFallbackConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<MonitoringDbContext>();
        optionsBuilder.UseMySql(
            connectionString,
            new MySqlServerVersion(new Version(8, 0, 36)),
            mySqlOptions => mySqlOptions.MigrationsAssembly(typeof(MonitoringDbContext).Assembly.GetName().Name));

        return new MonitoringDbContext(optionsBuilder.Options);
    }
}
