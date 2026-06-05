using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class TenantProductConfiguration : IEntityTypeConfiguration<TenantProduct>
{
    public void Configure(EntityTypeBuilder<TenantProduct> builder)
    {
        builder.ToTable("idt_TenantProducts");

        builder.HasKey(tp => new { tp.TenantId, tp.ProductId });

        builder.Property(tp => tp.IsEnabled)
            .IsRequired();

        builder.HasOne(tp => tp.Tenant)
            .WithMany(t => t.TenantProducts)
            .HasForeignKey(tp => tp.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tp => tp.Product)
            .WithMany(p => p.TenantProducts)
            .HasForeignKey(tp => tp.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
