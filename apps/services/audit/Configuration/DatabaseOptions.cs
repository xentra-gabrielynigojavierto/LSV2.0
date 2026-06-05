namespace PlatformAuditEventService.Configuration;

/// <summary>
/// Database connectivity and behavior options.
/// Bound from "Database" section in appsettings.
/// Environment variable override prefix: Database__
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Persistence provider.
    /// Allowed values:
    ///   "InMemory" — ephemeral, for tests only. Data lost on restart. Never use in production.
    ///   "Sqlite"   — durable, zero-config, file-backed. Development only. Schema created via EnsureCreated.
    ///   "MySQL"    — production-grade. Requires Database:ConnectionString and a configured server.
    /// Environment variable: Database__Provider
    /// </summary>
    public string Provider { get; set; } = "InMemory";

    /// <summary>
    /// SQLite database file path (used only when Provider = "Sqlite").
    /// Relative paths resolve from the working directory.
    /// Default: "audit-events-dev.db" (creates file in the service working directory).
    /// Override: Database__SqliteFilePath=... or Database__ConnectionString=Data Source=...
    /// </summary>
    public string SqliteFilePath { get; set; } = "audit-events-dev.db";

    /// <summary>
    /// MySQL connection string.
    /// Recommended: inject via environment variable Database__ConnectionString or
    /// ConnectionStrings__AuditEventDb (standard ASP.NET Core convention).
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// MySQL server version string for Pomelo (e.g. "8.0.0-mysql").
    /// Used only when Provider = "MySQL".
    /// </summary>
    public string ServerVersion { get; set; } = "8.0.0-mysql";

    /// <summary>
    /// Maximum number of connections in the connection pool.
    /// </summary>
    public int MaxPoolSize { get; set; } = 100;

    /// <summary>
    /// Minimum number of connections in the connection pool.
    /// </summary>
    public int MinPoolSize { get; set; } = 5;

    /// <summary>
    /// Connection timeout in seconds.
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Command (query) timeout in seconds.
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// When true, EF Core will run pending migrations on startup.
    /// Only applies when Provider = "MySQL".
    /// Keep false in production unless you own the migration window.
    /// </summary>
    public bool MigrateOnStartup { get; set; } = false;

    /// <summary>
    /// When true, a safe DB connectivity check runs at startup.
    /// Failure is logged as a warning but does NOT abort startup,
    /// allowing the service to remain up for health-check reporting.
    /// </summary>
    public bool VerifyConnectionOnStartup { get; set; } = true;

    /// <summary>
    /// Timeout in seconds for the startup connectivity probe.
    /// </summary>
    public int StartupProbeTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// When true, enables EF Core sensitive data logging (logs parameter values).
    /// NEVER enable in production — exposes PII.
    /// </summary>
    public bool EnableSensitiveDataLogging { get; set; } = false;

    /// <summary>
    /// When true, enables EF Core detailed errors (full SQL in exceptions).
    /// Safe to enable in Development; disable in production.
    /// </summary>
    public bool EnableDetailedErrors { get; set; } = false;
}
