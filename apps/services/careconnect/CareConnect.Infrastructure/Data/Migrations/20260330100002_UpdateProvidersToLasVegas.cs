using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateProvidersToLasVegas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE `Providers` SET
                    `AddressLine1`   = '1234 Spring Mountain Rd',
                    `City`           = 'Las Vegas',
                    `State`          = 'NV',
                    `PostalCode`     = '89102',
                    `Phone`          = '(702) 555-0101',
                    `Latitude`       = 36.1184,
                    `Longitude`      = -115.2027,
                    `GeoPointSource` = 'Manual',
                    `GeoUpdatedAtUtc`= NOW(),
                    `UpdatedAtUtc`   = NOW()
                WHERE `Id` = 'a1000000-0000-0000-0000-000000000001';

                UPDATE `Providers` SET
                    `AddressLine1`   = '2200 Sahara Ave',
                    `City`           = 'Las Vegas',
                    `State`          = 'NV',
                    `PostalCode`     = '89104',
                    `Phone`          = '(702) 555-0102',
                    `Latitude`       = 36.1420,
                    `Longitude`      = -115.1534,
                    `GeoPointSource` = 'Manual',
                    `GeoUpdatedAtUtc`= NOW(),
                    `UpdatedAtUtc`   = NOW()
                WHERE `Id` = 'a1000000-0000-0000-0000-000000000002';

                UPDATE `Providers` SET
                    `AddressLine1`   = '500 Shadow Lane',
                    `City`           = 'Las Vegas',
                    `State`          = 'NV',
                    `PostalCode`     = '89106',
                    `Phone`          = '(702) 555-0103',
                    `Latitude`       = 36.1734,
                    `Longitude`      = -115.1619,
                    `GeoPointSource` = 'Manual',
                    `GeoUpdatedAtUtc`= NOW(),
                    `UpdatedAtUtc`   = NOW()
                WHERE `Id` = 'a1000000-0000-0000-0000-000000000003';

                UPDATE `Providers` SET
                    `AddressLine1`   = '888 Desert Inn Rd',
                    `City`           = 'Las Vegas',
                    `State`          = 'NV',
                    `PostalCode`     = '89109',
                    `Phone`          = '(702) 555-0104',
                    `Latitude`       = 36.1348,
                    `Longitude`      = -115.1472,
                    `GeoPointSource` = 'Manual',
                    `GeoUpdatedAtUtc`= NOW(),
                    `UpdatedAtUtc`   = NOW()
                WHERE `Id` = 'a1000000-0000-0000-0000-000000000004';

                UPDATE `Providers` SET
                    `AddressLine1`   = '330 W Charleston Blvd',
                    `City`           = 'Las Vegas',
                    `State`          = 'NV',
                    `PostalCode`     = '89102',
                    `Phone`          = '(702) 555-0105',
                    `Latitude`       = 36.1556,
                    `Longitude`      = -115.1761,
                    `GeoPointSource` = 'Manual',
                    `GeoUpdatedAtUtc`= NOW(),
                    `UpdatedAtUtc`   = NOW()
                WHERE `Id` = 'a1000000-0000-0000-0000-000000000005';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE `Providers` SET
                    `AddressLine1`='1234 Wellness Blvd',`City`='Los Angeles',`State`='CA',`PostalCode`='90001',
                    `Phone`='(213) 555-0101',`Latitude`=34.0538,`Longitude`=-118.2434,
                    `GeoPointSource`='Manual',`GeoUpdatedAtUtc`='2024-03-01 09:00:00',`UpdatedAtUtc`=NOW()
                WHERE `Id` = 'a1000000-0000-0000-0000-000000000001';

                UPDATE `Providers` SET
                    `AddressLine1`='2200 Rehabilitation Drive',`City`='Los Angeles',`State`='CA',`PostalCode`='90002',
                    `Phone`='(213) 555-0102',`Latitude`=34.0481,`Longitude`=-118.2587,
                    `GeoPointSource`='Manual',`GeoUpdatedAtUtc`='2024-03-01 09:00:00',`UpdatedAtUtc`=NOW()
                WHERE `Id` = 'a1000000-0000-0000-0000-000000000002';

                UPDATE `Providers` SET
                    `AddressLine1`='500 Medical Center Drive',`City`='Los Angeles',`State`='CA',`PostalCode`='90010',
                    `Phone`='(213) 555-0103',`Latitude`=34.0633,`Longitude`=-118.3030,
                    `GeoPointSource`='Manual',`GeoUpdatedAtUtc`='2024-03-01 09:00:00',`UpdatedAtUtc`=NOW()
                WHERE `Id` = 'a1000000-0000-0000-0000-000000000003';

                UPDATE `Providers` SET
                    `AddressLine1`='888 Radiology Lane',`City`='Los Angeles',`State`='CA',`PostalCode`='90015',
                    `Phone`='(213) 555-0104',`Latitude`=34.0397,`Longitude`=-118.2706,
                    `GeoPointSource`='Manual',`GeoUpdatedAtUtc`='2024-03-01 09:00:00',`UpdatedAtUtc`=NOW()
                WHERE `Id` = 'a1000000-0000-0000-0000-000000000004';

                UPDATE `Providers` SET
                    `AddressLine1`='330 Pain Clinic Avenue',`City`='Los Angeles',`State`='CA',`PostalCode`='90020',
                    `Phone`='(213) 555-0105',`Latitude`=34.0721,`Longitude`=-118.3102,
                    `GeoPointSource`='Manual',`GeoUpdatedAtUtc`='2024-03-01 09:00:00',`UpdatedAtUtc`=NOW()
                WHERE `Id` = 'a1000000-0000-0000-0000-000000000005';
            ");
        }
    }
}
