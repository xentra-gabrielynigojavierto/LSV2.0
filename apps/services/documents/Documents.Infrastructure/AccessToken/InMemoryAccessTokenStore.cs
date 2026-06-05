using Documents.Domain.Interfaces;
using Documents.Domain.ValueObjects;
using System.Collections.Concurrent;

namespace Documents.Infrastructure.TokenStore;

/// <summary>
/// In-memory access token store — suitable for single-replica development only.
/// Use RedisAccessTokenStore for multi-replica production deployments.
/// </summary>
public sealed class InMemoryAccessTokenStore : IAccessTokenStore
{
    private readonly ConcurrentDictionary<string, AccessToken> _store = new();

    public Task StoreAsync(AccessToken token, CancellationToken ct = default)
    {
        _store[token.Token] = token;
        return Task.CompletedTask;
    }

    public Task<AccessToken?> GetAsync(string tokenString, CancellationToken ct = default)
    {
        _store.TryGetValue(tokenString, out var token);
        if (token is not null && token.IsExpired)
        {
            _store.TryRemove(tokenString, out _);
            return Task.FromResult<AccessToken?>(null);
        }
        return Task.FromResult<AccessToken?>(token);
    }

    public Task<bool> MarkUsedAsync(string tokenString, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(tokenString, out var token)) return Task.FromResult(false);
        if (token.IsUsed) return Task.FromResult(false);

        // Not perfectly atomic in a multi-threaded scenario but acceptable for in-memory/single-replica use
        token.IsUsed = true;
        return Task.FromResult(true);
    }

    public Task RevokeAsync(string tokenString, CancellationToken ct = default)
    {
        _store.TryRemove(tokenString, out _);
        return Task.CompletedTask;
    }
}
