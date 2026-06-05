using System.Data;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure;

/// <summary>
/// Encapsulates the repeating "check __EFMigrationsHistory → run idempotent SQL → record migration"
/// pattern used by startup guards that protect against partially-committed DDL migrations.
///
/// MySQL auto-commits DDL immediately, even inside a failed transaction. When a migration's DDL
/// succeeds but its data-seed step fails, EF has no history row and will try to re-apply the
/// migration on the next startup — causing "Duplicate column" errors. This helper detects that
/// scenario and records the migration so EF's Migrate() finds nothing to do.
///
/// Usage:
///   StartupMigrationGuard.ApplyIfMissing(
///       cmd, "20260418230627_SomeMigration", "8.0.7", logger, "LS-ID-XYZ",
///       apply:          c => { c.CommandText = "INSERT IGNORE ..."; c.ExecuteNonQuery(); },
///       prerequisite:   c => { c.CommandText = "SELECT COUNT(*) ..."; return Convert.ToInt64(c.ExecuteScalar()) > 0; },
///       warningMessage: "LS-ID-XYZ: DDL was partially committed — seeding idempotently and marking applied.");
/// </summary>
public static class StartupMigrationGuard
{
    /// <summary>
    /// Checks whether <paramref name="migrationId"/> is already recorded in
    /// <c>__EFMigrationsHistory</c>. If not (and any optional <paramref name="prerequisite"/>
    /// returns <c>true</c>), logs a warning, runs <paramref name="apply"/>, and inserts the
    /// history row.
    /// </summary>
    /// <param name="cmd">An open, reusable <see cref="IDbCommand"/> on the target connection.</param>
    /// <param name="migrationId">The EF migration ID (e.g. "20260418230627_AddTenantPermissionCatalog").</param>
    /// <param name="efVersion">The EF version string to store in the history row (e.g. "8.0.7").</param>
    /// <param name="logger">Logger used for warning/info messages.</param>
    /// <param name="guardLabel">Short label used in the default warning message (e.g. "LS-ID-TNT-011").</param>
    /// <param name="apply">Action that executes the idempotent SQL for this migration.</param>
    /// <param name="prerequisite">
    ///   Optional extra check executed before <paramref name="apply"/>.
    ///   Return <c>true</c> to proceed, <c>false</c> to skip silently.
    ///   Use this when the guard should only act if, for example, a DDL column already exists.
    ///   When <c>null</c>, the guard proceeds as long as the migration is missing from history.
    /// </param>
    /// <param name="warningMessage">
    ///   Optional override for the warning message logged when the migration is about to be
    ///   applied. When <c>null</c>, the default message
    ///   "<c>{guardLabel}: {migrationId} not in EF history — applying idempotently and recording.</c>"
    ///   is used. Supply a custom string to preserve existing guard-specific wording verbatim.
    /// </param>
    /// <param name="historyInsertPrefix">
    ///   The INSERT keyword prefix for recording the history row.
    ///   Defaults to <c>INSERT IGNORE</c> (MySQL). Pass <c>INSERT OR IGNORE</c> when
    ///   targeting SQLite (e.g. in tests).
    /// </param>
    /// <returns>
    ///   <c>true</c>  — migration was already in history; no action was taken.<br/>
    ///   <c>false</c> — migration was absent; either the prerequisite blocked the apply
    ///                  (no SQL ran) or the apply+record completed successfully.
    /// </returns>
    public static bool ApplyIfMissing(
        IDbCommand cmd,
        string migrationId,
        string efVersion,
        ILogger logger,
        string guardLabel,
        Action<IDbCommand> apply,
        Func<IDbCommand, bool>? prerequisite = null,
        string? warningMessage = null,
        string historyInsertPrefix = "INSERT IGNORE")
    {
        cmd.CommandText = $@"
            SELECT COUNT(*) FROM `__EFMigrationsHistory`
            WHERE `MigrationId` = '{migrationId}';";
        var alreadyRecorded = Convert.ToInt64(cmd.ExecuteScalar()) > 0;

        if (alreadyRecorded)
            return true;

        if (prerequisite is not null && !prerequisite(cmd))
            return false;

        logger.LogWarning(
            warningMessage
            ?? $"{guardLabel}: {migrationId} not in EF history — applying idempotently and recording.");

        apply(cmd);

        cmd.CommandText = $@"
            {historyInsertPrefix} INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
            VALUES ('{migrationId}', '{efVersion}');";
        cmd.ExecuteNonQuery();

        return false;
    }
}
