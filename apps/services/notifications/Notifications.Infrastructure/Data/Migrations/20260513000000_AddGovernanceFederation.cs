using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGovernanceFederation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // -----------------------------------------------------------------
            // ntf_GovernanceChannelScopes
            // -----------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "ntf_GovernanceChannelScopes",
                columns: table => new
                {
                    Id          = table.Column<string>(type: "char(36)", nullable: false),
                    ChannelType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    ScopeMode   = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Enabled     = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Priority    = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    CreatedAt   = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt   = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy   = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    UpdatedBy   = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                },
                constraints: table => { table.PrimaryKey("PK_ntf_GovernanceChannelScopes", x => x.Id); });

            migrationBuilder.CreateIndex("IX_ntf_GovChannelScope_Channel_Enabled",
                "ntf_GovernanceChannelScopes", new[] { "ChannelType", "Enabled" });
            migrationBuilder.CreateIndex("IX_ntf_GovChannelScope_Mode_Enabled",
                "ntf_GovernanceChannelScopes", new[] { "ScopeMode", "Enabled" });
            migrationBuilder.CreateIndex("IX_ntf_GovChannelScope_Priority",
                "ntf_GovernanceChannelScopes", "Priority");

            // -----------------------------------------------------------------
            // ntf_GovernanceFederatedRulePacks
            // -----------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "ntf_GovernanceFederatedRulePacks",
                columns: table => new
                {
                    Id              = table.Column<string>(type: "char(36)", nullable: false),
                    RulePackId      = table.Column<string>(type: "char(36)", nullable: false),
                    ChannelType     = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    FederationGroup = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    TenantId        = table.Column<string>(type: "char(36)", nullable: true),
                    Enabled         = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Priority        = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom   = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    EffectiveTo     = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt       = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt       = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy       = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    UpdatedBy       = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                },
                constraints: table => { table.PrimaryKey("PK_ntf_GovernanceFederatedRulePacks", x => x.Id); });

            migrationBuilder.CreateIndex("IX_ntf_GovFedPack_Channel_Enabled_Priority",
                "ntf_GovernanceFederatedRulePacks", new[] { "ChannelType", "Enabled", "Priority" });
            migrationBuilder.CreateIndex("IX_ntf_GovFedPack_Pack_Channel",
                "ntf_GovernanceFederatedRulePacks", new[] { "RulePackId", "ChannelType" });
            migrationBuilder.CreateIndex("IX_ntf_GovFedPack_Tenant_Channel_Enabled",
                "ntf_GovernanceFederatedRulePacks", new[] { "TenantId", "ChannelType", "Enabled" });
            migrationBuilder.CreateIndex("IX_ntf_GovFedPack_FedGroup_Enabled",
                "ntf_GovernanceFederatedRulePacks", new[] { "FederationGroup", "Enabled" });
            migrationBuilder.CreateIndex("IX_ntf_GovFedPack_EffectiveWindow",
                "ntf_GovernanceFederatedRulePacks", new[] { "EffectiveFrom", "EffectiveTo" });

            // -----------------------------------------------------------------
            // ntf_GovernanceFederationOverlays
            // -----------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "ntf_GovernanceFederationOverlays",
                columns: table => new
                {
                    Id           = table.Column<string>(type: "char(36)", nullable: false),
                    TenantId     = table.Column<string>(type: "char(36)", nullable: true),
                    ChannelType  = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    RulePackId   = table.Column<string>(type: "char(36)", nullable: true),
                    RuleId       = table.Column<string>(type: "char(36)", nullable: true),
                    OverlayType  = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    OverlayState = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    OverlayJson  = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true),
                    Priority     = table.Column<int>(type: "int", nullable: false),
                    Enabled      = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    EffectiveTo   = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt    = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt    = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy    = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    UpdatedBy    = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                },
                constraints: table => { table.PrimaryKey("PK_ntf_GovernanceFederationOverlays", x => x.Id); });

            migrationBuilder.CreateIndex("IX_ntf_GovFedOverlay_Channel_Enabled_Priority",
                "ntf_GovernanceFederationOverlays", new[] { "ChannelType", "Enabled", "Priority" });
            migrationBuilder.CreateIndex("IX_ntf_GovFedOverlay_Tenant_Channel_Enabled",
                "ntf_GovernanceFederationOverlays", new[] { "TenantId", "ChannelType", "Enabled" });
            migrationBuilder.CreateIndex("IX_ntf_GovFedOverlay_Pack_Channel",
                "ntf_GovernanceFederationOverlays", new[] { "RulePackId", "ChannelType" });
            migrationBuilder.CreateIndex("IX_ntf_GovFedOverlay_Rule_Channel",
                "ntf_GovernanceFederationOverlays", new[] { "RuleId", "ChannelType" });
            migrationBuilder.CreateIndex("IX_ntf_GovFedOverlay_Type_Enabled",
                "ntf_GovernanceFederationOverlays", new[] { "OverlayType", "Enabled" });

            // -----------------------------------------------------------------
            // ntf_GovernanceFederationAuditEvents
            // -----------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "ntf_GovernanceFederationAuditEvents",
                columns: table => new
                {
                    Id              = table.Column<string>(type: "char(36)", nullable: false),
                    TenantId        = table.Column<string>(type: "char(36)", nullable: true),
                    ChannelType     = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    FederationGroup = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    EntityType      = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    EntityId        = table.Column<string>(type: "char(36)", nullable: true),
                    EventType       = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    PreviousState   = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    NewState        = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Actor           = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    Reason          = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    MetadataJson    = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt       = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table => { table.PrimaryKey("PK_ntf_GovernanceFederationAuditEvents", x => x.Id); });

            migrationBuilder.CreateIndex("IX_ntf_GovFedAudit_Tenant_Date",
                "ntf_GovernanceFederationAuditEvents", new[] { "TenantId", "CreatedAt" });
            migrationBuilder.CreateIndex("IX_ntf_GovFedAudit_Channel_Date",
                "ntf_GovernanceFederationAuditEvents", new[] { "ChannelType", "CreatedAt" });
            migrationBuilder.CreateIndex("IX_ntf_GovFedAudit_EventType_Date",
                "ntf_GovernanceFederationAuditEvents", new[] { "EventType", "CreatedAt" });
            migrationBuilder.CreateIndex("IX_ntf_GovFedAudit_Entity",
                "ntf_GovernanceFederationAuditEvents", new[] { "EntityType", "EntityId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("ntf_GovernanceFederationAuditEvents");
            migrationBuilder.DropTable("ntf_GovernanceFederationOverlays");
            migrationBuilder.DropTable("ntf_GovernanceFederatedRulePacks");
            migrationBuilder.DropTable("ntf_GovernanceChannelScopes");
        }
    }
}
