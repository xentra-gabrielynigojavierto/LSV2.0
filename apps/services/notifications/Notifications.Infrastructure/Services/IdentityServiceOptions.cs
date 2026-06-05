namespace Notifications.Infrastructure.Services;

/// <summary>
/// Configuration for the Identity service HTTP client used by
/// <see cref="HttpRoleMembershipProvider"/> when fanning out role/org
/// addressed notifications to concrete users.
///
/// Bind from appsettings via:
///   "IdentityService": {
///     "BaseUrl":           "http://identity-service:5001",
///     "TimeoutSeconds":    5,
///     "AuthHeaderName":    "X-Service-Token",   // optional
///     "AuthHeaderValue":   "my-secret-value",   // optional
///     "MembershipCacheSeconds": 60              // brief in-process cache TTL
///   }
///
/// When <see cref="BaseUrl"/> is empty/unset the notifications service
/// falls back to the in-memory provider (test/dev seeding) — no HTTP
/// calls are made.
/// </summary>
public sealed class IdentityServiceOptions
{
    public const string SectionName = "IdentityService";

    /// <summary>Base URL of the Identity service (e.g. http://identity:5001).</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Per-request HTTP timeout in seconds. Defaults to 5 s.</summary>
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>Optional service-to-service auth header name (e.g. "X-Service-Token").</summary>
    public string? AuthHeaderName { get; set; }

    /// <summary>Value of the service-to-service auth header. Set via secret.</summary>
    public string? AuthHeaderValue { get; set; }

    /// <summary>
    /// TTL for the in-process membership cache, in seconds. Defaults to 60 s.
    /// With identity-driven invalidation wired via
    /// <c>POST /internal/membership-cache/invalidate</c> this can stay long
    /// without sacrificing freshness.
    /// Set to 0 to disable caching (each fan-out hits identity).
    /// </summary>
    public int MembershipCacheSeconds { get; set; } = 60;
}
