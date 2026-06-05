using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Task.Domain.Entities;

namespace Task.Infrastructure.Persistence.Configurations;

public class TaskStageConfigConfiguration : IEntityTypeConfiguration<TaskStageConfig>
{
    public void Configure(EntityTypeBuilder<TaskStageConfig> builder)
    {
        builder.ToTable("tasks_StageConfigs");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).IsRequired();
        builder.Property(s => s.TenantId).IsRequired();

        builder.Property(s => s.SourceProductCode).HasMaxLength(50);

        builder.Property(s => s.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.DisplayOrder).IsRequired();
        builder.Property(s => s.IsActive).IsRequired().HasDefaultValue(true);

        builder.Property(s => s.ProductSettingsJson)
            .HasColumnType("TEXT");

        builder.Property(s => s.CreatedByUserId).IsRequired();
        builder.Property(s => s.UpdatedByUserId);
        builder.Property(s => s.CreatedAtUtc).IsRequired();
        builder.Property(s => s.UpdatedAtUtc).IsRequired();

        builder.HasIndex(s => new { s.TenantId, s.SourceProductCode, s.Code })
            .HasDatabaseName("IX_StageConfigs_TenantId_Product_Code")
            .IsUnique();

        builder.HasIndex(s => new { s.TenantId, s.SourceProductCode })
            .HasDatabaseName("IX_StageConfigs_TenantId_Product");
    }
}
