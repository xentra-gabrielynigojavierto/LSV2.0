using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("idt_Tenants");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Code)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.IsActive)
            .IsRequired();

        builder.Property(t => t.CreatedAtUtc)
            .IsRequired();

        builder.Property(t => t.UpdatedAtUtc)
            .IsRequired();

        builder.Property(t => t.AddressLine1)
            .HasMaxLength(200);

        builder.Property(t => t.City)
            .HasMaxLength(100);

        builder.Property(t => t.State)
            .HasMaxLength(50);

        builder.Property(t => t.PostalCode)
            .HasMaxLength(20);

        builder.Property(t => t.Latitude);

        builder.Property(t => t.Longitude);

        builder.Property(t => t.GeoPointSource)
            .HasMaxLength(50);

        builder.Property(t => t.Subdomain)
            .HasMaxLength(63);

        builder.Property(t => t.ProvisioningStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(ProvisioningStatus.Pending);

        builder.Property(t => t.LastProvisioningAttemptUtc);

        builder.Property(t => t.ProvisioningFailureReason)
            .HasMaxLength(500);

        builder.Property(t => t.ProvisioningFailureStage)
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(ProvisioningFailureStage.None);

        builder.Property(t => t.VerificationAttemptCount)
            .HasDefaultValue(0);

        builder.Property(t => t.LastVerificationAttemptUtc);

        builder.Property(t => t.NextVerificationRetryAtUtc);

        builder.Property(t => t.IsVerificationRetryExhausted)
            .HasDefaultValue(false);

        builder.HasIndex(t => t.Code)
            .IsUnique();

        builder.HasIndex(t => t.Subdomain)
            .IsUnique()
            .HasFilter("`Subdomain` IS NOT NULL");

        builder.HasData(new
        {
            Id = SeedIds.TenantLegalSynq,
            Name = "LegalSynq Internal",
            Code = "LEGALSYNQ",
            IsActive = true,
            ProvisioningStatus = ProvisioningStatus.Active,
            ProvisioningFailureStage = ProvisioningFailureStage.None,
            VerificationAttemptCount = 0,
            IsVerificationRetryExhausted = false,
            CreatedAtUtc = SeedIds.SeededAt,
            UpdatedAtUtc = SeedIds.SeededAt
        });
    }
}
