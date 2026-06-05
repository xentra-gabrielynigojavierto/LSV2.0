using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CorrectSynqLienRoleMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Correct SYNQLIEN_SELLER: LAW_FIRM -> PROVIDER
            // Business model: Providers (medical) create and sell liens against their
            // receivables, not law firms. Law firms are the lien buyers/referrers in SynqFund.
            migrationBuilder.Sql(@"
                UPDATE `ProductRoles`
                SET `EligibleOrgType` = 'PROVIDER'
                WHERE `Code` = 'SYNQLIEN_SELLER';
            ");

            // SYNQLIEN_BUYER and SYNQLIEN_HOLDER were already correct (LIEN_OWNER).
            // The following are no-ops if already correct, but included for safety:
            migrationBuilder.Sql(@"
                UPDATE `ProductRoles`
                SET `EligibleOrgType` = 'LIEN_OWNER'
                WHERE `Code` IN ('SYNQLIEN_BUYER', 'SYNQLIEN_HOLDER');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert SYNQLIEN_SELLER back to original (incorrect) seed value.
            migrationBuilder.Sql(@"
                UPDATE `ProductRoles`
                SET `EligibleOrgType` = 'LAW_FIRM'
                WHERE `Code` = 'SYNQLIEN_SELLER';
            ");
        }
    }
}
