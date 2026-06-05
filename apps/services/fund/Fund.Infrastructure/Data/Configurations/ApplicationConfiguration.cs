using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fund.Infrastructure.Data.Configurations;

public class ApplicationConfiguration : IEntityTypeConfiguration<Domain.Application>
{
    public void Configure(EntityTypeBuilder<Domain.Application> builder)
    {
        builder.ToTable("fund_Applications");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.TenantId).IsRequired();

        builder.Property(a => a.ApplicationNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.ApplicantFirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.ApplicantLastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.Email)
            .IsRequired()
            .HasMaxLength(320);

        builder.Property(a => a.Phone)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(a => a.Status)
            .IsRequired()
            .HasMaxLength(50);

        // Funding details
        builder.Property(a => a.RequestedAmount)
            .HasColumnType("decimal(18,2)")
            .IsRequired(false);

        builder.Property(a => a.ApprovedAmount)
            .HasColumnType("decimal(18,2)")
            .IsRequired(false);

        builder.Property(a => a.CaseType)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(a => a.IncidentDate)
            .HasMaxLength(20)
            .IsRequired(false);

        builder.Property(a => a.AttorneyNotes)
            .HasMaxLength(4000)
            .IsRequired(false);

        builder.Property(a => a.ApprovalTerms)
            .HasMaxLength(4000)
            .IsRequired(false);

        builder.Property(a => a.DenialReason)
            .HasMaxLength(2000)
            .IsRequired(false);

        builder.Property(a => a.FunderId)
            .IsRequired(false);

        builder.Property(a => a.CreatedByUserId).IsRequired();
        builder.Property(a => a.UpdatedByUserId);
        builder.Property(a => a.CreatedAtUtc).IsRequired();
        builder.Property(a => a.UpdatedAtUtc).IsRequired();

        builder.HasIndex(a => new { a.TenantId, a.ApplicationNumber }).IsUnique();
        builder.HasIndex(a => new { a.TenantId, a.Status });
        builder.HasIndex(a => new { a.TenantId, a.CreatedAtUtc });
        builder.HasIndex(a => new { a.TenantId, a.FunderId });
    }
}
