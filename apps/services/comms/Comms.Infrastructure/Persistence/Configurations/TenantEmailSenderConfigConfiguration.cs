using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Comms.Domain.Entities;

namespace Comms.Infrastructure.Persistence.Configurations;

public class TenantEmailSenderConfigConfiguration : IEntityTypeConfiguration<TenantEmailSenderConfig>
{
    public void Configure(EntityTypeBuilder<TenantEmailSenderConfig> builder)
    {
        builder.ToTable("comms_TenantEmailSenderConfigs");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.DisplayName).IsRequired().HasMaxLength(256);
        builder.Property(e => e.FromEmail).IsRequired().HasMaxLength(512);
        builder.Property(e => e.ReplyToEmail).HasMaxLength(512);
        builder.Property(e => e.SenderType).IsRequired().HasMaxLength(50);
        builder.Property(e => e.VerificationStatus).IsRequired().HasMaxLength(20);

        builder.HasIndex(e => new { e.TenantId, e.FromEmail })
            .HasDatabaseName("IX_SenderConfigs_TenantId_FromEmail");

        builder.HasIndex(e => new { e.TenantId, e.IsDefault })
            .HasDatabaseName("IX_SenderConfigs_TenantId_IsDefault");

        builder.HasIndex(e => new { e.TenantId, e.SenderType })
            .HasDatabaseName("IX_SenderConfigs_TenantId_SenderType");
    }
}
