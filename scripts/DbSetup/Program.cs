using MySqlConnector;

var mode = args.Length > 0 ? args[0] : "check";

var connStr = Environment.GetEnvironmentVariable("ConnectionStrings__ReportsDb");
if (string.IsNullOrEmpty(connStr))
{
    Console.WriteLine("ERROR: ConnectionStrings__ReportsDb not set");
    return 1;
}

var builder = new MySqlConnectionStringBuilder(connStr);
builder.Database = "";

Console.WriteLine($"Connecting to {builder.Server}...");
using var conn = new MySqlConnection(builder.ConnectionString);
await conn.OpenAsync();
Console.WriteLine("Connected.\n");

if (mode == "reset-audit")
{
    var auditConnStr = Environment.GetEnvironmentVariable("ConnectionStrings__AuditEventDb");
    if (string.IsNullOrEmpty(auditConnStr))
    {
        Console.WriteLine("ERROR: ConnectionStrings__AuditEventDb not set");
        return 1;
    }
    var ab = new MySqlConnectionStringBuilder(auditConnStr);
    Console.WriteLine($"Resetting audit database: {ab.Database}");

    using var ac = new MySqlConnection(auditConnStr);
    await ac.OpenAsync();
    await new MySqlCommand("SET FOREIGN_KEY_CHECKS = 0", ac).ExecuteNonQueryAsync();

    using var tc = new MySqlCommand(
        $"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{ab.Database}' AND TABLE_TYPE = 'BASE TABLE'", ac);
    using var tr = await tc.ExecuteReaderAsync();
    var tables = new List<string>();
    while (await tr.ReadAsync()) tables.Add(tr.GetString(0));
    await tr.CloseAsync();

    foreach (var t in tables)
    {
        Console.WriteLine($"  DROP TABLE `{t}`");
        await new MySqlCommand($"DROP TABLE IF EXISTS `{t}`", ac).ExecuteNonQueryAsync();
    }
    await new MySqlCommand("SET FOREIGN_KEY_CHECKS = 1", ac).ExecuteNonQueryAsync();
    Console.WriteLine($"Dropped {tables.Count} tables. Migrations will recreate on next startup.");
    return 0;
}

var expectedDbs = new[] { "identity_db", "fund_db", "careconnect_db", "documents_db", "liens_db", "notifications_db", "reports_db", "audit_event_db" };

using var showCmd = new MySqlCommand("SHOW DATABASES", conn);
using var reader = await showCmd.ExecuteReaderAsync();
var existing = new HashSet<string>();
while (await reader.ReadAsync()) existing.Add(reader.GetString(0));
await reader.CloseAsync();

Console.WriteLine("Databases:");
foreach (var db in existing.OrderBy(x => x).Where(x => !new[] { "information_schema", "mysql", "performance_schema", "sys" }.Contains(x)))
    Console.WriteLine($"  {db}");

Console.WriteLine();
foreach (var db in expectedDbs)
{
    if (existing.Contains(db))
        Console.WriteLine($"  OK: {db}");
    else
    {
        Console.Write($"  CREATING: {db} ... ");
        await new MySqlCommand($"CREATE DATABASE `{db}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci", conn).ExecuteNonQueryAsync();
        Console.WriteLine("done");
    }
}

Console.WriteLine("\nDone.");
return 0;
