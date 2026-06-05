using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("idt_AuditLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.ActorName)
            .IsRequired()
            .HasMaxLength(320);

        builder.Property(a => a.ActorType)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.EntityType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.EntityId)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.MetadataJson)
            .HasMaxLength(4000);

        builder.Property(a => a.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(a => a.CreatedAtUtc);
        builder.HasIndex(a => a.ActorType);
        builder.HasIndex(a => a.EntityType);
    }
}
