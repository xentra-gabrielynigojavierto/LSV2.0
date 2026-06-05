using Comms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

#pragma warning disable CS8604
namespace Comms.Infrastructure.Persistence.Configurations;

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("comms_Messages");

        builder.HasKey(m => m.Id);

        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(m => m.Id).IsRequired();
        builder.Property(m => m.ConversationId).IsRequired();
        builder.Property(m => m.TenantId).IsRequired();
        builder.Property(m => m.OrgId).IsRequired();

        builder.Property(m => m.Channel)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(m => m.Direction)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(m => m.Body)
            .IsRequired()
            .HasMaxLength(10000);

        builder.Property(m => m.BodyPlainText)
            .HasMaxLength(10000);

        builder.Property(m => m.VisibilityType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(m => m.SentAtUtc).IsRequired();

        builder.Property(m => m.SenderUserId);

        builder.Property(m => m.SenderParticipantType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(m => m.ExternalSenderName)
            .HasMaxLength(200);

        builder.Property(m => m.ExternalSenderEmail)
            .HasMaxLength(320);

        builder.Property(m => m.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(m => m.CreatedByUserId).IsRequired();
        builder.Property(m => m.UpdatedByUserId);
        builder.Property(m => m.CreatedAtUtc).IsRequired();
        builder.Property(m => m.UpdatedAtUtc).IsRequired();

        builder.HasIndex(m => new { m.TenantId, m.ConversationId, m.SentAtUtc })
            .HasDatabaseName("IX_Messages_TenantId_ConversationId_SentAt");

        builder.HasIndex(m => new { m.TenantId, m.ConversationId })
            .HasDatabaseName("IX_Messages_TenantId_ConversationId");
    }
}
