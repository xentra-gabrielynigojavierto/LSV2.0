using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class OrganizationDomainConfiguration : IEntityTypeConfiguration<OrganizationDomain>
{
    public void Configure(EntityTypeBuilder<OrganizationDomain> builder)
    {
        builder.ToTable("idt_OrganizationDomains");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.OrganizationId).IsRequired();

        builder.Property(d => d.Domain)
            .IsRequired()
            .HasMaxLength(253);

        builder.Property(d => d.DomainType)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(d => d.IsPrimary).IsRequired();
        builder.Property(d => d.IsVerified).IsRequired();
        builder.Property(d => d.CreatedAtUtc).IsRequired();

        builder.HasIndex(d => d.Domain).IsUnique();
        builder.HasIndex(d => d.OrganizationId);

        builder.HasOne(d => d.Organization)
            .WithMany(o => o.Domains)
            .HasForeignKey(d => d.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasData(new
        {
            Id = SeedIds.OrgDomainLegalSynq,
            OrganizationId = SeedIds.OrgLegalSynq,
            Domain = "legalsynq.legalsynq.com",
            DomainType = Domain.DomainType.Subdomain,
            IsPrimary = true,
            IsVerified = true,
            CreatedAtUtc = SeedIds.SeededAt
        });
    }
}
