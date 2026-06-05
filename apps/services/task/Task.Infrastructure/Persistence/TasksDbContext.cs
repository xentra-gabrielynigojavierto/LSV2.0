using Microsoft.EntityFrameworkCore;
using Task.Domain.Entities;

namespace Task.Infrastructure.Persistence;

public class TasksDbContext : DbContext
{
    public TasksDbContext(DbContextOptions<TasksDbContext> options) : base(options) { }

    public DbSet<PlatformTask>           Tasks             => Set<PlatformTask>();
    public DbSet<TaskNote>               Notes             => Set<TaskNote>();
    public DbSet<TaskHistory>            History           => Set<TaskHistory>();
    public DbSet<TaskStageConfig>        StageConfigs      => Set<TaskStageConfig>();
    public DbSet<TaskStageTransition>    StageTransitions  => Set<TaskStageTransition>();
    public DbSet<TaskGovernanceSettings> GovernanceSettings => Set<TaskGovernanceSettings>();
    public DbSet<TaskTemplate>           Templates         => Set<TaskTemplate>();
    public DbSet<TaskReminder>           Reminders         => Set<TaskReminder>();
    public DbSet<TaskLinkedEntity>       LinkedEntities    => Set<TaskLinkedEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TasksDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BuildingBlocks.Domain.AuditableEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Property(nameof(BuildingBlocks.Domain.AuditableEntity.UpdatedAtUtc)).CurrentValue = now;
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
