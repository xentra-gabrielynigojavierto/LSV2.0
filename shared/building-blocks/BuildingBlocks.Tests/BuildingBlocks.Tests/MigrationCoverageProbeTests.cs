using System.Data;
using System.Data.Common;
using BuildingBlocks.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildingBlocks.Tests;

public class MigrationCoverageProbeTests
{
    // ---- Test DbContexts ----------------------------------------------------

    private class Widget
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }

    private class Gadget
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    private class TwoEntityContext : DbContext
    {
        public TwoEntityContext(DbContextOptions options) : base(options) { }
        public DbSet<Widget> Widgets => Set<Widget>();
        public DbSet<Gadget> Gadgets => Set<Gadget>();
    }

    private class OneEntityContext : DbContext
    {
        public OneEntityContext(DbContextOptions options) : base(options) { }
        public DbSet<Widget> Widgets => Set<Widget>();
    }

    // ---- Captured log assertion helper --------------------------------------

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

    private sealed class CapturingLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = new();
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    // =========================================================================
    // SQLite path — uses a real on-disk SQLite file so the probe runs against
    // the real PRAGMA table_info / sqlite_master pipeline.
    // =========================================================================

    private static (string path, string connStr) NewSqliteFile()
    {
        var path = Path.Combine(Path.GetTempPath(),
            $"mig-probe-{Guid.NewGuid():N}.sqlite");
        return (path, $"Data Source={path}");
    }

    [Fact]
    public async Task Sqlite_MatchingSchema_LogsInformation()
    {
        var (path, connStr) = NewSqliteFile();
        try
        {
            var opts = new DbContextOptionsBuilder<TwoEntityContext>()
                .UseSqlite(connStr).Options;
            await using (var setup = new TwoEntityContext(opts))
            {
                await setup.Database.EnsureCreatedAsync();
            }

            var logger = new CapturingLogger();
            await using var db = new TwoEntityContext(opts);
            await MigrationCoverageProbe.RunAsync(db, logger);

            var info = Assert.Single(logger.Entries.Where(e => e.Level == LogLevel.Information));
            Assert.Contains("passed", info.Message);
            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Sqlite_MissingColumn_LogsErrorMentioningTableColumnAndProperty()
    {
        var (path, connStr) = NewSqliteFile();
        try
        {
            // Create a "Widgets" table that is missing the Quantity column.
            await using (var conn = new SqliteConnection(connStr))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    @"CREATE TABLE ""Widgets"" (
                        ""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        ""Name"" TEXT NOT NULL
                      );";
                await cmd.ExecuteNonQueryAsync();
            }

            var opts = new DbContextOptionsBuilder<OneEntityContext>()
                .UseSqlite(connStr).Options;
            var logger = new CapturingLogger();
            await using var db = new OneEntityContext(opts);
            await MigrationCoverageProbe.RunAsync(db, logger);

            var error = Assert.Single(logger.Entries.Where(e => e.Level == LogLevel.Error));
            Assert.Contains("Widgets.Quantity", error.Message);
            Assert.Contains("Widget.Quantity", error.Message);
            Assert.Contains("FAILED", error.Message);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Sqlite_MissingTable_LogsErrorMentioningTableAndEntity()
    {
        var (path, connStr) = NewSqliteFile();
        try
        {
            // Create only "Widgets" — model also expects "Gadgets".
            await using (var conn = new SqliteConnection(connStr))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    @"CREATE TABLE ""Widgets"" (
                        ""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        ""Name"" TEXT NOT NULL,
                        ""Quantity"" INTEGER NOT NULL
                      );";
                await cmd.ExecuteNonQueryAsync();
            }

            var opts = new DbContextOptionsBuilder<TwoEntityContext>()
                .UseSqlite(connStr).Options;
            var logger = new CapturingLogger();
            await using var db = new TwoEntityContext(opts);
            await MigrationCoverageProbe.RunAsync(db, logger);

            var error = Assert.Single(logger.Entries.Where(e => e.Level == LogLevel.Error));
            Assert.Contains("Gadgets", error.Message);
            Assert.Contains("Gadget", error.Message);
            Assert.Contains("FAILED", error.Message);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // =========================================================================
    // MySQL path — exercised through the internal RunCoreAsync seam with a
    // fake DbConnection so we don't need a live MySQL server.
    // =========================================================================

    [Fact]
    public async Task MySql_MatchingSchema_LogsInformation()
    {
        var ctx = BuildModelOnlyContext();
        var conn = new FakeMySqlConnection(new Dictionary<string, string[]>
        {
            ["Widgets"] = new[] { "Id", "Name", "Quantity" },
            ["Gadgets"] = new[] { "Id", "Label" },
        });
        var logger = new CapturingLogger();

        await MigrationCoverageProbe.RunCoreAsync(
            "Pomelo.EntityFrameworkCore.MySql", conn, ctx.Model, logger);

        var info = Assert.Single(logger.Entries.Where(e => e.Level == LogLevel.Information));
        Assert.Contains("passed", info.Message);
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
        Assert.True(conn.WasOpened);
        Assert.True(conn.WasClosed);
    }

    [Fact]
    public async Task MySql_MissingColumn_LogsErrorMentioningTableColumnAndProperty()
    {
        var ctx = BuildModelOnlyContext();
        // Quantity column is absent on the live MySQL schema.
        var conn = new FakeMySqlConnection(new Dictionary<string, string[]>
        {
            ["Widgets"] = new[] { "Id", "Name" },
            ["Gadgets"] = new[] { "Id", "Label" },
        });
        var logger = new CapturingLogger();

        await MigrationCoverageProbe.RunCoreAsync(
            "Pomelo.EntityFrameworkCore.MySql", conn, ctx.Model, logger);

        var error = Assert.Single(logger.Entries.Where(e => e.Level == LogLevel.Error));
        Assert.Contains("Widgets.Quantity", error.Message);
        Assert.Contains("Widget.Quantity", error.Message);
    }

    // =========================================================================
    // Provider gating + best-effort error handling.
    // =========================================================================

    [Fact]
    public async Task UnsupportedProvider_LogsInformationAndSkips()
    {
        // Simulates a relational provider the probe doesn't know how to inspect
        // (e.g. SqlServer / Npgsql) — should skip cleanly rather than error.
        var ctx = BuildModelOnlyContext();
        var conn = new FakeMySqlConnection(new Dictionary<string, string[]>());
        var logger = new CapturingLogger();

        await MigrationCoverageProbe.RunCoreAsync(
            "Microsoft.EntityFrameworkCore.SqlServer", conn, ctx.Model, logger);

        var info = Assert.Single(logger.Entries.Where(e => e.Level == LogLevel.Information));
        Assert.Contains("skipped", info.Message);
        Assert.Contains("unsupported provider", info.Message);
        Assert.Contains("SqlServer", info.Message);
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task NonRelationalProvider_LogsWarningAndDoesNotThrow()
    {
        // InMemory is non-relational: db.Database.GetDbConnection() throws.
        // The public RunAsync must catch this and emit a Warning, never rethrow.
        var opts = new DbContextOptionsBuilder<TwoEntityContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new TwoEntityContext(opts);
        var logger = new CapturingLogger();

        await MigrationCoverageProbe.RunAsync(db, logger);

        var warn = Assert.Single(logger.Entries.Where(e => e.Level == LogLevel.Warning));
        Assert.NotNull(warn.Exception);
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task ConnectionThrows_LogsWarningAndDoesNotRethrow()
    {
        var ctx = BuildModelOnlyContext();
        var conn = new ThrowingConnection();
        var logger = new CapturingLogger();

        // Should NOT throw — the probe is best-effort.
        await MigrationCoverageProbe.RunCoreAsync(
            "Pomelo.EntityFrameworkCore.MySql", conn, ctx.Model, logger);

        var warn = Assert.Single(logger.Entries.Where(e => e.Level == LogLevel.Warning));
        Assert.NotNull(warn.Exception);
        Assert.Contains("could not run", warn.Message);
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
    }

    // ---- Plumbing -----------------------------------------------------------

    private static TwoEntityContext BuildModelOnlyContext()
    {
        // We need an EF model with our entities, but never actually execute SQL
        // through EF — the probe drives the connection directly. UseSqlite gives
        // us a relational model (StoreObjectIdentifier-friendly) without forcing
        // a real MySQL provider into the test project.
        var opts = new DbContextOptionsBuilder<TwoEntityContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        return new TwoEntityContext(opts);
    }

    // ---- Fake DbConnection that returns information_schema rows -------------
    // CS8765 = nullability on overridden ADO.NET members; cosmetic for fakes.
#pragma warning disable CS8765

    private sealed class FakeMySqlConnection : DbConnection
    {
        private readonly Dictionary<string, string[]> _tables;
        public bool WasOpened { get; private set; }
        public bool WasClosed { get; private set; }
        private ConnectionState _state = ConnectionState.Closed;

        public FakeMySqlConnection(Dictionary<string, string[]> tables)
        {
            _tables = tables;
        }

        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "test_schema";
        public override string DataSource => "fake";
        public override string ServerVersion => "8.0.0";
        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { _state = ConnectionState.Closed; WasClosed = true; }
        public override void Open() { _state = ConnectionState.Open; WasOpened = true; }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => throw new NotSupportedException();

        protected override DbCommand CreateDbCommand()
            => new FakeMySqlCommand(this, _tables);
        }

    private sealed class FakeMySqlCommand : DbCommand
    {
        private readonly FakeMySqlConnection _conn;
        private readonly Dictionary<string, string[]> _tables;
        private readonly FakeParameterCollection _params = new();

        public FakeMySqlCommand(FakeMySqlConnection conn, Dictionary<string, string[]> tables)
        {
            _conn = conn;
            _tables = tables;
        }

        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get => _conn; set { } }
        protected override DbParameterCollection DbParameterCollection => _params;
        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel() { }
        public override int ExecuteNonQuery() => 0;
        public override object? ExecuteScalar() => null;
        public override void Prepare() { }

        protected override DbParameter CreateDbParameter() => new FakeParameter();

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            // Flatten (table, column) tuples from the configured schema.
            var rows = _tables
                .SelectMany(kv => kv.Value.Select(col => (table: kv.Key, column: col)))
                .ToList();
            return new FakeInformationSchemaReader(rows);
        }
    }

    private sealed class FakeParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; }
        public override bool IsNullable { get; set; }
        public override string ParameterName { get; set; } = string.Empty;
        public override int Size { get; set; }
        public override string SourceColumn { get; set; } = string.Empty;
        public override bool SourceColumnNullMapping { get; set; }
        public override object? Value { get; set; }
        public override void ResetDbType() { }
    }

    private sealed class FakeParameterCollection : DbParameterCollection
    {
        private readonly List<object> _items = new();
        public override int Count => _items.Count;
        public override object SyncRoot => _items;
        public override int Add(object value) { _items.Add(value); return _items.Count - 1; }
        public override void AddRange(Array values) { foreach (var v in values) _items.Add(v); }
        public override void Clear() => _items.Clear();
        public override bool Contains(object value) => _items.Contains(value);
        public override bool Contains(string value) => false;
        public override void CopyTo(Array array, int index) => ((System.Collections.ICollection)_items).CopyTo(array, index);
        public override System.Collections.IEnumerator GetEnumerator() => _items.GetEnumerator();
        public override int IndexOf(object value) => _items.IndexOf(value);
        public override int IndexOf(string parameterName) => -1;
        public override void Insert(int index, object value) => _items.Insert(index, value);
        public override void Remove(object value) => _items.Remove(value);
        public override void RemoveAt(int index) => _items.RemoveAt(index);
        public override void RemoveAt(string parameterName) { }
        protected override DbParameter GetParameter(int index) => (DbParameter)_items[index];
        protected override DbParameter GetParameter(string parameterName) => throw new NotSupportedException();
        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value) => throw new NotSupportedException();
    }

    private sealed class FakeInformationSchemaReader : DbDataReader
    {
        private readonly List<(string table, string column)> _rows;
        private int _index = -1;
        public FakeInformationSchemaReader(List<(string, string)> rows) { _rows = rows; }

        public override bool Read() => ++_index < _rows.Count;
        public override string GetString(int ordinal)
            => ordinal == 0 ? _rows[_index].table : _rows[_index].column;
        public override int FieldCount => 2;
        public override bool HasRows => _rows.Count > 0;
        public override bool IsClosed => false;
        public override int Depth => 0;
        public override int RecordsAffected => -1;
        public override object this[int ordinal] => GetString(ordinal);
        public override object this[string name] => throw new NotSupportedException();
        public override bool NextResult() => false;
        public override System.Collections.IEnumerator GetEnumerator() => throw new NotSupportedException();
        public override bool GetBoolean(int ordinal) => throw new NotSupportedException();
        public override byte GetByte(int ordinal) => throw new NotSupportedException();
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
        public override char GetChar(int ordinal) => throw new NotSupportedException();
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
        public override string GetDataTypeName(int ordinal) => "string";
        public override DateTime GetDateTime(int ordinal) => throw new NotSupportedException();
        public override decimal GetDecimal(int ordinal) => throw new NotSupportedException();
        public override double GetDouble(int ordinal) => throw new NotSupportedException();
        public override Type GetFieldType(int ordinal) => typeof(string);
        public override float GetFloat(int ordinal) => throw new NotSupportedException();
        public override Guid GetGuid(int ordinal) => throw new NotSupportedException();
        public override short GetInt16(int ordinal) => throw new NotSupportedException();
        public override int GetInt32(int ordinal) => throw new NotSupportedException();
        public override long GetInt64(int ordinal) => throw new NotSupportedException();
        public override string GetName(int ordinal) => ordinal == 0 ? "table_name" : "column_name";
        public override int GetOrdinal(string name) => name == "table_name" ? 0 : 1;
        public override object GetValue(int ordinal) => GetString(ordinal);
        public override int GetValues(object[] values)
        {
            values[0] = _rows[_index].table;
            if (values.Length > 1) values[1] = _rows[_index].column;
            return Math.Min(2, values.Length);
        }
        public override bool IsDBNull(int ordinal) => false;
    }

    // ---- Fake DbConnection that throws on Open ------------------------------

    private sealed class ThrowingConnection : DbConnection
    {
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "x";
        public override string DataSource => "x";
        public override string ServerVersion => "x";
        public override ConnectionState State => ConnectionState.Closed;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() => throw new InvalidOperationException("simulated open failure");
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }
#pragma warning restore CS8765
}
