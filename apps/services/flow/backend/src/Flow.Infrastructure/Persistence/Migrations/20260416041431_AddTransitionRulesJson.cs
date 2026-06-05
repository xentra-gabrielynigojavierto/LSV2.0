using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransitionRulesJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RulesJson",
                table: "workflow_transitions",
                type: "varchar(2048)",
                maxLength: 2048,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "workflow_transitions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000001"),
                column: "RulesJson",
                value: null);

            migrationBuilder.UpdateData(
                table: "workflow_transitions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000002"),
                column: "RulesJson",
                value: null);

            migrationBuilder.UpdateData(
                table: "workflow_transitions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000003"),
                column: "RulesJson",
                value: null);

            migrationBuilder.UpdateData(
                table: "workflow_transitions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000004"),
                column: "RulesJson",
                value: null);

            migrationBuilder.UpdateData(
                table: "workflow_transitions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000005"),
                column: "RulesJson",
                value: null);

            migrationBuilder.UpdateData(
                table: "workflow_transitions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000006"),
                column: "RulesJson",
                value: null);

            migrationBuilder.UpdateData(
                table: "workflow_transitions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000007"),
                column: "RulesJson",
                value: null);

            migrationBuilder.UpdateData(
                table: "workflow_transitions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000008"),
                column: "RulesJson",
                value: null);

            migrationBuilder.UpdateData(
                table: "workflow_transitions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000009"),
                column: "RulesJson",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RulesJson",
                table: "workflow_transitions");
        }
    }
}
