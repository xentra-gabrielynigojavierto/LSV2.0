using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

/// <summary>
/// LS-ID-TNT-022-001 — Define Insights Permission Codes.
///
/// Seeds the SYNQ_INSIGHTS product and seven capability codes that gate every
/// user-visible action surface in the Insights module:
///
///   Dashboard
///     SYNQ_INSIGHTS.dashboard:view   — view the analytics dashboard
///
///   Reports
///     SYNQ_INSIGHTS.reports:view     — browse catalog + read results
///     SYNQ_INSIGHTS.reports:run      — execute a report
///     SYNQ_INSIGHTS.reports:export   — export to CSV / XLSX / PDF
///     SYNQ_INSIGHTS.reports:build    — create / customise report definitions
///
///   Schedules
///     SYNQ_INSIGHTS.schedules:manage — create / edit / activate / deactivate schedules
///     SYNQ_INSIGHTS.schedules:run    — trigger a schedule immediately
///
/// All DML uses INSERT IGNORE so the migration is idempotent and safe to
/// replay if it was interrupted before being recorded in __EFMigrationsHistory.
///
/// Default role assignments:
///   TenantAdmin   — all 7 capabilities
///   StandardUser  — dashboard:view + reports:view (read-only)
/// </summary>
[DbContext(typeof(IdentityDbContext))]
[Migration("20260419000001_AddInsightsPermissionCatalog")]
public partial class AddInsightsPermissionCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── SYNQ_INSIGHTS product ─────────────────────────────────────────────
        migrationBuilder.Sql(@"
            INSERT IGNORE INTO `idt_Products` (`Id`, `Code`, `CreatedAtUtc`, `Description`, `IsActive`, `Name`)
            VALUES ('10000000-0000-0000-0000-000000000007', 'SYNQ_INSIGHTS', '2025-01-01 00:00:00.000000',
                    'Analytics and reporting platform', 1, 'SynqInsights');");

        // ── Insights capability catalog ───────────────────────────────────────
        // IDs 0038-0044 follow the sequential decimal-in-UUID pattern used by
        // all prior capability seed blocks (0001-0037 in use).
        migrationBuilder.Sql(@"
            INSERT IGNORE INTO `idt_Capabilities`
                (`Id`, `ProductId`, `Code`, `Name`, `Description`, `Category`, `IsActive`, `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedBy`, `UpdatedBy`)
            VALUES
                ('60000000-0000-0000-0000-000000000038', '10000000-0000-0000-0000-000000000007',
                 'SYNQ_INSIGHTS.dashboard:view',    'View Insights Dashboard', 'View the Insights analytics and metrics dashboard',           'Dashboard', 1, '2025-01-01 00:00:00.000000', NULL, NULL, NULL),
                ('60000000-0000-0000-0000-000000000039', '10000000-0000-0000-0000-000000000007',
                 'SYNQ_INSIGHTS.reports:view',      'View Reports',            'Browse the report catalog and view report results',            'Reports',   1, '2025-01-01 00:00:00.000000', NULL, NULL, NULL),
                ('60000000-0000-0000-0000-000000000040', '10000000-0000-0000-0000-000000000007',
                 'SYNQ_INSIGHTS.reports:run',       'Run Reports',             'Execute a report to generate current results',                 'Reports',   1, '2025-01-01 00:00:00.000000', NULL, NULL, NULL),
                ('60000000-0000-0000-0000-000000000041', '10000000-0000-0000-0000-000000000007',
                 'SYNQ_INSIGHTS.reports:export',    'Export Reports',          'Export report results to CSV, XLSX, or PDF',                  'Reports',   1, '2025-01-01 00:00:00.000000', NULL, NULL, NULL),
                ('60000000-0000-0000-0000-000000000042', '10000000-0000-0000-0000-000000000007',
                 'SYNQ_INSIGHTS.reports:build',     'Build Reports',           'Create and customise report definitions',                     'Reports',   1, '2025-01-01 00:00:00.000000', NULL, NULL, NULL),
                ('60000000-0000-0000-0000-000000000043', '10000000-0000-0000-0000-000000000007',
                 'SYNQ_INSIGHTS.schedules:manage',  'Manage Schedules',        'Create, edit, activate, and deactivate report schedules',     'Schedules', 1, '2025-01-01 00:00:00.000000', NULL, NULL, NULL),
                ('60000000-0000-0000-0000-000000000044', '10000000-0000-0000-0000-000000000007',
                 'SYNQ_INSIGHTS.schedules:run',     'Run Schedules',           'Trigger a scheduled report to run immediately',               'Schedules', 1, '2025-01-01 00:00:00.000000', NULL, NULL, NULL);");

        // ── Default role → capability assignments ─────────────────────────────
        // TenantAdmin (0x02) receives all 7 Insights capabilities.
        // StandardUser (0x03) receives read-only access: dashboard:view + reports:view.
        migrationBuilder.Sql(@"
            INSERT IGNORE INTO `idt_RoleCapabilityAssignments` (`RoleId`, `CapabilityId`, `AssignedAtUtc`, `AssignedByUserId`)
            VALUES
                ('30000000-0000-0000-0000-000000000002', '60000000-0000-0000-0000-000000000038', '2025-01-01 00:00:00.000000', NULL),
                ('30000000-0000-0000-0000-000000000002', '60000000-0000-0000-0000-000000000039', '2025-01-01 00:00:00.000000', NULL),
                ('30000000-0000-0000-0000-000000000002', '60000000-0000-0000-0000-000000000040', '2025-01-01 00:00:00.000000', NULL),
                ('30000000-0000-0000-0000-000000000002', '60000000-0000-0000-0000-000000000041', '2025-01-01 00:00:00.000000', NULL),
                ('30000000-0000-0000-0000-000000000002', '60000000-0000-0000-0000-000000000042', '2025-01-01 00:00:00.000000', NULL),
                ('30000000-0000-0000-0000-000000000002', '60000000-0000-0000-0000-000000000043', '2025-01-01 00:00:00.000000', NULL),
                ('30000000-0000-0000-0000-000000000002', '60000000-0000-0000-0000-000000000044', '2025-01-01 00:00:00.000000', NULL),
                ('30000000-0000-0000-0000-000000000003', '60000000-0000-0000-0000-000000000038', '2025-01-01 00:00:00.000000', NULL),
                ('30000000-0000-0000-0000-000000000003', '60000000-0000-0000-0000-000000000039', '2025-01-01 00:00:00.000000', NULL);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            DELETE FROM `idt_RoleCapabilityAssignments`
            WHERE `CapabilityId` IN (
                '60000000-0000-0000-0000-000000000038',
                '60000000-0000-0000-0000-000000000039',
                '60000000-0000-0000-0000-000000000040',
                '60000000-0000-0000-0000-000000000041',
                '60000000-0000-0000-0000-000000000042',
                '60000000-0000-0000-0000-000000000043',
                '60000000-0000-0000-0000-000000000044'
            ) AND `RoleId` IN (
                '30000000-0000-0000-0000-000000000002',
                '30000000-0000-0000-0000-000000000003'
            );");

        migrationBuilder.Sql(@"
            DELETE FROM `idt_Capabilities`
            WHERE `Id` IN (
                '60000000-0000-0000-0000-000000000038',
                '60000000-0000-0000-0000-000000000039',
                '60000000-0000-0000-0000-000000000040',
                '60000000-0000-0000-0000-000000000041',
                '60000000-0000-0000-0000-000000000042',
                '60000000-0000-0000-0000-000000000043',
                '60000000-0000-0000-0000-000000000044'
            );");

        migrationBuilder.Sql(@"
            DELETE FROM `idt_Products`
            WHERE `Id` = '10000000-0000-0000-0000-000000000007';");
    }
}
