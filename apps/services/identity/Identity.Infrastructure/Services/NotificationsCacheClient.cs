using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Services;

/// <summary>
/// Configuration for <see cref="NotificationsCacheClient"/>. Bound from
/// the <c>NotificationsService</c> configuration section:
///
///   "NotificationsService": {
///     "BaseUrl":              "http://notifications-service:5005",
///     "TimeoutSeconds":       30,
///     "InternalServiceToken": "shared-secret-here"   // matches notifications' INTERNAL_SERVICE_TOKEN
///   }
///
/// When <see cref="BaseUrl"/> is empty/unset, identity skips the call —
/// notifications then falls back to its TTL-based cache freshness. The
/// <see cref="InternalServiceToken"/> must match the value notifications
/// expects in the <c>X-Internal-Service-Token</c> header (enforced by
/// <c>InternalTokenMiddleware</c> on every <c>/internal/*</c> route).
/// </summary>
public sealed class NotificationsServiceOptions
{
    public const string SectionName = "NotificationsService";

    public string? BaseUrl              { get; set; }
    public int     TimeoutSeconds       { get; set; } = 30;
    public string? InternalServiceToken { get; set; }

    /// <summary>
    /// LS-ID-TNT-006: Base URL of the tenant portal, used to construct
    /// password-reset links embedded in emails.
    ///
    /// Example: https://portal.legalsynq.com
    ///
    /// Leave empty if email delivery is not yet configured; the admin-reset-
    /// password endpoint will use the environment-gated token fallback instead.
    ///
    /// When <see cref="PortalBaseDomain"/> is also set, it takes precedence over
    /// this value for tenant-subdomain URL construction.
    /// </summary>
    public string? PortalBaseUrl { get; set; }

    /// <summary>
    /// LS-ID-TNT-016-01: Base domain for tenant-subdomain portal URLs in user-management emails.
    ///
    /// When set, all user-management email links are generated as:
    ///   https://{tenantSubdomain}.{PortalBaseDomain}/{path}?token=...
    ///
    /// Example: "demo.legalsynq.com" → "https://acme.demo.legalsynq.com/accept-invite?token=..."
    ///
    /// When empty or unset, the legacy <see cref="PortalBaseUrl"/> value is used instead.
    /// Set this in production to the same base domain used for tenant subdomains (Route53.BaseDomain).
    /// Leave empty in development to continue using PortalBaseUrl (localhost).
    /// </summary>
    public string? PortalBaseDomain { get; set; }
}

/// <summary>
/// Notifies the notifications service that role/membership state for a
/// tenant has changed so it can invalidate its in-process membership cache.
/// All calls are fire-and-observe: failures are logged but never gate the
/// caller (identity admin endpoints / role assignment services).
///
/// This is what lets the notifications service keep a long cache TTL for
/// cost reasons while still reflecting role-membership changes immediately
/// for high-stakes alerts (e.g. on-call notifications).
/// </summary>
public interface INotificationsCacheClient
{
    void InvalidateTenant(Guid tenantId, string eventType, string? reason = null);
}

/// <summary>
/// Operator-facing snapshot of identity → notifications invalidation activity.
/// Surfaced via <c>GET /api/admin/notifications-cache/status</c> so ops can
/// verify the wiring (base URL + shared token) is healthy without having to
/// inspect logs on both services.
/// </summary>
public sealed record NotificationsCacheClientStatsSnapshot
{
    /// <summary>True when identity has a NotificationsService base URL set.
    /// When false, every invalidation call short-circuits (the no-op client
    /// is wired) and notifications relies on its TTL for freshness.</summary>
    public bool   Configured                  { get; init; }

    /// <summary>Total number of invalidation calls dispatched to notifications
    /// since process start (skipped/empty-tenant calls are not counted).</summary>
    public long   AttemptedInvalidations      { get; init; }

    /// <summary>Calls that returned a 2xx response.</summary>
    public long   SucceededInvalidations      { get; init; }

    /// <summary>Calls that failed (non-2xx, timeout, network, parse).</summary>
    public long   FailedInvalidations         { get; init; }

    /// <summary>UTC timestamp of the most recent failure (null when none).</summary>
    public DateTime? LastFailureUtc           { get; init; }

    /// <summary>Short human-readable reason for the most recent failure
    /// (HTTP status or exception type). Null when there have been no failures.</summary>
    public string?   LastFailureReason       { get; init; }
}

/// <summary>Snapshot accessor for notifications-cache invalidation counters.</summary>
public interface INotificationsCacheClientDiagnostics
{
    NotificationsCacheClientStatsSnapshot GetSnapshot();
}

public sealed class NotificationsCacheClient : INotificationsCacheClient, INotificationsCacheClientDiagnostics
{
    // Matches the header enforced by Notifications.Api InternalTokenMiddleware
    // for every /internal/* route. Without it the call is rejected (401/503)
    // and notifications falls back to its TTL-based cache freshness.
    private const string TokenHeader = "X-Internal-Service-Token";

