using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedAdminOrgMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Link the seeded admin user to the LegalSynq Internal organization.
            // Uses a subquery on email so we don't hardcode the user's runtime-generated GUID.
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `UserOrganizationMemberships`
                    (`Id`, `UserId`, `OrganizationId`, `MemberRole`, `IsActive`, `JoinedAtUtc`, `GrantedByUserId`)
                SELECT
                    '40000000-0000-0000-0000-000000000003',
                    u.`Id`,
                    '40000000-0000-0000-0000-000000000001',
                    'OWNER',
                    1,
                    '2024-01-01 00:00:00',
                    NULL
                FROM `Users` u
                WHERE u.`Email` = 'admin@legalsynq.com'
                  AND u.`TenantId` = '20000000-0000-0000-0000-000000000001'
                LIMIT 1;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM `UserOrganizationMemberships`
                WHERE `Id` = '40000000-0000-0000-0000-000000000003';
            ");
        }
    }
}
