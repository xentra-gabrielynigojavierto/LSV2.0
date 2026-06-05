namespace PlatformAuditEventService.Authorization;

/// <summary>
/// Concrete, immutable implementation of <see cref="IQueryCallerContext"/>.
///
/// Stored in <c>HttpContext.Items</c> under <see cref="ItemKey"/> by
/// <see cref="Middleware.QueryAuthMiddleware"/> after the resolver produces it.
/// Controllers and the authorizer read it from there — they do not call the resolver directly.
/// </summary>
public sealed record QueryCallerContext : IQueryCallerContext
{
    /// <summary>Key used to store/retrieve this context from <c>HttpContext.Items</c>.</summary>
    public const string ItemKey = "QueryAuth.CallerContext";

    /// <inheritdoc/>
    public CallerScope Scope { get; init; } = CallerScope.Unknown;

    /// <inheritdoc/>
    public bool IsAuthenticated { get; init; }

    /// <inheritdoc/>
    public string? TenantId { get; init; }

    /// <inheritdoc/>
    public string? OrganizationId { get; init; }

    /// <inheritdoc/>
    public string? UserId { get; init; }

    /// <inheritdoc/>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <inheritdoc/>
    public string AuthMode { get; init; } = "None";

    // ── Factory helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates a dev-mode anonymous context with Unknown scope.
    /// Only produced when <c>QueryAuth:Mode = "None"</c>.
    /// Access is denied by the middleware gate for all protected paths.
    /// </summary>
    public static QueryCallerContext Anonymous() =>
        new()
        {
            Scope           = CallerScope.Unknown,
            IsAuthenticated = false,
            AuthMode        = "None",
        };

    /// <summary>
    /// Creates a context representing an authenticated caller with a resolved scope.
    /// </summary>
    public static QueryCallerContext Authenticated(
        CallerScope            scope,
        string?                tenantId,
        string?                organizationId,
        string?                userId,
        IReadOnlyList<string>  roles,
        string                 authMode) =>
        new()
        {
            Scope           = scope,
            IsAuthenticated = true,
            TenantId        = tenantId,
            OrganizationId  = organizationId,
            UserId          = userId,
            Roles           = roles,
            AuthMode        = authMode,
        };

    /// <summary>
    /// Creates an unauthenticated/failed context when resolution fails.
    /// All access will be denied by the authorizer.
    /// </summary>
    public static QueryCallerContext Failed(string authMode) =>
        new()
        {
            Scope           = CallerScope.Unknown,
            IsAuthenticated = false,
            AuthMode        = authMode,
        };
}
