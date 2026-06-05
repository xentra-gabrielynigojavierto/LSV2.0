using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class LookupValueConfiguration : IEntityTypeConfiguration<LookupValue>
{
    public void Configure(EntityTypeBuilder<LookupValue> builder)
    {
        builder.ToTable("liens_LookupValues");

        builder.HasKey(lv => lv.Id);

        builder.Property(lv => lv.Id).IsRequired();

        builder.Property(lv => lv.TenantId);

        builder.Property(lv => lv.Category)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(lv => lv.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(lv => lv.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(lv => lv.Description)
            .HasMaxLength(1000);

        builder.Property(lv => lv.SortOrder)
            .IsRequired();

        builder.Property(lv => lv.IsActive)
            .IsRequired();

        builder.Property(lv => lv.IsSystem)
            .IsRequired();

        builder.Property(lv => lv.CreatedByUserId).IsRequired();
        builder.Property(lv => lv.UpdatedByUserId);
        builder.Property(lv => lv.CreatedAtUtc).IsRequired();
        builder.Property(lv => lv.UpdatedAtUtc).IsRequired();

        builder.HasIndex(lv => new { lv.TenantId, lv.Category, lv.Code })
            .IsUnique()
            .HasDatabaseName("UX_LookupValues_TenantId_Category_Code");

        builder.HasIndex(lv => new { lv.TenantId, lv.Category })
            .HasDatabaseName("IX_LookupValues_TenantId_Category");
    }
}
