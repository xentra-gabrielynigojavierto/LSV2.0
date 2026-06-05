using Comms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Comms.Infrastructure.Persistence.Configurations;

public class EmailMessageReferenceConfiguration : IEntityTypeConfiguration<EmailMessageReference>
{
    public void Configure(EntityTypeBuilder<EmailMessageReference> builder)
    {
        builder.ToTable("comms_EmailMessageReferences");

        builder.HasKey(e => e.Id);

        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(e => e.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(e => e.Id).IsRequired();
        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.ConversationId).IsRequired();
        builder.Property(e => e.MessageId);

        builder.Property(e => e.ProviderMessageId).HasMaxLength(500);
        builder.Property(e => e.InternetMessageId).IsRequired().HasMaxLength(500);
        builder.Property(e => e.InReplyToMessageId).HasMaxLength(500);
        builder.Property(e => e.ReferencesHeader).HasMaxLength(4000);
        builder.Property(e => e.ProviderThreadId).HasMaxLength(500);

        builder.Property(e => e.EmailDirection).IsRequired().HasMaxLength(20);

        builder.Property(e => e.FromEmail).IsRequired().HasMaxLength(500);
        builder.Property(e => e.FromDisplayName).HasMaxLength(500);
        builder.Property(e => e.ToAddresses).IsRequired().HasMaxLength(2000);
        builder.Property(e => e.CcAddresses).HasMaxLength(2000);
        builder.Property(e => e.Subject).IsRequired().HasMaxLength(1000);

        builder.Property(e => e.ReceivedAtUtc);
        builder.Property(e => e.SentAtUtc);
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();
        builder.Property(e => e.CreatedByUserId);
        builder.Property(e => e.UpdatedByUserId);

        builder.HasIndex(e => new { e.TenantId, e.InternetMessageId })
            .HasDatabaseName("IX_EmailRefs_TenantId_InternetMessageId")
            .IsUnique();

        builder.HasIndex(e => new { e.TenantId, e.ProviderMessageId })
            .HasDatabaseName("IX_EmailRefs_TenantId_ProviderMessageId");

        builder.HasIndex(e => new { e.TenantId, e.InReplyToMessageId })
            .HasDatabaseName("IX_EmailRefs_TenantId_InReplyToMessageId");

        builder.HasIndex(e => new { e.TenantId, e.ConversationId })
            .HasDatabaseName("IX_EmailRefs_TenantId_ConversationId");

        builder.HasIndex(e => new { e.TenantId, e.ProviderThreadId })
            .HasDatabaseName("IX_EmailRefs_TenantId_ProviderThreadId");

        builder.HasIndex(e => new { e.TenantId, e.MessageId })
            .HasDatabaseName("IX_EmailRefs_TenantId_MessageId");
    }
}
