using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations;

public partial class AddNotificationDedupeKey : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            ALTER TABLE `cc_CareConnectNotifications`
            ADD COLUMN IF NOT EXISTS `DedupeKey` varchar(500) CHARACTER SET utf8mb4 NULL;
        ");

        migrationBuilder.Sql(@"
            CREATE UNIQUE INDEX IF NOT EXISTS `IX_CareConnectNotifications_DedupeKey`
            ON `cc_CareConnectNotifications` (`DedupeKey`);
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            DROP INDEX IF EXISTS `IX_CareConnectNotifications_DedupeKey`
            ON `cc_CareConnectNotifications`;
        ");

        migrationBuilder.Sql(@"
            ALTER TABLE `cc_CareConnectNotifications`
            DROP COLUMN IF EXISTS `DedupeKey`;
        ");
    }
}
