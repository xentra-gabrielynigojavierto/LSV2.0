using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class LienWorkflowStageConfiguration : IEntityTypeConfiguration<LienWorkflowStage>
{
    public void Configure(EntityTypeBuilder<LienWorkflowStage> builder)
    {
        builder.ToTable("liens_WorkflowStages");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).IsRequired();
        builder.Property(s => s.WorkflowConfigId).IsRequired();

        builder.Property(s => s.StageName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.StageOrder).IsRequired();
        builder.Property(s => s.IsActive).IsRequired();

        builder.Property(s => s.Description)
            .HasMaxLength(1000);

        builder.Property(s => s.DefaultOwnerRole)
            .HasMaxLength(100);

        builder.Property(s => s.SlaMetadata)
            .HasMaxLength(2000);

        builder.Property(s => s.CreatedByUserId).IsRequired();
        builder.Property(s => s.UpdatedByUserId);
        builder.Property(s => s.CreatedAtUtc).IsRequired();
        builder.Property(s => s.UpdatedAtUtc).IsRequired();

        builder.HasIndex(s => new { s.WorkflowConfigId, s.StageOrder })
            .HasDatabaseName("IX_WorkflowStages_ConfigId_Order");
    }
}
