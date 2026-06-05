using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSmsGovernanceTenantScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── ntf_SmsGovernanceTenantRulePackAssignments ────────────────────
            migrationBuilder.CreateTable(
                name: "ntf_SmsGovernanceTenantRulePackAssignments",
                columns: table => new
                {
                    Id               = table.Column<string>(type: "char(36)", nullable: false),
                    TenantId         = table.Column<string>(type: "char(36)", nullable: false),
                    RulePackId       = table.Column<string>(type: "char(36)", nullable: false),
                    AssignmentState  = table.Column<string>(type: "varchar(50)",  maxLength: 50,   nullable: false),
                    AssignmentMode   = table.Column<string>(type: "varchar(50)",  maxLength: 50,   nullable: false),
                    Priority         = table.Column<int>(type: "int", nullable: false, defaultValue: 100),
                    EffectiveFrom    = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    EffectiveTo      = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RolloutPlanId    = table.Column<string>(type: "char(36)", nullable: true),
                    RolloutStageId   = table.Column<string>(type: "char(36)", nullable: true),
                    ReleasePackageId = table.Column<string>(type: "char(36)", nullable: true),
                    AssignedBy       = table.Column<string>(type: "varchar(200)", maxLength: 200,  nullable: true),
                    DeactivationReason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    ActivatedAt      = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeactivatedAt    = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    SupersededAt     = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt        = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt        = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ntf_SmsGovernanceTenantRulePackAssignments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovTenantAssign_Tenant_State_Priority",
                table: "ntf_SmsGovernanceTenantRulePackAssignments",
                columns: new[] { "TenantId", "AssignmentState", "Priority" });
            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovTenantAssign_Tenant_Pack",
                table: "ntf_SmsGovernanceTenantRulePackAssignments",
                columns: new[] { "TenantId", "RulePackId" });
            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovTenantAssign_Pack_State",
                table: "ntf_SmsGovernanceTenantRulePackAssignments",
                columns: new[] { "RulePackId", "AssignmentState" });
            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovTenantAssign_Rollout_State",
                table: "ntf_SmsGovernanceTenantRulePackAssignments",
                columns: new[] { "RolloutPlanId", "AssignmentState" });
            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovTenantAssign_EffectiveWindow",
                table: "ntf_SmsGovernanceTenantRulePackAssignments",
                columns: new[] { "EffectiveFrom", "EffectiveTo" });

            // ── ntf_SmsGovernanceTenantOverlays ───────────────────────────────
            migrationBuilder.CreateTable(
                name: "ntf_SmsGovernanceTenantOverlays",
                columns: table => new
                {
                    Id           = table.Column<string>(type: "char(36)", nullable: false),
                    TenantId     = table.Column<string>(type: "char(36)", nullable: false),
                    RulePackId   = table.Column<string>(type: "char(36)", nullable: true),
                    RuleId       = table.Column<string>(type: "char(36)", nullable: true),
                    OverlayType  = table.Column<string>(type: "varchar(50)",   maxLength: 50,   nullable: false),
                    OverlayState = table.Column<string>(type: "varchar(50)",   maxLength: 50,   nullable: false),
                    OverrideJson = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true),
                    Priority     = table.Column<int>(type: "int", nullable: false, defaultValue: 100),
                    Enabled      = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    EffectiveTo   = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt    = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt    = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy    = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    UpdatedBy    = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ntf_SmsGovernanceTenantOverlays", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovTenantOverlay_Tenant_Enabled_Priority",
                table: "ntf_SmsGovernanceTenantOverlays",
                columns: new[] { "TenantId", "Enabled", "Priority" });
            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovTenantOverlay_Tenant_Pack",
                table: "ntf_SmsGovernanceTenantOverlays",
                columns: new[] { "TenantId", "RulePackId" });
            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovTenantOverlay_Tenant_Rule",
                table: "ntf_SmsGovernanceTenantOverlays",
                columns: new[] { "TenantId", "RuleId" });
            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovTenantOverlay_Type_Enabled",
                table: "ntf_SmsGovernanceTenantOverlays",
                columns: new[] { "OverlayType", "Enabled" });

            // ── ntf_SmsGovernanceTenantAssignmentAuditEvents ──────────────────
            migrationBuilder.CreateTable(
                name: "ntf_SmsGovernanceTenantAssignmentAuditEvents",
                columns: table => new
                {
                    Id            = table.Column<string>(type: "char(36)", nullable: false),
                    TenantId      = table.Column<string>(type: "char(36)", nullable: false),
                    AssignmentId  = table.Column<string>(type: "char(36)", nullable: true),
                    OverlayId     = table.Column<string>(type: "char(36)", nullable: true),
                    EventType     = table.Column<string>(type: "varchar(100)",  maxLength: 100,  nullable: false),
                    PreviousState = table.Column<string>(type: "varchar(50)",   maxLength: 50,   nullable: true),
                    NewState      = table.Column<string>(type: "varchar(50)",   maxLength: 50,   nullable: true),
                    Actor         = table.Column<string>(type: "varchar(200)",  maxLength: 200,  nullable: true),
                    Reason        = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    MetadataJson  = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt     = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ntf_SmsGovernanceTenantAssignmentAuditEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovTenantAudit_Tenant_Dt",
                table: "ntf_SmsGovernanceTenantAssignmentAuditEvents",
                columns: new[] { "TenantId", "CreatedAt" });
            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovTenantAudit_Assignment_Dt",
                table: "ntf_SmsGovernanceTenantAssignmentAuditEvents",
                columns: new[] { "AssignmentId", "CreatedAt" });
            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovTenantAudit_Overlay_Dt",
                table: "ntf_SmsGovernanceTenantAssignmentAuditEvents",
                columns: new[] { "OverlayId", "CreatedAt" });
            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovTenantAudit_EventType_Dt",
                table: "ntf_SmsGovernanceTenantAssignmentAuditEvents",
                columns: new[] { "EventType", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ntf_SmsGovernanceTenantRulePackAssignments");
            migrationBuilder.DropTable(name: "ntf_SmsGovernanceTenantOverlays");
            migrationBuilder.DropTable(name: "ntf_SmsGovernanceTenantAssignmentAuditEvents");
        }
    }
}
