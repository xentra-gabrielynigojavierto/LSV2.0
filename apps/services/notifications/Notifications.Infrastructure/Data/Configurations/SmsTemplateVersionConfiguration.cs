using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class SmsTemplateVersionConfiguration : IEntityTypeConfiguration<SmsTemplateVersion>
{
    public void Configure(EntityTypeBuilder<SmsTemplateVersion> b)
    {
        b.ToTable("ntf_SmsTemplateVersions");
        b.HasKey(v => v.Id);

        b.Property(v => v.Id).HasColumnType("char(36)");
        b.Property(v => v.TemplateId).IsRequired().HasColumnType("char(36)");
        b.Property(v => v.VersionNumber).HasDefaultValue(1);
        b.Property(v => v.TemplateBody).IsRequired().HasColumnType("text");
        b.Property(v => v.VariableSchemaJson).HasColumnType("text");
        b.Property(v => v.ContentClassification).IsRequired().HasMaxLength(50).HasDefaultValue("transactional");
        b.Property(v => v.ApprovalStatus).IsRequired().HasMaxLength(30).HasDefaultValue("draft");
        b.Property(v => v.ApprovedBy).HasMaxLength(200);
        b.Property(v => v.RejectionReason).HasColumnType("text");
        b.Property(v => v.CreatedBy).HasMaxLength(200);

        b.HasIndex(v => new { v.TemplateId, v.VersionNumber })
            .HasDatabaseName("IX_ntf_SmsTemplateVersions_Template_Version");

        b.HasIndex(v => v.ApprovalStatus)
            .HasDatabaseName("IX_ntf_SmsTemplateVersions_ApprovalStatus");

        b.HasIndex(v => v.ApprovedAt)
            .HasDatabaseName("IX_ntf_SmsTemplateVersions_ApprovedAt");
    }
}
