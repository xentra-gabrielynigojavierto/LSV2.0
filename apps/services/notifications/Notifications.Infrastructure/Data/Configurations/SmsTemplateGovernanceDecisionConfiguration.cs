using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class SmsTemplateGovernanceDecisionConfiguration : IEntityTypeConfiguration<SmsTemplateGovernanceDecision>
{
    public void Configure(EntityTypeBuilder<SmsTemplateGovernanceDecision> b)
    {
        b.ToTable("ntf_SmsTemplateGovernanceDecisions");
        b.HasKey(d => d.Id);

        b.Property(d => d.Id).HasColumnType("char(36)");
        b.Property(d => d.NotificationId).HasColumnType("char(36)");
        b.Property(d => d.AttemptId).HasColumnType("char(36)");
        b.Property(d => d.TemplateId).HasColumnType("char(36)");
        b.Property(d => d.TemplateVersionId).HasColumnType("char(36)");
        b.Property(d => d.TenantId).HasColumnType("char(36)");
        b.Property(d => d.DecisionType).IsRequired().HasMaxLength(30).HasDefaultValue("allow");
        b.Property(d => d.ReasonCode).IsRequired().HasMaxLength(80);
        b.Property(d => d.ContentClassification).HasMaxLength(50);
        b.Property(d => d.VariableValidationPassed).HasDefaultValue(true);
        b.Property(d => d.DecisionMetadataJson).HasColumnType("text");

        b.HasIndex(d => new { d.TenantId, d.CreatedAt })
            .HasDatabaseName("IX_ntf_SmsTemplateGovDecisions_Tenant_Dt");

        b.HasIndex(d => new { d.DecisionType, d.CreatedAt })
            .HasDatabaseName("IX_ntf_SmsTemplateGovDecisions_DecisionType_Dt");

        b.HasIndex(d => new { d.ReasonCode, d.CreatedAt })
            .HasDatabaseName("IX_ntf_SmsTemplateGovDecisions_ReasonCode_Dt");

        b.HasIndex(d => d.TemplateId)
            .HasDatabaseName("IX_ntf_SmsTemplateGovDecisions_TemplateId");
    }
}
