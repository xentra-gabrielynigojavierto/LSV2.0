using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Comms.Domain.Entities;

namespace Comms.Infrastructure.Persistence.Configurations;

public sealed class ConversationTimelineEntryConfiguration : IEntityTypeConfiguration<ConversationTimelineEntry>
{
    public void Configure(EntityTypeBuilder<ConversationTimelineEntry> builder)
    {
        builder.ToTable("comms_ConversationTimelineEntries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.ConversationId).IsRequired();
        builder.Property(e => e.EventType).IsRequired().HasMaxLength(50);
        builder.Property(e => e.EventSubType).HasMaxLength(50);
        builder.Property(e => e.ActorType).IsRequired().HasMaxLength(20);
        builder.Property(e => e.ActorDisplayName).HasMaxLength(200);
        builder.Property(e => e.OccurredAtUtc).IsRequired();
        builder.Property(e => e.Summary).IsRequired().HasMaxLength(500);
        builder.Property(e => e.MetadataJson).HasMaxLength(4000);
        builder.Property(e => e.Visibility).IsRequired().HasMaxLength(30);
        builder.Property(e => e.CreatedAtUtc).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.ConversationId, e.OccurredAtUtc })
            .HasDatabaseName("IX_Timeline_TenantId_ConversationId_OccurredAtUtc");

        builder.HasIndex(e => new { e.TenantId, e.ConversationId, e.EventType })
            .HasDatabaseName("IX_Timeline_TenantId_ConversationId_EventType");

        builder.HasIndex(e => new { e.TenantId, e.ConversationId, e.Visibility })
            .HasDatabaseName("IX_Timeline_TenantId_ConversationId_Visibility");
    }
}
