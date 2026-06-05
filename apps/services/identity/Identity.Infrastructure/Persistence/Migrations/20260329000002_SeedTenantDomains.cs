using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedTenantDomains : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Seed the primary subdomain for the LegalSynq internal tenant.
            // DomainType = SUBDOMAIN: used by the gateway to resolve tenant from Host header.
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `TenantDomains`
                    (`Id`, `TenantId`, `Domain`, `DomainType`, `IsPrimary`, `IsVerified`, `CreatedAtUtc`)
                VALUES
                    ('70000000-0000-0000-0000-000000000001',
                     '20000000-0000-0000-0000-000000000001',
                     'legalsynq.legalsynq.com',
                     'SUBDOMAIN',
                     1,
                     1,
                     '2024-01-01 00:00:00');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM `TenantDomains`
                WHERE `Id` = '70000000-0000-0000-0000-000000000001';
            ");
        }
    }
}
