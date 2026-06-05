using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Support.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketOwnershipFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "external_customer_id",
                table: "support_tickets",
                type: "char(36)",
                maxLength: 36,
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "requester_type",
                table: "support_tickets",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "InternalUser")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "visibility_scope",
                table: "support_tickets",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Internal")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_support_tickets_tenant_ext_customer",
                table: "support_tickets",
                columns: new[] { "tenant_id", "external_customer_id" });

            migrationBuilder.CreateIndex(
                name: "ix_support_tickets_tenant_requester_type",
                table: "support_tickets",
                columns: new[] { "tenant_id", "requester_type" });

            migrationBuilder.CreateIndex(
                name: "ix_support_tickets_tenant_visibility",
                table: "support_tickets",
                columns: new[] { "tenant_id", "visibility_scope" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_support_tickets_tenant_ext_customer",
                table: "support_tickets");

            migrationBuilder.DropIndex(
                name: "ix_support_tickets_tenant_requester_type",
                table: "support_tickets");

            migrationBuilder.DropIndex(
                name: "ix_support_tickets_tenant_visibility",
                table: "support_tickets");

            migrationBuilder.DropColumn(
                name: "external_customer_id",
                table: "support_tickets");

            migrationBuilder.DropColumn(
                name: "requester_type",
                table: "support_tickets");

            migrationBuilder.DropColumn(
                name: "visibility_scope",
                table: "support_tickets");
        }
    }
}
