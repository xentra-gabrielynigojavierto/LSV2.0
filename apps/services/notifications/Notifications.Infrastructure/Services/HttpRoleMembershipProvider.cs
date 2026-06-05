using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// Production <see cref="IRoleMembershipProvider"/> that resolves role- and
/// org-addressed notification recipients by calling the Identity service:
///
///   GET {BaseUrl}/api/admin/membership-lookup
///         ?tenantId={tid}&amp;roleKey={key}&amp;orgId={oid}
///
/// Lookups are tenant-scoped (Identity enforces tenant isolation server side)
/// and cached briefly in-process (TTL configurable via
/// <see cref="IdentityServiceOptions.MembershipCacheSeconds"/>) so a burst of
/// fan-out events does not hammer Identity for the same role/org pair.
///
/// All failures (network, 4xx/5xx, timeout, parse) return an empty member set
/// — the caller treats that as a blocked envelope and persists the outcome.
/// </summary>
public sealed class HttpRoleMembershipProvider : IRoleMembershipProvider, IMembershipCacheDiagnostics
{
    private readonly IHttpClientFactory                       _httpClientFactory;
    private readonly IdentityServiceOptions                   _options;
    private readonly IMemoryCache                             _cache;
    private readonly ILogger<HttpRoleMembershipProvider>      _logger;
    private readonly TimeSpan                                 _cacheTtl;

    // Per-tenant version stamp included in cache keys. Bumping the value
    // for a tenant invalidates every membership entry cached for that tenant
    // because subsequent lookups compute a different key. Old entries fall
    // out naturally when their TTL elapses.
    private readonly ConcurrentDictionary<Guid, long> _tenantVersions = new();

    // Operator-facing counters. All updates use Interlocked so the snapshot
    // surfaced via /internal/membership-cache/stats stays consistent under
    // concurrent fan-out without any locking on the hot path.
    private long      _hits;
    private long      _misses;
    private long      _invalidations;
    private long      _lastInvalidationUtcTicks;

    public HttpRoleMembershipProvider(
        IHttpClientFactory                  httpClientFactory,
        IOptions<IdentityServiceOptions>    options,
        IMemoryCache                        cache,
        ILogger<HttpRoleMembershipProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options           = options.Value;
        _cache             = cache;
        _logger            = logger;
        _cacheTtl          = TimeSpan.FromSeconds(Math.Max(0, _options.MembershipCacheSeconds));
    }

    public Task<IReadOnlyList<ResolvedRecipient>> GetRoleMembersAsync(
        Guid tenantId, string roleKey, string? orgId) =>
        LookupAsync(tenantId, roleKey, orgId);

    public Task<IReadOnlyList<ResolvedRecipient>> GetOrgMembersAsync(
        Guid tenantId, string orgId) =>
        LookupAsync(tenantId, roleKey: null, orgId);

