using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenE2ENotificationsIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_EmailRefs_TenantId_MessageId",
                table: "comms_EmailMessageReferences",
                columns: new[] { "TenantId", "MessageId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailRefs_TenantId_MessageId",
                table: "comms_EmailMessageReferences");
        }
    }
}
