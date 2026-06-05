using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformAuditEventService.Data.Migrations
{
    // ─────────────────────────────────────────────────────────────────────────
    // InitialSchema — Platform Audit/Event Service
    //
    // Creates the four primary tables for the new entity model:
    //   • AuditEventRecords       — canonical rich audit event persistence model
    //   • AuditExportJobs         — async export job tracking
    //   • IntegrityCheckpoints    — aggregate hash snapshots for tamper detection
    //   • IngestSourceRegistrations — advisory ingest source registry
    //
    // NOTE: The legacy AuditEvents table (used by the InMemory service layer)
    // is intentionally excluded from this migration because it pre-exists in
    // the production database and was created by a prior DB repair operation.
    // It remains in the EF model snapshot so the ORM can track it, but
    // this migration does not own its lifecycle.
    //
    // For a fresh database, run this migration after ensuring the AuditEvents
    // table exists (or run the DB repair script / legacy migration separately).
    //
    // Production deployment:
    //   Use the idempotent SQL script to avoid failures on partial applies:
    //     dotnet ef migrations script --idempotent -o migration.sql
    // ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            // ── AuditEventRecords ─────────────────────────────────────────────
            // Canonical append-only audit event model. bigint PK for clustered
            // scan efficiency; AuditId char(36) is the stable public identifier.
            migrationBuilder.CreateTable(
                name: "AuditEventRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AuditId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EventId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    EventType = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EventCategory = table.Column<sbyte>(type: "tinyint", nullable: false),
                    SourceSystem = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceService = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceEnvironment = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PlatformId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    TenantId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OrganizationId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserScopeId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ScopeType = table.Column<sbyte>(type: "tinyint", nullable: false),
                    ActorId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActorType = table.Column<sbyte>(type: "tinyint", nullable: false),
                    ActorName = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActorIpAddress = table.Column<string>(type: "varchar(45)", maxLength: 45, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActorUserAgent = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntityType = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntityId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Action = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BeforeJson = table.Column<string>(type: "mediumtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AfterJson = table.Column<string>(type: "mediumtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MetadataJson = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CorrelationId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SessionId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VisibilityScope = table.Column<sbyte>(type: "tinyint", nullable: false),
                    Severity = table.Column<sbyte>(type: "tinyint", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    Hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PreviousHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IdempotencyKey = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsReplay = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    TagsJson = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEventRecords", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // ── AuditExportJobs ───────────────────────────────────────────────
            // Async export job lifecycle tracking. Status (tinyint) transitions:
            // Pending(1) → Processing(2) → Completed(3) | Failed(4) | Cancelled(5) | Expired(6)
            migrationBuilder.CreateTable(
                name: "AuditExportJobs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ExportId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RequestedBy = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ScopeType = table.Column<sbyte>(type: "tinyint", nullable: false),
                    ScopeId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FilterJson = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Format = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<sbyte>(type: "tinyint", nullable: false),
                    FilePath = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditExportJobs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // ── IngestSourceRegistrations ─────────────────────────────────────
            // Advisory registry of known ingest sources. Not a hard enforcement gate.
            // (SourceSystem, SourceService) is UNIQUE; NULL SourceService = "all services".
            migrationBuilder.CreateTable(
                name: "IngestSourceRegistrations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    SourceSystem = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceService = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngestSourceRegistrations", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // ── IntegrityCheckpoints ──────────────────────────────────────────
            // Periodic aggregate hash snapshots. AggregateHash covers all record
            // Hash values in the [FromRecordedAtUtc, ToRecordedAtUtc) window.
            // RecordCount provides a fast deletion-detection signal.
            migrationBuilder.CreateTable(
                name: "IntegrityCheckpoints",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CheckpointType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FromRecordedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    ToRecordedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    AggregateHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RecordCount = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrityCheckpoints", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // ── AuditEventRecords indexes ─────────────────────────────────────

            migrationBuilder.CreateIndex(
                name: "IX_AuditEventRecords_ActorId",
                table: "AuditEventRecords",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEventRecords_CorrelationId",
                table: "AuditEventRecords",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEventRecords_EntityType_EntityId",
                table: "AuditEventRecords",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEventRecords_EventCategory",
                table: "AuditEventRecords",
                column: "EventCategory");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEventRecords_EventType",
                table: "AuditEventRecords",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEventRecords_OccurredAtUtc",
                table: "AuditEventRecords",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEventRecords_OrganizationId",
                table: "AuditEventRecords",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEventRecords_RecordedAtUtc",
                table: "AuditEventRecords",
                column: "RecordedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEventRecords_RequestId",
                table: "AuditEventRecords",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEventRecords_SessionId",
                table: "AuditEventRecords",
                column: "SessionId");

            // Composite: high/critical severity feeds
            migrationBuilder.CreateIndex(
                name: "IX_AuditEventRecords_Severity_RecordedAt",
                table: "AuditEventRecords",
                columns: new[] { "Severity", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEventRecords_TenantId",
                table: "AuditEventRecords",
                column: "TenantId");

            // Composite: tenant + category + time — category-filtered tenant dashboards
            migrationBuilder.CreateIndex(
                name: "IX_AuditEventRecords_TenantId_Category_OccurredAt",
                table: "AuditEventRecords",
                columns: new[] { "TenantId", "EventCategory", "OccurredAtUtc" });

            // Composite: primary tenant time-range query pattern
            migrationBuilder.CreateIndex(
                name: "IX_AuditEventRecords_TenantId_OccurredAt",
                table: "AuditEventRecords",
                columns: new[] { "TenantId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEventRecords_VisibilityScope",
                table: "AuditEventRecords",
                column: "VisibilityScope");

            // UNIQUE: public identifier lookup
            migrationBuilder.CreateIndex(
                name: "UX_AuditEventRecords_AuditId",
                table: "AuditEventRecords",
                column: "AuditId",
                unique: true);

            // UNIQUE: idempotency dedup — MySQL allows multiple NULLs in UNIQUE index
            migrationBuilder.CreateIndex(
                name: "UX_AuditEventRecords_IdempotencyKey",
                table: "AuditEventRecords",
                column: "IdempotencyKey",
                unique: true);

            // ── AuditExportJobs indexes ───────────────────────────────────────

            migrationBuilder.CreateIndex(
                name: "IX_AuditExportJobs_CreatedAtUtc",
                table: "AuditExportJobs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditExportJobs_RequestedBy",
                table: "AuditExportJobs",
                column: "RequestedBy");

            // Composite: "my exports" with status + time ordering
            migrationBuilder.CreateIndex(
                name: "IX_AuditExportJobs_RequestedBy_Status_CreatedAt",
                table: "AuditExportJobs",
                columns: new[] { "RequestedBy", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditExportJobs_ScopeType_ScopeId",
                table: "AuditExportJobs",
                columns: new[] { "ScopeType", "ScopeId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditExportJobs_Status",
                table: "AuditExportJobs",
                column: "Status");

            // UNIQUE: public identifier lookup
            migrationBuilder.CreateIndex(
                name: "UX_AuditExportJobs_ExportId",
                table: "AuditExportJobs",
                column: "ExportId",
                unique: true);

            // ── IngestSourceRegistrations indexes ─────────────────────────────

            migrationBuilder.CreateIndex(
                name: "IX_IngestSourceRegistrations_IsActive",
                table: "IngestSourceRegistrations",
                column: "IsActive");

            // UNIQUE: dedup + primary source lookup; NULLs are distinct in MySQL UNIQUE
            migrationBuilder.CreateIndex(
                name: "UX_IngestSourceRegistrations_SourceSystem_SourceService",
                table: "IngestSourceRegistrations",
                columns: new[] { "SourceSystem", "SourceService" },
                unique: true);

            // ── IntegrityCheckpoints indexes ──────────────────────────────────

            migrationBuilder.CreateIndex(
                name: "IX_IntegrityCheckpoints_CheckpointType",
                table: "IntegrityCheckpoints",
                column: "CheckpointType");

            // Composite: find checkpoint for a specific cadence + period
            migrationBuilder.CreateIndex(
                name: "IX_IntegrityCheckpoints_CheckpointType_FromAt",
                table: "IntegrityCheckpoints",
                columns: new[] { "CheckpointType", "FromRecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrityCheckpoints_CreatedAtUtc",
                table: "IntegrityCheckpoints",
                column: "CreatedAtUtc");

            // Composite: find the checkpoint covering a given time window
            migrationBuilder.CreateIndex(
                name: "IX_IntegrityCheckpoints_Window",
                table: "IntegrityCheckpoints",
                columns: new[] { "FromRecordedAtUtc", "ToRecordedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // NOTE: AuditEvents is intentionally excluded — this migration does not own it.
            migrationBuilder.DropTable(name: "AuditEventRecords");
            migrationBuilder.DropTable(name: "AuditExportJobs");
            migrationBuilder.DropTable(name: "IngestSourceRegistrations");
            migrationBuilder.DropTable(name: "IntegrityCheckpoints");
        }
    }
}
