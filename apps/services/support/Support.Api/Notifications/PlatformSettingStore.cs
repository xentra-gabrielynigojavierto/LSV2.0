using MySqlConnector;

namespace Support.Api.Notifications;

public interface IPlatformSettingStore
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
}

/// <summary>
/// Reads platform-wide settings from the <c>platform_settings</c> table in the
/// Tenant DB.  The table is created lazily (CREATE TABLE IF NOT EXISTS) on first
/// use so no migration is needed.
///
/// Non-empty values are cached for <see cref="CacheTtl"/> (5 min) to avoid a
/// DB round-trip on every notification.  Empty / null values are only cached for
/// <see cref="EmptyCacheTtl"/> (15 s) so that a newly saved setting is picked up
/// within seconds instead of waiting the full TTL.
///
/// Failures are swallowed so a misconfigured or temporarily unavailable DB never
/// blocks notification dispatch.
/// </summary>
public sealed class TenantDbPlatformSettingStore : IPlatformSettingStore
{
    /// <summary>TTL for a key that has a non-empty value in the DB.</summary>
    public static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    /// TTL for a key whose DB value is null or empty.  Short so that saving a
    /// setting for the first time takes effect within seconds.
    /// </summary>
    public static readonly TimeSpan EmptyCacheTtl = TimeSpan.FromSeconds(15);

    private readonly string? _cs;
    private readonly ILogger<TenantDbPlatformSettingStore> _log;

    private readonly Dictionary<string, (string? Value, DateTime Expires)>
        _cache = new(StringComparer.OrdinalIgnoreCase);

    private bool _tableEnsured;
    private readonly SemaphoreSlim _ensureLock = new(1, 1);

    public TenantDbPlatformSettingStore(IConfiguration config, ILogger<TenantDbPlatformSettingStore> log)
    {
        _cs  = config.GetConnectionString("TenantDb");
        _log = log;

        if (string.IsNullOrWhiteSpace(_cs))
            _log.LogWarning(
                "ConnectionStrings:TenantDb is not configured — platform setting store is disabled");
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_cs)) return null;

        if (_cache.TryGetValue(key, out var hit) && hit.Expires > DateTime.UtcNow)
            return hit.Value;

        try
        {
            await EnsureTableAsync(ct);

            await using var conn = new MySqlConnection(_cs);
            await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT `setting_value` FROM `platform_settings` WHERE `setting_key` = @k LIMIT 1";
            cmd.Parameters.AddWithValue("@k", key);

            var raw   = await cmd.ExecuteScalarAsync(ct);
            var value = raw as string;

            // Use a short TTL when the value is absent so that saving a setting for
            // the first time is reflected within seconds, not after the full 5 min TTL.
            var ttl = string.IsNullOrEmpty(value) ? EmptyCacheTtl : CacheTtl;
            _cache[key] = (value, DateTime.UtcNow.Add(ttl));
            return value;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to read platform setting key={Key}", key);
            return null;
        }
    }

    private async Task EnsureTableAsync(CancellationToken ct)
    {
        if (_tableEnsured) return;

        await _ensureLock.WaitAsync(ct);
        try
        {
            if (_tableEnsured) return;

            await using var conn = new MySqlConnection(_cs);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS `platform_settings` (
                    `setting_key`   VARCHAR(200) NOT NULL,
                    `setting_value` TEXT         NOT NULL DEFAULT '',
                    `updated_at`    DATETIME(3)  NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
                    PRIMARY KEY (`setting_key`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
            await cmd.ExecuteNonQueryAsync(ct);
            _tableEnsured = true;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to create platform_settings table; will retry on next request");
        }
        finally
        {
            _ensureLock.Release();
        }
    }
}
