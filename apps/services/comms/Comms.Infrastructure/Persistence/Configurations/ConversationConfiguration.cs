using Comms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Comms.Infrastructure.Persistence.Configurations;

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("comms_Conversations");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).IsRequired();
        builder.Property(c => c.TenantId).IsRequired();
        builder.Property(c => c.OrgId).IsRequired();

        builder.Property(c => c.ProductKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.ContextType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.ContextId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Subject)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(c => c.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.VisibilityType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.LastActivityAtUtc).IsRequired();

        builder.Property(c => c.CreatedByUserId).IsRequired();
        builder.Property(c => c.UpdatedByUserId);
        builder.Property(c => c.CreatedAtUtc).IsRequired();
        builder.Property(c => c.UpdatedAtUtc).IsRequired();

        builder.HasIndex(c => new { c.TenantId, c.ContextType, c.ContextId })
            .HasDatabaseName("IX_Conversations_TenantId_Context");

        builder.HasIndex(c => new { c.TenantId, c.OrgId, c.Status })
            .HasDatabaseName("IX_Conversations_TenantId_OrgId_Status");

        builder.HasIndex(c => new { c.TenantId, c.LastActivityAtUtc })
            .HasDatabaseName("IX_Conversations_TenantId_LastActivity");
    }
}
