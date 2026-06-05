using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class PolicyRuleConfiguration : IEntityTypeConfiguration<PolicyRule>
{
    public void Configure(EntityTypeBuilder<PolicyRule> builder)
    {
        builder.ToTable("idt_PolicyRules");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.PolicyId).IsRequired();

        builder.Property(r => r.ConditionType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(r => r.Field)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.Operator)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(r => r.Value)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(r => r.LogicalGroup)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(r => r.CreatedAtUtc).IsRequired();

        builder.HasIndex(r => r.PolicyId);
    }
}
