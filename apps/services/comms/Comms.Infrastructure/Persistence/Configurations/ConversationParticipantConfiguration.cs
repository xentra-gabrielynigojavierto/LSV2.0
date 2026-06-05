using Comms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

#pragma warning disable CS8604
namespace Comms.Infrastructure.Persistence.Configurations;

public class ConversationParticipantConfiguration : IEntityTypeConfiguration<ConversationParticipant>
{
    public void Configure(EntityTypeBuilder<ConversationParticipant> builder)
    {
        builder.ToTable("comms_ConversationParticipants");

        builder.HasKey(p => p.Id);

        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(p => p.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(p => p.Id).IsRequired();
        builder.Property(p => p.ConversationId).IsRequired();
        builder.Property(p => p.TenantId).IsRequired();
        builder.Property(p => p.OrgId).IsRequired();

        builder.Property(p => p.ParticipantType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.UserId);

        builder.Property(p => p.ExternalName)
            .HasMaxLength(200);

        builder.Property(p => p.ExternalEmail)
            .HasMaxLength(320);

        builder.Property(p => p.Role)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.CanReply).IsRequired();
        builder.Property(p => p.IsActive).IsRequired();
        builder.Property(p => p.JoinedAtUtc).IsRequired();

        builder.Property(p => p.CreatedByUserId).IsRequired();
        builder.Property(p => p.UpdatedByUserId);
        builder.Property(p => p.CreatedAtUtc).IsRequired();
        builder.Property(p => p.UpdatedAtUtc).IsRequired();

        builder.HasIndex(p => new { p.TenantId, p.ConversationId, p.IsActive })
            .HasDatabaseName("IX_Participants_TenantId_ConversationId_Active");

        builder.HasIndex(p => new { p.TenantId, p.UserId, p.IsActive })
            .HasDatabaseName("IX_Participants_TenantId_UserId_Active");

        builder.HasIndex(p => new { p.ConversationId, p.UserId, p.IsActive })
            .HasDatabaseName("IX_Participants_ConversationId_UserId_IsActive");
    }
}
