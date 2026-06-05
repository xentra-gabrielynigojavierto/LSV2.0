using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class PermissionPolicyConfiguration : IEntityTypeConfiguration<PermissionPolicy>
{
    public void Configure(EntityTypeBuilder<PermissionPolicy> builder)
    {
        builder.ToTable("idt_PermissionPolicies");

        builder.HasKey(pp => pp.Id);

        builder.Property(pp => pp.PermissionCode)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(pp => pp.PolicyId).IsRequired();
        builder.Property(pp => pp.IsActive).IsRequired();
        builder.Property(pp => pp.CreatedAtUtc).IsRequired();
        builder.Property(pp => pp.UpdatedAtUtc);

        builder.HasIndex(pp => new { pp.PermissionCode, pp.PolicyId }).IsUnique();
        builder.HasIndex(pp => pp.PermissionCode);
        builder.HasIndex(pp => pp.PolicyId);
    }
}
