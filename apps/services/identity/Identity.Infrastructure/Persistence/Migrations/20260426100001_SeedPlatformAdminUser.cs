using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

/// <summary>
/// Seeds the LegalSynq platform admin user (admin@legalsynq.com) into the
/// LEGALSYNQ tenant.  The user was expected to exist before the
/// SeedAdminOrgMembership migration ran, but the bootstrap provisioning step
/// was never executed against this database instance, so all logins to the
/// control center returned 401 "Invalid credentials."
///
/// This migration is idempotent (INSERT IGNORE) and is also applied via a
/// StartupMigrationGuard in Identity.Api/Program.cs so it runs exactly once
/// and is recorded in __EFMigrationsHistory.
///
/// Password: Admin123!
/// BCrypt hash (workFactor=12, BCrypt.Net-Next, $2a$ prefix):
///   $2a$12$/wvZFZf.T4qlqcaD9gn5GOKmjvXHCbr3/wUXu4wtRwLzj4W4XXA2a
/// </summary>
[Migration("20260426100001_SeedPlatformAdminUser")]
public partial class SeedPlatformAdminUser : Migration
{
    const string UserId            = "50000000-0000-0000-0000-000000000001";
    const string TenantId          = "20000000-0000-0000-0000-000000000001";
    const string OrgId             = "40000000-0000-0000-0000-000000000001";
    const string OrgMembershipId   = "40000000-0000-0000-0000-000000000003";
    const string PlatformAdminRole = "30000000-0000-0000-0000-000000000001";
    const string SraId             = "90000000-0000-0000-0000-000000000001";
    const string PasswordHash      =
        "$2a$12$/wvZFZf.T4qlqcaD9gn5GOKmjvXHCbr3/wUXu4wtRwLzj4W4XXA2a";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($@"
INSERT IGNORE INTO `idt_Users`
    (`Id`, `TenantId`, `Email`, `PasswordHash`,
     `FirstName`, `LastName`, `IsActive`,
     `IsLocked`, `FailedLoginCount`, `UserType`,
     `CreatedAtUtc`, `UpdatedAtUtc`)
VALUES (
    '{UserId}', '{TenantId}',
    'admin@legalsynq.com', '{PasswordHash}',
    'Platform', 'Admin', 1, 0, 0, 'PlatformInternal',
    '2024-01-01 00:00:00', '2024-01-01 00:00:00'
);");

        migrationBuilder.Sql($@"
INSERT IGNORE INTO `idt_UserOrganizationMemberships`
    (`Id`, `UserId`, `OrganizationId`, `MemberRole`,
     `IsActive`, `JoinedAtUtc`, `GrantedByUserId`)
VALUES (
    '{OrgMembershipId}', '{UserId}', '{OrgId}',
    'OWNER', 1, '2024-01-01 00:00:00', NULL
);");

        migrationBuilder.Sql($@"
INSERT IGNORE INTO `idt_ScopedRoleAssignments`
    (`Id`, `UserId`, `RoleId`, `ScopeType`, `TenantId`,
     `OrganizationId`, `OrganizationRelationshipId`, `ProductId`,
     `IsActive`, `AssignedAtUtc`, `UpdatedAtUtc`, `AssignedByUserId`)
VALUES (
    '{SraId}', '{UserId}', '{PlatformAdminRole}', 'GLOBAL', '{TenantId}',
    NULL, NULL, NULL, 1,
    '2024-01-01 00:00:00', '2024-01-01 00:00:00', NULL
);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($@"
DELETE FROM `idt_ScopedRoleAssignments` WHERE `Id` = '{SraId}';
DELETE FROM `idt_UserOrganizationMemberships` WHERE `Id` = '{OrgMembershipId}';
DELETE FROM `idt_Users` WHERE `Id` = '{UserId}';");
    }
}
