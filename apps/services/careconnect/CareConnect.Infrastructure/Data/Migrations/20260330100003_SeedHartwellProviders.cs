using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedHartwellProviders : Migration
    {
        // ── Provider IDs (prefix b1000000-...) ───────────────────────────────
        //   b1000000-0000-0000-0000-000000000001  Dr. Thomas Reed       (CHIRO)
        //   b1000000-0000-0000-0000-000000000002  Sandra Nguyen PT      (PT)
        //   b1000000-0000-0000-0000-000000000003  Dr. James Harrington  (ORTHO)
        //   b1000000-0000-0000-0000-000000000004  Nevada Imaging Center (IMG)
        //   b1000000-0000-0000-0000-000000000005  Dr. Priya Mehta       (PAIN)
        //
        // ── Tenant ────────────────────────────────────────────────────────────
        //   20000000-0000-0000-0000-000000000002  HARTWELL (law firm)
        //
        // ── Coordinates ───────────────────────────────────────────────────────
        //   All placed within Las Vegas bounding box used by the map default view
        //   (northLat≈36.15, southLat≈35.995, eastLng≈-114.887, westLng≈-115.224)
        // ─────────────────────────────────────────────────────────────────────

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Providers ─────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `Providers`
                    (`Id`,`TenantId`,`Name`,`OrganizationName`,`Email`,`Phone`,
                     `AddressLine1`,`City`,`State`,`PostalCode`,
                     `IsActive`,`AcceptingReferrals`,
                     `Latitude`,`Longitude`,`GeoPointSource`,`GeoUpdatedAtUtc`,
                     `CreatedByUserId`,`UpdatedByUserId`,`CreatedAtUtc`,`UpdatedAtUtc`)
                VALUES
                ('b1000000-0000-0000-0000-000000000001',
                 '20000000-0000-0000-0000-000000000002',
                 'Dr. Thomas Reed','Hartwell Provider Network',
                 'thomas.reed@hartwellproviders.com','(702) 555-0201',
                 '3150 N Tenaya Way','Las Vegas','NV','89128',
                 1,1,
                 36.1050,-115.2100,'Manual','2024-02-20 09:00:00',
                 '21000000-0000-0000-0000-000000000001','21000000-0000-0000-0000-000000000001',
                 '2024-02-20 09:00:00','2024-02-20 09:00:00'),

                ('b1000000-0000-0000-0000-000000000002',
                 '20000000-0000-0000-0000-000000000002',
                 'Sandra Nguyen PT','Hartwell Provider Network',
                 'sandra.nguyen@hartwellproviders.com','(702) 555-0202',
                 '4750 W Sahara Ave','Las Vegas','NV','89102',
                 1,1,
                 36.1390,-115.1950,'Manual','2024-02-20 09:00:00',
                 '21000000-0000-0000-0000-000000000001','21000000-0000-0000-0000-000000000001',
                 '2024-02-20 09:00:00','2024-02-20 09:00:00'),

                ('b1000000-0000-0000-0000-000000000003',
                 '20000000-0000-0000-0000-000000000002',
                 'Dr. James Harrington','Hartwell Provider Network',
                 'james.harrington@hartwellproviders.com','(702) 555-0203',
                 '701 Shadow Lane','Las Vegas','NV','89106',
                 1,0,
                 36.1280,-115.1680,'Manual','2024-02-20 09:00:00',
                 '21000000-0000-0000-0000-000000000001','21000000-0000-0000-0000-000000000001',
                 '2024-02-20 09:00:00','2024-02-20 09:00:00'),

                ('b1000000-0000-0000-0000-000000000004',
                 '20000000-0000-0000-0000-000000000002',
                 'Nevada Imaging Center','Hartwell Provider Network',
                 'info@nevadaimaging.com','(702) 555-0204',
                 '2020 Flamingo Rd','Las Vegas','NV','89119',
                 1,1,
                 36.1100,-115.1420,'Manual','2024-02-20 09:00:00',
                 '21000000-0000-0000-0000-000000000001','21000000-0000-0000-0000-000000000001',
                 '2024-02-20 09:00:00','2024-02-20 09:00:00'),

                ('b1000000-0000-0000-0000-000000000005',
                 '20000000-0000-0000-0000-000000000002',
                 'Dr. Priya Mehta','Hartwell Provider Network',
                 'priya.mehta@hartwellproviders.com','(702) 555-0205',
                 '5580 W Charleston Blvd','Las Vegas','NV','89146',
                 1,1,
                 36.1470,-115.2050,'Manual','2024-02-20 09:00:00',
                 '21000000-0000-0000-0000-000000000001','21000000-0000-0000-0000-000000000001',
                 '2024-02-20 09:00:00','2024-02-20 09:00:00');
            ");

            // ── ProviderCategories ────────────────────────────────────────────
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `ProviderCategories` (`ProviderId`,`CategoryId`) VALUES
                ('b1000000-0000-0000-0000-000000000001','40000000-0000-0000-0000-000000000001'),
                ('b1000000-0000-0000-0000-000000000002','40000000-0000-0000-0000-000000000002'),
                ('b1000000-0000-0000-0000-000000000003','40000000-0000-0000-0000-000000000003'),
                ('b1000000-0000-0000-0000-000000000004','40000000-0000-0000-0000-000000000004'),
                ('b1000000-0000-0000-0000-000000000005','40000000-0000-0000-0000-000000000005');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM `ProviderCategories` WHERE `ProviderId` LIKE 'b1000000-%';
                DELETE FROM `Providers`          WHERE `Id`         LIKE 'b1000000-%';
            ");
        }
    }
}
