using CareConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareConnect.Infrastructure.Data.Configurations;

public class CareConnectNotificationConfiguration : IEntityTypeConfiguration<CareConnectNotification>
{
    public void Configure(EntityTypeBuilder<CareConnectNotification> builder)
    {
        builder.ToTable("cc_CareConnectNotifications");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id).IsRequired();
        builder.Property(n => n.TenantId).IsRequired();
        builder.Property(n => n.NotificationType).IsRequired().HasMaxLength(50);
        builder.Property(n => n.RelatedEntityType).IsRequired().HasMaxLength(50);
        builder.Property(n => n.RelatedEntityId).IsRequired();
        builder.Property(n => n.RecipientType).IsRequired().HasMaxLength(50);
        builder.Property(n => n.RecipientAddress).HasMaxLength(500);
        builder.Property(n => n.Subject).HasMaxLength(500);
        builder.Property(n => n.Message).HasMaxLength(4000);
        builder.Property(n => n.Status).IsRequired().HasMaxLength(20);
        builder.Property(n => n.ScheduledForUtc);
        builder.Property(n => n.SentAtUtc);
        builder.Property(n => n.FailedAtUtc);
        builder.Property(n => n.FailureReason).HasMaxLength(1000);
        builder.Property(n => n.CreatedAtUtc).IsRequired();
        builder.Property(n => n.UpdatedAtUtc).IsRequired();
        builder.Property(n => n.CreatedByUserId);
        builder.Property(n => n.UpdatedByUserId);

        builder.Property(n => n.DedupeKey).HasMaxLength(500);

        builder.HasIndex(n => new { n.TenantId, n.Status, n.ScheduledForUtc });
        builder.HasIndex(n => new { n.TenantId, n.RelatedEntityType, n.RelatedEntityId });
        builder.HasIndex(n => new { n.TenantId, n.NotificationType });
        builder.HasIndex(n => n.DedupeKey)
            .IsUnique()
            .HasDatabaseName("IX_CareConnectNotifications_DedupeKey");
    }
}
