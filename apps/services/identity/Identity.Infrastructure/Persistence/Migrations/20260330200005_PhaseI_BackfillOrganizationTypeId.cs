using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [Migration("20260330200005_PhaseI_BackfillOrganizationTypeId")]
    public partial class PhaseI_BackfillOrganizationTypeId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Phase I: backfill OrganizationTypeId from the OrgType code string for any
            // existing Organization rows where OrganizationTypeId is still null.
            //
            // Organization.Create() (Phase H) now auto-resolves OrganizationTypeId, so all
            // organizations created after Phase H already have the FK set.  This migration
            // closes the gap for any rows created before Phase H.
            //
            // The five catalog GUIDs are sourced from OrgTypeMapper / SeedIds and are
            // stable seeded values that will not change.

            migrationBuilder.Sql(
                "UPDATE Organizations SET OrganizationTypeId = '70000000-0000-0000-0000-000000000001' " +
                "WHERE OrgType = 'INTERNAL' AND OrganizationTypeId IS NULL;");

            migrationBuilder.Sql(
                "UPDATE Organizations SET OrganizationTypeId = '70000000-0000-0000-0000-000000000002' " +
                "WHERE OrgType = 'LAW_FIRM' AND OrganizationTypeId IS NULL;");

            migrationBuilder.Sql(
                "UPDATE Organizations SET OrganizationTypeId = '70000000-0000-0000-0000-000000000003' " +
                "WHERE OrgType = 'PROVIDER' AND OrganizationTypeId IS NULL;");

            migrationBuilder.Sql(
                "UPDATE Organizations SET OrganizationTypeId = '70000000-0000-0000-0000-000000000004' " +
                "WHERE OrgType = 'FUNDER' AND OrganizationTypeId IS NULL;");

            migrationBuilder.Sql(
                "UPDATE Organizations SET OrganizationTypeId = '70000000-0000-0000-0000-000000000005' " +
                "WHERE OrgType = 'LIEN_OWNER' AND OrganizationTypeId IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data backfill — intentional no-op for Down.
            // Reversing this migration would incorrectly null-out OrganizationTypeId values
            // that may have been set legitimately by this or other operations.
        }
    }
}
