using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlatformAuditEventService.Entities;

namespace PlatformAuditEventService.Data.Configurations;

/// <summary>
/// EF Core entity type configuration for <see cref="OutboxMessage"/>.
///
/// Table: OutboxMessages
///   - bigint AUTO_INCREMENT PK
///   - char(36) MessageId — unique public identifier
///   - varchar EventType — event type string (e.g. "legalsynq.audit.record.ingested")
///   - longtext PayloadJson — full JSON payload
///   - datetime(6) CreatedAtUtc — when written (same tx as the AuditEventRecord)
///   - datetime(6)? ProcessedAtUtc — null until relay delivers successfully
///   - int RetryCount — delivery attempts
///   - bool IsPermanentlyFailed — retries exhausted; operator intervention required
///   - varchar BrokerName — target broker for routing
///
/// Indexes:
///   IX_OutboxMessages_ProcessedAtUtc_IsPermanentlyFailed — relay polling index:
///     (ProcessedAtUtc IS NULL AND IsPermanentlyFailed = 0) — only unpublished, non-failed messages.
///   IX_OutboxMessages_CreatedAtUtc — ordering for relay FIFO delivery.
/// </summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("aud_OutboxMessages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
               .ValueGeneratedOnAdd();

        builder.Property(m => m.MessageId)
               .IsRequired()
               .HasColumnType("char(36)");

        builder.HasIndex(m => m.MessageId)
               .IsUnique()
               .HasDatabaseName("IX_OutboxMessages_MessageId_Unique");

        builder.Property(m => m.EventType)
               .IsRequired()
               .HasMaxLength(256);

        builder.Property(m => m.PayloadJson)
               .IsRequired()
               .HasColumnType("longtext");

        builder.Property(m => m.CreatedAtUtc)
               .IsRequired()
               .HasColumnType("datetime(6)");

        builder.HasIndex(m => m.CreatedAtUtc)
               .HasDatabaseName("IX_OutboxMessages_CreatedAtUtc");

        builder.Property(m => m.ProcessedAtUtc)
               .HasColumnType("datetime(6)");

        // Composite index for relay polling: unprocessed, non-failed messages ordered by creation
        builder.HasIndex(m => new { m.ProcessedAtUtc, m.IsPermanentlyFailed })
               .HasDatabaseName("IX_OutboxMessages_Relay_Poll");

        builder.Property(m => m.RetryCount)
               .HasDefaultValue(0);

        builder.Property(m => m.LastError)
               .HasMaxLength(2000);

        builder.Property(m => m.IsPermanentlyFailed)
               .HasDefaultValue(false);

        builder.Property(m => m.BrokerName)
               .IsRequired()
               .HasMaxLength(128)
               .HasDefaultValue("default");
    }
}
