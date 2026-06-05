using Comms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Comms.Infrastructure.Persistence.Configurations;

public class ConversationAssignmentConfiguration : IEntityTypeConfiguration<ConversationAssignment>
{
    public void Configure(EntityTypeBuilder<ConversationAssignment> builder)
    {
        builder.ToTable("comms_ConversationAssignments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).IsRequired();
        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.ConversationId).IsRequired();
        builder.Property(a => a.QueueId);
        builder.Property(a => a.AssignedUserId);
        builder.Property(a => a.AssignedByUserId);

        builder.Property(a => a.AssignmentStatus)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.AssignedAtUtc).IsRequired();
        builder.Property(a => a.LastAssignedAtUtc).IsRequired();
        builder.Property(a => a.AcceptedAtUtc);
        builder.Property(a => a.UnassignedAtUtc);

        builder.Property(a => a.CreatedByUserId).IsRequired();
        builder.Property(a => a.UpdatedByUserId);
        builder.Property(a => a.CreatedAtUtc).IsRequired();
        builder.Property(a => a.UpdatedAtUtc).IsRequired();

        builder.HasIndex(a => new { a.TenantId, a.ConversationId })
            .IsUnique()
            .HasDatabaseName("IX_Assignments_TenantId_ConversationId");

        builder.HasIndex(a => new { a.TenantId, a.AssignedUserId })
            .HasDatabaseName("IX_Assignments_TenantId_AssignedUserId");

        builder.HasIndex(a => new { a.TenantId, a.QueueId })
            .HasDatabaseName("IX_Assignments_TenantId_QueueId");

        builder.HasIndex(a => new { a.TenantId, a.AssignmentStatus })
            .HasDatabaseName("IX_Assignments_TenantId_AssignmentStatus");
    }
}
