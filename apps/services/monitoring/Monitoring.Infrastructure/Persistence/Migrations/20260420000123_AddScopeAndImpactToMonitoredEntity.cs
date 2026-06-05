using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScopeAndImpactToMonitoredEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOT NULL columns added to an existing table need a default for any
            // pre-existing rows. We backfill with the same values the API layer
            // applies when a caller omits these fields on create:
            //   scope        -> "platform"   (MonitoredEntityDefaults.Scope)
            //   impact_level -> "Optional"   (MonitoredEntityDefaults.Impact)
            // This keeps existing rows readable (string-converted enum cannot
            // materialize an empty string as a valid ImpactLevel) and keeps the
            // backfill consistent with the API-layer default policy.
            // EF always sends explicit values on INSERT/UPDATE, so this default
            // never short-circuits domain validation for new rows.
            migrationBuilder.AddColumn<string>(
                name: "impact_level",
                table: "monitored_entities",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Optional")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "scope",
                table: "monitored_entities",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "platform")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "impact_level",
                table: "monitored_entities");

            migrationBuilder.DropColumn(
                name: "scope",
                table: "monitored_entities");
        }
    }
}
