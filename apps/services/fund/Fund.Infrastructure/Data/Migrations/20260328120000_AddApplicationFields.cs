using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fund.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [Migration("20260328120000_AddApplicationFields")]
    public partial class AddApplicationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RequestedAmount",
                table: "Applications",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ApprovedAmount",
                table: "Applications",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CaseType",
                table: "Applications",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IncidentDate",
                table: "Applications",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "AttorneyNotes",
                table: "Applications",
                type: "varchar(4000)",
                maxLength: 4000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ApprovalTerms",
                table: "Applications",
                type: "varchar(4000)",
                maxLength: 4000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DenialReason",
                table: "Applications",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "FunderId",
                table: "Applications",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_TenantId_FunderId",
                table: "Applications",
                columns: new[] { "TenantId", "FunderId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Applications_TenantId_FunderId",
                table: "Applications");

            migrationBuilder.DropColumn(name: "RequestedAmount", table: "Applications");
            migrationBuilder.DropColumn(name: "ApprovedAmount",  table: "Applications");
            migrationBuilder.DropColumn(name: "CaseType",        table: "Applications");
            migrationBuilder.DropColumn(name: "IncidentDate",    table: "Applications");
            migrationBuilder.DropColumn(name: "AttorneyNotes",   table: "Applications");
            migrationBuilder.DropColumn(name: "ApprovalTerms",   table: "Applications");
            migrationBuilder.DropColumn(name: "DenialReason",    table: "Applications");
            migrationBuilder.DropColumn(name: "FunderId",        table: "Applications");
        }
    }
}
