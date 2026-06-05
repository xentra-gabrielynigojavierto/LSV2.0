using Comms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Comms.Infrastructure.Persistence.Configurations;

public class ConversationQueueConfiguration : IEntityTypeConfiguration<ConversationQueue>
{
    public void Configure(EntityTypeBuilder<ConversationQueue> builder)
    {
        builder.ToTable("comms_ConversationQueues");

        builder.HasKey(q => q.Id);

        builder.Property(q => q.Id).IsRequired();
        builder.Property(q => q.TenantId).IsRequired();

        builder.Property(q => q.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(q => q.Code)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(q => q.Description)
            .HasMaxLength(1000);

        builder.Property(q => q.IsDefault).IsRequired();
        builder.Property(q => q.IsActive).IsRequired();

        builder.Property(q => q.CreatedByUserId).IsRequired();
        builder.Property(q => q.UpdatedByUserId);
        builder.Property(q => q.CreatedAtUtc).IsRequired();
        builder.Property(q => q.UpdatedAtUtc).IsRequired();

        builder.HasIndex(q => new { q.TenantId, q.Code })
            .IsUnique()
            .HasDatabaseName("IX_Queues_TenantId_Code");

        builder.HasIndex(q => new { q.TenantId, q.IsDefault })
            .HasDatabaseName("IX_Queues_TenantId_IsDefault");
    }
}
