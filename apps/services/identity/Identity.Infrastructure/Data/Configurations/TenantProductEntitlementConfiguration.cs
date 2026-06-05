using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class TenantProductEntitlementConfiguration : IEntityTypeConfiguration<TenantProductEntitlement>
{
    public void Configure(EntityTypeBuilder<TenantProductEntitlement> builder)
    {
        builder.ToTable("idt_TenantProductEntitlements");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.ProductCode).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Status)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(20);
        builder.Property(e => e.EnabledAtUtc);
        builder.Property(e => e.DisabledAtUtc);
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();
        builder.Property(e => e.CreatedByUserId);
        builder.Property(e => e.UpdatedByUserId);

        builder.HasIndex(e => new { e.TenantId, e.ProductCode })
               .IsUnique()
               .HasDatabaseName("IX_TenantProductEntitlements_TenantId_ProductCode");
    }
}
