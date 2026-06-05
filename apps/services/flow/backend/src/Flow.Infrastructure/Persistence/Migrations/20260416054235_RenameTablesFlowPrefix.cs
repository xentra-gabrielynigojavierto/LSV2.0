using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameTablesFlowPrefix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_automation_execution_logs_task_items_TaskId",
                table: "automation_execution_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_automation_execution_logs_workflow_automation_hooks_Workflow~",
                table: "automation_execution_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_notifications_flow_definitions_WorkflowDefinitionId",
                table: "notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_notifications_task_items_TaskId",
                table: "notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_task_items_flow_definitions_FlowDefinitionId",
                table: "task_items");

            migrationBuilder.DropForeignKey(
                name: "FK_task_items_workflow_stages_WorkflowStageId",
                table: "task_items");

            migrationBuilder.DropForeignKey(
                name: "FK_workflow_automation_hooks_flow_definitions_WorkflowDefinitio~",
                table: "workflow_automation_hooks");

            migrationBuilder.DropForeignKey(
                name: "FK_workflow_automation_hooks_workflow_transitions_WorkflowTrans~",
                table: "workflow_automation_hooks");

            migrationBuilder.DropForeignKey(
                name: "FK_workflow_stages_flow_definitions_WorkflowDefinitionId",
                table: "workflow_stages");

            migrationBuilder.DropForeignKey(
                name: "FK_workflow_transitions_flow_definitions_WorkflowDefinitionId",
                table: "workflow_transitions");

            migrationBuilder.DropForeignKey(
                name: "FK_workflow_transitions_workflow_stages_FromStageId",
                table: "workflow_transitions");

            migrationBuilder.DropForeignKey(
                name: "FK_workflow_transitions_workflow_stages_ToStageId",
                table: "workflow_transitions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_workflow_transitions",
                table: "workflow_transitions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_workflow_stages",
                table: "workflow_stages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_workflow_automation_hooks",
                table: "workflow_automation_hooks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_task_items",
                table: "task_items");

            migrationBuilder.DropPrimaryKey(
                name: "PK_notifications",
                table: "notifications");

            migrationBuilder.DropPrimaryKey(
                name: "PK_automation_execution_logs",
                table: "automation_execution_logs");

            migrationBuilder.RenameTable(
                name: "workflow_transitions",
                newName: "flow_workflow_transitions");

            migrationBuilder.RenameTable(
                name: "workflow_stages",
                newName: "flow_workflow_stages");

            migrationBuilder.RenameTable(
                name: "workflow_automation_hooks",
                newName: "flow_automation_hooks");

            migrationBuilder.RenameTable(
                name: "task_items",
                newName: "flow_task_items");

            migrationBuilder.RenameTable(
                name: "notifications",
                newName: "flow_notifications");

            migrationBuilder.RenameTable(
                name: "automation_execution_logs",
                newName: "flow_automation_execution_logs");

            migrationBuilder.RenameIndex(
                name: "IX_workflow_transitions_WorkflowDefinitionId_FromStageId_ToStag~",
                table: "flow_workflow_transitions",
                newName: "IX_flow_workflow_transitions_WorkflowDefinitionId_FromStageId_T~");

            migrationBuilder.RenameIndex(
                name: "IX_workflow_transitions_ToStageId",
                table: "flow_workflow_transitions",
                newName: "IX_flow_workflow_transitions_ToStageId");

            migrationBuilder.RenameIndex(
                name: "IX_workflow_transitions_FromStageId",
                table: "flow_workflow_transitions",
                newName: "IX_flow_workflow_transitions_FromStageId");

            migrationBuilder.RenameIndex(
                name: "IX_workflow_stages_WorkflowDefinitionId_Key",
                table: "flow_workflow_stages",
                newName: "IX_flow_workflow_stages_WorkflowDefinitionId_Key");

            migrationBuilder.RenameIndex(
                name: "IX_workflow_automation_hooks_WorkflowTransitionId",
                table: "flow_automation_hooks",
                newName: "IX_flow_automation_hooks_WorkflowTransitionId");

            migrationBuilder.RenameIndex(
                name: "IX_workflow_automation_hooks_WorkflowDefinitionId_WorkflowTrans~",
                table: "flow_automation_hooks",
                newName: "IX_flow_automation_hooks_WorkflowDefinitionId_WorkflowTransitio~");

            migrationBuilder.RenameIndex(
                name: "IX_task_items_WorkflowStageId",
                table: "flow_task_items",
                newName: "IX_flow_task_items_WorkflowStageId");

            migrationBuilder.RenameIndex(
                name: "IX_task_items_Status",
                table: "flow_task_items",
                newName: "IX_flow_task_items_Status");

            migrationBuilder.RenameIndex(
                name: "IX_task_items_FlowDefinitionId",
                table: "flow_task_items",
                newName: "IX_flow_task_items_FlowDefinitionId");

            migrationBuilder.RenameIndex(
                name: "IX_task_items_context_type_context_id",
                table: "flow_task_items",
                newName: "IX_flow_task_items_context_type_context_id");

            migrationBuilder.RenameIndex(
                name: "IX_task_items_AssignedToUserId",
                table: "flow_task_items",
                newName: "IX_flow_task_items_AssignedToUserId");

            migrationBuilder.RenameIndex(
                name: "IX_task_items_AssignedToRoleKey",
                table: "flow_task_items",
                newName: "IX_flow_task_items_AssignedToRoleKey");

            migrationBuilder.RenameIndex(
                name: "IX_task_items_AssignedToOrgId",
                table: "flow_task_items",
                newName: "IX_flow_task_items_AssignedToOrgId");

            migrationBuilder.RenameIndex(
                name: "IX_notifications_WorkflowDefinitionId",
                table: "flow_notifications",
                newName: "IX_flow_notifications_WorkflowDefinitionId");

            migrationBuilder.RenameIndex(
                name: "IX_notifications_TaskId",
                table: "flow_notifications",
                newName: "IX_flow_notifications_TaskId");

            migrationBuilder.RenameIndex(
                name: "IX_notifications_TargetUserId",
                table: "flow_notifications",
                newName: "IX_flow_notifications_TargetUserId");

            migrationBuilder.RenameIndex(
                name: "IX_notifications_TargetRoleKey",
                table: "flow_notifications",
                newName: "IX_flow_notifications_TargetRoleKey");

            migrationBuilder.RenameIndex(
                name: "IX_notifications_TargetOrgId",
                table: "flow_notifications",
                newName: "IX_flow_notifications_TargetOrgId");

            migrationBuilder.RenameIndex(
                name: "IX_notifications_Status",
                table: "flow_notifications",
                newName: "IX_flow_notifications_Status");

            migrationBuilder.RenameIndex(
                name: "IX_notifications_CreatedAt",
                table: "flow_notifications",
                newName: "IX_flow_notifications_CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_automation_execution_logs_WorkflowAutomationHookId",
                table: "flow_automation_execution_logs",
                newName: "IX_flow_automation_execution_logs_WorkflowAutomationHookId");

            migrationBuilder.RenameIndex(
                name: "IX_automation_execution_logs_TaskId",
                table: "flow_automation_execution_logs",
                newName: "IX_flow_automation_execution_logs_TaskId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_flow_workflow_transitions",
                table: "flow_workflow_transitions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_flow_workflow_stages",
                table: "flow_workflow_stages",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_flow_automation_hooks",
                table: "flow_automation_hooks",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_flow_task_items",
                table: "flow_task_items",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_flow_notifications",
                table: "flow_notifications",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_flow_automation_execution_logs",
                table: "flow_automation_execution_logs",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_flow_automation_execution_logs_flow_automation_hooks_Workflo~",
                table: "flow_automation_execution_logs",
                column: "WorkflowAutomationHookId",
                principalTable: "flow_automation_hooks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_flow_automation_execution_logs_flow_task_items_TaskId",
                table: "flow_automation_execution_logs",
                column: "TaskId",
                principalTable: "flow_task_items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_flow_automation_hooks_flow_definitions_WorkflowDefinitionId",
                table: "flow_automation_hooks",
                column: "WorkflowDefinitionId",
                principalTable: "flow_definitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_flow_automation_hooks_flow_workflow_transitions_WorkflowTran~",
                table: "flow_automation_hooks",
                column: "WorkflowTransitionId",
                principalTable: "flow_workflow_transitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_flow_notifications_flow_definitions_WorkflowDefinitionId",
                table: "flow_notifications",
                column: "WorkflowDefinitionId",
                principalTable: "flow_definitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_flow_notifications_flow_task_items_TaskId",
                table: "flow_notifications",
                column: "TaskId",
                principalTable: "flow_task_items",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_flow_task_items_flow_definitions_FlowDefinitionId",
                table: "flow_task_items",
                column: "FlowDefinitionId",
                principalTable: "flow_definitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_flow_task_items_flow_workflow_stages_WorkflowStageId",
                table: "flow_task_items",
                column: "WorkflowStageId",
                principalTable: "flow_workflow_stages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_flow_workflow_stages_flow_definitions_WorkflowDefinitionId",
                table: "flow_workflow_stages",
                column: "WorkflowDefinitionId",
                principalTable: "flow_definitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_flow_workflow_transitions_flow_definitions_WorkflowDefinitio~",
                table: "flow_workflow_transitions",
                column: "WorkflowDefinitionId",
                principalTable: "flow_definitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_flow_workflow_transitions_flow_workflow_stages_FromStageId",
                table: "flow_workflow_transitions",
                column: "FromStageId",
                principalTable: "flow_workflow_stages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_flow_workflow_transitions_flow_workflow_stages_ToStageId",
                table: "flow_workflow_transitions",
                column: "ToStageId",
                principalTable: "flow_workflow_stages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_flow_automation_execution_logs_flow_automation_hooks_Workflo~",
                table: "flow_automation_execution_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_flow_automation_execution_logs_flow_task_items_TaskId",
                table: "flow_automation_execution_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_flow_automation_hooks_flow_definitions_WorkflowDefinitionId",
                table: "flow_automation_hooks");

            migrationBuilder.DropForeignKey(
                name: "FK_flow_automation_hooks_flow_workflow_transitions_WorkflowTran~",
                table: "flow_automation_hooks");

            migrationBuilder.DropForeignKey(
                name: "FK_flow_notifications_flow_definitions_WorkflowDefinitionId",
                table: "flow_notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_flow_notifications_flow_task_items_TaskId",
                table: "flow_notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_flow_task_items_flow_definitions_FlowDefinitionId",
                table: "flow_task_items");

            migrationBuilder.DropForeignKey(
                name: "FK_flow_task_items_flow_workflow_stages_WorkflowStageId",
                table: "flow_task_items");

            migrationBuilder.DropForeignKey(
                name: "FK_flow_workflow_stages_flow_definitions_WorkflowDefinitionId",
                table: "flow_workflow_stages");

            migrationBuilder.DropForeignKey(
                name: "FK_flow_workflow_transitions_flow_definitions_WorkflowDefinitio~",
                table: "flow_workflow_transitions");

            migrationBuilder.DropForeignKey(
                name: "FK_flow_workflow_transitions_flow_workflow_stages_FromStageId",
                table: "flow_workflow_transitions");

            migrationBuilder.DropForeignKey(
                name: "FK_flow_workflow_transitions_flow_workflow_stages_ToStageId",
                table: "flow_workflow_transitions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_flow_workflow_transitions",
                table: "flow_workflow_transitions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_flow_workflow_stages",
                table: "flow_workflow_stages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_flow_task_items",
                table: "flow_task_items");

            migrationBuilder.DropPrimaryKey(
                name: "PK_flow_notifications",
                table: "flow_notifications");

            migrationBuilder.DropPrimaryKey(
                name: "PK_flow_automation_hooks",
                table: "flow_automation_hooks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_flow_automation_execution_logs",
                table: "flow_automation_execution_logs");

            migrationBuilder.RenameTable(
                name: "flow_workflow_transitions",
                newName: "workflow_transitions");

            migrationBuilder.RenameTable(
                name: "flow_workflow_stages",
                newName: "workflow_stages");

            migrationBuilder.RenameTable(
                name: "flow_task_items",
                newName: "task_items");

            migrationBuilder.RenameTable(
                name: "flow_notifications",
                newName: "notifications");

            migrationBuilder.RenameTable(
                name: "flow_automation_hooks",
                newName: "workflow_automation_hooks");

            migrationBuilder.RenameTable(
                name: "flow_automation_execution_logs",
                newName: "automation_execution_logs");

            migrationBuilder.RenameIndex(
                name: "IX_flow_workflow_transitions_WorkflowDefinitionId_FromStageId_T~",
                table: "workflow_transitions",
                newName: "IX_workflow_transitions_WorkflowDefinitionId_FromStageId_ToStag~");

            migrationBuilder.RenameIndex(
                name: "IX_flow_workflow_transitions_ToStageId",
                table: "workflow_transitions",
                newName: "IX_workflow_transitions_ToStageId");

            migrationBuilder.RenameIndex(
                name: "IX_flow_workflow_transitions_FromStageId",
                table: "workflow_transitions",
                newName: "IX_workflow_transitions_FromStageId");

            migrationBuilder.RenameIndex(
                name: "IX_flow_workflow_stages_WorkflowDefinitionId_Key",
                table: "workflow_stages",
                newName: "IX_workflow_stages_WorkflowDefinitionId_Key");

            migrationBuilder.RenameIndex(
                name: "IX_flow_task_items_WorkflowStageId",
                table: "task_items",
                newName: "IX_task_items_WorkflowStageId");

            migrationBuilder.RenameIndex(
                name: "IX_flow_task_items_Status",
                table: "task_items",
                newName: "IX_task_items_Status");

            migrationBuilder.RenameIndex(
                name: "IX_flow_task_items_FlowDefinitionId",
                table: "task_items",
                newName: "IX_task_items_FlowDefinitionId");

            migrationBuilder.RenameIndex(
                name: "IX_flow_task_items_context_type_context_id",
                table: "task_items",
                newName: "IX_task_items_context_type_context_id");

            migrationBuilder.RenameIndex(
                name: "IX_flow_task_items_AssignedToUserId",
                table: "task_items",
                newName: "IX_task_items_AssignedToUserId");

            migrationBuilder.RenameIndex(
                name: "IX_flow_task_items_AssignedToRoleKey",
                table: "task_items",
                newName: "IX_task_items_AssignedToRoleKey");

            migrationBuilder.RenameIndex(
                name: "IX_flow_task_items_AssignedToOrgId",
                table: "task_items",
                newName: "IX_task_items_AssignedToOrgId");

            migrationBuilder.RenameIndex(
                name: "IX_flow_notifications_WorkflowDefinitionId",
                table: "notifications",
                newName: "IX_notifications_WorkflowDefinitionId");

            migrationBuilder.RenameIndex(
                name: "IX_flow_notifications_TaskId",
                table: "notifications",
                newName: "IX_notifications_TaskId");

            migrationBuilder.RenameIndex(
                name: "IX_flow_notifications_TargetUserId",
                table: "notifications",
                newName: "IX_notifications_TargetUserId");

            migrationBuilder.RenameIndex(
                name: "IX_flow_notifications_TargetRoleKey",
                table: "notifications",
                newName: "IX_notifications_TargetRoleKey");

            migrationBuilder.RenameIndex(
                name: "IX_flow_notifications_TargetOrgId",
                table: "notifications",
                newName: "IX_notifications_TargetOrgId");

            migrationBuilder.RenameIndex(
                name: "IX_flow_notifications_Status",
                table: "notifications",
                newName: "IX_notifications_Status");

            migrationBuilder.RenameIndex(
                name: "IX_flow_notifications_CreatedAt",
                table: "notifications",
                newName: "IX_notifications_CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_flow_automation_hooks_WorkflowTransitionId",
                table: "workflow_automation_hooks",
                newName: "IX_workflow_automation_hooks_WorkflowTransitionId");

            migrationBuilder.RenameIndex(
                name: "IX_flow_automation_hooks_WorkflowDefinitionId_WorkflowTransitio~",
                table: "workflow_automation_hooks",
                newName: "IX_workflow_automation_hooks_WorkflowDefinitionId_WorkflowTrans~");

            migrationBuilder.RenameIndex(
                name: "IX_flow_automation_execution_logs_WorkflowAutomationHookId",
                table: "automation_execution_logs",
                newName: "IX_automation_execution_logs_WorkflowAutomationHookId");

            migrationBuilder.RenameIndex(
                name: "IX_flow_automation_execution_logs_TaskId",
                table: "automation_execution_logs",
                newName: "IX_automation_execution_logs_TaskId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_workflow_transitions",
                table: "workflow_transitions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_workflow_stages",
                table: "workflow_stages",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_task_items",
                table: "task_items",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_notifications",
                table: "notifications",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_workflow_automation_hooks",
                table: "workflow_automation_hooks",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_automation_execution_logs",
                table: "automation_execution_logs",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_automation_execution_logs_task_items_TaskId",
                table: "automation_execution_logs",
                column: "TaskId",
                principalTable: "task_items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_automation_execution_logs_workflow_automation_hooks_Workflow~",
                table: "automation_execution_logs",
                column: "WorkflowAutomationHookId",
                principalTable: "workflow_automation_hooks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_notifications_flow_definitions_WorkflowDefinitionId",
                table: "notifications",
                column: "WorkflowDefinitionId",
                principalTable: "flow_definitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_notifications_task_items_TaskId",
                table: "notifications",
                column: "TaskId",
                principalTable: "task_items",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_task_items_flow_definitions_FlowDefinitionId",
                table: "task_items",
                column: "FlowDefinitionId",
                principalTable: "flow_definitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_task_items_workflow_stages_WorkflowStageId",
                table: "task_items",
                column: "WorkflowStageId",
                principalTable: "workflow_stages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_workflow_automation_hooks_flow_definitions_WorkflowDefinitio~",
                table: "workflow_automation_hooks",
                column: "WorkflowDefinitionId",
                principalTable: "flow_definitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_workflow_automation_hooks_workflow_transitions_WorkflowTrans~",
                table: "workflow_automation_hooks",
                column: "WorkflowTransitionId",
                principalTable: "workflow_transitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_workflow_stages_flow_definitions_WorkflowDefinitionId",
                table: "workflow_stages",
                column: "WorkflowDefinitionId",
                principalTable: "flow_definitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_workflow_transitions_flow_definitions_WorkflowDefinitionId",
                table: "workflow_transitions",
                column: "WorkflowDefinitionId",
                principalTable: "flow_definitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_workflow_transitions_workflow_stages_FromStageId",
                table: "workflow_transitions",
                column: "FromStageId",
                principalTable: "workflow_stages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_workflow_transitions_workflow_stages_ToStageId",
                table: "workflow_transitions",
                column: "ToStageId",
                principalTable: "workflow_stages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
