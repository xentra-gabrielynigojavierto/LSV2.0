using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class TemplateConfiguration : IEntityTypeConfiguration<Template>
{
    public void Configure(EntityTypeBuilder<Template> builder)
    {
        builder.ToTable("ntf_Templates");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.TemplateKey).HasMaxLength(200);
        builder.Property(e => e.Channel).HasMaxLength(20);
        builder.Property(e => e.Name).HasMaxLength(200);
        builder.Property(e => e.Description).HasColumnType("text");
        builder.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("active");
        builder.Property(e => e.Scope).HasMaxLength(20).HasDefaultValue("tenant");
        builder.Property(e => e.ProductType).HasMaxLength(50);

        builder.HasIndex(e => new { e.TemplateKey, e.Channel, e.TenantId })
            .HasDatabaseName("IX_Templates_TemplateKey_Channel_TenantId");
    }
}

public class TemplateVersionConfiguration : IEntityTypeConfiguration<TemplateVersion>
{
    public void Configure(EntityTypeBuilder<TemplateVersion> builder)
    {
        builder.ToTable("ntf_TemplateVersions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.VersionNumber).HasDefaultValue(1);
        builder.Property(e => e.SubjectTemplate).HasColumnType("text");
        builder.Property(e => e.BodyTemplate).HasColumnType("longtext");
        builder.Property(e => e.TextTemplate).HasColumnType("text");
        builder.Property(e => e.EditorType).HasMaxLength(20);
        builder.Property(e => e.IsPublished).HasDefaultValue(false);
        builder.Property(e => e.PublishedBy).HasMaxLength(255);

        builder.HasIndex(e => e.TemplateId).HasDatabaseName("IX_TemplateVersions_TemplateId");
    }
}
