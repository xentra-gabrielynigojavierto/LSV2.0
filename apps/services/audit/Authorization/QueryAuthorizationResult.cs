namespace PlatformAuditEventService.Authorization;

/// <summary>
/// Result of an authorization check by <see cref="IQueryAuthorizer"/>.
///
/// When <see cref="IsAuthorized"/> is true, the query has been mutated in-place
/// to enforce scope constraints. The controller should proceed to the query service.
///
/// When <see cref="IsAuthorized"/> is false, the controller should short-circuit
/// and return the appropriate HTTP status code with <see cref="DenialReason"/> as
/// the response body message.
/// </summary>
public sealed record QueryAuthorizationResult
{
    /// <summary>Whether the caller is authorized to proceed with this query.</summary>
    public bool IsAuthorized { get; init; }

    /// <summary>
    /// Human-readable reason for the denial. Null when <see cref="IsAuthorized"/> is true.
    /// Safe to surface in API responses — does not leak internal state.
    /// </summary>
    public string? DenialReason { get; init; }

    /// <summary>
    /// HTTP status code to use when returning the error.
    /// 401 for unauthenticated; 403 for authenticated but unauthorized.
    /// Zero when <see cref="IsAuthorized"/> is true.
    /// </summary>
    public int StatusCode { get; init; }

    // ── Pre-built result factories ─────────────────────────────────────────────

    /// <summary>Authorization passed. Query has been constrained in-place.</summary>
    public static QueryAuthorizationResult Allowed() =>
        new() { IsAuthorized = true };

    /// <summary>Authenticated caller is denied access to this resource or scope.</summary>
    public static QueryAuthorizationResult Forbidden(string reason) =>
        new() { IsAuthorized = false, DenialReason = reason, StatusCode = StatusCodes.Status403Forbidden };

    /// <summary>No valid credentials were presented.</summary>
    public static QueryAuthorizationResult Unauthenticated(string? reason = null) =>
        new()
        {
            IsAuthorized = false,
            DenialReason = reason ?? "Authentication is required to access audit records.",
            StatusCode   = StatusCodes.Status401Unauthorized,
        };
}
