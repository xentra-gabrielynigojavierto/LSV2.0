using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedCareConnectTestData : Migration
    {
        // ── Provider IDs (prefix a1000000-...) ───────────────────────────────
        //   a1000000-0000-0000-0000-000000000001  Dr. Elena Ramirez  (CHIRO)
        //   a1000000-0000-0000-0000-000000000002  Marcus Chen        (PT)
        //   a1000000-0000-0000-0000-000000000003  Dr. Patricia Okafor (ORTHO)
        //   a1000000-0000-0000-0000-000000000004  Pacific Imaging Ctr (IMG)
        //   a1000000-0000-0000-0000-000000000005  Dr. Samuel Park    (PAIN)
        //
        // ── Tenant used ───────────────────────────────────────────────────────
        //   20000000-0000-0000-0000-000000000003  MERIDIAN (seeded in Identity service)
        //
        // ── Category IDs (already seeded in InitialCareConnectSchema) ────────
        //   40000000-0000-0000-0000-000000000001  CHIRO
        //   40000000-0000-0000-0000-000000000002  PT
        //   40000000-0000-0000-0000-000000000003  ORTHO
        //   40000000-0000-0000-0000-000000000004  IMG
        //   40000000-0000-0000-0000-000000000005  PAIN
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
                ('a1000000-0000-0000-0000-000000000001',
                 '20000000-0000-0000-0000-000000000003',
                 'Dr. Elena Ramirez','Meridian Care Group',
                 'elena.ramirez@meridiancare.com','(702) 555-0101',
                 '1234 Spring Mountain Rd','Las Vegas','NV','89102',
                 1,1,
                 36.1184,-115.2027,'Manual','2024-03-01 09:00:00',
                 '22000000-0000-0000-0000-000000000001','22000000-0000-0000-0000-000000000001',
                 '2024-03-01 09:00:00','2024-03-01 09:00:00'),

                ('a1000000-0000-0000-0000-000000000002',
                 '20000000-0000-0000-0000-000000000003',
                 'Marcus Chen','Meridian Care Group',
                 'marcus.chen@meridiancare.com','(702) 555-0102',
                 '2200 Sahara Ave','Las Vegas','NV','89104',
                 1,1,
                 36.1420,-115.1534,'Manual','2024-03-01 09:00:00',
                 '22000000-0000-0000-0000-000000000001','22000000-0000-0000-0000-000000000001',
                 '2024-03-01 09:00:00','2024-03-01 09:00:00'),

                ('a1000000-0000-0000-0000-000000000003',
                 '20000000-0000-0000-0000-000000000003',
                 'Dr. Patricia Okafor','Meridian Care Group',
                 'patricia.okafor@meridiancare.com','(702) 555-0103',
                 '500 Shadow Lane','Las Vegas','NV','89106',
                 1,0,
                 36.1734,-115.1619,'Manual','2024-03-01 09:00:00',
                 '22000000-0000-0000-0000-000000000001','22000000-0000-0000-0000-000000000001',
                 '2024-03-01 09:00:00','2024-03-01 09:00:00'),

                ('a1000000-0000-0000-0000-000000000004',
                 '20000000-0000-0000-0000-000000000003',
                 'Pacific Imaging Center','Meridian Care Group',
                 'info@pacificimaging.com','(702) 555-0104',
                 '888 Desert Inn Rd','Las Vegas','NV','89109',
                 1,1,
                 36.1348,-115.1472,'Manual','2024-03-01 09:00:00',
                 '22000000-0000-0000-0000-000000000001','22000000-0000-0000-0000-000000000001',
                 '2024-03-01 09:00:00','2024-03-01 09:00:00'),

                ('a1000000-0000-0000-0000-000000000005',
                 '20000000-0000-0000-0000-000000000003',
                 'Dr. Samuel Park','Meridian Care Group',
                 'samuel.park@meridiancare.com','(702) 555-0105',
                 '330 W Charleston Blvd','Las Vegas','NV','89102',
                 1,1,
                 36.1556,-115.1761,'Manual','2024-03-01 09:00:00',
                 '22000000-0000-0000-0000-000000000001','22000000-0000-0000-0000-000000000001',
                 '2024-03-01 09:00:00','2024-03-01 09:00:00');
            ");

            // ── ProviderCategories ────────────────────────────────────────────
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `ProviderCategories` (`ProviderId`,`CategoryId`) VALUES
                ('a1000000-0000-0000-0000-000000000001','40000000-0000-0000-0000-000000000001'),
                ('a1000000-0000-0000-0000-000000000002','40000000-0000-0000-0000-000000000002'),
                ('a1000000-0000-0000-0000-000000000003','40000000-0000-0000-0000-000000000003'),
                ('a1000000-0000-0000-0000-000000000004','40000000-0000-0000-0000-000000000004'),
                ('a1000000-0000-0000-0000-000000000005','40000000-0000-0000-0000-000000000005');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM `ProviderCategories` WHERE `ProviderId` LIKE 'a1000000-%';
                DELETE FROM `Providers`          WHERE `Id`         LIKE 'a1000000-%';
            ");
        }
    }
}
