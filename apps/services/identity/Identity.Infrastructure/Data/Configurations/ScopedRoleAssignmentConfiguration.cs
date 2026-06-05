using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class ScopedRoleAssignmentConfiguration : IEntityTypeConfiguration<ScopedRoleAssignment>
{
    public void Configure(EntityTypeBuilder<ScopedRoleAssignment> builder)
    {
        builder.ToTable("idt_ScopedRoleAssignments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.UserId).IsRequired();
        builder.Property(a => a.RoleId).IsRequired();
        builder.Property(a => a.ScopeType).IsRequired().HasMaxLength(30);

        builder.Property(a => a.TenantId);
        builder.Property(a => a.OrganizationId);
        builder.Property(a => a.OrganizationRelationshipId);
        builder.Property(a => a.ProductId);

        builder.Property(a => a.IsActive).IsRequired();
        builder.Property(a => a.AssignedAtUtc).IsRequired();
        builder.Property(a => a.UpdatedAtUtc).IsRequired();
        builder.Property(a => a.AssignedByUserId);

        builder.HasIndex(a => new { a.UserId, a.RoleId, a.ScopeType, a.OrganizationId, a.ProductId })
            .HasDatabaseName("IX_ScopedRoleAssignments_User_Role_Scope");
        builder.HasIndex(a => new { a.UserId, a.IsActive });
        builder.HasIndex(a => a.OrganizationRelationshipId);

        builder.HasOne(a => a.User)
            .WithMany(u => u.ScopedRoleAssignments)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Role)
            .WithMany()
            .HasForeignKey(a => a.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
