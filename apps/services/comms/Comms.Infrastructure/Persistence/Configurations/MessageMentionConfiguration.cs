using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Comms.Domain.Entities;

namespace Comms.Infrastructure.Persistence.Configurations;

public class MessageMentionConfiguration : IEntityTypeConfiguration<MessageMention>
{
    public void Configure(EntityTypeBuilder<MessageMention> builder)
    {
        builder.ToTable("sc_MessageMentions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.ConversationId).IsRequired();
        builder.Property(e => e.MessageId).IsRequired();
        builder.Property(e => e.MentionedUserId).IsRequired();
        builder.Property(e => e.MentionedByUserId).IsRequired();
        builder.Property(e => e.IsMentionedUserParticipant).IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.MessageId })
            .HasDatabaseName("IX_MessageMentions_TenantId_MessageId");

        builder.HasIndex(e => new { e.TenantId, e.ConversationId })
            .HasDatabaseName("IX_MessageMentions_TenantId_ConversationId");

        builder.HasIndex(e => new { e.TenantId, e.MentionedUserId })
            .HasDatabaseName("IX_MessageMentions_TenantId_MentionedUserId");

        builder.HasIndex(e => new { e.TenantId, e.MessageId, e.MentionedUserId })
            .IsUnique()
            .HasDatabaseName("IX_MessageMentions_UniquePerMessageUser");
    }
}
