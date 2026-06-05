using Microsoft.EntityFrameworkCore;
using Tenant.Domain;

namespace Tenant.Infrastructure.Data;

public class TenantDbContext : DbContext
{
    public TenantDbContext(DbContextOptions<TenantDbContext> options) : base(options) { }

    public DbSet<Domain.Tenant>              Tenants             => Set<Domain.Tenant>();
    public DbSet<TenantBranding>             Brandings           => Set<TenantBranding>();
    public DbSet<TenantDomain>               Domains             => Set<TenantDomain>();
    public DbSet<TenantProductEntitlement>   ProductEntitlements => Set<TenantProductEntitlement>();
    public DbSet<TenantCapability>           Capabilities        => Set<TenantCapability>();
    public DbSet<TenantSetting>              Settings            => Set<TenantSetting>();
    public DbSet<MigrationRun>               MigrationRuns       => Set<MigrationRun>();
    public DbSet<MigrationRunItem>           MigrationRunItems   => Set<MigrationRunItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TenantDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<Domain.Tenant>())
        {
            if (entry.State == EntityState.Modified)
                entry.Property("UpdatedAtUtc").CurrentValue = now;
        }

        foreach (var entry in ChangeTracker.Entries<TenantBranding>())
        {
            if (entry.State == EntityState.Modified)
                entry.Property("UpdatedAtUtc").CurrentValue = now;
        }

        foreach (var entry in ChangeTracker.Entries<TenantDomain>())
        {
            if (entry.State == EntityState.Modified)
                entry.Property("UpdatedAtUtc").CurrentValue = now;
        }

        foreach (var entry in ChangeTracker.Entries<TenantProductEntitlement>())
        {
            if (entry.State == EntityState.Modified)
                entry.Property("UpdatedAtUtc").CurrentValue = now;
        }

        foreach (var entry in ChangeTracker.Entries<TenantCapability>())
        {
            if (entry.State == EntityState.Modified)
                entry.Property("UpdatedAtUtc").CurrentValue = now;
        }

        foreach (var entry in ChangeTracker.Entries<TenantSetting>())
        {
            if (entry.State == EntityState.Modified)
                entry.Property("UpdatedAtUtc").CurrentValue = now;
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
