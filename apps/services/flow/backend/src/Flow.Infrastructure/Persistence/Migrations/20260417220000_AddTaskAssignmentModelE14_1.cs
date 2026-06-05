using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flow.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// LS-FLOW-E14.1 — Task Assignment Model Hardening.
    ///
    /// Adds the explicit assignment-mode surface to
    /// <c>flow_workflow_tasks</c>:
    ///   - <c>AssignmentMode</c> (varchar(32), NOT NULL, default 'Unassigned')
    ///   - <c>AssignedAt</c>     (datetime(6), NULL)
    ///   - <c>AssignedBy</c>     (varchar(256), NULL)
    ///   - <c>AssignmentReason</c> (varchar(512), NULL)
    /// plus a single composite index
    /// <c>(TenantId, AssignmentMode, Status)</c> to support the
    /// upcoming queue-scan workloads in E14.2.
    ///
    /// In-place backfill runs in the same migration so the deploy is
    /// idempotent on a populated database — every existing row is
    /// re-tagged from its (user, role, org) triple in a single SQL
    /// statement.
    /// </summary>
    public partial class AddTaskAssignmentModelE14_1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- Columns -------------------------------------------------
            migrationBuilder.AddColumn<string>(
                name: "AssignmentMode",
                table: "flow_workflow_tasks",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Unassigned")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<System.DateTime>(
                name: "AssignedAt",
                table: "flow_workflow_tasks",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedBy",
                table: "flow_workflow_tasks",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "AssignmentReason",
                table: "flow_workflow_tasks",
                type: "varchar(512)",
                maxLength: 512,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // ---- Backfill -----------------------------------------------
            //
            // Re-tag every existing row from its (user, role, org)
            // triple. Precedence User > Role > Org > Unassigned mirrors
            // the WorkflowTaskAssignment factory (E11.3) and the
            // WorkflowTaskAssignmentMode.Derive() helper. The CASE
            // expression is deterministic and idempotent — re-running
            // it produces identical output.
            migrationBuilder.Sql(@"
                UPDATE `flow_workflow_tasks`
                SET `AssignmentMode` = CASE
                    WHEN `AssignedUserId` IS NOT NULL AND `AssignedUserId` <> '' THEN 'DirectUser'
                    WHEN `AssignedRole`   IS NOT NULL AND `AssignedRole`   <> '' THEN 'RoleQueue'
                    WHEN `AssignedOrgId`  IS NOT NULL AND `AssignedOrgId`  <> '' THEN 'OrgQueue'
                    ELSE 'Unassigned'
                END;
            ");

            // ---- Index --------------------------------------------------
            migrationBuilder.CreateIndex(
                name: "ix_flow_workflow_tasks_tenant_mode_status",
                table: "flow_workflow_tasks",
                columns: new[] { "TenantId", "AssignmentMode", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_flow_workflow_tasks_tenant_mode_status",
                table: "flow_workflow_tasks");

            migrationBuilder.DropColumn(
                name: "AssignmentReason",
                table: "flow_workflow_tasks");

            migrationBuilder.DropColumn(
                name: "AssignmentMode",
                table: "flow_workflow_tasks");

            migrationBuilder.DropColumn(
                name: "AssignedBy",
                table: "flow_workflow_tasks");

            migrationBuilder.DropColumn(
                name: "AssignedAt",
                table: "flow_workflow_tasks");
        }
    }
}
