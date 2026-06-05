using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

/// <summary>
/// PUM-B02 — Role &amp; Permission Engine
///
/// Schema changes:
///   1. Adds Scope varchar(20) column to idt_Roles (nullable, no default constraint).
///   2. Back-fills Scope for existing system roles.
///
/// Data changes:
///   3. Seeds new Platform system roles: PlatformOps, PlatformSupport, PlatformBilling, PlatformAuditor.
///   4. Seeds new Tenant system roles: TenantManager, TenantStaff, TenantViewer.
///   5. Seeds PLATFORM.* permission catalog entries in idt_Capabilities (product: SYNQ_PLATFORM).
///   6. Seeds TENANT.settings:read permission (gap in existing TENANT catalog).
///   7. Seeds role → permission mappings for PlatformAdmin (all PLATFORM.*) and TenantAdmin (all TENANT.*).
///   8. Seeds role → permission mappings for new scoped roles (read-only subsets).
///
/// All INSERT statements use INSERT IGNORE so the migration is fully idempotent.
/// All changes are strictly additive — no existing rows are deleted or renamed.
/// </summary>
[DbContext(typeof(Identity.Infrastructure.Data.IdentityDbContext))]
[Migration("20260424210000_AddRoleScopeAndBaselineRolesPermissions")]
public partial class AddRoleScopeAndBaselineRolesPermissions : Migration
{
    // Well-known seed IDs — must match SeedIds.cs exactly.
    private const string TenantLegalSynq = "20000000-0000-0000-0000-000000000001";
    private const string ProductSynqPlatform = "10000000-0000-0000-0000-000000000006";

    // ── Existing role IDs ──────────────────────────────────────────────────
    private const string RolePlatformAdmin = "30000000-0000-0000-0000-000000000001";
    private const string RoleTenantAdmin   = "30000000-0000-0000-0000-000000000002";
    private const string RoleStandardUser  = "30000000-0000-0000-0000-000000000003";

    // ── New Platform role IDs ──────────────────────────────────────────────
    private const string RolePlatformOps     = "30000000-0000-0000-0000-000000000004";
    private const string RolePlatformSupport = "30000000-0000-0000-0000-000000000005";
    private const string RolePlatformBilling = "30000000-0000-0000-0000-000000000006";
    private const string RolePlatformAuditor = "30000000-0000-0000-0000-000000000007";

    // ── New Tenant role IDs ────────────────────────────────────────────────
    private const string RoleTenantManager = "30000000-0000-0000-0000-000000000008";
    private const string RoleTenantStaff   = "30000000-0000-0000-0000-000000000009";
    private const string RoleTenantViewer  = "30000000-0000-0000-0000-000000000010";

    // ── Existing TENANT permission IDs (used in role→perm mappings) ────────
    private const string PermTenantUsersView      = "60000000-0000-0000-0000-000000000030";
    private const string PermTenantUsersManage    = "60000000-0000-0000-0000-000000000031";
    private const string PermTenantGroupsManage   = "60000000-0000-0000-0000-000000000032";
    private const string PermTenantRolesAssign    = "60000000-0000-0000-0000-000000000033";
    private const string PermTenantProductsAssign = "60000000-0000-0000-0000-000000000034";
    private const string PermTenantSettingsManage = "60000000-0000-0000-0000-000000000035";
    private const string PermTenantAuditView      = "60000000-0000-0000-0000-000000000036";

