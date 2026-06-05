using Microsoft.Extensions.Options;
using PlatformAuditEventService.Configuration;
using PlatformAuditEventService.DTOs.Query;
using PlatformAuditEventService.Enums;

// Disambiguate: AuditEventQueryRequest exists in both DTOs and DTOs.Query
using AuditEventQueryRequest = PlatformAuditEventService.DTOs.Query.AuditEventQueryRequest;

namespace PlatformAuditEventService.Authorization;

/// <summary>
/// Canonical implementation of <see cref="IQueryAuthorizer"/>.
///
/// Enforcement is performed in two phases:
///
/// Phase 1 — Access check:
///   Determines whether the caller's scope permits this type of query at all.
///   Denials at this phase produce a 401 (unauthenticated) or 403 (insufficient scope).
///
/// Phase 2 — Constraint application:
///   Mutates the query in-place to enforce scope boundaries.
///   After this phase, the query is safe to execute regardless of what the caller
///   originally sent in the request.
///
/// Constraint rules:
/// <list type="bullet">
///   <item>Non-PlatformAdmin with a TenantId claim: <c>query.TenantId</c> is overridden to the caller's tenant.</item>
///   <item>OrganizationAdmin with an OrgId claim: <c>query.OrganizationId</c> is overridden.</item>
///   <item>UserSelf: <c>query.ActorId</c> is overridden to the caller's UserId.</item>
///   <item>Visibility floor: <c>query.MaxVisibility</c> is raised (made more restrictive) to the scope's minimum floor.</item>
/// </list>
///
/// The query object is mutated in-place so the scoped constraints are applied once,
/// centrally, before the query reaches the repository layer. No constraint re-application
/// is needed in the query service or repository.
/// </summary>
public sealed class QueryAuthorizer : IQueryAuthorizer
{
    private readonly QueryAuthOptions                  _opts;
    private readonly ILogger<QueryAuthorizer>          _logger;

