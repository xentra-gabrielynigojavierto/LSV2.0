using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PlatformAuditEventService.Data;

/// <summary>
/// Design-time factory for EF Core CLI tooling (dotnet-ef migrations add / update).
/// Reads the connection string from the environment variable
/// ConnectionStrings__AuditEventDb or falls back to a localhost dev default.
///
/// Usage:
///   cd apps/services/audit
///   dotnet ef migrations add InitialAuditSchema --output-dir Data/Migrations
///   dotnet ef database update
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AuditEventDbContext>
{
    public AuditEventDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__AuditEventDb")
            ?? "Server=localhost;Port=3306;Database=audit_event_db;User=root;Password=;AllowPublicKeyRetrieval=True;SslMode=None;";

        // Fixed version — avoids a live connection requirement for migration generation.
        // ServerVersion.AutoDetect requires an active MySQL connection and is only
        // suitable for runtime use. This matches the minimum supported MySQL version.
        var serverVersion = new MySqlServerVersion(new Version(8, 0, 0));

        var optionsBuilder = new DbContextOptionsBuilder<AuditEventDbContext>();
        optionsBuilder.UseMySql(connectionString, serverVersion, opts =>
        {
            opts.CommandTimeout(60);
            opts.EnableRetryOnFailure(maxRetryCount: 3);
        });

        return new AuditEventDbContext(optionsBuilder.Options);
    }
}
