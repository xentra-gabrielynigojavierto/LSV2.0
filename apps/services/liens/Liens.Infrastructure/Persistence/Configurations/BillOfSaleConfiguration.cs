using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class BillOfSaleConfiguration : IEntityTypeConfiguration<BillOfSale>
{
    public void Configure(EntityTypeBuilder<BillOfSale> builder)
    {
        builder.ToTable("liens_BillsOfSale");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id).IsRequired();
        builder.Property(b => b.TenantId).IsRequired();
        builder.Property(b => b.LienId).IsRequired();
        builder.Property(b => b.LienOfferId).IsRequired();

        builder.Property(b => b.BillOfSaleNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(b => b.ExternalReference)
            .HasMaxLength(200);

        builder.Property(b => b.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(b => b.SellerOrgId).IsRequired();
        builder.Property(b => b.BuyerOrgId).IsRequired();

        builder.Property(b => b.PurchaseAmount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(b => b.OriginalLienAmount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(b => b.DiscountPercent)
            .HasColumnType("decimal(5,2)");

        builder.Property(b => b.SellerContactName)
            .HasMaxLength(250);

        builder.Property(b => b.BuyerContactName)
            .HasMaxLength(250);

        builder.Property(b => b.Terms)
            .HasMaxLength(4000);

        builder.Property(b => b.Notes)
            .HasMaxLength(4000);

        builder.Property(b => b.DocumentId);

        builder.Property(b => b.IssuedAtUtc).IsRequired();
        builder.Property(b => b.ExecutedAtUtc);
        builder.Property(b => b.EffectiveAtUtc);
        builder.Property(b => b.CancelledAtUtc);

        builder.Property(b => b.CreatedByUserId).IsRequired();
        builder.Property(b => b.UpdatedByUserId);
        builder.Property(b => b.CreatedAtUtc).IsRequired();
        builder.Property(b => b.UpdatedAtUtc).IsRequired();

        builder.HasIndex(b => new { b.TenantId, b.BillOfSaleNumber })
            .IsUnique()
            .HasDatabaseName("UX_BillsOfSale_TenantId_BillOfSaleNumber");

        builder.HasIndex(b => b.LienId)
            .HasDatabaseName("IX_BillsOfSale_LienId");

        builder.HasIndex(b => b.LienOfferId)
            .HasDatabaseName("IX_BillsOfSale_LienOfferId");

        builder.HasIndex(b => new { b.TenantId, b.Status })
            .HasDatabaseName("IX_BillsOfSale_TenantId_Status");

        builder.HasIndex(b => new { b.TenantId, b.SellerOrgId })
            .HasDatabaseName("IX_BillsOfSale_TenantId_SellerOrgId");

        builder.HasIndex(b => new { b.TenantId, b.BuyerOrgId })
            .HasDatabaseName("IX_BillsOfSale_TenantId_BuyerOrgId");

        builder.HasIndex(b => b.DocumentId)
            .HasDatabaseName("IX_BillsOfSale_DocumentId");

        builder.HasOne<Lien>()
            .WithMany()
            .HasForeignKey(b => b.LienId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<LienOffer>()
            .WithMany()
            .HasForeignKey(b => b.LienOfferId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
