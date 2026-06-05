using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("idt_Products");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Code)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Description)
            .HasMaxLength(1000);

        builder.Property(p => p.IsActive)
            .IsRequired();

        builder.Property(p => p.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(p => p.Code)
            .IsUnique();

        builder.HasData(
            new { Id = SeedIds.ProductSynqFund,        Name = "SynqFund",        Code = "SYNQ_FUND",        Description = (string?)"Presettlement funding",                  IsActive = true, CreatedAtUtc = SeedIds.SeededAt },
            new { Id = SeedIds.ProductSynqLiens,       Name = "SynqLiens",       Code = "SYNQ_LIENS",       Description = (string?)"Lien management platform",              IsActive = true, CreatedAtUtc = SeedIds.SeededAt },
            new { Id = SeedIds.ProductSynqCareConnect, Name = "SynqCareConnect", Code = "SYNQ_CARECONNECT", Description = (string?)"Care coordination platform",            IsActive = true, CreatedAtUtc = SeedIds.SeededAt },
            new { Id = SeedIds.ProductSynqPay,         Name = "SynqPay",         Code = "SYNQ_PAY",         Description = (string?)"Payment processing platform",           IsActive = true, CreatedAtUtc = SeedIds.SeededAt },
            new { Id = SeedIds.ProductSynqAI,          Name = "SynqAI",          Code = "SYNQ_AI",          Description = (string?)"AI-powered legal intelligence platform", IsActive = true, CreatedAtUtc = SeedIds.SeededAt },
            // LS-ID-TNT-011: Virtual platform product — not a subscribable tenant product.
            // Serves as the FK anchor for tenant-level permission codes (TENANT.*).
            // Never added to TenantProducts; excluded from tenant product entitlement flows.
            new { Id = SeedIds.ProductSynqPlatform,    Name = "SynqPlatform",    Code = "SYNQ_PLATFORM",    Description = (string?)"Platform/tenant operation capabilities", IsActive = true, CreatedAtUtc = SeedIds.SeededAt }
        );
    }
}
