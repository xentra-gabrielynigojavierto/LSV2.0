using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class OrganizationProductConfiguration : IEntityTypeConfiguration<OrganizationProduct>
{
    public void Configure(EntityTypeBuilder<OrganizationProduct> builder)
    {
        builder.ToTable("idt_OrganizationProducts");

        builder.HasKey(op => new { op.OrganizationId, op.ProductId });

        builder.Property(op => op.IsEnabled).IsRequired();
        builder.Property(op => op.EnabledAtUtc);
        builder.Property(op => op.GrantedByUserId);

        builder.HasIndex(op => op.ProductId);

        builder.HasOne(op => op.Organization)
            .WithMany(o => o.OrganizationProducts)
            .HasForeignKey(op => op.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(op => op.Product)
            .WithMany(p => p.OrganizationProducts)
            .HasForeignKey(op => op.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            new { OrganizationId = SeedIds.OrgLegalSynq, ProductId = SeedIds.ProductSynqFund,        IsEnabled = true, EnabledAtUtc = (DateTime?)SeedIds.SeededAt, GrantedByUserId = (Guid?)null },
            new { OrganizationId = SeedIds.OrgLegalSynq, ProductId = SeedIds.ProductSynqLiens,       IsEnabled = true, EnabledAtUtc = (DateTime?)SeedIds.SeededAt, GrantedByUserId = (Guid?)null },
            new { OrganizationId = SeedIds.OrgLegalSynq, ProductId = SeedIds.ProductSynqCareConnect, IsEnabled = true, EnabledAtUtc = (DateTime?)SeedIds.SeededAt, GrantedByUserId = (Guid?)null },
            new { OrganizationId = SeedIds.OrgLegalSynq, ProductId = SeedIds.ProductSynqPay,         IsEnabled = true, EnabledAtUtc = (DateTime?)SeedIds.SeededAt, GrantedByUserId = (Guid?)null },
            new { OrganizationId = SeedIds.OrgLegalSynq, ProductId = SeedIds.ProductSynqAI,          IsEnabled = true, EnabledAtUtc = (DateTime?)SeedIds.SeededAt, GrantedByUserId = (Guid?)null }
        );
    }
}
