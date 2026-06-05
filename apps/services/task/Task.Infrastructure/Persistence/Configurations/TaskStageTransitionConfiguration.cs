using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Task.Domain.Entities;

namespace Task.Infrastructure.Persistence.Configurations;

public class TaskStageTransitionConfiguration : IEntityTypeConfiguration<TaskStageTransition>
{
    public void Configure(EntityTypeBuilder<TaskStageTransition> builder)
    {
        builder.ToTable("tasks_StageTransitions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).IsRequired();
        builder.Property(t => t.TenantId).IsRequired();

        builder.Property(t => t.SourceProductCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(t => t.FromStageId).IsRequired();
        builder.Property(t => t.ToStageId).IsRequired();

        builder.Property(t => t.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(t => t.SortOrder).IsRequired().HasDefaultValue(0);

        builder.Property(t => t.CreatedByUserId).IsRequired();
        builder.Property(t => t.UpdatedByUserId);
        builder.Property(t => t.CreatedAtUtc).IsRequired();
        builder.Property(t => t.UpdatedAtUtc).IsRequired();

        // Fast lookup: all transitions for a tenant+product
        builder.HasIndex(t => new { t.TenantId, t.SourceProductCode })
            .HasDatabaseName("IX_StageTransitions_TenantId_Product");

        // Fast lookup: what stages can I move to from a given stage?
        builder.HasIndex(t => new { t.TenantId, t.SourceProductCode, t.FromStageId })
            .HasDatabaseName("IX_StageTransitions_FromStage");

        // Duplicate prevention
        builder.HasIndex(t => new { t.TenantId, t.SourceProductCode, t.FromStageId, t.ToStageId })
            .IsUnique()
            .HasDatabaseName("UX_StageTransitions_Unique");
    }
}
