using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class LienTaskConfiguration : IEntityTypeConfiguration<LienTask>
{
    public void Configure(EntityTypeBuilder<LienTask> builder)
    {
        builder.ToTable("liens_Tasks");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).IsRequired();
        builder.Property(t => t.TenantId).IsRequired();

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(t => t.Description)
            .HasMaxLength(4000);

        builder.Property(t => t.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(t => t.Priority)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(t => t.AssignedUserId);
        builder.Property(t => t.CaseId);
        builder.Property(t => t.WorkflowStageId);
        builder.Property(t => t.DueDate);
        builder.Property(t => t.CompletedAt);
        builder.Property(t => t.ClosedByUserId);

        builder.Property(t => t.SourceType)
            .IsRequired()
            .HasMaxLength(30)
            .HasDefaultValue("MANUAL");

        builder.Property(t => t.GenerationRuleId);
        builder.Property(t => t.GeneratingTemplateId);

        // LS-LIENS-FLOW-007 — Flow instance linkage (soft reference, no FK)
        builder.Property(t => t.WorkflowInstanceId);
        builder.Property(t => t.WorkflowStepKey).HasMaxLength(200);

        builder.Property(t => t.CreatedByUserId).IsRequired();
        builder.Property(t => t.UpdatedByUserId);
        builder.Property(t => t.CreatedAtUtc).IsRequired();
        builder.Property(t => t.UpdatedAtUtc).IsRequired();

        builder.HasIndex(t => new { t.TenantId, t.Status })
            .HasDatabaseName("IX_Tasks_TenantId_Status");

        builder.HasIndex(t => new { t.TenantId, t.AssignedUserId })
            .HasDatabaseName("IX_Tasks_TenantId_AssignedUserId");

        builder.HasIndex(t => new { t.TenantId, t.CaseId })
            .HasDatabaseName("IX_Tasks_TenantId_CaseId");

        builder.HasIndex(t => new { t.TenantId, t.CreatedAtUtc })
            .HasDatabaseName("IX_Tasks_TenantId_CreatedAtUtc");

        // LS-LIENS-FLOW-007 — enables querying all tasks linked to a specific Flow instance
        builder.HasIndex(t => new { t.TenantId, t.WorkflowInstanceId })
            .HasDatabaseName("IX_Tasks_TenantId_WorkflowInstanceId");
    }
}
