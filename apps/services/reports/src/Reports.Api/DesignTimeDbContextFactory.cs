using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Reports.Infrastructure.Persistence;

namespace Reports.Api;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ReportsDbContext>
{
    public ReportsDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("ReportsDb")
            ?? throw new InvalidOperationException("Connection string 'ReportsDb' is not configured.");

        var optionsBuilder = new DbContextOptionsBuilder<ReportsDbContext>();
        optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0)));

        return new ReportsDbContext(optionsBuilder.Options);
    }
}
