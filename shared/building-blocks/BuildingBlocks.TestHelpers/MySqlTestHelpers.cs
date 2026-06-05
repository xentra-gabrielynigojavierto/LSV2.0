using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace BuildingBlocks.TestHelpers;

/// <summary>
/// Reusable MySQL helper for integration tests.
/// Creates an isolated database inside an already-running MySQL container and
/// returns the corresponding connection string.
/// </summary>
public static class MySqlTestHelpers
{
    /// <summary>
    /// Issues <c>CREATE DATABASE IF NOT EXISTS</c> against the root connection
    /// string, then returns a connection string that points to the new database.
    /// </summary>
    public static async Task<string> CreateDatabaseAsync(string rootConnectionString, string dbName)
    {
        await using var conn = new MySqlConnection(rootConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE IF NOT EXISTS `{dbName}`";
        await cmd.ExecuteNonQueryAsync();

        return Regex.Replace(rootConnectionString, @"Database=[^;]+", $"Database={dbName}",
            RegexOptions.IgnoreCase);
    }
}

/// <summary>
/// Captured log entry produced by <see cref="CapturingLogger"/>.
/// </summary>
public sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

/// <summary>
/// Minimal <see cref="ILogger"/> implementation that accumulates every log call
/// into <see cref="Entries"/> so tests can assert on logged messages.
/// </summary>
public sealed class CapturingLogger : ILogger
{
    public List<LogEntry> Entries { get; } = new();

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
        => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
        => Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
