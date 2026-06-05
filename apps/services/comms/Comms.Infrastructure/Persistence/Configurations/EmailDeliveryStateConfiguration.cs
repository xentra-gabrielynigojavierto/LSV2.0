using Comms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Comms.Infrastructure.Persistence.Configurations;

public class EmailDeliveryStateConfiguration : IEntityTypeConfiguration<EmailDeliveryState>
{
    public void Configure(EntityTypeBuilder<EmailDeliveryState> builder)
    {
        builder.ToTable("comms_EmailDeliveryStates");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).IsRequired();
        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.ConversationId).IsRequired();
        builder.Property(e => e.MessageId).IsRequired();
        builder.Property(e => e.EmailMessageReferenceId).IsRequired();

        builder.Property(e => e.DeliveryStatus).IsRequired().HasMaxLength(50);
        builder.Property(e => e.ProviderName).HasMaxLength(100);
        builder.Property(e => e.ProviderMessageId).HasMaxLength(500);
        builder.Property(e => e.NotificationsRequestId).HasMaxLength(100);
        builder.Property(e => e.LastStatusAtUtc);
        builder.Property(e => e.LastErrorCode).HasMaxLength(100);
        builder.Property(e => e.LastErrorMessage).HasMaxLength(2000);
        builder.Property(e => e.RetryCount);

        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();
        builder.Property(e => e.CreatedByUserId);
        builder.Property(e => e.UpdatedByUserId);

        builder.HasOne<EmailMessageReference>()
            .WithMany()
            .HasForeignKey(e => e.EmailMessageReferenceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.TenantId, e.EmailMessageReferenceId })
            .HasDatabaseName("IX_EmailDelivery_TenantId_EmailMessageReferenceId");

        builder.HasIndex(e => new { e.TenantId, e.ProviderMessageId })
            .HasDatabaseName("IX_EmailDelivery_TenantId_ProviderMessageId");

        builder.HasIndex(e => new { e.TenantId, e.ConversationId })
            .HasDatabaseName("IX_EmailDelivery_TenantId_ConversationId");

        builder.HasIndex(e => new { e.TenantId, e.NotificationsRequestId })
            .HasDatabaseName("IX_EmailDelivery_TenantId_NotificationsRequestId");
    }
}
