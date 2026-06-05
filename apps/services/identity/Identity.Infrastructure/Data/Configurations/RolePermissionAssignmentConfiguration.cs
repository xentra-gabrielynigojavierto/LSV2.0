using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class RolePermissionAssignmentConfiguration : IEntityTypeConfiguration<RolePermissionAssignment>
{
    public void Configure(EntityTypeBuilder<RolePermissionAssignment> builder)
    {
        builder.ToTable("idt_RoleCapabilityAssignments");

        builder.HasKey(a => new { a.RoleId, a.PermissionId });

        builder.Property(a => a.PermissionId).HasColumnName("CapabilityId");

        builder.Property(a => a.AssignedAtUtc).IsRequired();
        builder.Property(a => a.AssignedByUserId).HasColumnType("char(36)");

        builder.HasOne(a => a.Role)
            .WithMany(r => r.RolePermissionAssignments)
            .HasForeignKey(a => a.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Permission)
            .WithMany(c => c.RolePermissionAssignments)
            .HasForeignKey(a => a.PermissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.RoleId);
        builder.HasIndex(a => a.PermissionId);

        // LS-ID-TNT-011: Seed default tenant role → tenant permission mappings.
        //
        // TenantAdmin gets all 8 tenant permissions (full administrative access).
        // StandardUser gets only users:view (can see the user list, cannot administer it).
        // PlatformAdmin is handled by code-level bypass (IsPlatformAdmin) so no seed needed.
        //
        // These mappings are read by EffectiveAccessService.ResolvePermissionsAsync to
        // include tenant permissions in the JWT `permissions` claim alongside product permissions.

        var ta  = SeedIds.RoleTenantAdmin;
        var std = SeedIds.RoleStandardUser;
        var at  = SeedIds.SeededAt;

        builder.HasData(
            // TenantAdmin → all tenant permissions
            new { RoleId = ta, PermissionId = SeedIds.PermTenantUsersView,         AssignedAtUtc = at, AssignedByUserId = (Guid?)null },
            new { RoleId = ta, PermissionId = SeedIds.PermTenantUsersManage,       AssignedAtUtc = at, AssignedByUserId = (Guid?)null },
            new { RoleId = ta, PermissionId = SeedIds.PermTenantGroupsManage,      AssignedAtUtc = at, AssignedByUserId = (Guid?)null },
            new { RoleId = ta, PermissionId = SeedIds.PermTenantRolesAssign,       AssignedAtUtc = at, AssignedByUserId = (Guid?)null },
            new { RoleId = ta, PermissionId = SeedIds.PermTenantProductsAssign,    AssignedAtUtc = at, AssignedByUserId = (Guid?)null },
            new { RoleId = ta, PermissionId = SeedIds.PermTenantSettingsManage,    AssignedAtUtc = at, AssignedByUserId = (Guid?)null },
            new { RoleId = ta, PermissionId = SeedIds.PermTenantAuditView,         AssignedAtUtc = at, AssignedByUserId = (Guid?)null },
            new { RoleId = ta, PermissionId = SeedIds.PermTenantInvitationsManage, AssignedAtUtc = at, AssignedByUserId = (Guid?)null },

            // StandardUser → read-only tenant user visibility
            new { RoleId = std, PermissionId = SeedIds.PermTenantUsersView,        AssignedAtUtc = at, AssignedByUserId = (Guid?)null }
        );
    }
}
