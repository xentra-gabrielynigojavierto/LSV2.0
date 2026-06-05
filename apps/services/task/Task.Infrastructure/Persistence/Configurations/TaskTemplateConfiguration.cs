using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Task.Domain.Entities;

namespace Task.Infrastructure.Persistence.Configurations;

public class TaskTemplateConfiguration : IEntityTypeConfiguration<TaskTemplate>
{
    public void Configure(EntityTypeBuilder<TaskTemplate> builder)
    {
        builder.ToTable("tasks_Templates");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).IsRequired();
        builder.Property(t => t.TenantId).IsRequired();

        builder.Property(t => t.SourceProductCode).HasMaxLength(50);

        builder.Property(t => t.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Description).HasMaxLength(1000);

        builder.Property(t => t.DefaultTitle)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(t => t.DefaultDescription).HasMaxLength(4000);

        builder.Property(t => t.DefaultPriority)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("MEDIUM");

        builder.Property(t => t.DefaultScope)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("GENERAL");

        builder.Property(t => t.DefaultDueInDays);
        builder.Property(t => t.DefaultStageId);

        builder.Property(t => t.ProductSettingsJson)
            .HasColumnType("TEXT");

        builder.Property(t => t.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(t => t.Version).IsRequired().HasDefaultValue(1);

        builder.Property(t => t.CreatedByUserId).IsRequired();
        builder.Property(t => t.UpdatedByUserId);
        builder.Property(t => t.CreatedAtUtc).IsRequired();
        builder.Property(t => t.UpdatedAtUtc).IsRequired();

        builder.HasIndex(t => new { t.TenantId, t.SourceProductCode, t.Code })
            .HasDatabaseName("IX_Templates_TenantId_Product_Code")
            .IsUnique();

        builder.HasIndex(t => new { t.TenantId, t.SourceProductCode })
            .HasDatabaseName("IX_Templates_TenantId_Product");
    }
}
