using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Task.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-FLOW-02 — Adds Flow queue assignment metadata, lifecycle timestamps, and SLA state
    /// columns to tasks_Tasks. Prerequisite for Task service owning Flow read paths.
    ///
    /// New columns:
    ///   AssignmentMode     varchar(20)   nullable — DirectUser / RoleQueue / OrgQueue / Unassigned
    ///   AssignedRole       varchar(100)  nullable — role key when mode = RoleQueue
    ///   AssignedOrgId      varchar(100)  nullable — org ID when mode = OrgQueue
    ///   AssignedAt         datetime(6)   nullable — UTC timestamp of most recent assignment
    ///   AssignedBy         varchar(100)  nullable — JWT sub of the actor who assigned
    ///   AssignmentReason   varchar(500)  nullable — free-form note
    ///   StartedAt          datetime(6)   nullable — when task moved to IN_PROGRESS
    ///   CancelledAt        datetime(6)   nullable — when task was cancelled
    ///   SlaStatus          varchar(20)   NOT NULL DEFAULT 'OnTrack'
    ///   SlaBreachedAt      datetime(6)   nullable — first breach observation
    ///   LastSlaEvaluatedAt datetime(6)   nullable — last SLA evaluator run
    ///
    /// New indexes:
    ///   IX_Tasks_TenantId_AssignmentMode_Role
    ///   IX_Tasks_TenantId_AssignmentMode_Org
    /// </summary>
    public partial class AddFlowTaskFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Queue assignment metadata ──────────────────────────────────────────
            migrationBuilder.AddColumn<string>(
                name:        "AssignmentMode",
                table:       "tasks_Tasks",
                type:        "varchar(20)",
                maxLength:   20,
                nullable:    true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name:        "AssignedRole",
                table:       "tasks_Tasks",
                type:        "varchar(100)",
                maxLength:   100,
                nullable:    true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name:        "AssignedOrgId",
                table:       "tasks_Tasks",
                type:        "varchar(100)",
                maxLength:   100,
                nullable:    true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name:        "AssignedAt",
                table:       "tasks_Tasks",
                type:        "datetime(6)",
                nullable:    true);

            migrationBuilder.AddColumn<string>(
                name:        "AssignedBy",
                table:       "tasks_Tasks",
                type:        "varchar(100)",
                maxLength:   100,
                nullable:    true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name:        "AssignmentReason",
                table:       "tasks_Tasks",
                type:        "varchar(500)",
                maxLength:   500,
                nullable:    true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // ── Lifecycle timestamps ───────────────────────────────────────────────
            migrationBuilder.AddColumn<DateTime>(
                name:        "StartedAt",
                table:       "tasks_Tasks",
                type:        "datetime(6)",
                nullable:    true);

            migrationBuilder.AddColumn<DateTime>(
                name:        "CancelledAt",
                table:       "tasks_Tasks",
                type:        "datetime(6)",
                nullable:    true);

            // ── SLA state ─────────────────────────────────────────────────────────
            migrationBuilder.AddColumn<string>(
                name:         "SlaStatus",
                table:        "tasks_Tasks",
                type:         "varchar(20)",
                maxLength:    20,
                nullable:     false,
                defaultValue: "OnTrack")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name:        "SlaBreachedAt",
                table:       "tasks_Tasks",
                type:        "datetime(6)",
                nullable:    true);

            migrationBuilder.AddColumn<DateTime>(
                name:        "LastSlaEvaluatedAt",
                table:       "tasks_Tasks",
                type:        "datetime(6)",
                nullable:    true);

            // ── Indexes ───────────────────────────────────────────────────────────
            migrationBuilder.CreateIndex(
                name:    "IX_Tasks_TenantId_AssignmentMode_Role",
                table:   "tasks_Tasks",
                columns: new[] { "TenantId", "AssignmentMode", "AssignedRole" });

            migrationBuilder.CreateIndex(
                name:    "IX_Tasks_TenantId_AssignmentMode_Org",
                table:   "tasks_Tasks",
                columns: new[] { "TenantId", "AssignmentMode", "AssignedOrgId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name:  "IX_Tasks_TenantId_AssignmentMode_Role",
                table: "tasks_Tasks");

            migrationBuilder.DropIndex(
                name:  "IX_Tasks_TenantId_AssignmentMode_Org",
                table: "tasks_Tasks");

            migrationBuilder.DropColumn(name: "AssignmentMode",     table: "tasks_Tasks");
            migrationBuilder.DropColumn(name: "AssignedRole",       table: "tasks_Tasks");
            migrationBuilder.DropColumn(name: "AssignedOrgId",      table: "tasks_Tasks");
            migrationBuilder.DropColumn(name: "AssignedAt",         table: "tasks_Tasks");
            migrationBuilder.DropColumn(name: "AssignedBy",         table: "tasks_Tasks");
            migrationBuilder.DropColumn(name: "AssignmentReason",   table: "tasks_Tasks");
            migrationBuilder.DropColumn(name: "StartedAt",          table: "tasks_Tasks");
            migrationBuilder.DropColumn(name: "CancelledAt",        table: "tasks_Tasks");
            migrationBuilder.DropColumn(name: "SlaStatus",          table: "tasks_Tasks");
            migrationBuilder.DropColumn(name: "SlaBreachedAt",      table: "tasks_Tasks");
            migrationBuilder.DropColumn(name: "LastSlaEvaluatedAt", table: "tasks_Tasks");
        }
    }
}
