using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Monitoring.Domain.Common;
using Monitoring.Domain.Monitoring;

namespace Monitoring.Infrastructure.Persistence;

/// <summary>
/// Root EF Core DbContext for the Monitoring Service.
///
/// SaveChanges is intercepted to stamp <see cref="IAuditableEntity"/> timestamps
/// automatically: <c>CreatedAtUtc</c> on insert, <c>UpdatedAtUtc</c> on insert
/// and update. This keeps audit timestamping out of callers and consistent
/// across all persistence flows.
/// </summary>
public class MonitoringDbContext : DbContext
{
    public MonitoringDbContext(DbContextOptions<MonitoringDbContext> options)
        : base(options)
    {
    }

    public DbSet<MonitoredEntity> MonitoredEntities => Set<MonitoredEntity>();

    public DbSet<CheckResultRecord> CheckResults => Set<CheckResultRecord>();

    public DbSet<EntityCurrentStatus> EntityCurrentStatuses => Set<EntityCurrentStatus>();

    public DbSet<MonitoringAlert> MonitoringAlerts => Set<MonitoringAlert>();

    public DbSet<UptimeHourlyRollup> UptimeHourlyRollups => Set<UptimeHourlyRollup>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MonitoringDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampAuditTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        StampAuditTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void StampAuditTimestamps()
    {
        var utcNow = DateTime.UtcNow;

        foreach (EntityEntry entry in ChangeTracker.Entries())
        {
            if (entry.Entity is not IAuditableEntity auditable)
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    auditable.SetCreatedAt(utcNow);
                    auditable.SetUpdatedAt(utcNow);
                    break;
                case EntityState.Modified:
                    auditable.SetUpdatedAt(utcNow);
                    // Ensure CreatedAtUtc is never modified after creation.
                    entry.Property(nameof(IAuditableEntity.CreatedAtUtc)).IsModified = false;
                    break;
            }
        }
    }
}
