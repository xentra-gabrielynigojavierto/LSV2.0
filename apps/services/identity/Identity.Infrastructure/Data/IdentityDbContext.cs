using Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Data;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantDomain> TenantDomains => Set<TenantDomain>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<TenantProduct> TenantProducts => Set<TenantProduct>();

    // Organizations
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationDomain> OrganizationDomains => Set<OrganizationDomain>();
    public DbSet<OrganizationProduct> OrganizationProducts => Set<OrganizationProduct>();

    // Product role model
    public DbSet<ProductRole> ProductRoles => Set<ProductRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermissionMapping> RolePermissionMappings => Set<RolePermissionMapping>();

    // Role ↔ Permission assignments (for tenant custom roles)
    public DbSet<RolePermissionAssignment> RolePermissionAssignments => Set<RolePermissionAssignment>();

    // User organization membership
    public DbSet<UserOrganizationMembership> UserOrganizationMemberships => Set<UserOrganizationMembership>();

    // Audit
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Platform Phase 1–4 (authoritative models)
    public DbSet<OrganizationType>              OrganizationTypes              => Set<OrganizationType>();
    public DbSet<RelationshipType>              RelationshipTypes              => Set<RelationshipType>();
    public DbSet<OrganizationRelationship>      OrganizationRelationships      => Set<OrganizationRelationship>();
    public DbSet<ProductRelationshipTypeRule>   ProductRelationshipTypeRules   => Set<ProductRelationshipTypeRule>();
    public DbSet<ProductOrganizationTypeRule>   ProductOrganizationTypeRules   => Set<ProductOrganizationTypeRule>();
    public DbSet<ScopedRoleAssignment>          ScopedRoleAssignments          => Set<ScopedRoleAssignment>();

    // UIX-002: User Management
    // LS-COR-AUT-007: TenantGroups/GroupMemberships tables dropped — see migration.
    public DbSet<UserInvitation>                UserInvitations                => Set<UserInvitation>();

    // UIX-003-03: Security / admin-triggered password reset
    public DbSet<PasswordResetToken>            PasswordResetTokens            => Set<PasswordResetToken>();

    // LS-COR-AUT-002: Access Source-of-Truth
    public DbSet<TenantProductEntitlement>      TenantProductEntitlements       => Set<TenantProductEntitlement>();
    public DbSet<UserProductAccess>             UserProductAccessRecords        => Set<UserProductAccess>();
    public DbSet<UserRoleAssignment>            UserRoleAssignments             => Set<UserRoleAssignment>();

    // LS-COR-AUT-004: Groups + Inherited Access
    public DbSet<AccessGroup>                   AccessGroups                    => Set<AccessGroup>();
    public DbSet<AccessGroupMembership>         AccessGroupMemberships          => Set<AccessGroupMembership>();
    public DbSet<GroupProductAccess>            GroupProductAccessRecords        => Set<GroupProductAccess>();
    public DbSet<GroupRoleAssignment>           GroupRoleAssignments             => Set<GroupRoleAssignment>();

    // LS-COR-AUT-011: ABAC Policies
    public DbSet<Policy>                        Policies                        => Set<Policy>();
    public DbSet<PolicyRule>                    PolicyRules                      => Set<PolicyRule>();
    public DbSet<PermissionPolicy>             PermissionPolicies               => Set<PermissionPolicy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
