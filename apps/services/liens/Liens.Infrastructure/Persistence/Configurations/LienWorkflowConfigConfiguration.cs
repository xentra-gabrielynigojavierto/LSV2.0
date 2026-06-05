using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class LienWorkflowConfigConfiguration : IEntityTypeConfiguration<LienWorkflowConfig>
{
    public void Configure(EntityTypeBuilder<LienWorkflowConfig> builder)
    {
        builder.ToTable("liens_WorkflowConfigs");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id).IsRequired();
        builder.Property(w => w.TenantId).IsRequired();

        builder.Property(w => w.ProductCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(w => w.WorkflowName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(w => w.Version).IsRequired();
        builder.Property(w => w.IsActive).IsRequired();
        builder.Property(w => w.LastUpdatedAt).IsRequired();
        builder.Property(w => w.LastUpdatedByUserId);

        builder.Property(w => w.LastUpdatedByName)
            .HasMaxLength(200);

        builder.Property(w => w.LastUpdatedSource)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(w => w.CreatedByUserId).IsRequired();
        builder.Property(w => w.UpdatedByUserId);
        builder.Property(w => w.CreatedAtUtc).IsRequired();
        builder.Property(w => w.UpdatedAtUtc).IsRequired();

        builder.HasMany(w => w.Stages)
            .WithOne()
            .HasForeignKey(s => s.WorkflowConfigId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(w => new { w.TenantId, w.ProductCode })
            .IsUnique()
            .HasDatabaseName("UX_WorkflowConfigs_TenantId_ProductCode");
    }
}