    private readonly IHttpClientFactory                 _httpClientFactory;
    private readonly NotificationsServiceOptions        _options;
    private readonly ILogger<NotificationsCacheClient>  _logger;

    // Operator-facing counters. All updates use Interlocked so the snapshot
    // surfaced via /api/admin/notifications-cache/status stays consistent
    // under concurrent admin operations.
    private long    _attempted;
    private long    _succeeded;
    private long    _failed;
    private long    _lastFailureUtcTicks;
    private string? _lastFailureReason;

    public NotificationsCacheClient(
        IHttpClientFactory                 httpClientFactory,
        IOptions<NotificationsServiceOptions> options,
        ILogger<NotificationsCacheClient>  logger)
    {
        _httpClientFactory = httpClientFactory;
        _options           = options.Value;
        _logger            = logger;
    }

    public void InvalidateTenant(Guid tenantId, string eventType, string? reason = null)
    {
        if (tenantId == Guid.Empty) return;
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _logger.LogDebug(
                "NotificationsService:BaseUrl not configured; skipping cache invalidation for tenant {TenantId}.",
                tenantId);
            return;
        }

        Interlocked.Increment(ref _attempted);

        // Fire-and-observe: never block the originating admin call. Identity
        // emits the canonical audit event regardless of this side-effect.
        _ = Task.Run(async () =>
        {
            try
            {
                using var client = _httpClientFactory.CreateClient("NotificationsService");
                client.BaseAddress = new Uri(_options.BaseUrl!.TrimEnd('/') + "/");
                client.Timeout     = TimeSpan.FromSeconds(_options.TimeoutSeconds);

                if (!string.IsNullOrWhiteSpace(_options.InternalServiceToken))
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation(
                        TokenHeader, _options.InternalServiceToken);
                }

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));

                using var response = await client.PostAsJsonAsync(
                    "internal/membership-cache/invalidate",
                    new { tenantId, eventType, reason },
                    cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    RecordFailure($"HTTP {(int)response.StatusCode}");
                    _logger.LogWarning(
                        "Notifications membership-cache invalidate returned HTTP {Status} for tenant {TenantId} ({EventType}). " +
                        "Total invalidation failures: {FailedInvalidations}.",
                        (int)response.StatusCode, tenantId, eventType, Interlocked.Read(ref _failed));
                }
                else
                {
                    Interlocked.Increment(ref _succeeded);
                }
            }
            catch (Exception ex)
            {
                RecordFailure(ex.GetType().Name);
                _logger.LogWarning(ex,
                    "Notifications membership-cache invalidate failed for tenant {TenantId} ({EventType}). " +
                    "Notifications will rely on TTL-based freshness for this change. " +
                    "Total invalidation failures: {FailedInvalidations}.",
                    tenantId, eventType, Interlocked.Read(ref _failed));
            }
        });
    }

    private void RecordFailure(string reason)
    {
        Interlocked.Increment(ref _failed);
        Interlocked.Exchange(ref _lastFailureUtcTicks, DateTime.UtcNow.Ticks);
        Volatile.Write(ref _lastFailureReason, reason);
    }

    /// <summary>Snapshot of invalidation counters for the operator status endpoint.</summary>
    public NotificationsCacheClientStatsSnapshot GetSnapshot()
    {
        var lastTicks = Interlocked.Read(ref _lastFailureUtcTicks);
        return new NotificationsCacheClientStatsSnapshot
        {
            Configured             = !string.IsNullOrWhiteSpace(_options.BaseUrl),
            AttemptedInvalidations = Interlocked.Read(ref _attempted),
            SucceededInvalidations = Interlocked.Read(ref _succeeded),
            FailedInvalidations    = Interlocked.Read(ref _failed),
            LastFailureUtc         = lastTicks == 0 ? null : new DateTime(lastTicks, DateTimeKind.Utc),
            LastFailureReason      = Volatile.Read(ref _lastFailureReason),
        };
    }
}

/// <summary>
/// No-op fallback used when notifications integration is not configured.
/// Logs at debug level so test/dev environments stay quiet. Still satisfies
/// <see cref="INotificationsCacheClientDiagnostics"/> so the operator status
/// endpoint can clearly report "not configured" instead of 404'ing.
/// </summary>
public sealed class NoOpNotificationsCacheClient : INotificationsCacheClient, INotificationsCacheClientDiagnostics
{
    public void InvalidateTenant(Guid tenantId, string eventType, string? reason = null) { }

    public NotificationsCacheClientStatsSnapshot GetSnapshot() => new()
    {
        Configured             = false,
        AttemptedInvalidations = 0,
        SucceededInvalidations = 0,
        FailedInvalidations    = 0,
        LastFailureUtc         = null,
        LastFailureReason      = null,
    };
}
