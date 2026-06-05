using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("liens_Contacts");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).IsRequired();
        builder.Property(c => c.TenantId).IsRequired();
        builder.Property(c => c.OrgId).IsRequired();

        builder.Property(c => c.ContactType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.DisplayName)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(c => c.Title)
            .HasMaxLength(100);

        builder.Property(c => c.Organization)
            .HasMaxLength(200);

        builder.Property(c => c.Email)
            .HasMaxLength(320);

        builder.Property(c => c.Phone)
            .HasMaxLength(30);

        builder.Property(c => c.Fax)
            .HasMaxLength(30);

        builder.Property(c => c.Website)
            .HasMaxLength(500);

        builder.Property(c => c.AddressLine1)
            .HasMaxLength(300);

        builder.Property(c => c.City)
            .HasMaxLength(100);

        builder.Property(c => c.State)
            .HasMaxLength(100);

        builder.Property(c => c.PostalCode)
            .HasMaxLength(20);

        builder.Property(c => c.Notes)
            .HasMaxLength(4000);

        builder.Property(c => c.IsActive)
            .IsRequired();

        builder.Property(c => c.CreatedByUserId).IsRequired();
        builder.Property(c => c.UpdatedByUserId);
        builder.Property(c => c.CreatedAtUtc).IsRequired();
        builder.Property(c => c.UpdatedAtUtc).IsRequired();

        builder.HasIndex(c => new { c.TenantId, c.OrgId, c.ContactType })
            .HasDatabaseName("IX_Contacts_TenantId_OrgId_ContactType");

        builder.HasIndex(c => new { c.TenantId, c.OrgId, c.DisplayName })
            .HasDatabaseName("IX_Contacts_TenantId_OrgId_DisplayName");

        builder.HasIndex(c => new { c.TenantId, c.Email })
            .HasDatabaseName("IX_Contacts_TenantId_Email");
    }
}
