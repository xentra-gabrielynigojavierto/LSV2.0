using Testcontainers.MySql;

namespace BuildingBlocks.TestHelpers;

/// <summary>
/// Wraps the standard MySQL 8 Testcontainers configuration used across integration
/// test suites, eliminating duplicated <see cref="MySqlBuilder"/> setup.
///
/// Usage — as a per-test lifecycle (IAsyncLifetime):
/// <code>
///     private readonly MySqlTestContainer _container = new();
///     public Task InitializeAsync() => _container.StartAsync();
///     public Task DisposeAsync()    => _container.DisposeAsync().AsTask();
/// </code>
///
/// Usage — inside a container fixture:
/// <code>
///     private readonly MySqlTestContainer _container = new();
///     public string GetConnectionString() => _container.GetConnectionString();
///     public Task&lt;string&gt; CreateDatabaseAsync(string name) =>
///         _container.CreateDatabaseAsync(name);
/// </code>
/// </summary>
public sealed class MySqlTestContainer : IAsyncDisposable
{
    private readonly MySqlContainer _container = new MySqlBuilder()
        .WithImage("mysql:8.0")
        .WithDatabase("master")
        .WithUsername("root")
        .WithPassword("Test1234!")
        .Build();

    /// <summary>Starts the MySQL container.</summary>
    public Task StartAsync() => _container.StartAsync();

    /// <summary>Stops and disposes the MySQL container.</summary>
    public ValueTask DisposeAsync() => _container.DisposeAsync();

    /// <summary>Returns the root connection string for the running container.</summary>
    public string GetConnectionString() => _container.GetConnectionString();

    /// <summary>
    /// Creates a fresh isolated database and returns a connection string
    /// that targets it. Delegates to <see cref="MySqlTestHelpers.CreateDatabaseAsync"/>.
    /// </summary>
    public Task<string> CreateDatabaseAsync(string dbName)
        => MySqlTestHelpers.CreateDatabaseAsync(_container.GetConnectionString(), dbName);
}
