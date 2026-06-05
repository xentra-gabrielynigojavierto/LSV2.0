using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flow.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-FLOW-03 — Drop the <c>flow_workflow_tasks</c> shadow table.
    ///
    /// <para>
    /// All Flow services that previously read or wrote this table have been
    /// migrated to call the Task service API directly.  The table is no longer
    /// referenced by any DbSet, query, or entity configuration in
    /// <see cref="Flow.Infrastructure.Persistence.FlowDbContext"/>.
    /// </para>
    ///
    /// <para><b>Down</b> deliberately omits re-creating the table: restoring the
    /// shadow copy would require back-filling data from the Task service, which
    /// is outside the scope of an EF rollback.  If a full rollback is needed,
    /// redeploy the pre-TASK-FLOW-03 build and run the previous migration set.
    /// </para>
    /// </summary>
    public partial class DropWorkflowTaskShadowTable_TaskFlow03 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop all foreign-key indexes first (MySQL requires this before
            // dropping a table that is referenced by or references other tables).
            migrationBuilder.DropIndex(
                name: "ix_flow_workflow_tasks_instance",
                table: "flow_workflow_tasks");

            migrationBuilder.DropIndex(
                name: "ix_flow_workflow_tasks_status_dueat_eval",
                table: "flow_workflow_tasks");

            migrationBuilder.DropIndex(
                name: "ix_flow_workflow_tasks_tenant_mode_status",
                table: "flow_workflow_tasks");

            migrationBuilder.DropIndex(
                name: "ix_flow_workflow_tasks_tenant_role_status",
                table: "flow_workflow_tasks");

            migrationBuilder.DropIndex(
                name: "ix_flow_workflow_tasks_tenant_status",
                table: "flow_workflow_tasks");

            migrationBuilder.DropIndex(
                name: "ix_flow_workflow_tasks_tenant_status_dueat",
                table: "flow_workflow_tasks");

            migrationBuilder.DropIndex(
                name: "ix_flow_workflow_tasks_tenant_status_slastatus",
                table: "flow_workflow_tasks");

            migrationBuilder.DropIndex(
                name: "ix_flow_workflow_tasks_tenant_user_status",
                table: "flow_workflow_tasks");

            // Drop the shadow table. All authoritative task data lives in the
            // Task service (platform_tasks table in tasks_db).
            migrationBuilder.DropTable(
                name: "flow_workflow_tasks");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally left empty — see summary XML doc above.
        }
    }
}
