using BuildingBlocks.Diagnostics;
using BuildingBlocks.TestHelpers;
using CareConnect.Infrastructure.Data;
using Comms.Infrastructure.Persistence;
using Documents.Infrastructure.Database;
using Flow.Infrastructure.Persistence;
using Fund.Infrastructure.Data;
using Identity.Infrastructure.Data;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Notifications.Infrastructure.Data;
using Reports.Infrastructure.Persistence;

namespace BuildingBlocks.IntegrationTests;

/// <summary>
/// Integration tests for Task #68.
///
/// Spins up a real MySQL 8 container via Testcontainers, applies each service's
/// EF migrations against it, and asserts that <see cref="MigrationCoverageProbe"/>
/// logs the "passed" line — proving that the migrated schema and the EF model
/// are in sync after a real <c>MigrateAsync()</c>.
///
/// Also includes a regression / "drift" test that verifies the probe correctly
/// reports failure when the live schema is missing a column that the EF model
/// expects, demonstrating that the probe can catch the original Task #58 class
/// of bug against a real MySQL database (not just a fake in-memory schema).
///
/// Services excluded from this collection:
///   - audit        (PlatformAuditEventService.csproj uses Sdk="Microsoft.NET.Sdk.Web";
///                   referencing a WebSdk project from a non-web test project causes
///                   dotnet restore to hang during project-graph resolution. Coverage
///                   is provided by AuditSchemaMigrationTests in the audit.Tests project
///                   — see task #73.)
///
/// CI note: these tests require Docker (for Testcontainers container spin-up). In CI,
/// they run in the "Schema Drift Integration Tests" job defined in
/// `.github/workflows/ci.yml`, which uses a GitHub-hosted ubuntu-latest runner
/// (Docker pre-installed). Filter: `--filter "Category=Integration"`.
/// </summary>
[Collection("MySqlCollection")]
[Trait("Category", "Integration")]
public class MigrationCoverageIntegrationTests
{
    private static readonly MySqlServerVersion ServerVersion = new(new Version(8, 0, 0));

    private readonly MySqlContainerFixture _mysql;

    public MigrationCoverageIntegrationTests(MySqlContainerFixture mysql)
    {
        _mysql = mysql;
    }

    // =========================================================================
    // Happy-path: per-service migration + probe
    // Each test gets its own isolated database inside the shared container so
    // migrations from different services can't interfere with each other.
    // =========================================================================

    [Fact]
    public Task Probe_Passes_CareConnect() =>
        AssertProbePassesAsync<CareConnectDbContext>(
            "it_careconnect",
            cs => new CareConnectDbContext(
                new DbContextOptionsBuilder<CareConnectDbContext>()
                    .UseMySql(cs, ServerVersion)
                    .Options));

    [Fact]
    public Task Probe_Passes_Comms() =>
        AssertProbePassesAsync<CommsDbContext>(
            "it_comms",
            cs => new CommsDbContext(
                new DbContextOptionsBuilder<CommsDbContext>()
                    .UseMySql(cs, ServerVersion)
                    .Options));

    [Fact]
    public Task Probe_Passes_Flow() =>
        AssertProbePassesAsync<FlowDbContext>(
            "it_flow",
            cs => new FlowDbContext(
                new DbContextOptionsBuilder<FlowDbContext>()
                    .UseMySql(cs, ServerVersion)
                    .Options));

    [Fact]
    public Task Probe_Passes_Fund() =>
        AssertProbePassesAsync<FundDbContext>(
            "it_fund",
            cs => new FundDbContext(
                new DbContextOptionsBuilder<FundDbContext>()
                    .UseMySql(cs, ServerVersion)
                    .Options));

    [Fact]
    public Task Probe_Passes_Identity() =>
        AssertProbePassesAsync<IdentityDbContext>(
            "it_identity",
            cs => new IdentityDbContext(
                new DbContextOptionsBuilder<IdentityDbContext>()
                    .UseMySql(cs, ServerVersion)
                    .Options));

    [Fact]
    public Task Probe_Passes_Liens() =>
        AssertProbePassesAsync<LiensDbContext>(
            "it_liens",
            cs => new LiensDbContext(
                new DbContextOptionsBuilder<LiensDbContext>()
                    .UseMySql(cs, ServerVersion)
                    .Options));

