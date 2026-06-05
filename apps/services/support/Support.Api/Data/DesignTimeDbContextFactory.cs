using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Support.Api.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SupportDbContext>
{
    public SupportDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("SUPPORT_DB_CONNECTION")
            ?? "Server=localhost;Port=3306;Database=support;User=root;Password=;";
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseMySql(conn, new MySqlServerVersion(new Version(8, 0, 26)))
            .Options;
        return new SupportDbContext(options);
    }
}
