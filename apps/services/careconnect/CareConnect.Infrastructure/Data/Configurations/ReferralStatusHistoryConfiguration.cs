using CareConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareConnect.Infrastructure.Data.Configurations;

public class ReferralStatusHistoryConfiguration : IEntityTypeConfiguration<ReferralStatusHistory>
{
    public void Configure(EntityTypeBuilder<ReferralStatusHistory> builder)
    {
        builder.ToTable("cc_ReferralStatusHistories");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id).IsRequired();
        builder.Property(h => h.ReferralId).IsRequired();
        builder.Property(h => h.TenantId).IsRequired();
        builder.Property(h => h.OldStatus).IsRequired().HasMaxLength(50);
        builder.Property(h => h.NewStatus).IsRequired().HasMaxLength(50);
        builder.Property(h => h.ChangedByUserId);
        builder.Property(h => h.ChangedAtUtc).IsRequired();
        builder.Property(h => h.Notes).HasMaxLength(2000);

        builder.HasIndex(h => new { h.TenantId, h.ReferralId });
        builder.HasIndex(h => new { h.TenantId, h.ChangedAtUtc });

        builder.HasOne(h => h.Referral)
               .WithMany()
               .HasForeignKey(h => h.ReferralId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
