using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Task.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-B05 (TASK-017) — adds a unique index on tasks_LinkedEntities
    /// (TaskId, EntityType, EntityId) to enforce data-integrity at the DB level
    /// and back the application-layer dedup guard in TaskLinkedEntityRepository.AddAsync.
    /// </summary>
    public partial class LinkedEntityUniqueConstraint : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_LinkedEntities_TaskId_EntityType_EntityId",
                table: "tasks_LinkedEntities",
                columns: new[] { "TaskId", "EntityType", "EntityId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_LinkedEntities_TaskId_EntityType_EntityId",
                table: "tasks_LinkedEntities");
        }
    }
}
