using Documents.Domain.Interfaces;
using Documents.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace Documents.Infrastructure.TokenStore;

/// <summary>
/// Redis-backed access token store — safe for multi-replica production.
/// Implements atomic one-time-use via a Lua script (TOCTOU-safe).
/// </summary>
public sealed class RedisAccessTokenStore : IAccessTokenStore
{
    private const string KeyPrefix = "access_token:";

    private static readonly string MarkUsedLua = @"
local key  = KEYS[1]
local raw  = redis.call('GET', key)
if not raw then return -1 end
local tok  = cjson.decode(raw)
if tok.IsUsed then return 0 end
tok.IsUsed = true
local ttl  = redis.call('TTL', key)
if ttl < 1 then return -1 end
redis.call('SET', key, cjson.encode(tok), 'EX', ttl)
return 1
";

    private readonly IDatabase            _db;
    private readonly ILogger<RedisAccessTokenStore> _log;

    public RedisAccessTokenStore(IConnectionMultiplexer redis, ILogger<RedisAccessTokenStore> log)
    {
        _db  = redis.GetDatabase();
        _log = log;
    }

    public async Task StoreAsync(AccessToken token, CancellationToken ct = default)
    {
        var ttl     = token.ExpiresAt - DateTime.UtcNow;
        var seconds = Math.Max(1, (int)ttl.TotalSeconds);
        var json    = JsonSerializer.Serialize(token);
        await _db.StringSetAsync(Key(token.Token), json, TimeSpan.FromSeconds(seconds));
    }

    public async Task<AccessToken?> GetAsync(string tokenString, CancellationToken ct = default)
    {
        var raw = await _db.StringGetAsync(Key(tokenString));
        if (!raw.HasValue) return null;
        return Deserialise(raw!);
    }

    public async Task<bool> MarkUsedAsync(string tokenString, CancellationToken ct = default)
    {
        var result = (int) await _db.ScriptEvaluateAsync(MarkUsedLua, new RedisKey[] { Key(tokenString) });
        // 1 = marked, 0 = already used, -1 = not found
        return result == 1;
    }

    public async Task RevokeAsync(string tokenString, CancellationToken ct = default)
        => await _db.KeyDeleteAsync(Key(tokenString));

    private static string Key(string tokenString) => $"{KeyPrefix}{tokenString}";

    private static AccessToken Deserialise(string raw)
    {
        var token = JsonSerializer.Deserialize<AccessToken>(raw)!;
        return token;
    }
}
