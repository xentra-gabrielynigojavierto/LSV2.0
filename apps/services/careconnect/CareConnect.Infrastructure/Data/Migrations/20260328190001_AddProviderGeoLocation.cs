using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderGeoLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Providers",
                type: "decimal(10,7)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Providers",
                type: "decimal(10,7)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeoPointSource",
                table: "Providers",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "GeoUpdatedAtUtc",
                table: "Providers",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Providers_TenantId_Latitude_Longitude",
                table: "Providers",
                columns: new[] { "TenantId", "Latitude", "Longitude" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Providers_TenantId_Latitude_Longitude",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "GeoPointSource",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "GeoUpdatedAtUtc",
                table: "Providers");
        }
    }
}