    // ── New PLATFORM permission IDs ────────────────────────────────────────
    private const string PermPlatformUsersRead     = "60000000-0000-0000-0000-000000000052";
    private const string PermPlatformUsersManage   = "60000000-0000-0000-0000-000000000053";
    private const string PermPlatformRolesRead     = "60000000-0000-0000-0000-000000000054";
    private const string PermPlatformRolesManage   = "60000000-0000-0000-0000-000000000055";
    private const string PermPlatformTenantsRead   = "60000000-0000-0000-0000-000000000056";
    private const string PermPlatformTenantsManage = "60000000-0000-0000-0000-000000000057";
    private const string PermPlatformProductsRead  = "60000000-0000-0000-0000-000000000058";
    private const string PermPlatformProductsManage= "60000000-0000-0000-0000-000000000059";
    private const string PermPlatformMonitoringRead= "60000000-0000-0000-0000-000000000060";
    private const string PermPlatformAuditRead     = "60000000-0000-0000-0000-000000000061";
    private const string PermTenantSettingsRead    = "60000000-0000-0000-0000-000000000062";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── Step 1: Add Scope column ──────────────────────────────────────────
        migrationBuilder.AddColumn<string>(
            name: "Scope",
            table: "idt_Roles",
            type: "varchar(20)",
            maxLength: 20,
            nullable: true);

