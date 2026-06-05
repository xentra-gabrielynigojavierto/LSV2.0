using System.Collections.Concurrent;
using BuildingBlocks.Authorization;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Identity.Infrastructure.Services;

public class RedisPolicyVersionProvider : IPolicyVersionProvider
{
    private const string GlobalVersionKey = "legalsynq:policy:version";
    private const string TenantVersionKeyPrefix = "legalsynq:policy:version:";
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisPolicyVersionProvider> _logger;
    private readonly PolicyMetrics _metrics;

    private long _lastKnownGlobalVersion;
    private readonly ConcurrentDictionary<string, long> _lastKnownTenantVersions = new(StringComparer.OrdinalIgnoreCase);
    private volatile bool _frozen;

    public RedisPolicyVersionProvider(
        IConnectionMultiplexer redis,
        ILogger<RedisPolicyVersionProvider> logger,
        PolicyMetrics metrics)
    {
        _redis = redis;
        _logger = logger;
        _metrics = metrics;
    }

    public bool IsHealthy => !_frozen;
    public bool IsFrozen => _frozen;

    public long CurrentVersion
    {
        get => GetVersion(null);
    }

    public void Increment()
    {
        IncrementVersion(null);
    }

    public long GetVersion(string? tenantId = null)
    {
        var key = BuildKey(tenantId);
        try
        {
            var db = _redis.GetDatabase();
            var val = db.StringGet(key);
            if (val.TryParse(out long version))
            {
                SetLastKnown(tenantId, version);
                if (_frozen)
                {
                    _frozen = false;
                    _logger.LogInformation("PolicyVersionProvider: Redis recovered — exiting freeze mode");
                }
                return version;
            }
            return 0;
        }
        catch (Exception ex)
        {
            var lastKnown = GetLastKnown(tenantId);
            if (!_frozen)
            {
                _frozen = true;
                _metrics.RecordFreezeEvent();
                _logger.LogWarning(ex,
                    "PolicyVersionProvider: Redis read failed — entering FREEZE mode at version {Version} for key {Key}. Cache writes disabled.",
                    lastKnown, key);
            }
            return lastKnown;
        }
    }

    public void IncrementVersion(string? tenantId = null)
    {
        if (_frozen)
        {
            _logger.LogWarning(
                "PolicyVersionProvider: Increment skipped — provider is FROZEN (Redis unavailable). Version remains at {Version}",
                GetLastKnown(tenantId));
            return;
        }

        var key = BuildKey(tenantId);
        try
        {
            var db = _redis.GetDatabase();
            var newVersion = db.StringIncrement(key);
            SetLastKnown(tenantId, newVersion);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "PolicyVersionProvider: Redis increment failed for key {Key} — retrying once", key);
            try
            {
                var db = _redis.GetDatabase();
                var newVersion = db.StringIncrement(key);
                SetLastKnown(tenantId, newVersion);
            }
            catch (Exception retryEx)
            {
                _frozen = true;
                _metrics.RecordFreezeEvent();
                _logger.LogWarning(retryEx,
                    "PolicyVersionProvider: Redis increment retry failed — entering FREEZE mode. Version frozen at {Version}",
                    GetLastKnown(tenantId));
            }
        }
    }

    private static string BuildKey(string? tenantId)
    {
        return string.IsNullOrEmpty(tenantId) ? GlobalVersionKey : $"{TenantVersionKeyPrefix}{tenantId}";
    }

    private long GetLastKnown(string? tenantId)
    {
        if (string.IsNullOrEmpty(tenantId))
            return Interlocked.Read(ref _lastKnownGlobalVersion);
        return _lastKnownTenantVersions.GetValueOrDefault(tenantId, 0);
    }

    private void SetLastKnown(string? tenantId, long version)
    {
        if (string.IsNullOrEmpty(tenantId))
            Interlocked.Exchange(ref _lastKnownGlobalVersion, version);
        else
            _lastKnownTenantVersions[tenantId] = version;
    }
}
