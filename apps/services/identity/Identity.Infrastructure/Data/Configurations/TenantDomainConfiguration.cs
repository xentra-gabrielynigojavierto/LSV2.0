using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class TenantDomainConfiguration : IEntityTypeConfiguration<TenantDomain>
{
    public void Configure(EntityTypeBuilder<TenantDomain> builder)
    {
        builder.ToTable("idt_TenantDomains");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.Domain).HasMaxLength(253).IsRequired();
        builder.Property(e => e.DomainType).HasMaxLength(20).IsRequired();
        builder.Property(e => e.IsPrimary).IsRequired();
        builder.Property(e => e.IsVerified).IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();

        builder.HasOne(e => e.Tenant)
            .WithMany(t => t.Domains)
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.Domain)
            .IsUnique()
            .HasDatabaseName("IX_TenantDomains_Domain");

        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("IX_TenantDomains_TenantId");

        builder.HasIndex(new[] { "TenantId", "IsPrimary" })
            .HasDatabaseName("IX_TenantDomains_TenantId_IsPrimary");
    }
}
