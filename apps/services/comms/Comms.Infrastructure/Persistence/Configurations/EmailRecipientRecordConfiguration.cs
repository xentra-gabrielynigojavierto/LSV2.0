using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Comms.Domain.Entities;

namespace Comms.Infrastructure.Persistence.Configurations;

public class EmailRecipientRecordConfiguration : IEntityTypeConfiguration<EmailRecipientRecord>
{
    public void Configure(EntityTypeBuilder<EmailRecipientRecord> builder)
    {
        builder.ToTable("comms_EmailRecipientRecords");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.ConversationId).IsRequired();
        builder.Property(e => e.EmailMessageReferenceId).IsRequired();
        builder.Property(e => e.NormalizedEmail).IsRequired().HasMaxLength(512);
        builder.Property(e => e.DisplayName).HasMaxLength(256);
        builder.Property(e => e.RecipientType).IsRequired().HasMaxLength(10);
        builder.Property(e => e.RecipientVisibility).IsRequired().HasMaxLength(10);
        builder.Property(e => e.RecipientSource).HasMaxLength(50);

        builder.HasIndex(e => new { e.TenantId, e.EmailMessageReferenceId })
            .HasDatabaseName("IX_EmailRecipients_TenantId_EmailMessageReferenceId");

        builder.HasIndex(e => new { e.TenantId, e.ConversationId })
            .HasDatabaseName("IX_EmailRecipients_TenantId_ConversationId");

        builder.HasIndex(e => new { e.TenantId, e.NormalizedEmail })
            .HasDatabaseName("IX_EmailRecipients_TenantId_NormalizedEmail");

        builder.HasIndex(e => new { e.TenantId, e.EmailMessageReferenceId, e.RecipientVisibility })
            .HasDatabaseName("IX_EmailRecipients_TenantId_EmailMessageReferenceId_Visibility");
    }
}
