using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGovernanceExecutionRuntime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ntf_GovernanceExecutionRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NotificationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    AttemptId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ChannelType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DecisionType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReasonCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MatchedRuleIdsJson = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MatchedRulePackIdsJson = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AppliedOverlayIdsJson = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContentClassification = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TopologyResolutionStatus = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EngineStatus = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SafeMetadataJson = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsSimulation = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ntf_GovernanceExecutionRecords", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ntf_GovExecRecords_Channel_CreatedAt",
                table: "ntf_GovernanceExecutionRecords",
                columns: new[] { "ChannelType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ntf_GovExecRecords_Tenant_Channel_CreatedAt",
                table: "ntf_GovernanceExecutionRecords",
                columns: new[] { "TenantId", "ChannelType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ntf_GovExecRecords_Decision_CreatedAt",
                table: "ntf_GovernanceExecutionRecords",
                columns: new[] { "DecisionType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ntf_GovExecRecords_NotificationId",
                table: "ntf_GovernanceExecutionRecords",
                column: "NotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_ntf_GovExecRecords_Simulation_CreatedAt",
                table: "ntf_GovernanceExecutionRecords",
                columns: new[] { "IsSimulation", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ntf_GovernanceExecutionRecords");
        }
    }
}
