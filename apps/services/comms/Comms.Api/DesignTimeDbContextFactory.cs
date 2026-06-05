using Comms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Comms.Api;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CommsDbContext>
{
    public CommsDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("SynqCommDb")
            ?? throw new InvalidOperationException("Connection string 'SynqCommDb' is not configured.");

        var optionsBuilder = new DbContextOptionsBuilder<CommsDbContext>();
        optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0)));

        return new CommsDbContext(optionsBuilder.Options);
    }
}
