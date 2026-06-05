using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Comms.Domain.Entities;

namespace Comms.Infrastructure.Persistence.Configurations;

public sealed class QueueEscalationConfigConfiguration : IEntityTypeConfiguration<QueueEscalationConfig>
{
    public void Configure(EntityTypeBuilder<QueueEscalationConfig> builder)
    {
        builder.ToTable("comms_QueueEscalationConfigs");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.QueueId).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();
        builder.Property(e => e.CreatedByUserId).IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedByUserId).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.QueueId })
            .IsUnique()
            .HasDatabaseName("IX_QueueEscalationConfig_TenantId_QueueId");
    }
}
