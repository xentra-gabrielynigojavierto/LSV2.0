using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Comms.Domain.Entities;

namespace Comms.Infrastructure.Persistence.Configurations;

public class EmailTemplateConfigConfiguration : IEntityTypeConfiguration<EmailTemplateConfig>
{
    public void Configure(EntityTypeBuilder<EmailTemplateConfig> builder)
    {
        builder.ToTable("comms_EmailTemplateConfigs");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TemplateKey).IsRequired().HasMaxLength(128);
        builder.Property(e => e.DisplayName).IsRequired().HasMaxLength(256);
        builder.Property(e => e.SubjectTemplate).HasMaxLength(1024);
        builder.Property(e => e.BodyTextTemplate).HasColumnType("TEXT");
        builder.Property(e => e.BodyHtmlTemplate).HasColumnType("TEXT");
        builder.Property(e => e.TemplateScope).IsRequired().HasMaxLength(20);

        builder.HasIndex(e => new { e.TenantId, e.TemplateKey })
            .HasDatabaseName("IX_Templates_TenantId_TemplateKey");

        builder.HasIndex(e => new { e.TemplateScope, e.TemplateKey })
            .HasDatabaseName("IX_Templates_TemplateScope_TemplateKey");
    }
}
