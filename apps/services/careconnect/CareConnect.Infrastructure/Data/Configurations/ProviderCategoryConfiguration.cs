using CareConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareConnect.Infrastructure.Data.Configurations;

public class ProviderCategoryConfiguration : IEntityTypeConfiguration<ProviderCategory>
{
    public void Configure(EntityTypeBuilder<ProviderCategory> builder)
    {
        builder.ToTable("cc_ProviderCategories");

        builder.HasKey(pc => new { pc.ProviderId, pc.CategoryId });

        builder.Property(pc => pc.ProviderId).IsRequired();
        builder.Property(pc => pc.CategoryId).IsRequired();
    }
}