        // ── Step 2: Back-fill Scope for existing system roles ─────────────────
        migrationBuilder.Sql($@"
            UPDATE `idt_Roles`
            SET `Scope` = 'Platform'
            WHERE `Id` = '{RolePlatformAdmin}';

            UPDATE `idt_Roles`
            SET `Scope` = 'Tenant'
            WHERE `Id` IN ('{RoleTenantAdmin}', '{RoleStandardUser}');
        ");

        // ── Step 3: Seed new Platform system roles ────────────────────────────
        migrationBuilder.Sql($@"
            INSERT IGNORE INTO `idt_Roles` (`Id`, `TenantId`, `Name`, `Description`, `IsSystemRole`, `Scope`, `CreatedAtUtc`, `UpdatedAtUtc`)
            VALUES
                ('{RolePlatformOps}',     '{TenantLegalSynq}', 'PlatformOps',     'Platform operations — read access to all areas, limited management', 1, 'Platform', '2024-01-01 00:00:00', '2024-01-01 00:00:00'),
                ('{RolePlatformSupport}', '{TenantLegalSynq}', 'PlatformSupport', 'Platform support — read access to users and tenants',                 1, 'Platform', '2024-01-01 00:00:00', '2024-01-01 00:00:00'),
                ('{RolePlatformBilling}', '{TenantLegalSynq}', 'PlatformBilling', 'Platform billing — manages product and entitlement configuration',    1, 'Platform', '2024-01-01 00:00:00', '2024-01-01 00:00:00'),
                ('{RolePlatformAuditor}', '{TenantLegalSynq}', 'PlatformAuditor', 'Platform auditor — read-only access to audit logs and monitoring',    1, 'Platform', '2024-01-01 00:00:00', '2024-01-01 00:00:00');
        ");

        // ── Step 4: Seed new Tenant system roles ──────────────────────────────
        migrationBuilder.Sql($@"
            INSERT IGNORE INTO `idt_Roles` (`Id`, `TenantId`, `Name`, `Description`, `IsSystemRole`, `Scope`, `CreatedAtUtc`, `UpdatedAtUtc`)
            VALUES
                ('{RoleTenantManager}', '{TenantLegalSynq}', 'TenantManager', 'Tenant manager — manages users and settings within a tenant',      1, 'Tenant', '2024-01-01 00:00:00', '2024-01-01 00:00:00'),
                ('{RoleTenantStaff}',   '{TenantLegalSynq}', 'TenantStaff',   'Tenant staff — standard operational access within a tenant',       1, 'Tenant', '2024-01-01 00:00:00', '2024-01-01 00:00:00'),
                ('{RoleTenantViewer}',  '{TenantLegalSynq}', 'TenantViewer',  'Tenant viewer — read-only access within a tenant',                 1, 'Tenant', '2024-01-01 00:00:00', '2024-01-01 00:00:00');
        ");

        // ── Step 5: Seed PLATFORM.* permissions ───────────────────────────────
        // All linked to the SYNQ_PLATFORM pseudo-product (10000000-...-000000000006).
        // Code format follows the existing TENANT.* convention: {NAMESPACE}.{domain}:{action}.
        // Consistent with the Permission.IsValidCode() regex: ^[A-Z0-9_]+\.[a-z][a-z0-9_]*(?:\:[a-z][a-z0-9_]*)*$
        migrationBuilder.Sql($@"
            INSERT IGNORE INTO `idt_Capabilities` (`Id`, `ProductId`, `Code`, `Name`, `Description`, `Category`, `IsActive`, `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedBy`, `UpdatedBy`)
            VALUES
                ('{PermPlatformUsersRead}',     '{ProductSynqPlatform}', 'PLATFORM.users:read',     'Read Platform Users',      'View the list and details of all platform users',            'Users',      1, '2024-01-01 00:00:00', NULL, NULL, NULL),
                ('{PermPlatformUsersManage}',   '{ProductSynqPlatform}', 'PLATFORM.users:manage',   'Manage Platform Users',    'Create, edit, deactivate, and lock platform users',          'Users',      1, '2024-01-01 00:00:00', NULL, NULL, NULL),
                ('{PermPlatformRolesRead}',     '{ProductSynqPlatform}', 'PLATFORM.roles:read',     'Read Platform Roles',      'View roles and role-permission mappings',                    'Roles',      1, '2024-01-01 00:00:00', NULL, NULL, NULL),
                ('{PermPlatformRolesManage}',   '{ProductSynqPlatform}', 'PLATFORM.roles:manage',   'Manage Platform Roles',    'Assign and revoke roles for platform users',                 'Roles',      1, '2024-01-01 00:00:00', NULL, NULL, NULL),
                ('{PermPlatformTenantsRead}',   '{ProductSynqPlatform}', 'PLATFORM.tenants:read',   'Read Tenants',             'View tenant list, details, and configuration',               'Tenants',    1, '2024-01-01 00:00:00', NULL, NULL, NULL),
                ('{PermPlatformTenantsManage}', '{ProductSynqPlatform}', 'PLATFORM.tenants:manage', 'Manage Tenants',           'Create and update tenant records',                           'Tenants',    1, '2024-01-01 00:00:00', NULL, NULL, NULL),
                ('{PermPlatformProductsRead}',  '{ProductSynqPlatform}', 'PLATFORM.products:read',  'Read Products',            'View product catalog and entitlements',                      'Products',   1, '2024-01-01 00:00:00', NULL, NULL, NULL),
                ('{PermPlatformProductsManage}','{ProductSynqPlatform}', 'PLATFORM.products:manage','Manage Products',          'Enable and disable product entitlements for tenants',        'Products',   1, '2024-01-01 00:00:00', NULL, NULL, NULL),
                ('{PermPlatformMonitoringRead}', '{ProductSynqPlatform}', 'PLATFORM.monitoring:read','Read Monitoring',         'View platform health metrics and service status',            'Monitoring', 1, '2024-01-01 00:00:00', NULL, NULL, NULL),
                ('{PermPlatformAuditRead}',     '{ProductSynqPlatform}', 'PLATFORM.audit:read',     'Read Platform Audit Logs', 'View platform-level audit events across all tenants',        'Audit',      1, '2024-01-01 00:00:00', NULL, NULL, NULL);
        ");

        // ── Step 6: Seed TENANT.settings:read permission ──────────────────────
        migrationBuilder.Sql($@"
            INSERT IGNORE INTO `idt_Capabilities` (`Id`, `ProductId`, `Code`, `Name`, `Description`, `Category`, `IsActive`, `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedBy`, `UpdatedBy`)
            VALUES
                ('{PermTenantSettingsRead}', '{ProductSynqPlatform}', 'TENANT.settings:read', 'Read Tenant Settings', 'View tenant configuration and preferences (read-only)', 'Settings', 1, '2024-01-01 00:00:00', NULL, NULL, NULL);
        ");

        // ── Step 7: Role → permission mappings ────────────────────────────────
        // PlatformAdmin: full PLATFORM.* + full TENANT.* (all admin capabilities)
        migrationBuilder.Sql($@"
            INSERT IGNORE INTO `idt_RoleCapabilityAssignments` (`RoleId`, `CapabilityId`, `AssignedAtUtc`, `AssignedByUserId`)
            VALUES
                -- PlatformAdmin → all PLATFORM.* permissions
                ('{RolePlatformAdmin}', '{PermPlatformUsersRead}',     '2024-01-01 00:00:00', NULL),
                ('{RolePlatformAdmin}', '{PermPlatformUsersManage}',   '2024-01-01 00:00:00', NULL),
                ('{RolePlatformAdmin}', '{PermPlatformRolesRead}',     '2024-01-01 00:00:00', NULL),
                ('{RolePlatformAdmin}', '{PermPlatformRolesManage}',   '2024-01-01 00:00:00', NULL),
                ('{RolePlatformAdmin}', '{PermPlatformTenantsRead}',   '2024-01-01 00:00:00', NULL),
                ('{RolePlatformAdmin}', '{PermPlatformTenantsManage}', '2024-01-01 00:00:00', NULL),
                ('{RolePlatformAdmin}', '{PermPlatformProductsRead}',  '2024-01-01 00:00:00', NULL),
                ('{RolePlatformAdmin}', '{PermPlatformProductsManage}','2024-01-01 00:00:00', NULL),
                ('{RolePlatformAdmin}', '{PermPlatformMonitoringRead}','2024-01-01 00:00:00', NULL),
                ('{RolePlatformAdmin}', '{PermPlatformAuditRead}',     '2024-01-01 00:00:00', NULL),
                -- PlatformOps: read all PLATFORM + read TENANT
                ('{RolePlatformOps}', '{PermPlatformUsersRead}',     '2024-01-01 00:00:00', NULL),
                ('{RolePlatformOps}', '{PermPlatformRolesRead}',     '2024-01-01 00:00:00', NULL),
                ('{RolePlatformOps}', '{PermPlatformTenantsRead}',   '2024-01-01 00:00:00', NULL),
                ('{RolePlatformOps}', '{PermPlatformProductsRead}',  '2024-01-01 00:00:00', NULL),
                ('{RolePlatformOps}', '{PermPlatformMonitoringRead}','2024-01-01 00:00:00', NULL),
                ('{RolePlatformOps}', '{PermTenantUsersView}',       '2024-01-01 00:00:00', NULL),
                ('{RolePlatformOps}', '{PermTenantSettingsRead}',    '2024-01-01 00:00:00', NULL),
                -- PlatformSupport: read users and tenants
                ('{RolePlatformSupport}', '{PermPlatformUsersRead}',   '2024-01-01 00:00:00', NULL),
                ('{RolePlatformSupport}', '{PermPlatformTenantsRead}', '2024-01-01 00:00:00', NULL),
                ('{RolePlatformSupport}', '{PermTenantUsersView}',     '2024-01-01 00:00:00', NULL),
                -- PlatformBilling: manage products and entitlements
                ('{RolePlatformBilling}', '{PermPlatformTenantsRead}',   '2024-01-01 00:00:00', NULL),
                ('{RolePlatformBilling}', '{PermPlatformProductsRead}',  '2024-01-01 00:00:00', NULL),
                ('{RolePlatformBilling}', '{PermPlatformProductsManage}','2024-01-01 00:00:00', NULL),
                -- PlatformAuditor: read-only monitoring + audit
                ('{RolePlatformAuditor}', '{PermPlatformMonitoringRead}','2024-01-01 00:00:00', NULL),
                ('{RolePlatformAuditor}', '{PermPlatformAuditRead}',     '2024-01-01 00:00:00', NULL),
                ('{RolePlatformAuditor}', '{PermTenantAuditView}',       '2024-01-01 00:00:00', NULL),
                -- TenantAdmin → all TENANT.* permissions (existing catalog)
                ('{RoleTenantAdmin}', '{PermTenantUsersView}',      '2024-01-01 00:00:00', NULL),
                ('{RoleTenantAdmin}', '{PermTenantUsersManage}',    '2024-01-01 00:00:00', NULL),
                ('{RoleTenantAdmin}', '{PermTenantGroupsManage}',   '2024-01-01 00:00:00', NULL),
                ('{RoleTenantAdmin}', '{PermTenantRolesAssign}',    '2024-01-01 00:00:00', NULL),
                ('{RoleTenantAdmin}', '{PermTenantProductsAssign}', '2024-01-01 00:00:00', NULL),
                ('{RoleTenantAdmin}', '{PermTenantSettingsManage}', '2024-01-01 00:00:00', NULL),
                ('{RoleTenantAdmin}', '{PermTenantSettingsRead}',   '2024-01-01 00:00:00', NULL),
                ('{RoleTenantAdmin}', '{PermTenantAuditView}',      '2024-01-01 00:00:00', NULL),
                -- TenantManager: user management + settings manage (no roles/products assign)
                ('{RoleTenantManager}', '{PermTenantUsersView}',    '2024-01-01 00:00:00', NULL),
                ('{RoleTenantManager}', '{PermTenantUsersManage}',  '2024-01-01 00:00:00', NULL),
                ('{RoleTenantManager}', '{PermTenantGroupsManage}', '2024-01-01 00:00:00', NULL),
                ('{RoleTenantManager}', '{PermTenantSettingsManage}','2024-01-01 00:00:00', NULL),
                ('{RoleTenantManager}', '{PermTenantSettingsRead}', '2024-01-01 00:00:00', NULL),
                -- TenantStaff: view users + read settings
                ('{RoleTenantStaff}', '{PermTenantUsersView}',   '2024-01-01 00:00:00', NULL),
                ('{RoleTenantStaff}', '{PermTenantSettingsRead}','2024-01-01 00:00:00', NULL),
                -- TenantViewer: read-only (users view only)
                ('{RoleTenantViewer}', '{PermTenantUsersView}',   '2024-01-01 00:00:00', NULL),
                ('{RoleTenantViewer}', '{PermTenantSettingsRead}','2024-01-01 00:00:00', NULL);
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Remove role-permission mappings for new roles
        migrationBuilder.Sql($@"
            DELETE FROM `idt_RoleCapabilityAssignments`
            WHERE `RoleId` IN (
                '{RolePlatformAdmin}',
                '{RolePlatformOps}',
                '{RolePlatformSupport}',
                '{RolePlatformBilling}',
                '{RolePlatformAuditor}',
                '{RoleTenantAdmin}',
                '{RoleTenantManager}',
                '{RoleTenantStaff}',
                '{RoleTenantViewer}'
            )
            AND `CapabilityId` IN (
                '{PermPlatformUsersRead}',
                '{PermPlatformUsersManage}',
                '{PermPlatformRolesRead}',
                '{PermPlatformRolesManage}',
                '{PermPlatformTenantsRead}',
                '{PermPlatformTenantsManage}',
                '{PermPlatformProductsRead}',
                '{PermPlatformProductsManage}',
                '{PermPlatformMonitoringRead}',
                '{PermPlatformAuditRead}',
                '{PermTenantSettingsRead}'
            );
        ");

        // Remove new permissions
        migrationBuilder.Sql($@"
            DELETE FROM `idt_Capabilities`
            WHERE `Id` IN (
                '{PermPlatformUsersRead}',
                '{PermPlatformUsersManage}',
                '{PermPlatformRolesRead}',
                '{PermPlatformRolesManage}',
                '{PermPlatformTenantsRead}',
                '{PermPlatformTenantsManage}',
                '{PermPlatformProductsRead}',
                '{PermPlatformProductsManage}',
                '{PermPlatformMonitoringRead}',
                '{PermPlatformAuditRead}',
                '{PermTenantSettingsRead}'
            );
        ");

        // Remove new roles
        migrationBuilder.Sql($@"
            DELETE FROM `idt_Roles`
            WHERE `Id` IN (
                '{RolePlatformOps}',
                '{RolePlatformSupport}',
                '{RolePlatformBilling}',
                '{RolePlatformAuditor}',
                '{RoleTenantManager}',
                '{RoleTenantStaff}',
                '{RoleTenantViewer}'
            );
        ");

        // Remove Scope column
        migrationBuilder.DropColumn(name: "Scope", table: "idt_Roles");
    }
}
