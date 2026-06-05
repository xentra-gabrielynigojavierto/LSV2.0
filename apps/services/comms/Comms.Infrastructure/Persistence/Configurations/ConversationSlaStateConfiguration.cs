using Comms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Comms.Infrastructure.Persistence.Configurations;

public class ConversationSlaStateConfiguration : IEntityTypeConfiguration<ConversationSlaState>
{
    public void Configure(EntityTypeBuilder<ConversationSlaState> builder)
    {
        builder.ToTable("comms_ConversationSlaStates");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).IsRequired();
        builder.Property(s => s.TenantId).IsRequired();
        builder.Property(s => s.ConversationId).IsRequired();

        builder.Property(s => s.Priority)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.FirstResponseDueAtUtc);
        builder.Property(s => s.ResolutionDueAtUtc);
        builder.Property(s => s.FirstResponseAtUtc);
        builder.Property(s => s.ResolvedAtUtc);
        builder.Property(s => s.BreachedFirstResponse).IsRequired();
        builder.Property(s => s.BreachedResolution).IsRequired();

        builder.Property(s => s.WaitingOn)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.LastEvaluatedAtUtc);
        builder.Property(s => s.SlaStartedAtUtc).IsRequired();

        builder.Property(s => s.CreatedByUserId).IsRequired();
        builder.Property(s => s.UpdatedByUserId);
        builder.Property(s => s.CreatedAtUtc).IsRequired();
        builder.Property(s => s.UpdatedAtUtc).IsRequired();

        builder.HasIndex(s => new { s.TenantId, s.ConversationId })
            .IsUnique()
            .HasDatabaseName("IX_SlaState_TenantId_ConversationId");

        builder.HasIndex(s => new { s.TenantId, s.BreachedFirstResponse })
            .HasDatabaseName("IX_SlaState_TenantId_BreachedFirstResponse");

        builder.HasIndex(s => new { s.TenantId, s.BreachedResolution })
            .HasDatabaseName("IX_SlaState_TenantId_BreachedResolution");

        builder.HasIndex(s => new { s.TenantId, s.Priority })
            .HasDatabaseName("IX_SlaState_TenantId_Priority");
    }
}