    [Fact]
    public Task Probe_Passes_Reports() =>
        AssertProbePassesAsync<ReportsDbContext>(
            "it_reports",
            cs => new ReportsDbContext(
                new DbContextOptionsBuilder<ReportsDbContext>()
                    .UseMySql(cs, ServerVersion)
                    .Options));

    [Fact]
    public Task Probe_Passes_Notifications() =>
        AssertProbePassesAsync<NotificationsDbContext>(
            "it_notifications",
            cs => new NotificationsDbContext(
                new DbContextOptionsBuilder<NotificationsDbContext>()
                    .UseMySql(cs, ServerVersion)
                    .Options));

    [Fact]
    public Task Probe_Passes_Documents() =>
        AssertProbePassesAsync<DocsDbContext>(
            "it_documents",
            cs => new DocsDbContext(
                new DbContextOptionsBuilder<DocsDbContext>()
                    .UseMySql(cs, ServerVersion)
                    .Options));

    private async Task AssertProbePassesAsync<TContext>(
        string dbName,
        Func<string, TContext> factory)
        where TContext : DbContext
    {
        var cs = await _mysql.CreateDatabaseAsync(dbName);
        await using var db = factory(cs);

        await db.Database.MigrateAsync();

        var logger = new CapturingLogger();
        await MigrationCoverageProbe.RunAsync(db, logger);

        var passedEntry = logger.Entries
            .SingleOrDefault(e => e.Level == LogLevel.Information && e.Message.Contains("passed"));

        Assert.True(passedEntry is not null,
            $"[{dbName}] Expected MigrationCoverageProbe to log 'passed' after MigrateAsync(), " +
            $"but it did not. Actual log entries: [{string.Join(" | ", logger.Entries.Select(e => $"{e.Level}: {e.Message}"))}]. " +
            "This usually means the EF model has columns or tables that were never added to a migration.");

        Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Error);
    }

    // =========================================================================
    // Drift / regression test — demonstrates that the probe actually catches
    // model-vs-schema mismatch on a real MySQL database.
    //
    // A simple test-only entity ("DriftWidget") is mapped in the EF model with
    // three columns (Id, Name, Quantity). The database table is created
    // manually with only two columns (Id, Name). The probe must detect the
    // missing Quantity column and log an ERROR containing "FAILED".
    // =========================================================================

    [Fact]
    public async Task Probe_DetectsMissingColumn_LogsError_OnRealMySql()
    {
        var cs = await _mysql.CreateDatabaseAsync("it_drift");

        await using var conn = new MySqlConnection(cs);
        await conn.OpenAsync();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                @"CREATE TABLE IF NOT EXISTS `drift_widgets` (
                    `Id`   INT          NOT NULL AUTO_INCREMENT PRIMARY KEY,
                    `Name` VARCHAR(255) NOT NULL
                  )";
            await cmd.ExecuteNonQueryAsync();
        }

        var opts = new DbContextOptionsBuilder<DriftTestContext>()
            .UseMySql(cs, ServerVersion)
            .Options;
        await using var db = new DriftTestContext(opts);

        var logger = new CapturingLogger();
        await MigrationCoverageProbe.RunAsync(db, logger);

        var errors = logger.Entries.Where(e => e.Level >= LogLevel.Error).ToList();
        Assert.NotEmpty(errors);

        var errorMsg = errors.First().Message;
        Assert.Contains("FAILED", errorMsg);
        Assert.Contains("drift_widgets.Quantity", errorMsg);
        Assert.Contains("DriftWidget.Quantity", errorMsg);
    }

    // =========================================================================
    // Drift test context — used only by the regression test above.
    // =========================================================================

    private class DriftWidget
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// This property has no backing column in the manually-created test table.
        /// The probe must detect this and log an error.
        /// </summary>
        public int Quantity { get; set; }
    }

    private class DriftTestContext : DbContext
    {
        public DriftTestContext(DbContextOptions<DriftTestContext> options) : base(options) { }

        public DbSet<DriftWidget> DriftWidgets => Set<DriftWidget>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<DriftWidget>().ToTable("drift_widgets");
        }
    }
}
