using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class NotificationEventConfiguration : IEntityTypeConfiguration<NotificationEvent>
{
    public void Configure(EntityTypeBuilder<NotificationEvent> builder)
    {
        builder.ToTable("ntf_NotificationEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Provider).HasMaxLength(50);
        builder.Property(e => e.Channel).HasMaxLength(20);
        builder.Property(e => e.RawEventType).HasMaxLength(100);
        builder.Property(e => e.NormalizedEventType).HasMaxLength(50);
        builder.Property(e => e.ProviderMessageId).HasMaxLength(500);
        builder.Property(e => e.MetadataJson).HasColumnType("text");
        builder.Property(e => e.DedupKey).HasMaxLength(500);

        builder.HasIndex(e => e.DedupKey).HasDatabaseName("UX_NotificationEvents_DedupKey").IsUnique();
        builder.HasIndex(e => e.NotificationId).HasDatabaseName("IX_NotificationEvents_NotificationId");
    }
}

public class RecipientContactHealthConfiguration : IEntityTypeConfiguration<RecipientContactHealth>
{
    public void Configure(EntityTypeBuilder<RecipientContactHealth> builder)
    {
        builder.ToTable("ntf_RecipientContactHealth");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Channel).HasMaxLength(20);
        builder.Property(e => e.ContactValue).HasMaxLength(500);
        builder.Property(e => e.HealthStatus).HasMaxLength(30).HasDefaultValue("valid");
        builder.Property(e => e.BounceCount).HasDefaultValue(0);
        builder.Property(e => e.ComplaintCount).HasDefaultValue(0);
        builder.Property(e => e.DeliveryCount).HasDefaultValue(0);
        builder.Property(e => e.LastRawEventType).HasMaxLength(100);

        builder.HasIndex(e => new { e.TenantId, e.Channel, e.ContactValue })
            .HasDatabaseName("UX_RecipientContactHealth_TenantId_Channel_ContactValue")
            .IsUnique();
    }
}

public class DeliveryIssueConfiguration : IEntityTypeConfiguration<DeliveryIssue>
{
    public void Configure(EntityTypeBuilder<DeliveryIssue> builder)
    {
        builder.ToTable("ntf_DeliveryIssues");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Channel).HasMaxLength(20);
        builder.Property(e => e.Provider).HasMaxLength(50);
        builder.Property(e => e.IssueType).HasMaxLength(50);
        builder.Property(e => e.RecommendedAction).HasColumnType("text");
        builder.Property(e => e.DetailsJson).HasColumnType("text");
        builder.Property(e => e.IsResolved).HasDefaultValue(false);

        builder.HasIndex(e => new { e.TenantId, e.NotificationId }).HasDatabaseName("IX_DeliveryIssues_TenantId_NotificationId");
    }
}

public class ContactSuppressionConfiguration : IEntityTypeConfiguration<ContactSuppression>
{
    public void Configure(EntityTypeBuilder<ContactSuppression> builder)
    {
        builder.ToTable("ntf_ContactSuppressions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Channel).HasMaxLength(20);
        builder.Property(e => e.ContactValue).HasMaxLength(500);
        builder.Property(e => e.SuppressionType).HasMaxLength(50);
        builder.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("active");
        builder.Property(e => e.Reason).HasColumnType("text");
        builder.Property(e => e.Source).HasMaxLength(50);
        builder.Property(e => e.CreatedBy).HasMaxLength(255);
        builder.Property(e => e.Notes).HasColumnType("text");

        builder.HasIndex(e => new { e.TenantId, e.Channel, e.ContactValue })
            .HasDatabaseName("IX_ContactSuppressions_TenantId_Channel_ContactValue");
    }
}
