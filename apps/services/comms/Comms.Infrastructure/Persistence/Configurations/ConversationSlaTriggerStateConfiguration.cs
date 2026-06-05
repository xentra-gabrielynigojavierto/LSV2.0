using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Comms.Domain.Entities;

namespace Comms.Infrastructure.Persistence.Configurations;

public sealed class ConversationSlaTriggerStateConfiguration : IEntityTypeConfiguration<ConversationSlaTriggerState>
{
    public void Configure(EntityTypeBuilder<ConversationSlaTriggerState> builder)
    {
        builder.ToTable("comms_ConversationSlaTriggerStates");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.ConversationId).IsRequired();
        builder.Property(e => e.CreatedByUserId).IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedByUserId).IsRequired();

        builder.Property(e => e.EvaluationVersion).IsConcurrencyToken();

        builder.HasIndex(e => e.TenantId).HasDatabaseName("IX_SlaTriggerState_TenantId");
        builder.HasIndex(e => new { e.TenantId, e.ConversationId })
            .IsUnique()
            .HasDatabaseName("IX_SlaTriggerState_TenantId_ConversationId");
        builder.HasIndex(e => new { e.TenantId, e.FirstResponseBreachSentAtUtc })
            .HasDatabaseName("IX_SlaTriggerState_TenantId_FirstResponseBreachSentAtUtc");
        builder.HasIndex(e => new { e.TenantId, e.ResolutionBreachSentAtUtc })
            .HasDatabaseName("IX_SlaTriggerState_TenantId_ResolutionBreachSentAtUtc");
    }
}
