using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [Migration("20260401220001_UIX005_AddRoleCapabilityAssignments")]
    public partial class UIX005_AddRoleCapabilityAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // UIX-005: Role ↔ Capability permission assignments
            // Links tenant custom Role to a granular Capability.
            // Composite PK (RoleId, CapabilityId) enforces uniqueness.
            migrationBuilder.CreateTable(
                name: "RoleCapabilityAssignments",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(
                        type: "char(36)",
                        nullable: false),

                    CapabilityId = table.Column<Guid>(
                        type: "char(36)",
                        nullable: false),

                    AssignedAtUtc = table.Column<DateTime>(
                        type: "datetime(6)",
                        nullable: false),

                    AssignedByUserId = table.Column<Guid>(
                        type: "char(36)",
                        nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleCapabilityAssignments", x => new { x.RoleId, x.CapabilityId });

                    table.ForeignKey(
                        name: "FK_RoleCapabilityAssignments_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);

                    table.ForeignKey(
                        name: "FK_RoleCapabilityAssignments_Capabilities_CapabilityId",
                        column: x => x.CapabilityId,
                        principalTable: "Capabilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoleCapabilityAssignments_RoleId",
                table: "RoleCapabilityAssignments",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleCapabilityAssignments_CapabilityId",
                table: "RoleCapabilityAssignments",
                column: "CapabilityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "RoleCapabilityAssignments");
        }
    }
}
