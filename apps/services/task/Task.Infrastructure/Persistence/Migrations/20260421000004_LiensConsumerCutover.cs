using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Task.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LiensConsumerCutover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // TASK-B04 — extend tasks_Notes to support Liens consumer features:
            //   • AuthorName — display name from the originating product (e.g. Liens user name)
            //   • IsEdited   — soft flag set when the note body is later modified
            //   • IsDeleted  — soft-delete flag (rows are never hard-deleted)
            //   • Note max length 4000 → 5000 to match Liens note limits

            migrationBuilder.AddColumn<string>(
                name: "AuthorName",
                table: "tasks_Notes",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsEdited",
                table: "tasks_Notes",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "tasks_Notes",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                table: "tasks_Notes",
                type: "varchar(5000)",
                maxLength: 5000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(4000)",
                oldMaxLength: 4000)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            // Partial index on IsDeleted so GetByTaskAsync(!IsDeleted) is efficient
            migrationBuilder.CreateIndex(
                name: "IX_Notes_TenantId_TaskId_IsDeleted",
                table: "tasks_Notes",
                columns: new[] { "TenantId", "TaskId", "IsDeleted" });

            // Composite index to support Liens consumer GET /api/tasks?sourceEntityType=...&sourceEntityId=...
            migrationBuilder.CreateIndex(
                name: "IX_Tasks_TenantId_SourceEntityType_SourceEntityId",
                table: "tasks_Tasks",
                columns: new[] { "TenantId", "SourceEntityType", "SourceEntityId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notes_TenantId_TaskId_IsDeleted",
                table: "tasks_Notes");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_TenantId_SourceEntityType_SourceEntityId",
                table: "tasks_Tasks");

            migrationBuilder.DropColumn(
                name: "AuthorName",
                table: "tasks_Notes");

            migrationBuilder.DropColumn(
                name: "IsEdited",
                table: "tasks_Notes");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "tasks_Notes");

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                table: "tasks_Notes",
                type: "varchar(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(5000)",
                oldMaxLength: 5000)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
