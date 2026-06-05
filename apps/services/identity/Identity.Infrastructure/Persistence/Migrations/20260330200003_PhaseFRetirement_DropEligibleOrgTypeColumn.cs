using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

/// <summary>
/// Phase F retirement: drops the ProductRole.EligibleOrgType column.
///
/// GATING CONDITIONS (must be verified before applying):
///   1. legacyStringOnly = 0  — no ProductRole relies on EligibleOrgType without an OrgTypeRule.
///      Confirmed by migration 20260330110003 (OrgTypeRules seeded) + code inspection.
///   2. withBothPaths  = 0    — all EligibleOrgType values have been nulled.
///      Confirmed by migration 20260330200001 (NullifyEligibleOrgType).
///   3. AuthService.IsEligibleWithPath Path 2 removed from code (done in this PR).
///
/// After this migration:
///   - The ProductRoles table no longer has an EligibleOrgType column.
///   - All eligibility checks go exclusively through ProductOrganizationTypeRules (Path 1).
///   - The startup diagnostic and /api/admin/legacy-coverage endpoint reflect this.
///
/// This migration is safe to apply because:
///   - All 7 restricted ProductRoles have confirmed active OrgTypeRule rows (Phase E).
///   - EligibleOrgType was nulled out in migration 20260330200001 before column drop.
///   - AuthService no longer reads EligibleOrgType after this code change.
/// </summary>
[Migration("20260330200003_PhaseFRetirement_DropEligibleOrgTypeColumn")]
public partial class PhaseFRetirement_DropEligibleOrgTypeColumn : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Drop the composite index before dropping the column.
        migrationBuilder.Sql(@"
SET @db = DATABASE();
SET @ix = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
              WHERE TABLE_SCHEMA = @db
              AND   TABLE_NAME  = 'ProductRoles'
              AND   INDEX_NAME  = 'IX_ProductRoles_ProductId_EligibleOrgType') > 0,
    'DROP INDEX `IX_ProductRoles_ProductId_EligibleOrgType` ON `ProductRoles`',
    'SELECT 1');
PREPARE stmt FROM @ix; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

        // Drop the column itself.
        migrationBuilder.Sql(@"
ALTER TABLE `ProductRoles`
    DROP COLUMN IF EXISTS `EligibleOrgType`;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Re-add the column (nulled out — values lost, cannot be restored without data).
        migrationBuilder.Sql(@"
ALTER TABLE `ProductRoles`
    ADD COLUMN `EligibleOrgType` varchar(50) CHARACTER SET utf8mb4 NULL;");

        migrationBuilder.Sql(@"
SET @db = DATABASE();
SET @ix = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
              WHERE TABLE_SCHEMA = @db
              AND   TABLE_NAME  = 'ProductRoles'
              AND   INDEX_NAME  = 'IX_ProductRoles_ProductId_EligibleOrgType') = 0,
    'CREATE INDEX `IX_ProductRoles_ProductId_EligibleOrgType` ON `ProductRoles` (`ProductId`, `EligibleOrgType`)',
    'SELECT 1');
PREPARE stmt FROM @ix; EXECUTE stmt; DEALLOCATE PREPARE stmt;");
    }
}
