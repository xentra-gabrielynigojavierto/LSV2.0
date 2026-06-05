using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Fix-up migration that uses subqueries against the live tenant/org IDs in the DB
    /// (rather than hardcoded GUIDs) to:
    ///   1. Enable CareConnect on every organization that belongs to HARTWELL or MERIDIAN.
    ///   2. Give every HARTWELL/MERIDIAN user without an active org membership one,
    ///      linked to the first org in their tenant (oldest by CreatedAtUtc).
    /// </summary>
    public partial class FixCareConnectOrgProducts : Migration
    {
        private const string CareConnectProductId = "10000000-0000-0000-0000-000000000003";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Enable CareConnect for every org in HARTWELL or MERIDIAN ──
            // Uses INSERT IGNORE so it is safe to re-run; duplicate (OrgId,ProductId) pairs
            // will be silently skipped.
            migrationBuilder.Sql($@"
                INSERT IGNORE INTO `OrganizationProducts`
                    (`OrganizationId`, `ProductId`, `IsEnabled`, `EnabledAtUtc`, `GrantedByUserId`)
                SELECT
                    o.`Id`,
                    '{CareConnectProductId}',
                    1,
                    '2024-02-16 09:00:00',
                    NULL
                FROM `Organizations` o
                INNER JOIN `Tenants` t ON t.`Id` = o.`TenantId`
                WHERE t.`Code` IN ('HARTWELL', 'MERIDIAN');
            ");

            // ── 2. Membership fix-up for users with no active org membership ──
            // For each orphaned user in HARTWELL/MERIDIAN, pick the oldest org in the
            // tenant (by CreatedAtUtc) and insert a membership row.
            // UUID() is used for the membership Id (non-deterministic is fine here).
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `UserOrganizationMemberships`
                    (`Id`, `UserId`, `OrganizationId`, `MemberRole`, `IsActive`, `JoinedAtUtc`, `GrantedByUserId`)
                SELECT
                    UUID(),
                    u.`Id`,
                    (
                        SELECT o2.`Id`
                        FROM `Organizations` o2
                        WHERE o2.`TenantId` = u.`TenantId`
                        ORDER BY o2.`CreatedAtUtc`
                        LIMIT 1
                    ),
                    CASE
                        WHEN EXISTS (
                            SELECT 1
                            FROM `UserRoles` ur
                            INNER JOIN `Roles` r ON r.`Id` = ur.`RoleId`
                            WHERE ur.`UserId` = u.`Id`
                              AND r.`Name` = 'TenantAdmin'
                        ) THEN 'ADMIN'
                        ELSE 'MEMBER'
                    END,
                    1,
                    NOW(),
                    NULL
                FROM `Users` u
                INNER JOIN `Tenants` t ON t.`Id` = u.`TenantId`
                WHERE t.`Code` IN ('HARTWELL', 'MERIDIAN')
                  AND u.`IsActive` = 1
                  AND NOT EXISTS (
                      SELECT 1
                      FROM `UserOrganizationMemberships` m
                      WHERE m.`UserId` = u.`Id`
                        AND m.`IsActive` = 1
                  )
                  AND EXISTS (
                      SELECT 1
                      FROM `Organizations` o3
                      WHERE o3.`TenantId` = u.`TenantId`
                  );
            ");

            // ── 3. Ensure TenantProducts row exists (legacy display layer) ────
            migrationBuilder.Sql($@"
                INSERT IGNORE INTO `TenantProducts`
                    (`TenantId`, `ProductId`, `IsEnabled`, `EnabledAtUtc`)
                SELECT
                    t.`Id`,
                    '{CareConnectProductId}',
                    1,
                    '2024-02-16 09:00:00'
                FROM `Tenants` t
                WHERE t.`Code` IN ('HARTWELL', 'MERIDIAN');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove org product rows that this migration may have added.
            // User org memberships created by step 2 are left in place (safe to keep).
            migrationBuilder.Sql($@"
                DELETE op FROM `OrganizationProducts` op
                INNER JOIN `Organizations` o ON o.`Id` = op.`OrganizationId`
                INNER JOIN `Tenants` t ON t.`Id` = o.`TenantId`
                WHERE t.`Code` IN ('HARTWELL', 'MERIDIAN')
                  AND op.`ProductId` = '{CareConnectProductId}';
            ");

            migrationBuilder.Sql($@"
                DELETE tp FROM `TenantProducts` tp
                INNER JOIN `Tenants` t ON t.`Id` = tp.`TenantId`
                WHERE t.`Code` IN ('HARTWELL', 'MERIDIAN')
                  AND tp.`ProductId` = '{CareConnectProductId}';
            ");
        }
    }
}
