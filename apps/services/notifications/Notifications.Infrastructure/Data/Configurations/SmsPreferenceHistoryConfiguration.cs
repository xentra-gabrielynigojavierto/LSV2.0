using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class SmsPreferenceHistoryConfiguration : IEntityTypeConfiguration<SmsPreferenceHistory>
{
    public void Configure(EntityTypeBuilder<SmsPreferenceHistory> builder)
    {
        builder.ToTable("ntf_SmsPreferenceHistories");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Phone).HasMaxLength(50).IsRequired();
        builder.Property(e => e.PreviousState).HasMaxLength(20);
        builder.Property(e => e.NewState).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Reason).HasColumnType("text");
        builder.Property(e => e.KeywordReceived).HasMaxLength(50);
        builder.Property(e => e.Provider).HasMaxLength(50);
        builder.Property(e => e.ProviderMessageId).HasMaxLength(255);
        builder.Property(e => e.InboundToNumber).HasMaxLength(50);
        builder.Property(e => e.CreatedBy).HasMaxLength(255);
        builder.Property(e => e.MetadataJson).HasColumnType("text");

        builder.HasIndex(e => new { e.TenantId, e.Phone })
            .HasDatabaseName("IX_SmsPreferenceHistories_TenantId_Phone");

        builder.HasIndex(e => e.Phone)
            .HasDatabaseName("IX_SmsPreferenceHistories_Phone");
    }
}
