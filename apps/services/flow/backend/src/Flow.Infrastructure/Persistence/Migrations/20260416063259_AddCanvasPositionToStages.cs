using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCanvasPositionToStages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CanvasX",
                table: "flow_workflow_stages",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CanvasY",
                table: "flow_workflow_stages",
                type: "double",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "flow_workflow_stages",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000001"),
                columns: new[] { "CanvasX", "CanvasY" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "flow_workflow_stages",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000002"),
                columns: new[] { "CanvasX", "CanvasY" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "flow_workflow_stages",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000003"),
                columns: new[] { "CanvasX", "CanvasY" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "flow_workflow_stages",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000004"),
                columns: new[] { "CanvasX", "CanvasY" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "flow_workflow_stages",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000005"),
                columns: new[] { "CanvasX", "CanvasY" },
                values: new object[] { null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanvasX",
                table: "flow_workflow_stages");

            migrationBuilder.DropColumn(
                name: "CanvasY",
                table: "flow_workflow_stages");
        }
    }
}
