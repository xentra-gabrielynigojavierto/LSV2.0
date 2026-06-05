using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ActorName    = table.Column<string>(type: "varchar(320)",  maxLength: 320,  nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                    ActorType    = table.Column<string>(type: "varchar(20)",   maxLength: 20,   nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                    Action       = table.Column<string>(type: "varchar(100)",  maxLength: 100,  nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                    EntityType   = table.Column<string>(type: "varchar(100)",  maxLength: 100,  nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                    EntityId     = table.Column<string>(type: "varchar(500)",  maxLength: 500,  nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                    MetadataJson = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true) .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorType",
                table: "AuditLogs",
                column: "ActorType");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAtUtc",
                table: "AuditLogs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType",
                table: "AuditLogs",
                column: "EntityType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AuditLogs");
        }
    }
}
