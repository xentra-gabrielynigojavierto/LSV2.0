using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class AccessGroupMembershipConfiguration : IEntityTypeConfiguration<AccessGroupMembership>
{
    public void Configure(EntityTypeBuilder<AccessGroupMembership> builder)
    {
        builder.ToTable("idt_AccessGroupMemberships");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.GroupId).IsRequired();
        builder.Property(e => e.UserId).IsRequired();
        builder.Property(e => e.MembershipStatus).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.AddedAtUtc).IsRequired();
        builder.Property(e => e.RemovedAtUtc);
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();
        builder.Property(e => e.CreatedByUserId);
        builder.Property(e => e.UpdatedByUserId);

        builder.HasIndex(e => new { e.TenantId, e.GroupId, e.UserId })
               .IsUnique()
               .HasDatabaseName("IX_AccessGroupMemberships_TenantId_GroupId_UserId");
    }
}
