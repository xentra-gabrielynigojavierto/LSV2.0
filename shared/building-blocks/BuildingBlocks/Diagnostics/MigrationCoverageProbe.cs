using System.Data.Common;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;

[assembly: InternalsVisibleTo("BuildingBlocks.Tests")]

namespace BuildingBlocks.Diagnostics;

/// <summary>
/// Boot-time self-test that compares every EF-mapped table and column against
/// the live database schema. If a model property has no backing column on the
/// live database, an ERROR is logged so the regression is loud at boot.
///
/// Catches the class of bug behind Task #58: a migration committed without
/// its [Migration] attribute (or otherwise un-applied) leaves the EF model
/// and the live schema out of sync, which previously surfaced only as
/// runtime "Unknown column" SQL errors.
///
/// Best-effort: any exception is logged at Warning and swallowed so a
/// transient DB issue at startup never prevents the service from booting.
///
/// Supports MySQL (information_schema) and SQLite (sqlite_master +
/// pragma_table_info). Other providers are skipped with an Information log.
/// </summary>
public static class MigrationCoverageProbe
{
    public static async Task RunAsync(DbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        DbConnection conn;
        string providerName;
        try
        {
            providerName = db.Database.ProviderName ?? string.Empty;
            conn = db.Database.GetDbConnection();
        }
        catch (Exception ex)
        {
            // Mirrors the best-effort contract: never let a startup probe abort
            // the boot — e.g. non-relational providers (InMemory) throw on
            // GetDbConnection().
            logger.LogWarning(ex, "Migration coverage self-test could not run");
            return;
        }
        await RunCoreAsync(providerName, conn, db.Model, logger, cancellationToken);
    }

    // Test-visible seam: lets unit tests drive the probe without standing up
    // a full DbContext. The DbContext-facing public RunAsync is the only
    // production caller.
    internal static async Task RunCoreAsync(
        string providerName,
        DbConnection conn,
        IModel model,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await conn.OpenAsync(cancellationToken);
            try
            {
                Dictionary<string, HashSet<string>> actualColumns;

                if (providerName.Contains("MySql", StringComparison.OrdinalIgnoreCase))
                {
                    actualColumns = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                    using var listCmd = conn.CreateCommand();
                    // Parameterized to avoid quoting/escape edge cases on the
                    // schema name (e.g. database names containing apostrophes).
                    listCmd.CommandText = @"SELECT table_name, column_name
                        FROM information_schema.columns
                        WHERE table_schema = @schema";
                    var schemaParam = listCmd.CreateParameter();
                    schemaParam.ParameterName = "@schema";
                    schemaParam.Value = conn.Database ?? string.Empty;
                    listCmd.Parameters.Add(schemaParam);
                    using var reader = await listCmd.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var t = reader.GetString(0);
                        var c = reader.GetString(1);
                        if (!actualColumns.TryGetValue(t, out var set))
                        {
                            set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            actualColumns[t] = set;
                        }
                        set.Add(c);
                    }
                }
                else if (providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
                {
                    actualColumns = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                    var tableNames = new List<string>();
                    using (var listCmd = conn.CreateCommand())
                    {
                        listCmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
                        using var reader = await listCmd.ExecuteReaderAsync(cancellationToken);
                        while (await reader.ReadAsync(cancellationToken))
                            tableNames.Add(reader.GetString(0));
                    }
                    foreach (var table in tableNames)
                    {
                        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        using var infoCmd = conn.CreateCommand();
                        infoCmd.CommandText = $"PRAGMA table_info(\"{table.Replace("\"", "\"\"")}\")";
                        using var reader = await infoCmd.ExecuteReaderAsync(cancellationToken);
                        // PRAGMA table_info returns columns: cid, name, type, notnull, dflt_value, pk
                        while (await reader.ReadAsync(cancellationToken))
                            set.Add(reader.GetString(1));
                        actualColumns[table] = set;
                    }
                }
                else
                {
                    logger.LogInformation(
                        "Migration coverage check skipped — unsupported provider {Provider}.",
                        providerName);
                    return;
                }

                var missing = new List<string>();
                var missingTables = new List<string>();
                foreach (var entityType in model.GetEntityTypes())
                {
                    var tableName = entityType.GetTableName();
                    if (string.IsNullOrEmpty(tableName)) continue;

                    var storeId = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());

                    if (!actualColumns.TryGetValue(tableName, out var presentColumns))
                    {
                        missingTables.Add($"{tableName} (mapped by {entityType.ClrType.Name})");
                        continue;
                    }

                    foreach (var prop in entityType.GetProperties())
                    {
                        var columnName = prop.GetColumnName(storeId);
                        if (string.IsNullOrEmpty(columnName)) continue;
                        if (!presentColumns.Contains(columnName))
                        {
                            missing.Add($"{tableName}.{columnName} (mapped by {entityType.ClrType.Name}.{prop.Name})");
                        }
                    }
                }

                if (missingTables.Count > 0 || missing.Count > 0)
                {
                    logger.LogError(
                        "Migration coverage check FAILED — schema is out of sync with the EF model. " +
                        "A migration is likely missing its [Migration] attribute or was never applied. " +
                        "Missing tables: [{MissingTables}]. Missing columns: [{MissingColumns}].",
                        string.Join("; ", missingTables),
                        string.Join("; ", missing));
                }
                else
                {
                    logger.LogInformation(
                        "Migration coverage check passed — all EF-mapped tables and columns are present on the live schema.");
                }
            }
            finally
            {
                await conn.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Migration coverage self-test could not run");
        }
    }
}
