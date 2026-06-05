using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Task.Domain.Entities;

namespace Task.Infrastructure.Persistence.Configurations;

public class TaskGovernanceSettingsConfiguration : IEntityTypeConfiguration<TaskGovernanceSettings>
{
    public void Configure(EntityTypeBuilder<TaskGovernanceSettings> builder)
    {
        builder.ToTable("tasks_GovernanceSettings");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id).IsRequired();
        builder.Property(g => g.TenantId).IsRequired();

        builder.Property(g => g.SourceProductCode).HasMaxLength(50);

        builder.Property(g => g.RequireAssignee).IsRequired().HasDefaultValue(false);
        builder.Property(g => g.RequireDueDate).IsRequired().HasDefaultValue(false);
        builder.Property(g => g.RequireStage).IsRequired().HasDefaultValue(false);
        builder.Property(g => g.AllowUnassign).IsRequired().HasDefaultValue(true);
        builder.Property(g => g.AllowCancel).IsRequired().HasDefaultValue(true);
        builder.Property(g => g.AllowCompleteWithoutStage).IsRequired().HasDefaultValue(true);
        builder.Property(g => g.AllowNotesOnClosedTasks).IsRequired().HasDefaultValue(false);

        builder.Property(g => g.DefaultPriority)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("MEDIUM");

        builder.Property(g => g.DefaultTaskScope)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("GENERAL");

        builder.Property(g => g.Version).IsRequired().HasDefaultValue(1);

        // TASK-MIG-01 — product-specific extensions stored as a JSON blob.
        // NULL for tenant defaults and products with no overrides.
        builder.Property(g => g.ProductSettingsJson)
            .HasColumnType("TEXT");

        builder.Property(g => g.CreatedByUserId).IsRequired();
        builder.Property(g => g.UpdatedByUserId);
        builder.Property(g => g.CreatedAtUtc).IsRequired();
        builder.Property(g => g.UpdatedAtUtc).IsRequired();

        builder.HasIndex(g => new { g.TenantId, g.SourceProductCode })
            .HasDatabaseName("IX_GovernanceSettings_TenantId_Product")
            .IsUnique();
    }
}
