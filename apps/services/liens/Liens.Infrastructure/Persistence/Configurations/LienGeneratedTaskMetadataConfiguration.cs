using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class LienGeneratedTaskMetadataConfiguration : IEntityTypeConfiguration<LienGeneratedTaskMetadata>
{
    public void Configure(EntityTypeBuilder<LienGeneratedTaskMetadata> builder)
    {
        builder.ToTable("liens_GeneratedTaskMetadata");

        builder.HasKey(m => m.TaskId);

        builder.Property(m => m.TaskId).IsRequired();
        builder.Property(m => m.TenantId).IsRequired();
        builder.Property(m => m.GenerationRuleId).IsRequired();
        builder.Property(m => m.TaskTemplateId).IsRequired();

        builder.Property(m => m.TriggerEventType)
            .IsRequired()
            .HasMaxLength(60);

        builder.Property(m => m.TriggerEntityType)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(m => m.TriggerEntityId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(m => m.SourceType)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(m => m.GeneratedAt).IsRequired();

        builder.HasIndex(m => new { m.TenantId, m.GenerationRuleId })
            .HasDatabaseName("IX_GeneratedTaskMetadata_TenantId_RuleId");
    }
}
