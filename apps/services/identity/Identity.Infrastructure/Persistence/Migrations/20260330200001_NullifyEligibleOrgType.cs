using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

/// <summary>
/// Phase F prep: nulls EligibleOrgType for all 7 restricted ProductRoles.
///
/// SAFETY CHECK: this migration only targets the 7 well-known seed IDs whose
/// corresponding ProductOrganizationTypeRule rows were verified present in
/// migration 20260330110003_AddProductOrgTypeRules. The legacy string values
/// are preserved in comments for historical reference.
///
/// Before: legacyStringOnly=0, withBothPaths=7
/// After:  legacyStringOnly=0, withBothPaths=0, withDbRuleOnly=7
/// </summary>
[Migration("20260330200001_NullifyEligibleOrgType")]
public partial class NullifyEligibleOrgType : Migration
{
    // Seed IDs (mirrors SeedIds.cs constants)
    private const string CareConnectReferrer = "50000000-0000-0000-0000-000000000001"; // LAW_FIRM
    private const string CareConnectReceiver = "50000000-0000-0000-0000-000000000002"; // PROVIDER
    private const string SynqLienSeller      = "50000000-0000-0000-0000-000000000003"; // LAW_FIRM
    private const string SynqLienBuyer       = "50000000-0000-0000-0000-000000000004"; // LIEN_OWNER
    private const string SynqLienHolder      = "50000000-0000-0000-0000-000000000005"; // LIEN_OWNER
    private const string SynqFundReferrer    = "50000000-0000-0000-0000-000000000006"; // LAW_FIRM
    private const string SynqFundFunder      = "50000000-0000-0000-0000-000000000007"; // FUNDER

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Null out the legacy EligibleOrgType string for every restricted ProductRole.
        // Each of these roles already has a corresponding active ProductOrganizationTypeRule
        // (seeded in migration 20260330110003). The OrgTypeRule is the authoritative path;
        // the string is the legacy fallback that is now redundant.
        //
        // After this migration, AuthService.IsEligibleWithPath will ALWAYS use Path 1
        // (DB-backed rule table) for these roles because pr.OrgTypeRules.Count > 0.
        // Path 2 (legacy string check) becomes dead code.
        migrationBuilder.Sql($@"
UPDATE `ProductRoles`
SET    `EligibleOrgType` = NULL
WHERE  `Id` IN (
    '{CareConnectReferrer}',
    '{CareConnectReceiver}',
    '{SynqLienSeller}',
    '{SynqLienBuyer}',
    '{SynqLienHolder}',
    '{SynqFundReferrer}',
    '{SynqFundFunder}'
);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Restore the original EligibleOrgType values from the seed data.
        migrationBuilder.Sql($@"
UPDATE `ProductRoles` SET `EligibleOrgType` = 'LAW_FIRM'   WHERE `Id` = '{CareConnectReferrer}';
UPDATE `ProductRoles` SET `EligibleOrgType` = 'PROVIDER'   WHERE `Id` = '{CareConnectReceiver}';
UPDATE `ProductRoles` SET `EligibleOrgType` = 'LAW_FIRM'   WHERE `Id` = '{SynqLienSeller}';
UPDATE `ProductRoles` SET `EligibleOrgType` = 'LIEN_OWNER' WHERE `Id` = '{SynqLienBuyer}';
UPDATE `ProductRoles` SET `EligibleOrgType` = 'LIEN_OWNER' WHERE `Id` = '{SynqLienHolder}';
UPDATE `ProductRoles` SET `EligibleOrgType` = 'LAW_FIRM'   WHERE `Id` = '{SynqFundReferrer}';
UPDATE `ProductRoles` SET `EligibleOrgType` = 'FUNDER'     WHERE `Id` = '{SynqFundFunder}';");
    }
}
