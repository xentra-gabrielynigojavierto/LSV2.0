using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Task.Domain.Entities;

namespace Task.Infrastructure.Persistence.Configurations;

public class TaskLinkedEntityConfiguration : IEntityTypeConfiguration<TaskLinkedEntity>
{
    public void Configure(EntityTypeBuilder<TaskLinkedEntity> builder)
    {
        builder.ToTable("tasks_LinkedEntities");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).IsRequired();
        builder.Property(e => e.TaskId).IsRequired();
        builder.Property(e => e.TenantId).IsRequired();

        builder.Property(e => e.SourceProductCode).HasMaxLength(50);
        builder.Property(e => e.EntityType).IsRequired().HasMaxLength(100);
        builder.Property(e => e.EntityId).IsRequired().HasMaxLength(100);
        builder.Property(e => e.RelationshipType).IsRequired().HasMaxLength(50);
        builder.Property(e => e.CreatedAtUtc).IsRequired();

        builder.HasIndex(e => e.TaskId)
            .HasDatabaseName("IX_LinkedEntities_TaskId");

        builder.HasIndex(e => new { e.TenantId, e.EntityType, e.EntityId })
            .HasDatabaseName("IX_LinkedEntities_EntityRef");

        // TASK-B05 (TASK-017) — prevent duplicate (taskId, entityType, entityId) rows
        builder.HasIndex(e => new { e.TaskId, e.EntityType, e.EntityId })
            .IsUnique()
            .HasDatabaseName("UX_LinkedEntities_TaskId_EntityType_EntityId");
    }
}
