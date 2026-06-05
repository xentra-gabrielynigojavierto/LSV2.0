using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedAuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 28 representative audit log entries matching the Control Center mock data.
            // IDs use the 80000000-... prefix to avoid collisions with other seed ranges.
            // Raw SQL is used here because the lightweight designer file does not carry the
            // full model snapshot, so EF's InsertData cannot resolve column types.
            // In C# verbatim strings (@"") double-quote is escaped as "".
            migrationBuilder.Sql(@"
INSERT INTO `AuditLogs` (`Id`,`ActorName`,`ActorType`,`Action`,`EntityType`,`EntityId`,`MetadataJson`,`CreatedAtUtc`) VALUES
('80000000-0000-0000-0000-000000000001','admin@legalsynq.com','Admin','user.invite','User','o.chen@hartwell.law','{""tenantCode"":""HARTWELL"",""role"":""CaseManager""}','2025-03-10 11:05:00'),
('80000000-0000-0000-0000-000000000002','admin@legalsynq.com','Admin','user.deactivate','User','a.diallo@meridiancare.com','{""tenantCode"":""MERIDIAN"",""reason"":""extended-leave""}','2024-12-10 10:00:00'),
('80000000-0000-0000-0000-000000000003','admin@legalsynq.com','Admin','user.lock','User','tanya@bluehavenrecovery.org','{""tenantCode"":""BLUEHAVEN"",""reason"":""policy-violation""}','2024-09-15 08:30:00'),
('80000000-0000-0000-0000-000000000004','n.patel@legalsynq.com','Admin','user.invite','User','s.kirk@thornfieldlaw.com','{""tenantCode"":""THORNFIELD"",""role"":""CaseManager""}','2025-02-20 14:05:00'),
('80000000-0000-0000-0000-000000000005','admin@legalsynq.com','Admin','user.lock','User','p.langford@graystonegov.org','{""tenantCode"":""GRAYSTONE"",""reason"":""account-suspended""}','2024-10-01 12:10:00'),
('80000000-0000-0000-0000-000000000006','n.patel@legalsynq.com','Admin','user.invite','User','y.tanaka@nexushealth.net','{""tenantCode"":""NEXUSHEALTH"",""role"":""ReadOnly""}','2025-03-15 11:05:00'),
('80000000-0000-0000-0000-000000000007','admin@legalsynq.com','Admin','user.unlock','User','j.whitmore@hartwell.law','{""tenantCode"":""HARTWELL""}','2025-01-15 14:05:00'),
('80000000-0000-0000-0000-000000000008','admin@legalsynq.com','Admin','user.password_reset','User','r.moss@pinnaclelegal.com','{""tenantCode"":""PINNACLE"",""method"":""email-link""}','2025-03-05 08:15:00'),
('80000000-0000-0000-0000-000000000009','admin@legalsynq.com','Admin','tenant.create','Tenant','HARTWELL','{""tenantType"":""LawFirm""}','2024-02-15 08:30:00'),
('80000000-0000-0000-0000-000000000010','admin@legalsynq.com','Admin','tenant.create','Tenant','NEXUSHEALTH','{""tenantType"":""Provider""}','2024-06-18 08:45:00'),
('80000000-0000-0000-0000-000000000011','admin@legalsynq.com','Admin','tenant.suspend','Tenant','GRAYSTONE','{""previousStatus"":""Active"",""reason"":""non-payment""}','2024-10-01 12:00:00'),
('80000000-0000-0000-0000-000000000012','admin@legalsynq.com','Admin','tenant.deactivate','Tenant','BLUEHAVEN','{""previousStatus"":""Active""}','2024-09-01 09:00:00'),
('80000000-0000-0000-0000-000000000013','n.patel@legalsynq.com','Admin','tenant.create','Tenant','THORNFIELD','{""tenantType"":""LawFirm""}','2024-06-05 11:30:00'),
('80000000-0000-0000-0000-000000000014','n.patel@legalsynq.com','Admin','tenant.update','Tenant','MERIDIAN','{""field"":""primaryContactEmail"",""previous"":""old@meridiancare.com"",""next"":""ops@meridiancare.com""}','2025-01-05 14:30:00'),
('80000000-0000-0000-0000-000000000015','admin@legalsynq.com','Admin','entitlement.enable','Entitlement','HARTWELL:SynqFund','{""tenantCode"":""HARTWELL"",""product"":""SynqFund""}','2024-02-16 09:00:00'),
('80000000-0000-0000-0000-000000000016','admin@legalsynq.com','Admin','entitlement.enable','Entitlement','MERIDIAN:CareConnect','{""tenantCode"":""MERIDIAN"",""product"":""CareConnect""}','2024-03-02 10:15:00'),
('80000000-0000-0000-0000-000000000017','n.patel@legalsynq.com','Admin','entitlement.disable','Entitlement','BLUEHAVEN:CareConnect','{""tenantCode"":""BLUEHAVEN"",""product"":""CareConnect"",""reason"":""subscription-lapsed""}','2024-09-02 10:00:00'),
('80000000-0000-0000-0000-000000000018','admin@legalsynq.com','Admin','entitlement.enable','Entitlement','THORNFIELD:SynqLien','{""tenantCode"":""THORNFIELD"",""product"":""SynqLien""}','2024-06-06 08:00:00'),
('80000000-0000-0000-0000-000000000019','n.patel@legalsynq.com','Admin','entitlement.enable','Entitlement','NEXUSHEALTH:SynqRx','{""tenantCode"":""NEXUSHEALTH"",""product"":""SynqRx""}','2024-07-01 11:00:00'),
('80000000-0000-0000-0000-000000000020','admin@legalsynq.com','Admin','entitlement.disable','Entitlement','GRAYSTONE:SynqBill','{""tenantCode"":""GRAYSTONE"",""product"":""SynqBill"",""reason"":""account-suspended""}','2024-10-02 08:00:00'),
('80000000-0000-0000-0000-000000000021','admin@legalsynq.com','Admin','role.assign','Role','PlatformAdmin','{""assignedTo"":""n.patel@legalsynq.com""}','2024-01-05 08:10:00'),
('80000000-0000-0000-0000-000000000022','admin@legalsynq.com','Admin','role.assign','Role','SupportAdmin','{""assignedTo"":""support@legalsynq.com""}','2024-03-15 10:00:00'),
('80000000-0000-0000-0000-000000000023','admin@legalsynq.com','Admin','role.revoke','Role','ReadOnly','{""revokedFrom"":""temp@legalsynq.com"",""reason"":""contract-ended""}','2024-11-30 17:00:00'),
('80000000-0000-0000-0000-000000000024','identity-service','System','system.migration','System','identity-db','{""migration"":""20260328200000_AddMultiOrgProductRoleModel"",""result"":""applied""}','2026-03-28 20:00:10'),
('80000000-0000-0000-0000-000000000025','identity-service','System','system.health_check','System','identity-service','{""status"":""healthy"",""durationMs"":12}','2025-03-29 06:00:00'),
('80000000-0000-0000-0000-000000000026','identity-service','System','user.session_expired','User','p.langford@graystonegov.org','{""tenantCode"":""GRAYSTONE"",""reason"":""jwt-ttl""}','2024-09-20 18:00:00'),
('80000000-0000-0000-0000-000000000027','admin@legalsynq.com','Admin','tenant.activate','Tenant','PINNACLE','{""previousStatus"":""Inactive""}','2024-04-10 14:30:00'),
('80000000-0000-0000-0000-000000000028','n.patel@legalsynq.com','Admin','user.deactivate','User','h.bates@graystonegov.org','{""tenantCode"":""GRAYSTONE"",""reason"":""account-suspended""}','2024-09-30 10:05:00');
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DELETE FROM `AuditLogs` WHERE `Id` LIKE '80000000-0000-0000-0000-%';"
            );
        }
    }
}
