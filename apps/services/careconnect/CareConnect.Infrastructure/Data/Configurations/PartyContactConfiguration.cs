using CareConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareConnect.Infrastructure.Data.Configurations;

public class PartyContactConfiguration : IEntityTypeConfiguration<PartyContact>
{
    public void Configure(EntityTypeBuilder<PartyContact> builder)
    {
        builder.ToTable("cc_PartyContacts");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).IsRequired();
        builder.Property(c => c.PartyId).IsRequired();
        builder.Property(c => c.ContactType).IsRequired().HasMaxLength(20);
        builder.Property(c => c.Value).IsRequired().HasMaxLength(320);
        builder.Property(c => c.IsPrimary).IsRequired();
        builder.Property(c => c.IsVerified).IsRequired();
        builder.Property(c => c.CreatedAtUtc).IsRequired();

        builder.HasIndex(c => new { c.PartyId, c.ContactType, c.Value })
            .IsUnique()
            .HasDatabaseName("IX_PartyContacts_PartyId_Type_Value");
        builder.HasIndex(c => new { c.ContactType, c.Value })
            .HasDatabaseName("IX_PartyContacts_Type_Value");
    }
}
