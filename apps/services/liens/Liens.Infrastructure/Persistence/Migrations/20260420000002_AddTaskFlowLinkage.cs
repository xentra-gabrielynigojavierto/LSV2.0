using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskFlowLinkage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // LS-LIENS-FLOW-007 — adds soft Flow workflow instance linkage to liens_Tasks.
            // No FK constraint: the task succeeds even if the Flow instance is later removed.
            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowInstanceId",
                table: "liens_Tasks",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "WorkflowStepKey",
                table: "liens_Tasks",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_TenantId_WorkflowInstanceId",
                table: "liens_Tasks",
                columns: new[] { "TenantId", "WorkflowInstanceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tasks_TenantId_WorkflowInstanceId",
                table: "liens_Tasks");

            migrationBuilder.DropColumn(
                name: "WorkflowInstanceId",
                table: "liens_Tasks");

            migrationBuilder.DropColumn(
                name: "WorkflowStepKey",
                table: "liens_Tasks");
        }
    }
}
