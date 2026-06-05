using BuildingBlocks.Diagnostics;
using BuildingBlocks.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlatformAuditEventService.Data;

namespace PlatformAuditEventService.Tests.IntegrationTests;

/// <summary>
/// MySQL integration tests for the audit service's EF schema (Task #73).
///
/// Spins up a real MySQL 8 container via Testcontainers, applies the audit service's
/// EF migrations against it, and asserts that <see cref="MigrationCoverageProbe"/>
/// logs the "passed" line — proving the migrated schema and the EF model are in sync.
///
/// This test lives inside the audit service's own test project rather than in
/// BuildingBlocks.IntegrationTests because PlatformAuditEventService.csproj uses
/// Sdk="Microsoft.NET.Sdk.Web", and referencing a Web-SDK project from a plain
/// class-library test project causes dotnet restore to hang during project-graph
/// resolution. The audit.Tests project already uses the Web SDK project reference
/// via Microsoft.AspNetCore.Mvc.Testing, so it is not affected by this limitation.
///
/// CI note: these tests require Docker (for Testcontainers container spin-up).
/// </summary>
public sealed class AuditSchemaMigrationTests : IAsyncLifetime
{
    private static readonly MySqlServerVersion ServerVersion = new(new Version(8, 0, 0));

    private readonly MySqlTestContainer _container = new();

    public Task InitializeAsync() => _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    // =========================================================================
    // Happy-path: apply all audit migrations and assert probe passes
    // =========================================================================

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Probe_Passes_Audit()
    {
        var cs = await _container.CreateDatabaseAsync("it_audit");

        await using var db = new AuditEventDbContext(
            new DbContextOptionsBuilder<AuditEventDbContext>()
                .UseMySql(cs, ServerVersion)
                .Options);

        await db.Database.MigrateAsync();

        var logger = new CapturingLogger();
        await MigrationCoverageProbe.RunAsync(db, logger);

        var passedEntry = logger.Entries
            .SingleOrDefault(e => e.Level == LogLevel.Information && e.Message.Contains("passed"));

        Assert.True(passedEntry is not null,
            "Expected MigrationCoverageProbe to log 'passed' after MigrateAsync() on the audit " +
            "service schema, but it did not. " +
            $"Actual log entries: [{string.Join(" | ", logger.Entries.Select(e => $"{e.Level}: {e.Message}"))}]. " +
            "This usually means the EF model has columns or tables that were never added to a migration.");

        Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Error);
    }
}
