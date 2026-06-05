using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CareConnect.Api;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CareConnectDbContext>
{
    public CareConnectDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("CareConnectDb")
            ?? throw new InvalidOperationException("Connection string 'CareConnectDb' is not configured.");

        var optionsBuilder = new DbContextOptionsBuilder<CareConnectDbContext>();
        optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0)));

        return new CareConnectDbContext(optionsBuilder.Options);
    }
}
