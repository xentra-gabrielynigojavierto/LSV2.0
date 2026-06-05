using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Task.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-FLOW-04 — Adds analytics-optimised indexes to tasks_Tasks.
    ///
    /// New indexes:
    ///   IX_Tasks_TenantId_Status_SlaStatus   — SLA group queries (GROUP BY SlaStatus WHERE Status IN (...))
    ///   IX_Tasks_TenantId_SlaBreachedAt      — breach-window count (WHERE SlaBreachedAt BETWEEN start AND end)
    ///   IX_Tasks_TenantId_CompletedAt        — completed-in-window count (WHERE Status=COMPLETED AND CompletedAt BETWEEN ...)
    ///   IX_Tasks_TenantId_AssignedAt         — assigned-in-window count (WHERE AssignedAt BETWEEN ...)
    /// </summary>
    public partial class AddAnalyticsIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name:    "IX_Tasks_TenantId_Status_SlaStatus",
                table:   "tasks_Tasks",
                columns: new[] { "TenantId", "Status", "SlaStatus" });

            migrationBuilder.CreateIndex(
                name:    "IX_Tasks_TenantId_SlaBreachedAt",
                table:   "tasks_Tasks",
                columns: new[] { "TenantId", "SlaBreachedAt" });

            migrationBuilder.CreateIndex(
                name:    "IX_Tasks_TenantId_CompletedAt",
                table:   "tasks_Tasks",
                columns: new[] { "TenantId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name:    "IX_Tasks_TenantId_AssignedAt",
                table:   "tasks_Tasks",
                columns: new[] { "TenantId", "AssignedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name:  "IX_Tasks_TenantId_Status_SlaStatus",
                table: "tasks_Tasks");

            migrationBuilder.DropIndex(
                name:  "IX_Tasks_TenantId_SlaBreachedAt",
                table: "tasks_Tasks");

            migrationBuilder.DropIndex(
                name:  "IX_Tasks_TenantId_CompletedAt",
                table: "tasks_Tasks");

            migrationBuilder.DropIndex(
                name:  "IX_Tasks_TenantId_AssignedAt",
                table: "tasks_Tasks");
        }
    }
}
