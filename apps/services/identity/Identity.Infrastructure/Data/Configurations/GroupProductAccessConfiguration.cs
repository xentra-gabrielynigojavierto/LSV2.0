using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class GroupProductAccessConfiguration : IEntityTypeConfiguration<GroupProductAccess>
{
    public void Configure(EntityTypeBuilder<GroupProductAccess> builder)
    {
        builder.ToTable("idt_GroupProductAccess");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.GroupId).IsRequired();
        builder.Property(e => e.ProductCode).IsRequired().HasMaxLength(100);
        builder.Property(e => e.AccessStatus).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.GrantedAtUtc);
        builder.Property(e => e.RevokedAtUtc);
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();
        builder.Property(e => e.CreatedByUserId);
        builder.Property(e => e.UpdatedByUserId);

        builder.HasIndex(e => new { e.TenantId, e.GroupId, e.ProductCode })
               .IsUnique()
               .HasDatabaseName("IX_GroupProductAccess_TenantId_GroupId_ProductCode");
    }
}
