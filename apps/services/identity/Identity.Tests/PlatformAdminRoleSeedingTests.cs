using BuildingBlocks.TestHelpers;
using Identity.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace Identity.Tests;

/// <summary>
/// Integration tests for LS-ID-SUP-002 (Task #203).
///
/// Verifies that Identity.Api startup — the actual Program.cs path that runs
/// migrations and the SUP-002 guard — leaves every active platform-tenant user
/// with exactly one active GLOBAL ScopedRoleAssignment carrying the PlatformAdmin
/// roleId after a fresh deployment.
///
/// Scenario simulated:
///   1. EF migrations are applied to a clean MySQL database, creating the full
///      schema and recording all migrations in __EFMigrationsHistory.
///   2. Active platform-tenant users are seeded AFTER migrations run, so the
///      backfill SQL in the 20260426 migrations did not cover them.
///   3. The three 20260426 history rows are stripped to simulate the deployment
///      state where the guard has not yet executed.
///   4. WebApplicationFactory&lt;Program&gt; is started against this database,
///      exercising the real Program.cs startup sequence (SUP-002 guard → EF Migrate).
///   5. Assertions confirm each platform-tenant user has exactly one active GLOBAL
///      ScopedRoleAssignment with RoleId == PlatformAdmin.
///
/// CI: runs under the "Identity Integration Tests" workflow on every PR.
/// Filter: --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public sealed class PlatformAdminRoleSeedingTests : IAsyncLifetime
{
    private const string PlatformTenantId  = "20000000-0000-0000-0000-000000000001";
    private const string RolePlatformAdmin = "30000000-0000-0000-0000-000000000001";

    private static readonly MySqlServerVersion ServerVersion = new(new Version(8, 0, 0));

    private readonly MySqlTestContainer _container = new();
    private string _cs = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _cs = await _container.CreateDatabaseAsync("identity_sup002_test");
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    private IdentityDbContext BuildDbContext() =>
        new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseMySql(_cs, ServerVersion)
            .Options);

    // ── Tests ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Given active platform-tenant users who exist before the SUP-002 guard
    /// migrations are recorded, after Identity.Api startup each user must have
    /// exactly one active GLOBAL ScopedRoleAssignment with the PlatformAdmin roleId.
    /// </summary>
    [Fact]
    public async Task Startup_AssignsPlatformAdminRole_ToAllActivePlatformTenantUsers()
    {
        // ── Step 1: Apply all EF migrations to a clean database ───────────────
        await using (var db = BuildDbContext())
            await db.Database.MigrateAsync();

        // ── Step 2: Seed active platform-tenant users ─────────────────────────
        // These users are created AFTER migrations ran, so the backfill SQL inside
        // the 20260426 migrations did not cover them.
        var platformUserId1 = Guid.NewGuid();
        var platformUserId2 = Guid.NewGuid();

        await using (var conn = new MySqlConnection(_cs))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();

            cmd.CommandText = $@"
                INSERT INTO `idt_Users`
                    (`Id`, `TenantId`, `Email`, `PasswordHash`,
                     `FirstName`, `LastName`, `IsActive`,
                     `CreatedAtUtc`, `UpdatedAtUtc`)
                VALUES
                    ('{platformUserId1}', '{PlatformTenantId}',
                     'platform1@sup002.test', 'hash',
                     'Platform', 'UserOne', 1,
                     '2024-01-01 00:00:00', '2024-01-01 00:00:00'),
                    ('{platformUserId2}', '{PlatformTenantId}',
                     'platform2@sup002.test', 'hash',
                     'Platform', 'UserTwo', 1,
                     '2024-01-01 00:00:00', '2024-01-01 00:00:00');";
            await cmd.ExecuteNonQueryAsync();

            // ── Step 3: Strip the three 20260426 migration history rows ────────
            // Simulates a deployment where the SUP-002 guard has not yet run.
            cmd.CommandText = @"
                DELETE FROM `__EFMigrationsHistory`
                WHERE `MigrationId` IN (
                    '20260426000001_SeedSupportRoles',
                    '20260426000002_FixSupportRolesBackfill',
                    '20260426000003_CorrectPlatformAdminRole'
                );";
            await cmd.ExecuteNonQueryAsync();

            // Ensure no pre-existing SRAs for our test users.
            cmd.CommandText = $@"
                DELETE FROM `idt_ScopedRoleAssignments`
                WHERE `UserId` IN ('{platformUserId1}', '{platformUserId2}');";
            await cmd.ExecuteNonQueryAsync();
        }

        // ── Step 4: Run Identity.Api startup via WebApplicationFactory ─────────
        // This exercises the real Program.cs path:
        //   SUP-002 guard (finds 20260426 entries missing → applies backfill SQL)
        //   → EF Migrate() (finds those entries now recorded → no-op)
        using var factory = BuildFactory(_cs);
        using var _ = factory.CreateClient();   // triggers host startup

        // ── Step 5: Assert ─────────────────────────────────────────────────────
        await using (var conn = new MySqlConnection(_cs))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();

            // 5a. The three 20260426 migration IDs must be re-recorded in
            //     __EFMigrationsHistory — that is part of LS-ID-SUP-002's contract.
            foreach (var migId in new[]
            {
                "20260426000001_SeedSupportRoles",
                "20260426000002_FixSupportRolesBackfill",
                "20260426000003_CorrectPlatformAdminRole",
            })
            {
                cmd.CommandText = $@"
                    SELECT COUNT(*) FROM `__EFMigrationsHistory`
                    WHERE `MigrationId` = '{migId}';";
                var recorded = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                Assert.True(recorded == 1,
                    $"Migration '{migId}' should be recorded in __EFMigrationsHistory " +
                    $"after SUP-002 guard runs, but found {recorded} row(s).");
            }

            // 5b. Query ALL active platform-tenant users from the database — not just
            //     the two we seeded — so that any future seeded user is also covered.
            cmd.CommandText = $@"
                SELECT `Id` FROM `idt_Users`
                WHERE  `TenantId` = '{PlatformTenantId}'
                  AND  `IsActive` = 1;";
            var allPlatformUserIds = new List<string>();
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    allPlatformUserIds.Add(reader.GetString(0));
            }

            Assert.NotEmpty(allPlatformUserIds);

            foreach (var uid in allPlatformUserIds)
            {
                // Each user must have EXACTLY ONE active GLOBAL SRA in total.
                cmd.CommandText = $@"
                    SELECT COUNT(*) FROM `idt_ScopedRoleAssignments`
                    WHERE  `UserId`    = '{uid}'
                      AND  `ScopeType` = 'GLOBAL'
                      AND  `IsActive`  = 1;";
                var total = Convert.ToInt64(await cmd.ExecuteScalarAsync());

                Assert.True(total == 1,
                    $"Platform-tenant user {uid} should have exactly 1 active GLOBAL " +
                    $"ScopedRoleAssignment after SUP-002 guard runs, but has {total}.");

                // And that single SRA must carry the PlatformAdmin roleId.
                cmd.CommandText = $@"
                    SELECT COUNT(*) FROM `idt_ScopedRoleAssignments`
                    WHERE  `UserId`    = '{uid}'
                      AND  `ScopeType` = 'GLOBAL'
                      AND  `IsActive`  = 1
                      AND  `RoleId`   = '{RolePlatformAdmin}';";
                var platformAdminCount = Convert.ToInt64(await cmd.ExecuteScalarAsync());

                Assert.True(platformAdminCount == 1,
                    $"Platform-tenant user {uid}: the single active GLOBAL SRA must have " +
                    $"RoleId={RolePlatformAdmin} (PlatformAdmin), but found {platformAdminCount} " +
                    $"with that roleId (total active GLOBAL SRAs: {total}).");
            }
        }
    }

    /// <summary>
    /// Given one active and one inactive platform-tenant user, after the SUP-002
    /// guard runs the inactive user must have zero GLOBAL ScopedRoleAssignments
    /// while the active user must have exactly one with the PlatformAdmin roleId.
    ///
    /// Regression guard: a WHERE clause regression that drops the IsActive = 1
    /// filter would silently grant PlatformAdmin to suspended accounts.
    /// </summary>
    [Fact]
    public async Task Startup_DoesNotAssignPlatformAdminRole_ToInactivePlatformTenantUsers()
    {
        // ── Step 1: Apply all EF migrations to a clean database ───────────────
        await using (var db = BuildDbContext())
            await db.Database.MigrateAsync();

        // ── Step 2: Seed one active and one inactive platform-tenant user ──────
        var activeUserId   = Guid.NewGuid();
        var inactiveUserId = Guid.NewGuid();

        await using (var conn = new MySqlConnection(_cs))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();

            cmd.CommandText = $@"
                INSERT INTO `idt_Users`
                    (`Id`, `TenantId`, `Email`, `PasswordHash`,
                     `FirstName`, `LastName`, `IsActive`,
                     `CreatedAtUtc`, `UpdatedAtUtc`)
                VALUES
                    ('{activeUserId}', '{PlatformTenantId}',
                     'active@sup002inactive.test', 'hash',
                     'Active', 'PlatformUser', 1,
                     '2024-01-01 00:00:00', '2024-01-01 00:00:00'),
                    ('{inactiveUserId}', '{PlatformTenantId}',
                     'inactive@sup002inactive.test', 'hash',
                     'Inactive', 'PlatformUser', 0,
                     '2024-01-01 00:00:00', '2024-01-01 00:00:00');";
            await cmd.ExecuteNonQueryAsync();

            // ── Step 3: Strip the three 20260426 migration history rows ────────
            cmd.CommandText = @"
                DELETE FROM `__EFMigrationsHistory`
                WHERE `MigrationId` IN (
                    '20260426000001_SeedSupportRoles',
                    '20260426000002_FixSupportRolesBackfill',
                    '20260426000003_CorrectPlatformAdminRole'
                );";
            await cmd.ExecuteNonQueryAsync();

            // Ensure no pre-existing SRAs for our test users.
            cmd.CommandText = $@"
                DELETE FROM `idt_ScopedRoleAssignments`
                WHERE `UserId` IN ('{activeUserId}', '{inactiveUserId}');";
            await cmd.ExecuteNonQueryAsync();
        }

        // ── Step 4: Run Identity.Api startup via WebApplicationFactory ─────────
        using var factory = BuildFactory(_cs);
        using var _ = factory.CreateClient();   // triggers host startup

        // ── Step 5: Assert ─────────────────────────────────────────────────────
        await using (var conn = new MySqlConnection(_cs))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();

            // 5a. The inactive user must have zero GLOBAL ScopedRoleAssignments —
            //     checked both narrowly (active-only) and broadly (any status) so a
            //     regression that inserts an inactive row is also caught.
            cmd.CommandText = $@"
                SELECT COUNT(*) FROM `idt_ScopedRoleAssignments`
                WHERE  `UserId`    = '{inactiveUserId}'
                  AND  `ScopeType` = 'GLOBAL'
                  AND  `IsActive`  = 1;";
            var inactiveActiveCount = Convert.ToInt64(await cmd.ExecuteScalarAsync());

            Assert.True(inactiveActiveCount == 0,
                $"Inactive platform-tenant user {inactiveUserId} should have zero active " +
                $"GLOBAL ScopedRoleAssignments after the SUP-002 guard runs, but has {inactiveActiveCount}.");

            cmd.CommandText = $@"
                SELECT COUNT(*) FROM `idt_ScopedRoleAssignments`
                WHERE  `UserId`    = '{inactiveUserId}'
                  AND  `ScopeType` = 'GLOBAL';";
            var inactiveTotalCount = Convert.ToInt64(await cmd.ExecuteScalarAsync());

            Assert.True(inactiveTotalCount == 0,
                $"Inactive platform-tenant user {inactiveUserId} should have zero GLOBAL " +
                $"ScopedRoleAssignments of any status after the SUP-002 guard runs, but has {inactiveTotalCount}.");

            // 5b. The active user must have exactly one active GLOBAL SRA …
            cmd.CommandText = $@"
                SELECT COUNT(*) FROM `idt_ScopedRoleAssignments`
                WHERE  `UserId`    = '{activeUserId}'
                  AND  `ScopeType` = 'GLOBAL'
                  AND  `IsActive`  = 1;";
            var activeTotal = Convert.ToInt64(await cmd.ExecuteScalarAsync());

            Assert.True(activeTotal == 1,
                $"Active platform-tenant user {activeUserId} should have exactly 1 active " +
                $"GLOBAL ScopedRoleAssignment after the SUP-002 guard runs, but has {activeTotal}.");

            // … and that SRA must carry the PlatformAdmin roleId.
            cmd.CommandText = $@"
                SELECT COUNT(*) FROM `idt_ScopedRoleAssignments`
                WHERE  `UserId`    = '{activeUserId}'
                  AND  `ScopeType` = 'GLOBAL'
                  AND  `IsActive`  = 1
                  AND  `RoleId`   = '{RolePlatformAdmin}';";
            var activePlatformAdminCount = Convert.ToInt64(await cmd.ExecuteScalarAsync());

            Assert.True(activePlatformAdminCount == 1,
                $"Active platform-tenant user {activeUserId}: the single active GLOBAL SRA " +
                $"must have RoleId={RolePlatformAdmin} (PlatformAdmin), but found " +
                $"{activePlatformAdminCount} with that roleId.");
        }
    }

    /// <summary>
    /// Idempotency guard: running the SUP-002 guard a second time must not produce a
    /// second ScopedRoleAssignment for any active platform-tenant user.
    ///
    /// Scenario:
    ///   1. Migrations are applied once to a clean database and active users are seeded.
    ///   2. The three 20260426 history rows are deleted and Identity.Api is started
    ///      (first boot) — the guard assigns exactly one GLOBAL SRA per user.
    ///   3. The three rows are deleted again and Identity.Api is started a second time
    ///      (second boot) — the NOT EXISTS sub-select must prevent any new inserts.
    ///   4. Each user must still have exactly one active GLOBAL SRA with the
    ///      PlatformAdmin roleId.
    ///
    /// A regression in the NOT EXISTS clause would result in count == 2 and the
    /// assertion would fail, catching the duplicate-assignment bug before it ships.
    /// </summary>
    [Fact]
    public async Task Startup_DoesNotDuplicatePlatformAdminRole_WhenRunTwice()
    {
        // ── Step 1: Apply all EF migrations to a clean database ───────────────
        await using (var db = BuildDbContext())
            await db.Database.MigrateAsync();

        // ── Step 2: Seed active platform-tenant users ─────────────────────────
        var platformUserId1 = Guid.NewGuid();
        var platformUserId2 = Guid.NewGuid();

        await using (var conn = new MySqlConnection(_cs))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();

            cmd.CommandText = $@"
                INSERT INTO `idt_Users`
                    (`Id`, `TenantId`, `Email`, `PasswordHash`,
                     `FirstName`, `LastName`, `IsActive`,
                     `CreatedAtUtc`, `UpdatedAtUtc`)
                VALUES
                    ('{platformUserId1}', '{PlatformTenantId}',
                     'idempotent1@sup002.test', 'hash',
                     'Idempotent', 'UserOne', 1,
                     '2024-01-01 00:00:00', '2024-01-01 00:00:00'),
                    ('{platformUserId2}', '{PlatformTenantId}',
                     'idempotent2@sup002.test', 'hash',
                     'Idempotent', 'UserTwo', 1,
                     '2024-01-01 00:00:00', '2024-01-01 00:00:00');";
            await cmd.ExecuteNonQueryAsync();

            // Ensure no pre-existing SRAs so the first boot starts from a clean slate.
            cmd.CommandText = $@"
                DELETE FROM `idt_ScopedRoleAssignments`
                WHERE `UserId` IN ('{platformUserId1}', '{platformUserId2}');";
            await cmd.ExecuteNonQueryAsync();
        }

        // ── Step 3: First boot — guard runs and assigns SRAs ──────────────────
        // Strip migration history rows so the guard believes it hasn't run yet.
        await StripSup002MigrationHistoryAsync();

        using (var factory1 = BuildFactory(_cs))
        using (var _1 = factory1.CreateClient()) { /* triggers host startup */ }

        // ── Step 4: Second boot — guard must be a no-op ───────────────────────
        // Strip the same rows again to simulate a replay (e.g. a failed restart).
        await StripSup002MigrationHistoryAsync();

        using (var factory2 = BuildFactory(_cs))
        using (var _2 = factory2.CreateClient()) { /* triggers host startup */ }

        // ── Step 5: Assert — exactly ONE SRA per user, not two ────────────────
        await using (var conn = new MySqlConnection(_cs))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();

            // Collect all active platform-tenant users (not just the two we seeded).
            cmd.CommandText = $@"
                SELECT `Id` FROM `idt_Users`
                WHERE  `TenantId` = '{PlatformTenantId}'
                  AND  `IsActive` = 1;";
            var allPlatformUserIds = new List<string>();
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    allPlatformUserIds.Add(reader.GetString(0));
            }

            Assert.NotEmpty(allPlatformUserIds);

            foreach (var uid in allPlatformUserIds)
            {
                // After two guard runs each user must still have EXACTLY ONE active
                // GLOBAL SRA — a count of 2 would indicate a duplicate assignment.
                cmd.CommandText = $@"
                    SELECT COUNT(*) FROM `idt_ScopedRoleAssignments`
                    WHERE  `UserId`    = '{uid}'
                      AND  `ScopeType` = 'GLOBAL'
                      AND  `IsActive`  = 1;";
                var total = Convert.ToInt64(await cmd.ExecuteScalarAsync());

                Assert.True(total == 1,
                    $"Platform-tenant user {uid} should have exactly 1 active GLOBAL " +
                    $"ScopedRoleAssignment after two guard runs, but has {total}. " +
                    $"This indicates the NOT EXISTS guard is not preventing duplicate inserts.");

                // That single SRA must still carry the PlatformAdmin roleId.
                cmd.CommandText = $@"
                    SELECT COUNT(*) FROM `idt_ScopedRoleAssignments`
                    WHERE  `UserId`    = '{uid}'
                      AND  `ScopeType` = 'GLOBAL'
                      AND  `IsActive`  = 1
                      AND  `RoleId`   = '{RolePlatformAdmin}';";
                var platformAdminCount = Convert.ToInt64(await cmd.ExecuteScalarAsync());

                Assert.True(platformAdminCount == 1,
                    $"Platform-tenant user {uid}: the single active GLOBAL SRA must have " +
                    $"RoleId={RolePlatformAdmin} (PlatformAdmin) after two guard runs, " +
                    $"but found {platformAdminCount} with that roleId (total active GLOBAL SRAs: {total}).");
            }
        }
    }

    /// <summary>
    /// Pre-existing manually-assigned role guard: if an active platform-tenant user
    /// already has an active GLOBAL ScopedRoleAssignment that was hand-assigned by an
    /// admin (not by the SUP-002 guard), the guard must skip that user entirely and
    /// must NOT insert a second SRA alongside the pre-existing one.
    ///
    /// This covers the NOT EXISTS sub-select in all three 20260426 migrations. If any
    /// of them ignored an existing SRA, the user would end up with two active GLOBAL
    /// SRAs and the assertion would fail.
    /// </summary>
    [Fact]
    public async Task Startup_DoesNotAddSecondSRA_WhenUserAlreadyHasManuallyAssignedRole()
    {
        // ── Step 1: Apply all EF migrations to a clean database ───────────────
        await using (var db = BuildDbContext())
            await db.Database.MigrateAsync();

        // ── Step 2: Seed an active platform-tenant user ───────────────────────
        var platformUserId = Guid.NewGuid();

        await using (var conn = new MySqlConnection(_cs))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();

            cmd.CommandText = $@"
                INSERT INTO `idt_Users`
                    (`Id`, `TenantId`, `Email`, `PasswordHash`,
                     `FirstName`, `LastName`, `IsActive`,
                     `CreatedAtUtc`, `UpdatedAtUtc`)
                VALUES
                    ('{platformUserId}', '{PlatformTenantId}',
                     'manual-role@sup002.test', 'hash',
                     'Manual', 'RoleUser', 1,
                     '2024-01-01 00:00:00', '2024-01-01 00:00:00');";
            await cmd.ExecuteNonQueryAsync();

            // ── Step 3: Manually insert a pre-existing active GLOBAL SRA ──────
            // Simulates a role that was hand-assigned by an admin before this
            // deployment. We use RoleSupportAdmin (any non-guard roleId works) to
            // make clear this was not written by the SUP-002 guard.
            //
            // Note: AssignedByUserId is set to a non-NULL sentinel UUID to signal
            // that a human admin performed this assignment, distinguishing it from
            // guard-written rows (where AssignedByUserId IS NULL).
            const string RoleSupportAdmin         = "30000000-0000-0000-0000-000000000011";
            const string SentinelAdminId          = "00000000-0000-0000-0000-000000000001";
            var          manualSraId              = Guid.NewGuid();

            cmd.CommandText = $@"
                INSERT INTO `idt_ScopedRoleAssignments`
                    (`Id`, `UserId`, `RoleId`, `ScopeType`, `TenantId`,
                     `OrganizationId`, `OrganizationRelationshipId`, `ProductId`,
                     `IsActive`, `AssignedAtUtc`, `UpdatedAtUtc`, `AssignedByUserId`)
                VALUES
                    ('{manualSraId}', '{platformUserId}', '{RoleSupportAdmin}',
                     'GLOBAL', '{PlatformTenantId}',
                     NULL, NULL, NULL,
                     1, '2024-01-01 00:00:00', '2024-01-01 00:00:00', '{SentinelAdminId}');";
            await cmd.ExecuteNonQueryAsync();

            // ── Step 4: Strip the three 20260426 migration history rows ────────
            // Simulates a deployment where the SUP-002 guard has not yet run,
            // forcing the guard to evaluate this user on the next startup.
            cmd.CommandText = @"
                DELETE FROM `__EFMigrationsHistory`
                WHERE `MigrationId` IN (
                    '20260426000001_SeedSupportRoles',
                    '20260426000002_FixSupportRolesBackfill',
                    '20260426000003_CorrectPlatformAdminRole'
                );";
            await cmd.ExecuteNonQueryAsync();
        }

        // ── Step 5: Run Identity.Api startup via WebApplicationFactory ─────────
        // The NOT EXISTS sub-select in each migration's INSERT statement must find
        // the pre-existing SRA and skip this user entirely.
        using var factory = BuildFactory(_cs);
        using var _ = factory.CreateClient();   // triggers host startup

        // ── Step 6: Assert ─────────────────────────────────────────────────────
        await using (var conn = new MySqlConnection(_cs))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();

            // The user must have EXACTLY ONE active GLOBAL SRA — the manually-assigned
            // one. A count of 2 means the guard inserted a second SRA despite the
            // pre-existing entry, which is the regression this test catches.
            cmd.CommandText = $@"
                SELECT COUNT(*) FROM `idt_ScopedRoleAssignments`
                WHERE  `UserId`    = '{platformUserId}'
                  AND  `ScopeType` = 'GLOBAL'
                  AND  `IsActive`  = 1;";
            var total = Convert.ToInt64(await cmd.ExecuteScalarAsync());

            Assert.True(total == 1,
                $"Platform-tenant user {platformUserId} already had a manually-assigned " +
                $"active GLOBAL SRA before the SUP-002 guard ran. After startup the user " +
                $"should still have exactly 1 active GLOBAL SRA, but has {total}. " +
                $"The NOT EXISTS guard is not skipping users who already have a role.");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Removes the three SUP-002 migration history rows from __EFMigrationsHistory,
    /// simulating a deployment state where the guard has not yet executed (or a
    /// crashed restart that will replay the guard from the top).
    /// </summary>
    private async Task StripSup002MigrationHistoryAsync()
    {
        await using var conn = new MySqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            DELETE FROM `__EFMigrationsHistory`
            WHERE `MigrationId` IN (
                '20260426000001_SeedSupportRoles',
                '20260426000002_FixSupportRolesBackfill',
                '20260426000003_CorrectPlatformAdminRole'
            );";
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Factory helper ────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="WebApplicationFactory{TEntryPoint}"/> that boots the real
    /// Identity.Api host against the provided MySQL connection string.
    /// Background hosted services are removed so they cannot race with the test DB.
    /// </summary>
    private static WebApplicationFactory<Program> BuildFactory(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:IdentityDb"]          = connectionString,
                    ["Jwt:SigningKey"]                         = "test-only-signing-key-32-chars-padded-ok",
                    ["Jwt:Issuer"]                            = "test-issuer",
                    ["Jwt:Audience"]                          = "test-audience",
                    ["NotificationsService:BaseUrl"]          = "http://localhost:19999",
                    ["NotificationsService:PortalBaseUrl"]    = "http://localhost:19998",
                    ["NotificationsService:PortalBaseDomain"] = "",
                });
            });

            builder.ConfigureTestServices(services =>
            {
                // Remove background hosted services so they don't attempt to
                // poll external dependencies (Redis, audit sink, etc.) during startup.
                var hosted = services
                    .Where(d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService))
                    .ToList();
                foreach (var d in hosted) services.Remove(d);
            });
        });
}
