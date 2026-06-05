using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddSmsGovernanceReleaseManagement : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── ntf_SmsGovernanceReleasePackages ─────────────────────────────────
        migrationBuilder.CreateTable(
            name: "ntf_SmsGovernanceReleasePackages",
            columns: table => new
            {
                Id                    = table.Column<string>(type: "char(36)", nullable: false),
                TenantId              = table.Column<string>(type: "char(36)", nullable: true),
                Name                  = table.Column<string>(maxLength: 200, nullable: false),
                Description           = table.Column<string>(maxLength: 1000, nullable: true),
                ReleaseState          = table.Column<string>(maxLength: 30, nullable: false),
                ReleaseType           = table.Column<string>(maxLength: 30, nullable: false),
                ScheduledActivationAt = table.Column<DateTime>(nullable: true),
                ActivatedAt           = table.Column<DateTime>(nullable: true),
                SupersededAt          = table.Column<DateTime>(nullable: true),
                SupersededByReleaseId = table.Column<string>(type: "char(36)", nullable: true),
                RejectedAt            = table.Column<DateTime>(nullable: true),
                ArchivedAt            = table.Column<DateTime>(nullable: true),
                CreatedAt             = table.Column<DateTime>(nullable: false),
                UpdatedAt             = table.Column<DateTime>(nullable: false),
                CreatedBy             = table.Column<string>(maxLength: 200, nullable: true),
                UpdatedBy             = table.Column<string>(maxLength: 200, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_ntf_SmsGovernanceReleasePackages", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsGovRelPkgs_Tenant_State_Created",
            table: "ntf_SmsGovernanceReleasePackages",
            columns: new[] { "TenantId", "ReleaseState", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsGovRelPkgs_State_Scheduled",
            table: "ntf_SmsGovernanceReleasePackages",
            columns: new[] { "ReleaseState", "ScheduledActivationAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsGovRelPkgs_Type_Created",
            table: "ntf_SmsGovernanceReleasePackages",
            columns: new[] { "ReleaseType", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsGovRelPkgs_Created",
            table: "ntf_SmsGovernanceReleasePackages",
            column: "CreatedAt");

        // ── ntf_SmsGovernanceReleaseItems ─────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "ntf_SmsGovernanceReleaseItems",
            columns: table => new
            {
                Id                  = table.Column<string>(type: "char(36)", nullable: false),
                ReleasePackageId    = table.Column<string>(type: "char(36)", nullable: false),
                EntityType          = table.Column<string>(maxLength: 30, nullable: false),
                EntityId            = table.Column<string>(type: "char(36)", nullable: false),
                EntityVersionNumber = table.Column<int>(nullable: true),
                ActionType          = table.Column<string>(maxLength: 20, nullable: false),
                EntitySnapshotJson  = table.Column<string>(type: "mediumtext", nullable: true),
                CreatedAt           = table.Column<DateTime>(nullable: false),
                CreatedBy           = table.Column<string>(maxLength: 200, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_ntf_SmsGovernanceReleaseItems", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsGovRelItems_Package",
            table: "ntf_SmsGovernanceReleaseItems",
            column: "ReleasePackageId");

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsGovRelItems_Entity",
            table: "ntf_SmsGovernanceReleaseItems",
            columns: new[] { "EntityType", "EntityId" });

        // ── ntf_SmsGovernanceApprovalRequests ─────────────────────────────────
        migrationBuilder.CreateTable(
            name: "ntf_SmsGovernanceApprovalRequests",
            columns: table => new
            {
                Id               = table.Column<string>(type: "char(36)", nullable: false),
                ReleasePackageId = table.Column<string>(type: "char(36)", nullable: false),
                ApprovalStage    = table.Column<int>(nullable: false),
                ApproverRole     = table.Column<string>(maxLength: 100, nullable: false),
                RequiredApprovals = table.Column<int>(nullable: false),
                Status           = table.Column<string>(maxLength: 20, nullable: false),
                RequestedAt      = table.Column<DateTime>(nullable: false),
                ResolvedAt       = table.Column<DateTime>(nullable: true),
                CreatedAt        = table.Column<DateTime>(nullable: false),
                UpdatedAt        = table.Column<DateTime>(nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ntf_SmsGovernanceApprovalRequests", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsGovApprReqs_Package_Stage",
            table: "ntf_SmsGovernanceApprovalRequests",
            columns: new[] { "ReleasePackageId", "ApprovalStage" });

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsGovApprReqs_Status_Requested",
            table: "ntf_SmsGovernanceApprovalRequests",
            columns: new[] { "Status", "RequestedAt" });

        // ── ntf_SmsGovernanceApprovalDecisions ────────────────────────────────
        migrationBuilder.CreateTable(
            name: "ntf_SmsGovernanceApprovalDecisions",
            columns: table => new
            {
                Id                = table.Column<string>(type: "char(36)", nullable: false),
                ApprovalRequestId = table.Column<string>(type: "char(36)", nullable: false),
                ReleasePackageId  = table.Column<string>(type: "char(36)", nullable: false),
                Decision          = table.Column<string>(maxLength: 20, nullable: false),
                DecisionReason    = table.Column<string>(maxLength: 1000, nullable: true),
                DecidedBy         = table.Column<string>(maxLength: 200, nullable: true),
                DecidedByRole     = table.Column<string>(maxLength: 100, nullable: true),
                CreatedAt         = table.Column<DateTime>(nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ntf_SmsGovernanceApprovalDecisions", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsGovApprDecs_Request_Created",
            table: "ntf_SmsGovernanceApprovalDecisions",
            columns: new[] { "ApprovalRequestId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsGovApprDecs_Package_Created",
            table: "ntf_SmsGovernanceApprovalDecisions",
            columns: new[] { "ReleasePackageId", "CreatedAt" });

        // ── ntf_SmsGovernanceReleaseAuditEvents ───────────────────────────────
        migrationBuilder.CreateTable(
            name: "ntf_SmsGovernanceReleaseAuditEvents",
            columns: table => new
            {
                Id               = table.Column<string>(type: "char(36)", nullable: false),
                ReleasePackageId = table.Column<string>(type: "char(36)", nullable: false),
                EventType        = table.Column<string>(maxLength: 40, nullable: false),
                PreviousState    = table.Column<string>(maxLength: 30, nullable: true),
                NewState         = table.Column<string>(maxLength: 30, nullable: true),
                Actor            = table.Column<string>(maxLength: 200, nullable: true),
                Reason           = table.Column<string>(maxLength: 1000, nullable: true),
                MetadataJson     = table.Column<string>(type: "mediumtext", nullable: true),
                CreatedAt        = table.Column<DateTime>(nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ntf_SmsGovernanceReleaseAuditEvents", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsGovRelAudit_Package_Created",
            table: "ntf_SmsGovernanceReleaseAuditEvents",
            columns: new[] { "ReleasePackageId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsGovRelAudit_EventType_Created",
            table: "ntf_SmsGovernanceReleaseAuditEvents",
            columns: new[] { "EventType", "CreatedAt" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ntf_SmsGovernanceReleaseAuditEvents");
        migrationBuilder.DropTable(name: "ntf_SmsGovernanceApprovalDecisions");
        migrationBuilder.DropTable(name: "ntf_SmsGovernanceApprovalRequests");
        migrationBuilder.DropTable(name: "ntf_SmsGovernanceReleaseItems");
        migrationBuilder.DropTable(name: "ntf_SmsGovernanceReleasePackages");
    }
}
