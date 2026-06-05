using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

/// <summary>
/// Seeds the seven SYNQ_LIENS task capability codes that gate every
/// action surface in the Task Manager module:
///
///   SYNQ_LIENS.task:read       — view tasks within a lien or case
///   SYNQ_LIENS.task:create     — create a new task
///   SYNQ_LIENS.task:edit:own   — update status/details of tasks assigned to or created by the caller
///   SYNQ_LIENS.task:edit:all   — update any task regardless of ownership
///   SYNQ_LIENS.task:assign     — assign a task to a user or team
///   SYNQ_LIENS.task:complete   — mark a task as completed
///   SYNQ_LIENS.task:cancel     — cancel a task
///
/// ProductRole → Capability assignments:
///   SYNQLIEN_SELLER (law firm) : read, create, edit:own, complete, cancel
///   SYNQLIEN_BUYER             : read
///   SYNQLIEN_HOLDER (servicer) : read, create, edit:own, edit:all, assign, complete, cancel
///
/// All DML uses INSERT IGNORE so the migration is idempotent and safe to
/// replay if it was interrupted before being recorded in __EFMigrationsHistory.
/// </summary>
[DbContext(typeof(IdentityDbContext))]
[Migration("20260421100001_SeedLienTaskPermissions")]
public partial class SeedLienTaskPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── Task capability catalog (IDs 0045-0051) ───────────────────────────
        migrationBuilder.Sql(@"
            INSERT IGNORE INTO `idt_Capabilities`
                (`Id`, `ProductId`, `Code`, `Name`, `Description`, `Category`, `IsActive`, `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedBy`, `UpdatedBy`)
            VALUES
                ('60000000-0000-0000-0000-000000000045', '10000000-0000-0000-0000-000000000002',
                 'SYNQ_LIENS.task:read',     'Read Tasks',     'View tasks within a lien or case',
                 'Task', 1, '2024-01-01 00:00:00.000000', NULL, NULL, NULL),
                ('60000000-0000-0000-0000-000000000046', '10000000-0000-0000-0000-000000000002',
                 'SYNQ_LIENS.task:create',   'Create Task',    'Create a new task',
                 'Task', 1, '2024-01-01 00:00:00.000000', NULL, NULL, NULL),
                ('60000000-0000-0000-0000-000000000047', '10000000-0000-0000-0000-000000000002',
                 'SYNQ_LIENS.task:edit:own', 'Edit Own Tasks', 'Update status and details of tasks assigned to or created by you',
                 'Task', 1, '2024-01-01 00:00:00.000000', NULL, NULL, NULL),
                ('60000000-0000-0000-0000-000000000048', '10000000-0000-0000-0000-000000000002',
                 'SYNQ_LIENS.task:edit:all', 'Edit All Tasks', 'Update any task regardless of ownership',
                 'Task', 1, '2024-01-01 00:00:00.000000', NULL, NULL, NULL),
                ('60000000-0000-0000-0000-000000000049', '10000000-0000-0000-0000-000000000002',
                 'SYNQ_LIENS.task:assign',   'Assign Task',    'Assign a task to a user or team',
                 'Task', 1, '2024-01-01 00:00:00.000000', NULL, NULL, NULL),
                ('60000000-0000-0000-0000-000000000050', '10000000-0000-0000-0000-000000000002',
                 'SYNQ_LIENS.task:complete', 'Complete Task',  'Mark a task as completed',
                 'Task', 1, '2024-01-01 00:00:00.000000', NULL, NULL, NULL),
                ('60000000-0000-0000-0000-000000000051', '10000000-0000-0000-0000-000000000002',
                 'SYNQ_LIENS.task:cancel',   'Cancel Task',    'Cancel a task',
                 'Task', 1, '2024-01-01 00:00:00.000000', NULL, NULL, NULL);");

        // ── ProductRole → Capability mappings (idt_RoleCapabilities) ─────────
        // SYNQLIEN_SELLER  = 50000000-...0003
        // SYNQLIEN_BUYER   = 50000000-...0004
        // SYNQLIEN_HOLDER  = 50000000-...0005
        migrationBuilder.Sql(@"
            INSERT IGNORE INTO `idt_RoleCapabilities` (`ProductRoleId`, `CapabilityId`)
            VALUES
                -- Seller: read, create, edit:own, complete, cancel
                ('50000000-0000-0000-0000-000000000003', '60000000-0000-0000-0000-000000000045'),
                ('50000000-0000-0000-0000-000000000003', '60000000-0000-0000-0000-000000000046'),
                ('50000000-0000-0000-0000-000000000003', '60000000-0000-0000-0000-000000000047'),
                ('50000000-0000-0000-0000-000000000003', '60000000-0000-0000-0000-000000000050'),
                ('50000000-0000-0000-0000-000000000003', '60000000-0000-0000-0000-000000000051'),
                -- Buyer: read only
                ('50000000-0000-0000-0000-000000000004', '60000000-0000-0000-0000-000000000045'),
                -- Holder: full task management
                ('50000000-0000-0000-0000-000000000005', '60000000-0000-0000-0000-000000000045'),
                ('50000000-0000-0000-0000-000000000005', '60000000-0000-0000-0000-000000000046'),
                ('50000000-0000-0000-0000-000000000005', '60000000-0000-0000-0000-000000000047'),
                ('50000000-0000-0000-0000-000000000005', '60000000-0000-0000-0000-000000000048'),
                ('50000000-0000-0000-0000-000000000005', '60000000-0000-0000-0000-000000000049'),
                ('50000000-0000-0000-0000-000000000005', '60000000-0000-0000-0000-000000000050'),
                ('50000000-0000-0000-0000-000000000005', '60000000-0000-0000-0000-000000000051');");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            DELETE FROM `idt_RoleCapabilities`
            WHERE `CapabilityId` IN (
                '60000000-0000-0000-0000-000000000045',
                '60000000-0000-0000-0000-000000000046',
                '60000000-0000-0000-0000-000000000047',
                '60000000-0000-0000-0000-000000000048',
                '60000000-0000-0000-0000-000000000049',
                '60000000-0000-0000-0000-000000000050',
                '60000000-0000-0000-0000-000000000051'
            );");

        migrationBuilder.Sql(@"
            DELETE FROM `idt_Capabilities`
            WHERE `Id` IN (
                '60000000-0000-0000-0000-000000000045',
                '60000000-0000-0000-0000-000000000046',
                '60000000-0000-0000-0000-000000000047',
                '60000000-0000-0000-0000-000000000048',
                '60000000-0000-0000-0000-000000000049',
                '60000000-0000-0000-0000-000000000050',
                '60000000-0000-0000-0000-000000000051'
            );");
    }
}
