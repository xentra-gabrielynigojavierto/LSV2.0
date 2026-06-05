using PlatformAuditEventService.DTOs.Query;

namespace PlatformAuditEventService.Authorization;

/// <summary>
/// Enforces authorization rules for query API requests.
///
/// Responsibilities (in order):
/// <list type="number">
///   <item>Verify the caller is authorized to make this type of query at all.</item>
///   <item>Apply scope constraints by mutating the query in-place:
///     <list type="bullet">
///       <item>Override <c>TenantId</c> for non-PlatformAdmin callers.</item>
///       <item>Override <c>OrganizationId</c> for OrganizationAdmin callers.</item>
///       <item>Override <c>ActorId</c> for UserSelf callers.</item>
///       <item>Set <c>MaxVisibility</c> based on the caller's scope.</item>
///     </list>
///   </item>
/// </list>
///
/// After a successful authorization, the mutated query object is safe
/// to pass directly to <see cref="Services.IAuditEventQueryService.QueryAsync"/>.
///
/// The authorizer is stateless and synchronous — no I/O or caching needed.
/// Register as a singleton.
/// </summary>
public interface IQueryAuthorizer
{
    /// <summary>
    /// Authorize the <paramref name="caller"/> to execute <paramref name="query"/>.
    ///
    /// When the result is <see cref="QueryAuthorizationResult.IsAuthorized"/> = true,
    /// the query has already been mutated to enforce scope constraints.
    ///
    /// When false, the query MUST NOT be executed. Return the HTTP status code
    /// and denial reason from the result directly to the client.
    /// </summary>
    QueryAuthorizationResult Authorize(
        IQueryCallerContext   caller,
        AuditEventQueryRequest query);
}
