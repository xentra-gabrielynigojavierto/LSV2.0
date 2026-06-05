using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenant.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantDomains : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Create tenant_Domains table ───────────────────────────────────

            migrationBuilder.CreateTable(
                name: "tenant_Domains",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Host = table.Column<string>(type: "varchar(253)", maxLength: 253, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DomainType = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsPrimary = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_Domains", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_Domains_tenant_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenant_Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_Domains_TenantId",
                table: "tenant_Domains",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_Domains_Host",
                table: "tenant_Domains",
                column: "Host");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_Domains_Status",
                table: "tenant_Domains",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "tenant_Domains");
        }
    }
}
