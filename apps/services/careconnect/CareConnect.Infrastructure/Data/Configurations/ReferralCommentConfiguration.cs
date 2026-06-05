using CareConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareConnect.Infrastructure.Data.Configurations;

public class ReferralCommentConfiguration : IEntityTypeConfiguration<ReferralComment>
{
    public void Configure(EntityTypeBuilder<ReferralComment> builder)
    {
        builder.ToTable("cc_ReferralComments");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.ReferralId).IsRequired();
        builder.Property(e => e.SenderType).HasMaxLength(20).IsRequired();
        builder.Property(e => e.SenderName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Message).HasMaxLength(4000).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.ReferralId, e.CreatedAt })
            .HasDatabaseName("IX_ReferralComments_TenantId_ReferralId_CreatedAt");
    }
}
