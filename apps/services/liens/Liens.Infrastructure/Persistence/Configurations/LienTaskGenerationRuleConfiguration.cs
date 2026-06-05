using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class LienTaskGenerationRuleConfiguration : IEntityTypeConfiguration<LienTaskGenerationRule>
{
    public void Configure(EntityTypeBuilder<LienTaskGenerationRule> builder)
    {
        builder.ToTable("liens_TaskGenerationRules");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).IsRequired();
        builder.Property(r => r.TenantId).IsRequired();

        builder.Property(r => r.ProductCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.Description)
            .HasMaxLength(1000);

        builder.Property(r => r.EventType)
            .IsRequired()
            .HasMaxLength(60);

        builder.Property(r => r.TaskTemplateId).IsRequired();

        builder.Property(r => r.ContextType)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(r => r.ApplicableWorkflowStageId);

        builder.Property(r => r.DuplicatePreventionMode)
            .IsRequired()
            .HasMaxLength(60);

        builder.Property(r => r.AssignmentMode)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(r => r.DueDateMode)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(r => r.DueDateOffsetDays);

        builder.Property(r => r.IsActive).IsRequired();
        builder.Property(r => r.Version).IsRequired();
        builder.Property(r => r.LastUpdatedAt).IsRequired();
        builder.Property(r => r.LastUpdatedByUserId);

        builder.Property(r => r.LastUpdatedByName)
            .HasMaxLength(200);

        builder.Property(r => r.LastUpdatedSource)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(r => r.CreatedByUserId);
        builder.Property(r => r.UpdatedByUserId);
        builder.Property(r => r.CreatedAtUtc).IsRequired();
        builder.Property(r => r.UpdatedAtUtc).IsRequired();

        builder.HasIndex(r => new { r.TenantId, r.EventType })
            .HasDatabaseName("IX_TaskGenerationRules_TenantId_EventType");

        builder.HasIndex(r => new { r.TenantId, r.IsActive })
            .HasDatabaseName("IX_TaskGenerationRules_TenantId_IsActive");
    }
}
