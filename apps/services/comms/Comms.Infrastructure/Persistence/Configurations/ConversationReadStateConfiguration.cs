using Comms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

#pragma warning disable CS8604
namespace Comms.Infrastructure.Persistence.Configurations;

public class ConversationReadStateConfiguration : IEntityTypeConfiguration<ConversationReadState>
{
    public void Configure(EntityTypeBuilder<ConversationReadState> builder)
    {
        builder.ToTable("comms_ConversationReadStates");

        builder.HasKey(r => r.Id);

        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(r => r.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(r => r.Id).IsRequired();
        builder.Property(r => r.TenantId).IsRequired();
        builder.Property(r => r.ConversationId).IsRequired();
        builder.Property(r => r.UserId).IsRequired();
        builder.Property(r => r.LastReadMessageId);
        builder.Property(r => r.LastReadAtUtc);

        builder.Property(r => r.CreatedByUserId).IsRequired();
        builder.Property(r => r.UpdatedByUserId);
        builder.Property(r => r.CreatedAtUtc).IsRequired();
        builder.Property(r => r.UpdatedAtUtc).IsRequired();

        builder.HasIndex(r => new { r.TenantId, r.ConversationId, r.UserId })
            .IsUnique()
            .HasDatabaseName("IX_ReadStates_TenantId_ConversationId_UserId");
    }
}
