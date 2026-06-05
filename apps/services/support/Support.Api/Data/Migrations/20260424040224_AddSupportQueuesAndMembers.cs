using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Support.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportQueuesAndMembers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "support_queues",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", maxLength: 36, nullable: false, collation: "ascii_general_ci"),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    product_code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by_user_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    updated_by_user_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_queues", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "support_queue_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", maxLength: 36, nullable: false, collation: "ascii_general_ci"),
                    queue_id = table.Column<Guid>(type: "char(36)", maxLength: 36, nullable: false, collation: "ascii_general_ci"),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    user_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    role = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_queue_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_support_queue_members_queue",
                        column: x => x.queue_id,
                        principalTable: "support_queues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_support_queue_members_queue",
                table: "support_queue_members",
                column: "queue_id");

            migrationBuilder.CreateIndex(
                name: "ix_support_queue_members_tenant",
                table: "support_queue_members",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_support_queue_members_user",
                table: "support_queue_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_support_queue_members_queue_user",
                table: "support_queue_members",
                columns: new[] { "queue_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_support_queues_is_active",
                table: "support_queues",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_support_queues_tenant",
                table: "support_queues",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_support_queues_tenant_product",
                table: "support_queues",
                columns: new[] { "tenant_id", "product_code" });

            migrationBuilder.CreateIndex(
                name: "ux_support_queues_tenant_name",
                table: "support_queues",
                columns: new[] { "tenant_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "support_queue_members");

            migrationBuilder.DropTable(
                name: "support_queues");
        }
    }
}
