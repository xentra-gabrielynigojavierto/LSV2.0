using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenant.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEntitlementsCapabilitiesSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── tenant_ProductEntitlements ─────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "tenant_ProductEntitlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ProductKey = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    ProductDisplayName = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    IsEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsDefault = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PlanCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_ProductEntitlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_ProductEntitlements_tenant_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenant_Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_ProductEntitlements_TenantId",
                table: "tenant_ProductEntitlements",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_ProductEntitlements_TenantId_ProductKey",
                table: "tenant_ProductEntitlements",
                columns: new[] { "TenantId", "ProductKey" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_ProductEntitlements_TenantId_IsDefault",
                table: "tenant_ProductEntitlements",
                columns: new[] { "TenantId", "IsDefault" });

            // ── tenant_Capabilities ────────────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "tenant_Capabilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ProductEntitlementId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CapabilityKey = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    IsEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_Capabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_Capabilities_tenant_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenant_Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tenant_Capabilities_tenant_ProductEntitlements_ProductEntitlementId",
                        column: x => x.ProductEntitlementId,
                        principalTable: "tenant_ProductEntitlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_Capabilities_TenantId",
                table: "tenant_Capabilities",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_Capabilities_ProductEntitlementId",
                table: "tenant_Capabilities",
                column: "ProductEntitlementId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_Capabilities_TenantId_CapabilityKey",
                table: "tenant_Capabilities",
                columns: new[] { "TenantId", "CapabilityKey" });

            // ── tenant_Settings ────────────────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "tenant_Settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SettingKey = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    SettingValue = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false),
                    ValueType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    ProductKey = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_Settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_Settings_tenant_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenant_Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_Settings_TenantId",
                table: "tenant_Settings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_Settings_TenantId_SettingKey",
                table: "tenant_Settings",
                columns: new[] { "TenantId", "SettingKey" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_Settings_TenantId_ProductKey",
                table: "tenant_Settings",
                columns: new[] { "TenantId", "ProductKey" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "tenant_Settings");
            migrationBuilder.DropTable(name: "tenant_Capabilities");
            migrationBuilder.DropTable(name: "tenant_ProductEntitlements");
        }
    }
}
