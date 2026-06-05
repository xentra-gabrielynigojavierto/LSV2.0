using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Task.Domain.Entities;

namespace Task.Infrastructure.Persistence.Configurations;

public class PlatformTaskConfiguration : IEntityTypeConfiguration<PlatformTask>
{
    public void Configure(EntityTypeBuilder<PlatformTask> builder)
    {
        builder.ToTable("tasks_Tasks");
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
            .HasMaxLength(30)
            .HasDefaultValue("OPEN");

        builder.Property(t => t.Priority)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("MEDIUM");

        builder.Property(t => t.Scope)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("GENERAL");

        builder.Property(t => t.AssignedUserId);

        builder.Property(t => t.SourceProductCode).HasMaxLength(50);
        builder.Property(t => t.SourceEntityType).HasMaxLength(100);
        builder.Property(t => t.SourceEntityId);

        // TASK-B04-01 — generation traceability columns
        builder.Property(t => t.GenerationRuleId);
        builder.Property(t => t.GeneratingTemplateId);

        builder.Property(t => t.CurrentStageId);

        // Flow linkage fields
        builder.Property(t => t.WorkflowInstanceId);
        builder.Property(t => t.WorkflowStepKey).HasMaxLength(100);
        builder.Property(t => t.WorkflowLinkageChangedAt);

        builder.Property(t => t.DueAt);
        builder.Property(t => t.CompletedAt);
        builder.Property(t => t.ClosedByUserId);

        // TASK-FLOW-02 — Flow queue assignment metadata
        builder.Property(t => t.AssignmentMode).HasMaxLength(20);
        builder.Property(t => t.AssignedRole).HasMaxLength(100);
        builder.Property(t => t.AssignedOrgId).HasMaxLength(100);
        builder.Property(t => t.AssignedAt);
        builder.Property(t => t.AssignedBy).HasMaxLength(100);
        builder.Property(t => t.AssignmentReason).HasMaxLength(500);

        // TASK-FLOW-02 — Additional lifecycle timestamps
        builder.Property(t => t.StartedAt);
        builder.Property(t => t.CancelledAt);

        // TASK-FLOW-02 — SLA state (pushed from Flow SLA evaluator)
        builder.Property(t => t.SlaStatus)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("OnTrack");
        builder.Property(t => t.SlaBreachedAt);
        builder.Property(t => t.LastSlaEvaluatedAt);

        builder.Property(t => t.CreatedByUserId).IsRequired();
        builder.Property(t => t.UpdatedByUserId);
        builder.Property(t => t.CreatedAtUtc).IsRequired();
        builder.Property(t => t.UpdatedAtUtc).IsRequired();

        // Existing indexes
        builder.HasIndex(t => new { t.TenantId, t.Status })
            .HasDatabaseName("IX_Tasks_TenantId_Status");

        builder.HasIndex(t => new { t.TenantId, t.AssignedUserId })
            .HasDatabaseName("IX_Tasks_TenantId_AssignedUserId");

        builder.HasIndex(t => new { t.TenantId, t.Scope, t.SourceProductCode })
            .HasDatabaseName("IX_Tasks_TenantId_Scope_Product");

        builder.HasIndex(t => new { t.TenantId, t.CreatedAtUtc })
            .HasDatabaseName("IX_Tasks_TenantId_CreatedAt");

        builder.HasIndex(t => new { t.TenantId, t.CurrentStageId })
            .HasDatabaseName("IX_Tasks_TenantId_StageId");

        // New indexes for B03 query paths
        builder.HasIndex(t => t.WorkflowInstanceId)
            .HasDatabaseName("IX_Tasks_WorkflowInstanceId");

        builder.HasIndex(t => new { t.TenantId, t.SourceEntityType, t.SourceEntityId })
            .HasDatabaseName("IX_Tasks_SourceEntity");

        builder.HasIndex(t => new { t.TenantId, t.AssignedUserId, t.Status })
            .HasDatabaseName("IX_Tasks_TenantId_AssignedUser_Status");

        // TASK-B04-01 — supports duplicate-prevention queries from LienTaskGenerationEngine
        builder.HasIndex(t => new { t.TenantId, t.SourceProductCode, t.GenerationRuleId })
            .HasDatabaseName("IX_Tasks_TenantId_Product_GenerationRule");

        builder.HasIndex(t => new { t.TenantId, t.SourceProductCode, t.GeneratingTemplateId })
            .HasDatabaseName("IX_Tasks_TenantId_Product_GeneratingTemplate");

        // TASK-FLOW-02 — queue read indexes for role/org queue filtering
        builder.HasIndex(t => new { t.TenantId, t.AssignmentMode, t.AssignedRole })
            .HasDatabaseName("IX_Tasks_TenantId_AssignmentMode_Role");

        builder.HasIndex(t => new { t.TenantId, t.AssignmentMode, t.AssignedOrgId })
            .HasDatabaseName("IX_Tasks_TenantId_AssignmentMode_Org");

        // TASK-FLOW-04 — analytics query indexes
        builder.HasIndex(t => new { t.TenantId, t.Status, t.SlaStatus })
            .HasDatabaseName("IX_Tasks_TenantId_Status_SlaStatus");

        builder.HasIndex(t => new { t.TenantId, t.SlaBreachedAt })
            .HasDatabaseName("IX_Tasks_TenantId_SlaBreachedAt");

        builder.HasIndex(t => new { t.TenantId, t.CompletedAt })
            .HasDatabaseName("IX_Tasks_TenantId_CompletedAt");

        builder.HasIndex(t => new { t.TenantId, t.AssignedAt })
            .HasDatabaseName("IX_Tasks_TenantId_AssignedAt");
    }
}
