using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenant.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMigrationRunTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                table: "tenant_Tenants",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "tenant_MigrationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Mode = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Scope = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AllowCreates = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AllowUpdates = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IdentityAccessible = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    TenantAccessible = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    TotalScanned = table.Column<int>(type: "int", nullable: false),
                    TenantsCreated = table.Column<int>(type: "int", nullable: false),
                    TenantsUpdated = table.Column<int>(type: "int", nullable: false),
                    TenantsSkipped = table.Column<int>(type: "int", nullable: false),
                    Conflicts = table.Column<int>(type: "int", nullable: false),
                    Errors = table.Column<int>(type: "int", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    ErrorMessage = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_MigrationRuns", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "tenant_MigrationRunItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RunId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    IdentityTenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActionTaken = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantUpserted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    BrandingUpserted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DomainUpserted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Warnings = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Errors = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_MigrationRunItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_MigrationRunItems_tenant_MigrationRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "tenant_MigrationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_MigrationRunItems_IdentityTenantId",
                table: "tenant_MigrationRunItems",
                column: "IdentityTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_MigrationRunItems_RunId",
                table: "tenant_MigrationRunItems",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_MigrationRuns_StartedAtUtc",
                table: "tenant_MigrationRuns",
                column: "StartedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_MigrationRunItems");

            migrationBuilder.DropTable(
                name: "tenant_MigrationRuns");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                table: "tenant_Tenants");
        }
    }
}
