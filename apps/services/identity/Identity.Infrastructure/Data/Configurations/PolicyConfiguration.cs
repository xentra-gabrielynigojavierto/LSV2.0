using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class PolicyConfiguration : IEntityTypeConfiguration<Policy>
{
    public void Configure(EntityTypeBuilder<Policy> builder)
    {
        builder.ToTable("idt_Policies");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.PolicyCode)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Description)
            .HasMaxLength(1000);

        builder.Property(p => p.ProductCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.IsActive).IsRequired();
        builder.Property(p => p.Priority).IsRequired();
        builder.Property(p => p.Effect)
            .IsRequired()
            .HasDefaultValue(Identity.Domain.PolicyEffect.Allow)
            .HasConversion<string>()
            .HasMaxLength(10);
        builder.Property(p => p.CreatedAtUtc).IsRequired();
        builder.Property(p => p.UpdatedAtUtc);
        builder.Property(p => p.CreatedBy);
        builder.Property(p => p.UpdatedBy);

        builder.HasIndex(p => p.PolicyCode).IsUnique();
        builder.HasIndex(p => p.ProductCode);

        builder.HasMany(p => p.Rules)
            .WithOne(r => r.Policy)
            .HasForeignKey(r => r.PolicyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.PermissionPolicies)
            .WithOne(pp => pp.Policy)
            .HasForeignKey(pp => pp.PolicyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
