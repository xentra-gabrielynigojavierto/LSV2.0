using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Support.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportAttachmentsAndProductRefs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "support_ticket_attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", maxLength: 36, nullable: false, collation: "ascii_general_ci"),
                    ticket_id = table.Column<Guid>(type: "char(36)", maxLength: 36, nullable: false, collation: "ascii_general_ci"),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    document_id = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    file_name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    content_type = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    uploaded_by_user_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_ticket_attachments", x => x.id);
                    table.ForeignKey(
                        name: "fk_support_ticket_attachments_ticket",
                        column: x => x.ticket_id,
                        principalTable: "support_tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "support_ticket_product_refs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", maxLength: 36, nullable: false, collation: "ascii_general_ci"),
                    ticket_id = table.Column<Guid>(type: "char(36)", maxLength: 36, nullable: false, collation: "ascii_general_ci"),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    product_code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    entity_type = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    entity_id = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    display_label = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    metadata_json = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_by_user_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_ticket_product_refs", x => x.id);
                    table.ForeignKey(
                        name: "fk_support_ticket_product_refs_ticket",
                        column: x => x.ticket_id,
                        principalTable: "support_tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_support_ticket_attachments_created_at",
                table: "support_ticket_attachments",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_support_ticket_attachments_document",
                table: "support_ticket_attachments",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_support_ticket_attachments_tenant",
                table: "support_ticket_attachments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_support_ticket_attachments_ticket",
                table: "support_ticket_attachments",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "ux_support_ticket_attachments_tenant_ticket_document",
                table: "support_ticket_attachments",
                columns: new[] { "tenant_id", "ticket_id", "document_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_support_ticket_product_refs_created_at",
                table: "support_ticket_product_refs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_support_ticket_product_refs_entity_id",
                table: "support_ticket_product_refs",
                column: "entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_support_ticket_product_refs_entity_type",
                table: "support_ticket_product_refs",
                column: "entity_type");

            migrationBuilder.CreateIndex(
                name: "ix_support_ticket_product_refs_product",
                table: "support_ticket_product_refs",
                column: "product_code");

            migrationBuilder.CreateIndex(
                name: "ix_support_ticket_product_refs_tenant",
                table: "support_ticket_product_refs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_support_ticket_product_refs_ticket",
                table: "support_ticket_product_refs",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "ux_support_ticket_product_refs_unique",
                table: "support_ticket_product_refs",
                columns: new[] { "tenant_id", "ticket_id", "product_code", "entity_type", "entity_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "support_ticket_attachments");

            migrationBuilder.DropTable(
                name: "support_ticket_product_refs");
        }
    }
}
