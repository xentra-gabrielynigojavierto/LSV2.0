using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalViewIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SlaState_TenantId_Priority",
                table: "comms_ConversationSlaStates",
                columns: new[] { "TenantId", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_TenantId_AssignmentStatus",
                table: "comms_ConversationAssignments",
                columns: new[] { "TenantId", "AssignmentStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SlaState_TenantId_Priority",
                table: "comms_ConversationSlaStates");

            migrationBuilder.DropIndex(
                name: "IX_Assignments_TenantId_AssignmentStatus",
                table: "comms_ConversationAssignments");
        }
    }
}
