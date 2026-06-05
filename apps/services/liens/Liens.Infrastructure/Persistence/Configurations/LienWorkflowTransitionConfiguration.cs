using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class LienWorkflowTransitionConfiguration : IEntityTypeConfiguration<LienWorkflowTransition>
{
    public void Configure(EntityTypeBuilder<LienWorkflowTransition> builder)
    {
        builder.ToTable("liens_WorkflowTransitions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).IsRequired();
        builder.Property(t => t.WorkflowConfigId).IsRequired();
        builder.Property(t => t.FromStageId).IsRequired();
        builder.Property(t => t.ToStageId).IsRequired();
        builder.Property(t => t.IsActive).IsRequired();
        builder.Property(t => t.SortOrder).IsRequired();

        builder.Property(t => t.CreatedByUserId).IsRequired();
        builder.Property(t => t.UpdatedByUserId);
        builder.Property(t => t.CreatedAtUtc).IsRequired();
        builder.Property(t => t.UpdatedAtUtc).IsRequired();

        // FK to workflow config — cascade delete; removing config removes all transitions
        builder.HasOne<LienWorkflowConfig>()
            .WithMany(w => w.Transitions)
            .HasForeignKey(t => t.WorkflowConfigId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to stages — restrict; stages cannot be deleted while referenced by a transition
        builder.HasOne<LienWorkflowStage>()
            .WithMany()
            .HasForeignKey(t => t.FromStageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<LienWorkflowStage>()
            .WithMany()
            .HasForeignKey(t => t.ToStageId)
            .OnDelete(DeleteBehavior.Restrict);

        // Efficient lookup: "what stages can I move to from this stage?"
        builder.HasIndex(t => new { t.WorkflowConfigId, t.FromStageId })
            .HasDatabaseName("IX_WorkflowTransitions_WorkflowId_FromStage");

        // Prevent duplicate transitions
        builder.HasIndex(t => new { t.WorkflowConfigId, t.FromStageId, t.ToStageId })
            .IsUnique()
            .HasDatabaseName("UX_WorkflowTransitions_Unique");
    }
}
