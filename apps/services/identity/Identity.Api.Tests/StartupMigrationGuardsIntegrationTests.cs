using Identity.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Identity.Api.Tests;

// Integration tests for StartupMigrationGuard.ApplyIfMissing against a real
// SQLite in-memory database. Uses historyInsertPrefix "INSERT OR IGNORE" (SQLite
// syntax) instead of the production default "INSERT IGNORE" (MySQL).
public sealed class StartupMigrationGuardsIntegrationTests : IDisposable
{
    private const string SqliteInsert = "INSERT OR IGNORE";
    private const string EfVersion    = "8.0.7";
    private const string GuardLabel   = "TEST";

    private readonly SqliteConnection _conn;

    public StartupMigrationGuardsIntegrationTests()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        CreateSchema();
    }

    public void Dispose() => _conn.Dispose();

    private void CreateSchema()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE `__EFMigrationsHistory` (
                `MigrationId`    TEXT NOT NULL PRIMARY KEY,
                `ProductVersion` TEXT NOT NULL
            );
            CREATE TABLE `test_roles` (
                `Id`   TEXT NOT NULL PRIMARY KEY,
                `Name` TEXT NOT NULL
            );";
        cmd.ExecuteNonQuery();
    }

    private bool HistoryRowExists(string migrationId)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM `__EFMigrationsHistory` WHERE `MigrationId` = '{migrationId}';";
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    private long HistoryRowCount()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM `__EFMigrationsHistory`;";
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private long TestRoleCount()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM `test_roles`;";
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private bool RunGuard(string migrationId, Action<System.Data.IDbCommand>? apply = null)
    {
        using var cmd = _conn.CreateCommand();
        return StartupMigrationGuard.ApplyIfMissing(
            cmd, migrationId, EfVersion, NullLogger.Instance, GuardLabel,
            apply: apply ?? (_ => {}),
            historyInsertPrefix: SqliteInsert);
    }

    [Fact]
    public void RepairPath_HistoryRowAbsent_RunsApplyAndReturnsFalse()
    {
        const string migrationId = "20260426000001_SeedSupportRoles";

        var recorded = RunGuard(migrationId);

        Assert.False(recorded);
        Assert.True(HistoryRowExists(migrationId));

        using var check = _conn.CreateCommand();
        check.CommandText = $"SELECT `ProductVersion` FROM `__EFMigrationsHistory` WHERE `MigrationId` = '{migrationId}';";
        Assert.Equal(EfVersion, check.ExecuteScalar() as string);
    }

    [Fact]
    public void SteadyState_HistoryRowPresent_DoesNotInsertDuplicateAndReturnsTrue()
    {
        const string migrationId = "20260426000001_SeedSupportRoles";
        using (var setup = _conn.CreateCommand())
        {
            setup.CommandText = $"INSERT OR IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES ('{migrationId}', '{EfVersion}');";
            setup.ExecuteNonQuery();
        }

        var rowsBefore = HistoryRowCount();
        var recorded = RunGuard(migrationId);

        Assert.True(recorded);
        Assert.Equal(rowsBefore, HistoryRowCount());
    }

    // Models "migration effects applied but history row absent": the apply callback
    // INSERTs a row into a real table, then the guard records the history row.
    // Verifies the INSERT executed (row visible in DB) and that a second run skips
    // apply and produces no duplicates — matching the LS-ID-SUP-002 repair scenario.
    [Fact]
    public void RepairPath_WithRealApplySql_ApplyInsertsRowAndHistoryIsRecorded()
    {
        const string migrationId = "20260426000001_SeedSupportRoles";
        const string roleId      = "10000000-0000-0000-0000-000000000001";

        void ApplyRole(System.Data.IDbCommand cmd)
        {
            cmd.CommandText = $"INSERT OR IGNORE INTO `test_roles` (`Id`, `Name`) VALUES ('{roleId}', 'Support');";
            cmd.ExecuteNonQuery();
        }

        // First run — repair path: apply fires, history row inserted.
        var recorded = RunGuard(migrationId, ApplyRole);

        Assert.False(recorded);
        Assert.True(HistoryRowExists(migrationId));
        Assert.Equal(1L, TestRoleCount());

        // Second run — steady-state: already recorded, apply must NOT be called.
        var recordedAgain = RunGuard(migrationId, ApplyRole);

        Assert.True(recordedAgain);
        Assert.Equal(1L, TestRoleCount());
        Assert.Equal(1L, HistoryRowCount());
    }

    [Fact]
    public void LsIdSup002Sequence_AllThreeMigrationsAbsent_AllThreeRowsInDb()
    {
        string[] migrationIds =
        [
            "20260426000001_SeedSupportRoles",
            "20260426000002_FixSupportRolesBackfill",
            "20260426000003_CorrectPlatformAdminRole",
        ];

        foreach (var migId in migrationIds)
            Assert.False(RunGuard(migId), $"Guard must return false (was missing) for {migId}.");

        Assert.Equal(migrationIds.Length, (int)HistoryRowCount());
        foreach (var migId in migrationIds)
            Assert.True(HistoryRowExists(migId));
    }

    [Fact]
    public void GuardIsIdempotent_SecondRunDoesNotDuplicateRows()
    {
        string[] migrationIds =
        [
            "20260426000001_SeedSupportRoles",
            "20260426000002_FixSupportRolesBackfill",
            "20260426000003_CorrectPlatformAdminRole",
        ];

        foreach (var migId in migrationIds)
            RunGuard(migId);

        var countAfterFirstPass = HistoryRowCount();

        foreach (var migId in migrationIds)
            Assert.True(RunGuard(migId), $"Guard must return true (already recorded) for {migId} on second run.");

        Assert.Equal(countAfterFirstPass, HistoryRowCount());
    }

    [Fact]
    public void ApplyAction_CalledOnRepairPath_SkippedOnSteadyStatePath()
    {
        const string repairId      = "20260426000002_FixSupportRolesBackfill";
        const string steadyStateId = "20260426000003_CorrectPlatformAdminRole";

        using (var setup = _conn.CreateCommand())
        {
            setup.CommandText = $"INSERT OR IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES ('{steadyStateId}', '{EfVersion}');";
            setup.ExecuteNonQuery();
        }

        var repairApplyCalls  = 0;
        var steadyApplyCalls  = 0;

        RunGuard(repairId,      apply: _ => repairApplyCalls++);
        RunGuard(steadyStateId, apply: _ => steadyApplyCalls++);

        Assert.Equal(1, repairApplyCalls);
        Assert.Equal(0, steadyApplyCalls);
    }
}
