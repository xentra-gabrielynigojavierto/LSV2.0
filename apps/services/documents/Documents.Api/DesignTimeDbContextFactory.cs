using Documents.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Documents.Api;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DocsDbContext>
{
    public DocsDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DocsDb")
            ?? "server=localhost;port=3306;database=documents_design;user=root;password=root";

        var optionsBuilder = new DbContextOptionsBuilder<DocsDbContext>();
        optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0)));

        return new DocsDbContext(optionsBuilder.Options);
    }
}
