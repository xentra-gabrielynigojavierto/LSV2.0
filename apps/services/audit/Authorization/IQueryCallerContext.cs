using PlatformAuditEventService.Enums;

namespace PlatformAuditEventService.Authorization;

/// <summary>
/// Runtime identity and authorization context for a single query API request.
///
/// Resolved once per request by <see cref="IQueryCallerResolver"/> and stored in
/// <c>HttpContext.Items</c> under <see cref="QueryCallerContext.ItemKey"/>.
///
/// Consumers (controllers, authorizer) must never store this beyond the request lifetime.
/// </summary>
public interface IQueryCallerContext
{
    /// <summary>
    /// The resolved conceptual authorization scope.
    /// Determines which enforcement rules apply to this caller's requests.
    /// </summary>
    CallerScope Scope { get; }

    /// <summary>
    /// Whether the caller has presented valid credentials.
    /// False for anonymous/dev callers when <c>QueryAuth:Mode = "None"</c>.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// The caller's tenant identifier, extracted from claims or context.
    /// Null for platform-admin callers who are not scoped to a tenant, or in anonymous mode.
    /// </summary>
    string? TenantId { get; }

    /// <summary>
    /// The caller's organization identifier within their tenant, if applicable.
    /// Meaningful when <see cref="Scope"/> is <see cref="CallerScope.OrganizationAdmin"/>.
    /// </summary>
    string? OrganizationId { get; }

    /// <summary>
    /// The caller's stable user identifier (e.g. the <c>sub</c> claim from OIDC).
    /// Used to enforce self-scope constraints when <see cref="Scope"/> is <see cref="CallerScope.UserSelf"/>.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// The raw roles the caller presented in their identity token.
    /// Used by <see cref="IQueryCallerResolver"/> to determine <see cref="Scope"/>.
    /// Available for downstream consumers that need fine-grained role checks.
    /// </summary>
    IReadOnlyList<string> Roles { get; }

    /// <summary>
    /// The auth resolution mode active for this request.
    /// Matches <c>QueryAuth:Mode</c> — e.g. "None", "Bearer".
    /// </summary>
    string AuthMode { get; }
}
