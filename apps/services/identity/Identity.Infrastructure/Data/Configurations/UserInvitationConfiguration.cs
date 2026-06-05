using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class UserInvitationConfiguration : IEntityTypeConfiguration<UserInvitation>
{
    public void Configure(EntityTypeBuilder<UserInvitation> builder)
    {
        builder.ToTable("idt_UserInvitations");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.UserId).IsRequired();
        builder.Property(i => i.TenantId).IsRequired();
        builder.Property(i => i.InvitedByUserId);
        builder.Property(i => i.TokenHash).IsRequired().HasMaxLength(512);
        builder.Property(i => i.Status).IsRequired().HasMaxLength(20);
        builder.Property(i => i.PortalOrigin).IsRequired().HasMaxLength(30);
        builder.Property(i => i.ExpiresAtUtc).IsRequired();
        builder.Property(i => i.AcceptedAtUtc);
        builder.Property(i => i.RevokedAtUtc);
        builder.Property(i => i.CreatedAtUtc).IsRequired();

        builder.HasIndex(i => i.UserId);
        builder.HasIndex(i => new { i.UserId, i.Status });
        builder.HasIndex(i => i.TokenHash).IsUnique();

        builder.HasOne(i => i.User)
            .WithMany()
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
