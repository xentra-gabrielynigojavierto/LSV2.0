using BuildingBlocks.TestHelpers;

namespace BuildingBlocks.IntegrationTests;

/// <summary>
/// Starts a single MySQL 8 container for the entire test collection and exposes
/// a helper that creates isolated per-test databases inside it.
/// </summary>
public sealed class MySqlContainerFixture : IAsyncLifetime
{
    private readonly MySqlTestContainer _container = new();

    public Task InitializeAsync() => _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>
    /// Creates a fresh database with the supplied name and returns its
    /// fully-qualified connection string (suitable for Pomelo/EF).
    /// Calling this a second time with the same name is a no-op.
    /// </summary>
    public Task<string> CreateDatabaseAsync(string name)
        => _container.CreateDatabaseAsync(name);
}

/// <summary>
/// Binds the test collection so that all integration tests share the same
/// MySQL container instance, keeping startup cost to a single container spin-up.
/// </summary>
[CollectionDefinition("MySqlCollection")]
public class MySqlCollection : ICollectionFixture<MySqlContainerFixture> { }
