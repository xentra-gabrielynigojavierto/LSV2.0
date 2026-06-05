using System.Data;
using Identity.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Identity.Api.Tests;

public class StartupMigrationGuardsTests
{
    private const string EfVersion  = "8.0.7";
    private const string GuardLabel = "TEST";

    private static IDbCommand MakeCmd(HashSet<string> history) =>
        new InMemoryMigrationHistoryCommand(history);

    [Fact]
    public void RepairPath_WhenHistoryRowAbsent_RunsApplyAndReturnsFalse()
    {
        var history = new HashSet<string>(StringComparer.Ordinal);
        using var cmd = MakeCmd(history);
        const string migrationId = "20260426000001_SeedSupportRoles";

        var recorded = StartupMigrationGuard.ApplyIfMissing(
            cmd, migrationId, EfVersion, NullLogger.Instance, GuardLabel, apply: _ => {});

        Assert.False(recorded);
        Assert.Contains(migrationId, history);
    }

    [Fact]
    public void SteadyState_WhenHistoryRowPresent_DoesNotInsertAndReturnsTrue()
    {
        const string migrationId = "20260426000001_SeedSupportRoles";
        var history = new HashSet<string>(StringComparer.Ordinal) { migrationId };
        var countBefore = history.Count;
        using var cmd = MakeCmd(history);

        var recorded = StartupMigrationGuard.ApplyIfMissing(
            cmd, migrationId, EfVersion, NullLogger.Instance, GuardLabel, apply: _ => {});

        Assert.True(recorded);
        Assert.Equal(countBefore, history.Count);
    }

    [Fact]
    public void ApplyAction_InvokedOnRepairPath_SkippedOnSteadyStatePath()
    {
        const string repairId      = "20260426000002_FixSupportRolesBackfill";
        const string steadyStateId = "20260426000003_CorrectPlatformAdminRole";
        var history = new HashSet<string>(StringComparer.Ordinal) { steadyStateId };
        var repairApplyCalls  = 0;
        var steadyApplyCalls  = 0;
        using var cmd = MakeCmd(history);

        StartupMigrationGuard.ApplyIfMissing(cmd, repairId,      EfVersion, NullLogger.Instance, GuardLabel, apply: _ => repairApplyCalls++);
        StartupMigrationGuard.ApplyIfMissing(cmd, steadyStateId, EfVersion, NullLogger.Instance, GuardLabel, apply: _ => steadyApplyCalls++);

        Assert.Equal(1, repairApplyCalls);
        Assert.Equal(0, steadyApplyCalls);
    }

    [Fact]
    public void LsIdSup002Sequence_AllThreeMigrationsAbsent_AllThreeRowsRecorded()
    {
        string[] migrationIds =
        [
            "20260426000001_SeedSupportRoles",
            "20260426000002_FixSupportRolesBackfill",
            "20260426000003_CorrectPlatformAdminRole",
        ];
        var history = new HashSet<string>(StringComparer.Ordinal);
        using var cmd = MakeCmd(history);

        foreach (var migId in migrationIds)
            Assert.False(StartupMigrationGuard.ApplyIfMissing(cmd, migId, EfVersion, NullLogger.Instance, GuardLabel, apply: _ => {}));

        foreach (var migId in migrationIds)
            Assert.Contains(migId, history);
    }

    [Fact]
    public void LsIdSup002Sequence_AlreadyRecorded_IdempotentOnSecondRun()
    {
        string[] migrationIds =
        [
            "20260426000001_SeedSupportRoles",
            "20260426000002_FixSupportRolesBackfill",
            "20260426000003_CorrectPlatformAdminRole",
        ];
        var history = new HashSet<string>(StringComparer.Ordinal);
        using var cmd = MakeCmd(history);

        foreach (var migId in migrationIds)
            StartupMigrationGuard.ApplyIfMissing(cmd, migId, EfVersion, NullLogger.Instance, GuardLabel, apply: _ => {});

        var countAfterFirstPass = history.Count;

        foreach (var migId in migrationIds)
            Assert.True(StartupMigrationGuard.ApplyIfMissing(cmd, migId, EfVersion, NullLogger.Instance, GuardLabel, apply: _ => {}));

        Assert.Equal(countAfterFirstPass, history.Count);
    }

    [Fact]
    public void Prerequisite_WhenBlockedByPrerequisite_SkipsApplyAndReturnsFalse()
    {
        const string migrationId = "20260418230627_AddTenantPermissionCatalog";
        var history = new HashSet<string>(StringComparer.Ordinal);
        using var cmd = MakeCmd(history);
        var applyCalls = 0;

        // prerequisite returns false → apply must not run, history must not be recorded
        var recorded = StartupMigrationGuard.ApplyIfMissing(
            cmd, migrationId, EfVersion, NullLogger.Instance, GuardLabel,
            apply: _ => applyCalls++,
            prerequisite: _ => false);

        Assert.False(recorded);
        Assert.Equal(0, applyCalls);
        Assert.DoesNotContain(migrationId, history);
    }
}

file sealed class InMemoryMigrationHistoryCommand(HashSet<string> history) : IDbCommand
{
    public string CommandText { get; set; } = string.Empty;

    public object? ExecuteScalar()
    {
        if (CommandText.Contains("__EFMigrationsHistory") && CommandText.Contains("SELECT"))
        {
            var migId = ExtractFirstQuotedValue(CommandText);
            return migId != null && history.Contains(migId) ? (object)1L : 0L;
        }
        return 0L;
    }

    public int ExecuteNonQuery()
    {
        if (CommandText.Contains("__EFMigrationsHistory") && CommandText.Contains("INSERT"))
        {
            var valuesIdx = CommandText.IndexOf("VALUES", StringComparison.OrdinalIgnoreCase);
            if (valuesIdx >= 0)
            {
                var id = ExtractFirstQuotedValue(CommandText[valuesIdx..]);
                if (id != null) history.Add(id);
            }
        }
        return 0;
    }

    private static string? ExtractFirstQuotedValue(string sql)
    {
        var first = sql.IndexOf('\'');
        if (first < 0) return null;
        var second = sql.IndexOf('\'', first + 1);
        if (second <= first) return null;
        var value = sql.Substring(first + 1, second - first - 1);
        return value.Length > 5 ? value : null;
    }

    public int CommandTimeout { get; set; }
    public CommandType CommandType { get; set; } = CommandType.Text;
    public IDbConnection? Connection { get; set; }
    public IDataParameterCollection Parameters { get; } = new DataParameterCollection();
    public IDbTransaction? Transaction { get; set; }
    public UpdateRowSource UpdatedRowSource { get; set; }

    public void Cancel() { }
    public IDbDataParameter CreateParameter() => throw new NotSupportedException();
    public IDataReader ExecuteReader() => throw new NotSupportedException();
    public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotSupportedException();
    public void Prepare() { }
    public void Dispose() { }
}

file sealed class DataParameterCollection : System.Collections.ArrayList, IDataParameterCollection
{
    public bool Contains(string parameterName) => false;
    public int IndexOf(string parameterName)   => -1;
    public void RemoveAt(string parameterName) { }
    public object this[string parameterName]
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }
}
