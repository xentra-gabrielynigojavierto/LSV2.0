using Comms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

#pragma warning disable CS8604
namespace Comms.Infrastructure.Persistence.Configurations;

public class MessageAttachmentConfiguration : IEntityTypeConfiguration<MessageAttachment>
{
    public void Configure(EntityTypeBuilder<MessageAttachment> builder)
    {
        builder.ToTable("comms_MessageAttachments");

        builder.HasKey(a => a.Id);

        builder.HasOne<Message>()
            .WithMany()
            .HasForeignKey(a => a.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(a => a.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(a => a.Id).IsRequired();
        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.ConversationId).IsRequired();
        builder.Property(a => a.MessageId).IsRequired();
        builder.Property(a => a.DocumentId).IsRequired();

        builder.Property(a => a.FileName)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.ContentType)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.FileSizeBytes);
        builder.Property(a => a.IsActive).IsRequired();

        builder.Property(a => a.CreatedByUserId).IsRequired();
        builder.Property(a => a.UpdatedByUserId);
        builder.Property(a => a.CreatedAtUtc).IsRequired();
        builder.Property(a => a.UpdatedAtUtc).IsRequired();

        builder.HasIndex(a => new { a.TenantId, a.MessageId })
            .HasDatabaseName("IX_MessageAttachments_TenantId_MessageId");

        builder.HasIndex(a => new { a.TenantId, a.ConversationId })
            .HasDatabaseName("IX_MessageAttachments_TenantId_ConversationId");

        builder.HasIndex(a => new { a.TenantId, a.DocumentId })
            .HasDatabaseName("IX_MessageAttachments_TenantId_DocumentId");
    }
}
