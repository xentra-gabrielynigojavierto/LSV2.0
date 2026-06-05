using CareConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareConnect.Infrastructure.Data.Configurations;

public class ReferralProviderReassignmentConfiguration : IEntityTypeConfiguration<ReferralProviderReassignment>
{
    public void Configure(EntityTypeBuilder<ReferralProviderReassignment> builder)
    {
        builder.ToTable("cc_ReferralProviderReassignments");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).IsRequired();
        builder.Property(r => r.ReferralId).IsRequired();
        builder.Property(r => r.TenantId).IsRequired();
        builder.Property(r => r.PreviousProviderId);
        builder.Property(r => r.NewProviderId).IsRequired();
        builder.Property(r => r.ReassignedByUserId);
        builder.Property(r => r.ReassignedAtUtc).IsRequired();

        builder.HasIndex(r => new { r.TenantId, r.ReferralId });
        builder.HasIndex(r => new { r.TenantId, r.ReassignedAtUtc });

        builder.HasOne(r => r.Referral)
               .WithMany()
               .HasForeignKey(r => r.ReferralId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
