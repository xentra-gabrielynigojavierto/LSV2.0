using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class ProductRelationshipTypeRuleConfiguration : IEntityTypeConfiguration<ProductRelationshipTypeRule>
{
    public void Configure(EntityTypeBuilder<ProductRelationshipTypeRule> builder)
    {
        builder.ToTable("idt_ProductRelationshipTypeRules");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.ProductId).IsRequired();
        builder.Property(r => r.RelationshipTypeId).IsRequired();
        builder.Property(r => r.IsActive).IsRequired();
        builder.Property(r => r.CreatedAtUtc).IsRequired();

        builder.HasIndex(r => new { r.ProductId, r.RelationshipTypeId }).IsUnique();

        builder.HasOne(r => r.Product)
            .WithMany()
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.RelationshipType)
            .WithMany(rt => rt.ProductRules)
            .HasForeignKey(r => r.RelationshipTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // CareConnect uses REFERS_TO and ACCEPTS_REFERRALS_FROM
        builder.HasData(
            new { Id = SeedIds.PrRelRuleCareConnectRefersTo,             ProductId = SeedIds.ProductSynqCareConnect, RelationshipTypeId = SeedIds.RelTypeRefersTo,             IsActive = true, CreatedAtUtc = SeedIds.SeededAt },
            new { Id = SeedIds.PrRelRuleCareConnectAcceptsReferralsFrom, ProductId = SeedIds.ProductSynqCareConnect, RelationshipTypeId = SeedIds.RelTypeAcceptsReferralsFrom, IsActive = true, CreatedAtUtc = SeedIds.SeededAt },
            new { Id = SeedIds.PrRelRuleSynqFundFundedBy,                ProductId = SeedIds.ProductSynqFund,        RelationshipTypeId = SeedIds.RelTypeFundedBy,             IsActive = true, CreatedAtUtc = SeedIds.SeededAt },
            new { Id = SeedIds.PrRelRuleSynqLienAssignsLienTo,           ProductId = SeedIds.ProductSynqLiens,       RelationshipTypeId = SeedIds.RelTypeAssignsLienTo,        IsActive = true, CreatedAtUtc = SeedIds.SeededAt }
        );
    }
}
