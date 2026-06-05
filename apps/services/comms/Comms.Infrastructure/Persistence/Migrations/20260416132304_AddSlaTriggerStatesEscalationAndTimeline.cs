using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSlaTriggerStatesEscalationAndTimeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "comms_ConversationAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ConversationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    QueueId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    AssignedUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    AssignedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    AssignmentStatus = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastAssignedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UnassignedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comms_ConversationAssignments", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "comms_ConversationQueues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDefault = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comms_ConversationQueues", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "comms_ConversationSlaStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ConversationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Priority = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FirstResponseDueAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ResolutionDueAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FirstResponseAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    BreachedFirstResponse = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    BreachedResolution = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    WaitingOn = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastEvaluatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    SlaStartedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comms_ConversationSlaStates", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "comms_ConversationSlaTriggerStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ConversationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FirstResponseWarningSentAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FirstResponseBreachSentAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ResolutionWarningSentAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ResolutionBreachSentAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastEvaluatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastEscalatedToUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    LastEscalatedQueueId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    WarningThresholdSnapshotMinutes = table.Column<int>(type: "int", nullable: true),
                    EvaluationVersion = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comms_ConversationSlaTriggerStates", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "comms_ConversationTimelineEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ConversationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EventType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EventSubType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActorType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActorId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ActorDisplayName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Summary = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MetadataJson = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RelatedMessageId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    RelatedAssignmentId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    RelatedSlaId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Visibility = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comms_ConversationTimelineEntries", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "comms_QueueEscalationConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    QueueId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FallbackUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comms_QueueEscalationConfigs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_TenantId_AssignedUserId",
                table: "comms_ConversationAssignments",
                columns: new[] { "TenantId", "AssignedUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_TenantId_ConversationId",
                table: "comms_ConversationAssignments",
                columns: new[] { "TenantId", "ConversationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_TenantId_QueueId",
                table: "comms_ConversationAssignments",
                columns: new[] { "TenantId", "QueueId" });

            migrationBuilder.CreateIndex(
                name: "IX_Queues_TenantId_Code",
                table: "comms_ConversationQueues",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Queues_TenantId_IsDefault",
                table: "comms_ConversationQueues",
                columns: new[] { "TenantId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_SlaState_TenantId_BreachedFirstResponse",
                table: "comms_ConversationSlaStates",
                columns: new[] { "TenantId", "BreachedFirstResponse" });

            migrationBuilder.CreateIndex(
                name: "IX_SlaState_TenantId_BreachedResolution",
                table: "comms_ConversationSlaStates",
                columns: new[] { "TenantId", "BreachedResolution" });

            migrationBuilder.CreateIndex(
                name: "IX_SlaState_TenantId_ConversationId",
                table: "comms_ConversationSlaStates",
                columns: new[] { "TenantId", "ConversationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SlaTriggerState_TenantId",
                table: "comms_ConversationSlaTriggerStates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SlaTriggerState_TenantId_ConversationId",
                table: "comms_ConversationSlaTriggerStates",
                columns: new[] { "TenantId", "ConversationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SlaTriggerState_TenantId_FirstResponseBreachSentAtUtc",
                table: "comms_ConversationSlaTriggerStates",
                columns: new[] { "TenantId", "FirstResponseBreachSentAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SlaTriggerState_TenantId_ResolutionBreachSentAtUtc",
                table: "comms_ConversationSlaTriggerStates",
                columns: new[] { "TenantId", "ResolutionBreachSentAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Timeline_TenantId_ConversationId_EventType",
                table: "comms_ConversationTimelineEntries",
                columns: new[] { "TenantId", "ConversationId", "EventType" });

            migrationBuilder.CreateIndex(
                name: "IX_Timeline_TenantId_ConversationId_OccurredAtUtc",
                table: "comms_ConversationTimelineEntries",
                columns: new[] { "TenantId", "ConversationId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Timeline_TenantId_ConversationId_Visibility",
                table: "comms_ConversationTimelineEntries",
                columns: new[] { "TenantId", "ConversationId", "Visibility" });

            migrationBuilder.CreateIndex(
                name: "IX_QueueEscalationConfig_TenantId_QueueId",
                table: "comms_QueueEscalationConfigs",
                columns: new[] { "TenantId", "QueueId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "comms_ConversationAssignments");

            migrationBuilder.DropTable(
                name: "comms_ConversationQueues");

            migrationBuilder.DropTable(
                name: "comms_ConversationSlaStates");

            migrationBuilder.DropTable(
                name: "comms_ConversationSlaTriggerStates");

            migrationBuilder.DropTable(
                name: "comms_ConversationTimelineEntries");

            migrationBuilder.DropTable(
                name: "comms_QueueEscalationConfigs");
        }
    }
}
