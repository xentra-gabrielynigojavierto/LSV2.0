using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class RelationshipTypeConfiguration : IEntityTypeConfiguration<RelationshipType>
{
    public void Configure(EntityTypeBuilder<RelationshipType> builder)
    {
        builder.ToTable("idt_RelationshipTypes");

        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.Code)
            .IsRequired()
            .HasMaxLength(80);

        builder.Property(rt => rt.DisplayName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(rt => rt.Description)
            .HasMaxLength(500);

        builder.Property(rt => rt.IsDirectional).IsRequired();
        builder.Property(rt => rt.IsSystem).IsRequired();
        builder.Property(rt => rt.IsActive).IsRequired();
        builder.Property(rt => rt.CreatedAtUtc).IsRequired();

        builder.HasIndex(rt => rt.Code).IsUnique();

        builder.HasData(
            new { Id = SeedIds.RelTypeRefersTo,             Code = "REFERS_TO",              DisplayName = "Refers To",               Description = (string?)"Sending organization refers clients to the receiving organization",        IsDirectional = true,  IsSystem = true, IsActive = true, CreatedAtUtc = SeedIds.SeededAt },
            new { Id = SeedIds.RelTypeAcceptsReferralsFrom, Code = "ACCEPTS_REFERRALS_FROM", DisplayName = "Accepts Referrals From",   Description = (string?)"Receiving organization accepts referrals from the sending organization",   IsDirectional = true,  IsSystem = true, IsActive = true, CreatedAtUtc = SeedIds.SeededAt },
            new { Id = SeedIds.RelTypeFundedBy,             Code = "FUNDED_BY",              DisplayName = "Funded By",               Description = (string?)"Case or org is funded by the target organization",                         IsDirectional = true,  IsSystem = true, IsActive = true, CreatedAtUtc = SeedIds.SeededAt },
            new { Id = SeedIds.RelTypeServicesFor,          Code = "SERVICES_FOR",           DisplayName = "Services For",            Description = (string?)"Organization provides services for the target organization or client",       IsDirectional = true,  IsSystem = true, IsActive = true, CreatedAtUtc = SeedIds.SeededAt },
            new { Id = SeedIds.RelTypeAssignsLienTo,        Code = "ASSIGNS_LIEN_TO",        DisplayName = "Assigns Lien To",         Description = (string?)"Organization assigns a lien to the target lien-owner organization",        IsDirectional = true,  IsSystem = true, IsActive = true, CreatedAtUtc = SeedIds.SeededAt },
            new { Id = SeedIds.RelTypeMemberOfNetwork,      Code = "MEMBER_OF_NETWORK",      DisplayName = "Member Of Network",       Description = (string?)"Organization is a member of the target network or group",                   IsDirectional = false, IsSystem = true, IsActive = true, CreatedAtUtc = SeedIds.SeededAt }
        );
    }
}