    public QueryAuthorizer(
        IOptions<QueryAuthOptions>  opts,
        ILogger<QueryAuthorizer>    logger)
    {
        _opts   = opts.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public QueryAuthorizationResult Authorize(
        IQueryCallerContext    caller,
        AuditEventQueryRequest query)
    {
        // ── Phase 1: Access check ─────────────────────────────────────────────

        // Unknown scope = failed resolution. Require authentication.
        if (caller.Scope == CallerScope.Unknown)
        {
            _logger.LogWarning(
                "Query access denied — caller scope is Unknown (failed resolution). AuthMode={Mode}",
                caller.AuthMode);
            return caller.IsAuthenticated
                ? QueryAuthorizationResult.Forbidden("Your identity could not be mapped to a recognized authorization scope.")
                : QueryAuthorizationResult.Unauthenticated();
        }

        // UserSelf callers must have a resolved UserId to enforce self-scope.
        if (caller.Scope == CallerScope.UserSelf && string.IsNullOrWhiteSpace(caller.UserId))
        {
            _logger.LogWarning(
                "Query access denied — UserSelf scope but no UserId in claims. AuthMode={Mode}",
                caller.AuthMode);
            return QueryAuthorizationResult.Forbidden(
                "Self-scope access requires a resolvable user identifier in the identity token.");
        }

        // Non-PlatformAdmin callers cannot request records outside their own tenant.
        // If the query has a TenantId set to a different tenant than the caller's, deny.
        if (caller.Scope != CallerScope.PlatformAdmin
            && !string.IsNullOrWhiteSpace(caller.TenantId)
            && !string.IsNullOrWhiteSpace(query.TenantId)
            && !query.TenantId.Equals(caller.TenantId, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Query access denied — cross-tenant request. " +
                "Scope={Scope} CallerTenantId={CallerTenant} RequestedTenantId={RequestedTenant}",
                caller.Scope, caller.TenantId, query.TenantId);
            return QueryAuthorizationResult.Forbidden(
                "You are not authorized to access records outside your own tenant.");
        }

        // EnforceTenantScope: non-PlatformAdmin callers must have a tenant claim.
        if (_opts.EnforceTenantScope
            && caller.Scope != CallerScope.PlatformAdmin
            && string.IsNullOrWhiteSpace(caller.TenantId))
        {
            _logger.LogWarning(
                "Query access denied — EnforceTenantScope is true but caller has no TenantId. " +
                "Scope={Scope} AuthMode={Mode}",
                caller.Scope, caller.AuthMode);
            return QueryAuthorizationResult.Forbidden(
                "A tenant-scoped identity is required. Your token does not contain a tenant identifier.");
        }

        // ── Phase 2: Constraint application ──────────────────────────────────

        ApplyTenantConstraint(caller, query);
        ApplyOrganizationConstraint(caller, query);
        ApplyActorConstraint(caller, query);
        ApplyVisibilityConstraint(caller, query);

        _logger.LogDebug(
            "Query authorized. Scope={Scope} TenantId={Tenant} UserId={User} " +
            "EffectiveTenantId={EffTenant} EffectiveMaxVisibility={EffVis}",
            caller.Scope, caller.TenantId, caller.UserId,
            query.TenantId, query.MaxVisibility);

        return QueryAuthorizationResult.Allowed();
    }

    // ── Constraint application helpers ────────────────────────────────────────

    /// <summary>
    /// Overrides <c>query.TenantId</c> with the caller's tenant for all non-PlatformAdmin scopes.
    /// Prevents non-platform callers from accessing other tenants' data regardless of
    /// what they send in the query string.
    /// </summary>
    private static void ApplyTenantConstraint(
        IQueryCallerContext    caller,
        AuditEventQueryRequest query)
    {
        if (caller.Scope == CallerScope.PlatformAdmin) return;
        if (string.IsNullOrWhiteSpace(caller.TenantId)) return;

        // Override — caller's claim wins over anything in the request.
        query.TenantId = caller.TenantId;
    }

    /// <summary>
    /// Overrides <c>query.OrganizationId</c> with the caller's org for OrganizationAdmin scope.
    /// </summary>
    private static void ApplyOrganizationConstraint(
        IQueryCallerContext    caller,
        AuditEventQueryRequest query)
    {
        if (caller.Scope != CallerScope.OrganizationAdmin) return;
        if (string.IsNullOrWhiteSpace(caller.OrganizationId)) return;

        query.OrganizationId = caller.OrganizationId;
    }

    /// <summary>
    /// Overrides <c>query.ActorId</c> with the caller's UserId for UserSelf scope.
    /// Ensures the caller can only see their own activity regardless of the actorId param.
    /// </summary>
    private static void ApplyActorConstraint(
        IQueryCallerContext    caller,
        AuditEventQueryRequest query)
    {
        if (caller.Scope != CallerScope.UserSelf) return;

        // Override — self-scope always forces actorId = caller's own ID.
        query.ActorId = caller.UserId;
    }

    /// <summary>
    /// Sets <c>query.MaxVisibility</c> to the most restrictive of:
    ///   1. The floor dictated by the caller's scope.
    ///   2. The value already on the query (if the caller requested something more restrictive).
    ///
    /// Higher <see cref="VisibilityScope"/> numeric values = more restrictive.
    /// This ensures the floor is always applied, while respecting the caller's own restrictions.
    /// </summary>
    private static void ApplyVisibilityConstraint(
        IQueryCallerContext    caller,
        AuditEventQueryRequest query)
    {
        var floor = VisibilityFloorFor(caller.Scope);
        if (floor is null) return; // PlatformAdmin — no restriction.

        // Take the more restrictive of the scope floor and any existing value.
        query.MaxVisibility = query.MaxVisibility.HasValue
            ? (VisibilityScope)Math.Max((int)query.MaxVisibility.Value, (int)floor.Value)
            : floor.Value;
    }

    // ── Scope → visibility floor mapping ─────────────────────────────────────

    /// <summary>
    /// Returns the minimum (most restrictive) <see cref="VisibilityScope"/> the
    /// caller may query, based on their resolved scope.
    /// Returns null for <see cref="CallerScope.PlatformAdmin"/> (no restriction).
    /// </summary>
    private static VisibilityScope? VisibilityFloorFor(CallerScope scope) =>
        scope switch
        {
            CallerScope.PlatformAdmin    => null,                       // No floor — sees Platform, Tenant, Org, User
            CallerScope.TenantAdmin      => VisibilityScope.Tenant,     // Tenant, Org, User
            CallerScope.Restricted       => VisibilityScope.Tenant,     // Same as TenantAdmin but semantically read-only
            CallerScope.OrganizationAdmin => VisibilityScope.Organization, // Org, User
            CallerScope.TenantUser       => VisibilityScope.User,       // User only
            CallerScope.UserSelf         => VisibilityScope.User,       // User only, actor-constrained
            _                            => VisibilityScope.Internal,   // Unknown → effectively no access (Internal = never returned)
        };
}