    /// <summary>
    /// Invalidate every cached membership entry for <paramref name="tenantId"/>
    /// by bumping the per-tenant version stamp embedded in cache keys. Existing
    /// entries become unreachable and the next lookup re-fetches from identity.
    /// Called by the internal membership-cache invalidation endpoint when
    /// identity emits a role/membership/user-deactivation change event.
    /// </summary>
    public void InvalidateTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty) return;
        var newVersion = _tenantVersions.AddOrUpdate(tenantId, 1L, (_, v) => v + 1);
        var totalInvalidations = Interlocked.Increment(ref _invalidations);
        Interlocked.Exchange(ref _lastInvalidationUtcTicks, DateTime.UtcNow.Ticks);
        _logger.LogInformation(
            "Membership cache invalidated for tenant {TenantId} (version now {Version}, total invalidations {TotalInvalidations}).",
            tenantId, newVersion, totalInvalidations);
    }

    /// <summary>
    /// Snapshot of cache hit / miss / invalidation counters for the operator
    /// status endpoint. Cheap (only reads volatile counters); safe to poll.
    /// </summary>
    public MembershipCacheStatsSnapshot GetSnapshot()
    {
        var lastTicks = Interlocked.Read(ref _lastInvalidationUtcTicks);
        return new MembershipCacheStatsSnapshot
        {
            IdentityConfigured  = !string.IsNullOrWhiteSpace(_options.BaseUrl),
            CacheTtlSeconds     = (int)_cacheTtl.TotalSeconds,
            Hits                = Interlocked.Read(ref _hits),
            Misses              = Interlocked.Read(ref _misses),
            Invalidations       = Interlocked.Read(ref _invalidations),
            LastInvalidationUtc = lastTicks == 0 ? null : new DateTime(lastTicks, DateTimeKind.Utc),
        };
    }

    private async Task<IReadOnlyList<ResolvedRecipient>> LookupAsync(
        Guid tenantId, string? roleKey, string? orgId)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _logger.LogDebug("IdentityService:BaseUrl not configured; returning empty membership set.");
            return Array.Empty<ResolvedRecipient>();
        }

        var version  = _tenantVersions.GetOrAdd(tenantId, 0L);
        var cacheKey = $"notif:membership|{tenantId:N}|v={version}|r={roleKey?.ToLowerInvariant() ?? "*"}|o={orgId ?? "*"}";

        if (_cacheTtl > TimeSpan.Zero &&
            _cache.TryGetValue(cacheKey, out IReadOnlyList<ResolvedRecipient>? cached) &&
            cached is not null)
        {
            Interlocked.Increment(ref _hits);
            _logger.LogDebug(
                "Membership cache HIT for tenant {TenantId} (roleKey={RoleKey}, orgId={OrgId}).",
                tenantId, roleKey ?? "(null)", orgId ?? "(null)");
            return cached;
        }

        Interlocked.Increment(ref _misses);
        _logger.LogDebug(
            "Membership cache MISS for tenant {TenantId} (roleKey={RoleKey}, orgId={OrgId}); fetching from identity.",
            tenantId, roleKey ?? "(null)", orgId ?? "(null)");

        var members = await FetchAsync(tenantId, roleKey, orgId);

        if (_cacheTtl > TimeSpan.Zero)
        {
            _cache.Set(cacheKey, members, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _cacheTtl,
                Size = members.Count + 1,
            });
        }

        return members;
    }

    private async Task<IReadOnlyList<ResolvedRecipient>> FetchAsync(
        Guid tenantId, string? roleKey, string? orgId)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient("IdentityService");
            client.BaseAddress = new Uri(_options.BaseUrl!.TrimEnd('/') + "/");
            client.Timeout     = TimeSpan.FromSeconds(_options.TimeoutSeconds);

            if (!string.IsNullOrWhiteSpace(_options.AuthHeaderName) &&
                !string.IsNullOrWhiteSpace(_options.AuthHeaderValue))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation(
                    _options.AuthHeaderName, _options.AuthHeaderValue);
            }

            var url = $"api/admin/membership-lookup?tenantId={tenantId:D}";
            if (!string.IsNullOrWhiteSpace(roleKey))
                url += $"&roleKey={Uri.EscapeDataString(roleKey)}";
            if (!string.IsNullOrWhiteSpace(orgId))
                url += $"&orgId={Uri.EscapeDataString(orgId)}";

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            using var response = await client.GetAsync(url, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Identity membership-lookup returned HTTP {Status} for tenant {TenantId} " +
                    "(roleKey={RoleKey}, orgId={OrgId}). Treating as empty membership.",
                    (int)response.StatusCode, tenantId, roleKey ?? "(null)", orgId ?? "(null)");
                return Array.Empty<ResolvedRecipient>();
            }

            var body = await response.Content.ReadFromJsonAsync<MembershipLookupResponse>(
                cancellationToken: cts.Token);

            if (body?.Items is null || body.Items.Count == 0)
                return Array.Empty<ResolvedRecipient>();

            var result = new List<ResolvedRecipient>(body.Items.Count);
            foreach (var item in body.Items)
            {
                if (item.UserId == Guid.Empty && string.IsNullOrEmpty(item.Email))
                    continue;
                result.Add(new ResolvedRecipient
                {
                    UserId = item.UserId == Guid.Empty ? null : item.UserId.ToString("D"),
                    Email  = item.Email,
                    Phone  = string.IsNullOrWhiteSpace(item.Phone) ? null : item.Phone!.Trim(),
                    OrgId  = item.OrganizationId?.ToString("D") ?? orgId,
                });
            }
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Identity membership-lookup timed out after {TimeoutSeconds}s for tenant {TenantId} " +
                "(roleKey={RoleKey}, orgId={OrgId}). Treating as empty membership.",
                _options.TimeoutSeconds, tenantId, roleKey ?? "(null)", orgId ?? "(null)");
            return Array.Empty<ResolvedRecipient>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Identity membership-lookup failed for tenant {TenantId} " +
                "(roleKey={RoleKey}, orgId={OrgId}). Treating as empty membership.",
                tenantId, roleKey ?? "(null)", orgId ?? "(null)");
            return Array.Empty<ResolvedRecipient>();
        }
    }

    private sealed class MembershipLookupResponse
    {
        [JsonPropertyName("items")]
        public List<MembershipLookupItem>? Items { get; set; }

        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }
    }

    private sealed class MembershipLookupItem
    {
        [JsonPropertyName("userId")]
        public Guid UserId { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("phone")]
        public string? Phone { get; set; }

        [JsonPropertyName("organizationId")]
        public Guid? OrganizationId { get; set; }
    }
}
