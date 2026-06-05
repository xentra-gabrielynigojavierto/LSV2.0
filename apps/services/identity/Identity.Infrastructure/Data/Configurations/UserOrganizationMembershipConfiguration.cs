using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class UserOrganizationMembershipConfiguration : IEntityTypeConfiguration<UserOrganizationMembership>
{
    public void Configure(EntityTypeBuilder<UserOrganizationMembership> builder)
    {
        builder.ToTable("idt_UserOrganizationMemberships");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.UserId).IsRequired();
        builder.Property(m => m.OrganizationId).IsRequired();

        builder.Property(m => m.MemberRole)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(m => m.IsPrimary).IsRequired().HasDefaultValue(false);
        builder.Property(m => m.IsActive).IsRequired();
        builder.Property(m => m.JoinedAtUtc).IsRequired();
        builder.Property(m => m.GrantedByUserId);

        builder.HasIndex(m => new { m.UserId, m.OrganizationId }).IsUnique();
        builder.HasIndex(m => m.OrganizationId);
        builder.HasIndex(m => new { m.UserId, m.IsActive });

        builder.HasOne(m => m.User)
            .WithMany(u => u.OrganizationMemberships)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Organization)
            .WithMany(o => o.Memberships)
            .HasForeignKey(m => m.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
