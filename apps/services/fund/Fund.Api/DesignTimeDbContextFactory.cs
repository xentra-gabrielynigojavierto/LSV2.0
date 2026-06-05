using Fund.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fund.Api;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FundDbContext>
{
    public FundDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("FundDb")
            ?? throw new InvalidOperationException("Connection string 'FundDb' is not configured.");

        var optionsBuilder = new DbContextOptionsBuilder<FundDbContext>();
        optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0)));

        return new FundDbContext(optionsBuilder.Options);
    }
}
