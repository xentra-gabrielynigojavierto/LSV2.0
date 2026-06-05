using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUptimeRollups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "uptime_hourly_rollups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    monitored_entity_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    entity_name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    bucket_hour_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    up_count = table.Column<int>(type: "int", nullable: false),
                    degraded_count = table.Column<int>(type: "int", nullable: false),
                    down_count = table.Column<int>(type: "int", nullable: false),
                    unknown_count = table.Column<int>(type: "int", nullable: false),
                    total_count = table.Column<int>(type: "int", nullable: false),
                    sum_elapsed_ms = table.Column<long>(type: "bigint", nullable: false),
                    max_elapsed_ms = table.Column<long>(type: "bigint", nullable: false),
                    uptime_ratio = table.Column<double>(type: "double", nullable: true),
                    weighted_availability = table.Column<double>(type: "double", nullable: true),
                    insufficient_data = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    computed_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uptime_hourly_rollups", x => x.id);
                    table.ForeignKey(
                        name: "FK_uptime_hourly_rollups_monitored_entities_monitored_entity_id",
                        column: x => x.monitored_entity_id,
                        principalTable: "monitored_entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_uptime_hourly_entity_hour",
                table: "uptime_hourly_rollups",
                columns: new[] { "monitored_entity_id", "bucket_hour_utc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_uptime_hourly_bucket_hour",
                table: "uptime_hourly_rollups",
                column: "bucket_hour_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "uptime_hourly_rollups");
        }
    }
}
