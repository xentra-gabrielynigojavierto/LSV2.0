using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Data.Migrations
{
    public partial class AddRetryFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "ntf_Notifications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxRetries",
                table: "ntf_Notifications",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextRetryAt",
                table: "ntf_Notifications",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Status_NextRetryAt",
                table: "ntf_Notifications",
                columns: new[] { "Status", "NextRetryAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_Status_NextRetryAt",
                table: "ntf_Notifications");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "ntf_Notifications");

            migrationBuilder.DropColumn(
                name: "MaxRetries",
                table: "ntf_Notifications");

            migrationBuilder.DropColumn(
                name: "NextRetryAt",
                table: "ntf_Notifications");
        }
    }
}
