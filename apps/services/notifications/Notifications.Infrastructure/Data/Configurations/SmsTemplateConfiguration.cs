using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class SmsTemplateConfiguration : IEntityTypeConfiguration<SmsTemplate>
{
    public void Configure(EntityTypeBuilder<SmsTemplate> b)
    {
        b.ToTable("ntf_SmsTemplates");
        b.HasKey(t => t.Id);

        b.Property(t => t.Id).HasColumnType("char(36)");
        b.Property(t => t.TenantId).HasColumnType("char(36)");
        b.Property(t => t.TemplateKey).IsRequired().HasMaxLength(200);
        b.Property(t => t.Name).IsRequired().HasMaxLength(200);
        b.Property(t => t.Description).HasColumnType("text");
        b.Property(t => t.Category).HasMaxLength(100);
        b.Property(t => t.Status).IsRequired().HasMaxLength(30).HasDefaultValue("draft");
        b.Property(t => t.CurrentVersion).HasDefaultValue(0);
        b.Property(t => t.ContentClassification).IsRequired().HasMaxLength(50).HasDefaultValue("transactional");
        b.Property(t => t.RequiresApproval).HasDefaultValue(true);
        b.Property(t => t.Enabled).HasDefaultValue(true);
        b.Property(t => t.CreatedBy).HasMaxLength(200);
        b.Property(t => t.UpdatedBy).HasMaxLength(200);

        b.HasIndex(t => new { t.TenantId, t.TemplateKey })
            .HasDatabaseName("IX_ntf_SmsTemplates_Tenant_Key")
            .IsUnique();

        b.HasIndex(t => new { t.Status, t.Enabled })
            .HasDatabaseName("IX_ntf_SmsTemplates_Status_Enabled");

        b.HasIndex(t => t.ContentClassification)
            .HasDatabaseName("IX_ntf_SmsTemplates_Classification");
    }
}
