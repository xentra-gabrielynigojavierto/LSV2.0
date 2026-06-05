using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tenant.Domain;

namespace Tenant.Infrastructure.Data.Configurations;

public class MigrationRunConfiguration : IEntityTypeConfiguration<MigrationRun>
{
    public void Configure(EntityTypeBuilder<MigrationRun> builder)
    {
        builder.ToTable("tenant_MigrationRuns");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Mode)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(r => r.Scope)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(r => r.ErrorMessage)
            .HasMaxLength(2000);

        builder.HasMany(r => r.Items)
            .WithOne(i => i.Run)
            .HasForeignKey(i => i.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.StartedAtUtc);
    }
}

public class MigrationRunItemConfiguration : IEntityTypeConfiguration<MigrationRunItem>
{
    public void Configure(EntityTypeBuilder<MigrationRunItem> builder)
    {
        builder.ToTable("tenant_MigrationRunItems");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Code)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(i => i.ActionTaken)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(i => i.Warnings)
            .HasMaxLength(4000);

        builder.Property(i => i.Errors)
            .HasMaxLength(4000);

        builder.HasIndex(i => i.RunId);
        builder.HasIndex(i => i.IdentityTenantId);
    }
}
