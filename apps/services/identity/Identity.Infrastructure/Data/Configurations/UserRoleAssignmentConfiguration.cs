using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class UserRoleAssignmentConfiguration : IEntityTypeConfiguration<UserRoleAssignment>
{
    public void Configure(EntityTypeBuilder<UserRoleAssignment> builder)
    {
        builder.ToTable("idt_UserRoleAssignments");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.UserId).IsRequired();
        builder.Property(e => e.ProductCode).HasMaxLength(50);
        builder.Property(e => e.RoleCode).IsRequired().HasMaxLength(100);
        builder.Property(e => e.AssignmentStatus)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(20);
        builder.Property(e => e.OrganizationId);
        builder.Property(e => e.SourceType).IsRequired().HasMaxLength(20).HasDefaultValue("Direct");
        builder.Property(e => e.AssignedAtUtc);
        builder.Property(e => e.RemovedAtUtc);
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();
        builder.Property(e => e.CreatedByUserId);
        builder.Property(e => e.UpdatedByUserId);

        builder.HasIndex(e => new { e.TenantId, e.UserId, e.RoleCode })
               .HasDatabaseName("IX_UserRoleAssignments_TenantId_UserId_RoleCode");
    }
}
