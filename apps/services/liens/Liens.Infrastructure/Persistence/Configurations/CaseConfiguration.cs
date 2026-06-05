using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class CaseConfiguration : IEntityTypeConfiguration<Case>
{
    public void Configure(EntityTypeBuilder<Case> builder)
    {
        builder.ToTable("liens_Cases");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).IsRequired();
        builder.Property(c => c.TenantId).IsRequired();
        builder.Property(c => c.OrgId).IsRequired();

        builder.Property(c => c.CaseNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.ExternalReference)
            .HasMaxLength(200);

        builder.Property(c => c.Title)
            .HasMaxLength(300);

        builder.Property(c => c.ClientFirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.ClientLastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.ClientDob)
            .HasColumnType("date");

        builder.Property(c => c.ClientPhone)
            .HasMaxLength(30);

        builder.Property(c => c.ClientEmail)
            .HasMaxLength(320);

        builder.Property(c => c.ClientAddress)
            .HasMaxLength(500);

        builder.Property(c => c.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.DateOfIncident)
            .HasColumnType("date");

        builder.Property(c => c.OpenedAtUtc);
        builder.Property(c => c.ClosedAtUtc);

        builder.Property(c => c.InsuranceCarrier)
            .HasMaxLength(200);

        builder.Property(c => c.PolicyNumber)
            .HasMaxLength(100);

        builder.Property(c => c.ClaimNumber)
            .HasMaxLength(100);

        builder.Property(c => c.DemandAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(c => c.SettlementAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(c => c.Description)
            .HasMaxLength(4000);

        builder.Property(c => c.Notes)
            .HasMaxLength(4000);

        builder.Property(c => c.CreatedByUserId).IsRequired();
        builder.Property(c => c.UpdatedByUserId);
        builder.Property(c => c.CreatedAtUtc).IsRequired();
        builder.Property(c => c.UpdatedAtUtc).IsRequired();

        builder.HasIndex(c => new { c.TenantId, c.CaseNumber })
            .IsUnique()
            .HasDatabaseName("UX_Cases_TenantId_CaseNumber");

        builder.HasIndex(c => new { c.TenantId, c.OrgId, c.Status })
            .HasDatabaseName("IX_Cases_TenantId_OrgId_Status");

        builder.HasIndex(c => new { c.TenantId, c.OrgId, c.CreatedAtUtc })
            .HasDatabaseName("IX_Cases_TenantId_OrgId_CreatedAtUtc");

        builder.HasIndex(c => new { c.TenantId, c.Status })
            .HasDatabaseName("IX_Cases_TenantId_Status");
    }
}
