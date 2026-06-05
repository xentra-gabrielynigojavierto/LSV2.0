using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class SmsContactPreferenceConfiguration : IEntityTypeConfiguration<SmsContactPreference>
{
    public void Configure(EntityTypeBuilder<SmsContactPreference> builder)
    {
        builder.ToTable("ntf_SmsContactPreferences");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Phone).HasMaxLength(50).IsRequired();
        builder.Property(e => e.PreferenceState).HasMaxLength(20).HasDefaultValue("unknown");
        builder.Property(e => e.Source).HasMaxLength(50);
        builder.Property(e => e.Reason).HasColumnType("text");
        builder.Property(e => e.KeywordReceived).HasMaxLength(50);
        builder.Property(e => e.ProviderMessageId).HasMaxLength(255);
        builder.Property(e => e.UpdatedBy).HasMaxLength(255);

        builder.HasIndex(e => new { e.TenantId, e.Phone })
            .HasDatabaseName("UX_SmsContactPreferences_TenantId_Phone")
            .IsUnique();
    }
}
