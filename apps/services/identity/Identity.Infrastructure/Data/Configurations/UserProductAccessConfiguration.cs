using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class UserProductAccessConfiguration : IEntityTypeConfiguration<UserProductAccess>
{
    public void Configure(EntityTypeBuilder<UserProductAccess> builder)
    {
        builder.ToTable("idt_UserProductAccess");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.UserId).IsRequired();
        builder.Property(e => e.ProductCode).IsRequired().HasMaxLength(50);
        builder.Property(e => e.AccessStatus)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(20);
        builder.Property(e => e.OrganizationId);
        builder.Property(e => e.SourceType).IsRequired().HasMaxLength(20).HasDefaultValue("Direct");
        builder.Property(e => e.GrantedAtUtc);
        builder.Property(e => e.RevokedAtUtc);
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();
        builder.Property(e => e.CreatedByUserId);
        builder.Property(e => e.UpdatedByUserId);

        builder.HasIndex(e => new { e.TenantId, e.UserId, e.ProductCode })
               .IsUnique()
               .HasDatabaseName("IX_UserProductAccess_TenantId_UserId_ProductCode");
    }
}
