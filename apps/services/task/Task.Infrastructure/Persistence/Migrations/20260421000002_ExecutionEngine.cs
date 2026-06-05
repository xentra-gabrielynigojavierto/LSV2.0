using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Task.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExecutionEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add CurrentStageId to tasks_Tasks
            migrationBuilder.AddColumn<Guid>(
                name: "CurrentStageId",
                table: "tasks_Tasks",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_TenantId_StageId",
                table: "tasks_Tasks",
                columns: new[] { "TenantId", "CurrentStageId" });

            // tasks_StageConfigs
            migrationBuilder.CreateTable(
                name: "tasks_StageConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SourceProductCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks_StageConfigs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_StageConfigs_TenantId_Product",
                table: "tasks_StageConfigs",
                columns: new[] { "TenantId", "SourceProductCode" });

            migrationBuilder.CreateIndex(
                name: "IX_StageConfigs_TenantId_Product_Code",
                table: "tasks_StageConfigs",
                columns: new[] { "TenantId", "SourceProductCode", "Code" },
                unique: true);

            // tasks_GovernanceSettings
            migrationBuilder.CreateTable(
                name: "tasks_GovernanceSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SourceProductCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequireAssignee = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    RequireDueDate = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    RequireStage = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    AllowUnassign = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    AllowCancel = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    AllowCompleteWithoutStage = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    AllowNotesOnClosedTasks = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    DefaultPriority = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "MEDIUM")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DefaultTaskScope = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "GENERAL")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks_GovernanceSettings", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_GovernanceSettings_TenantId_Product",
                table: "tasks_GovernanceSettings",
                columns: new[] { "TenantId", "SourceProductCode" },
                unique: true);

            // tasks_Templates
            migrationBuilder.CreateTable(
                name: "tasks_Templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SourceProductCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DefaultTitle = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DefaultDescription = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DefaultPriority = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "MEDIUM")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DefaultScope = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "GENERAL")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DefaultDueInDays = table.Column<int>(type: "int", nullable: true),
                    DefaultStageId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks_Templates", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Templates_TenantId_Product",
                table: "tasks_Templates",
                columns: new[] { "TenantId", "SourceProductCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Templates_TenantId_Product_Code",
                table: "tasks_Templates",
                columns: new[] { "TenantId", "SourceProductCode", "Code" },
                unique: true);

            // tasks_Reminders
            migrationBuilder.CreateTable(
                name: "tasks_Reminders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TaskId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ReminderType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RemindAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "PENDING")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastAttemptAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FailureReason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks_Reminders", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_TenantId_TaskId",
                table: "tasks_Reminders",
                columns: new[] { "TenantId", "TaskId" });

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_Status_RemindAt",
                table: "tasks_Reminders",
                columns: new[] { "Status", "RemindAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "tasks_Reminders");
            migrationBuilder.DropTable(name: "tasks_Templates");
            migrationBuilder.DropTable(name: "tasks_GovernanceSettings");
            migrationBuilder.DropTable(name: "tasks_StageConfigs");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_TenantId_StageId",
                table: "tasks_Tasks");

            migrationBuilder.DropColumn(
                name: "CurrentStageId",
                table: "tasks_Tasks");
        }
    }
}
