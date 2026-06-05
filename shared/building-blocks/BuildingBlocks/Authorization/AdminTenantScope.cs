// BLK-GOV-02: Centralized tenant-scope guard for PlatformOrTenantAdmin endpoints.
//
// Replaces the fragile inline patterns scattered across admin endpoint handlers:
//
//   BEFORE (repeated everywhere):
//     Guid? scopeTenantId = ctx.IsPlatformAdmin
//         ? null
//         : (ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing."));
//
//   AFTER:
//     var scope = AdminTenantScope.PlatformWide(ctx);
//     if (scope.IsError) return scope.Error!;
//     Guid? scopeTenantId = scope.TenantId;
//
// Three resolution modes:
//   PlatformWide      — PlatformAdmin: null (platform-wide); TenantAdmin: ctx.TenantId
//   SingleTenant      — PlatformAdmin: must supply explicitTenantId (400 if missing); TenantAdmin: ctx.TenantId
//   CheckOwnership    — PlatformAdmin: always allowed; TenantAdmin: allowed only if resource.TenantId == ctx.TenantId
//
// Governance denial visibility (BLK-GOV-02 Part G):
//   When a governance denial is produced, a Warning-level structured log entry is emitted
//   using the service-locator pattern from HttpContext.RequestServices (same pattern as
//   RequireProductRoleFilter).  Passing httpContext is optional but strongly recommended for
//   production observability.
//
// BLK-COMP-01: Cross-tenant ownership denials (CheckOwnership) also emit a canonical
//   security.governance.denial audit event via IAuditEventClient (GetService — optional,
//   never throws). This makes every cross-tenant access attempt reconstructable under audit.
using BuildingBlocks.Context;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace BuildingBlocks.Authorization;

/// <summary>
/// BLK-GOV-02: Centralized tenant-scope resolver for PlatformOrTenantAdmin endpoints.
/// </summary>
public static class AdminTenantScope
{
    private const string LogCategory = "BuildingBlocks.Authorization.AdminTenantScope";

    // ──────────────────────────────────────────────────────────────────────────
    // Mode 1 — PlatformWide
    // ──────────────────────────────────────────────────────────────────────────
    // PlatformAdmin  → TenantId = null  (platform-wide; no tenant filter applied)
    // TenantAdmin    → TenantId = ctx.TenantId (always scoped to their own tenant)
    //
    // Use for: analytics, dashboards, performance metrics, activation queue reads —
    //          any operation where PlatformAdmin legitimately views all tenants.
    // ──────────────────────────────────────────────────────────────────────────
    public static AdminScopeResult PlatformWide(
        ICurrentRequestContext ctx,
        HttpContext?            httpContext = null)
    {
        if (ctx.IsPlatformAdmin)
            return AdminScopeResult.Success(tenantId: null, isPlatformWide: true);

        if (ctx.TenantId is null)
        {
            GetLogger(httpContext)?.LogWarning(
                "GovernanceDenial: TenantAdmin userId={UserId} has no tenant_id claim (PlatformWide).",
                ctx.UserId);
            throw new InvalidOperationException(
                "Governance[PlatformWide]: TenantAdmin caller is missing the tenant_id claim.");
        }

        return AdminScopeResult.Success(ctx.TenantId, isPlatformWide: false);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Mode 2 — SingleTenant
    // ──────────────────────────────────────────────────────────────────────────
    // PlatformAdmin  → TenantId = explicitTenantId; returns 400 if not supplied.
    // TenantAdmin    → TenantId = ctx.TenantId; explicitTenantId is ignored for safety.
    //
    // Use for: backfill operations, bulk-link, single-provider admin actions —
    //          any mutation that must operate on exactly one tenant.
    // ──────────────────────────────────────────────────────────────────────────
    public static AdminScopeResult SingleTenant(
        ICurrentRequestContext ctx,
        Guid?                  explicitTenantId,
        HttpContext?            httpContext = null)
    {
        if (ctx.IsPlatformAdmin)
        {
            if (explicitTenantId is null)
            {
                GetLogger(httpContext)?.LogWarning(
                    "GovernanceDenial: PlatformAdmin userId={UserId} called single-tenant endpoint " +
                    "at {Path} without supplying ?tenantId.",
                    ctx.UserId,
                    httpContext?.Request.Path.Value);
                return AdminScopeResult.MissingTenantId();
            }
            return AdminScopeResult.Success(explicitTenantId.Value, isPlatformWide: false);
        }

        if (ctx.TenantId is null)
        {
            GetLogger(httpContext)?.LogWarning(
                "GovernanceDenial: TenantAdmin userId={UserId} has no tenant_id claim (SingleTenant).",
                ctx.UserId);
            throw new InvalidOperationException(
                "Governance[SingleTenant]: TenantAdmin caller is missing the tenant_id claim.");
        }

        return AdminScopeResult.Success(ctx.TenantId.Value, isPlatformWide: false);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Mode 3 — CheckOwnership
    // ──────────────────────────────────────────────────────────────────────────
    // PlatformAdmin  → always allowed (returns null).
    // TenantAdmin    → allowed only when ctx.TenantId == resourceTenantId;
    //                  returns Results.Forbid() for cross-tenant access.
    //
    // Use for: activation approval, activation detail — any operation where the
    //          caller is verifying they own the specific resource being accessed.
    //
    // Returns null when access is granted; returns an IResult to return
    // immediately when access is denied.
    // ──────────────────────────────────────────────────────────────────────────
    public static IResult? CheckOwnership(
        ICurrentRequestContext ctx,
        Guid                   resourceTenantId,
        HttpContext?            httpContext = null)
    {
        if (ctx.IsPlatformAdmin)
            return null; // PlatformAdmin can access any tenant's resource.

        if (ctx.TenantId is null)
        {
            GetLogger(httpContext)?.LogWarning(
                "GovernanceDenial: TenantAdmin userId={UserId} has no tenant_id claim (CheckOwnership).",
                ctx.UserId);
            throw new InvalidOperationException(
                "Governance[CheckOwnership]: TenantAdmin caller is missing the tenant_id claim.");
        }

        if (ctx.TenantId.Value != resourceTenantId)
        {
            GetLogger(httpContext)?.LogWarning(
                "GovernanceDenial: TenantAdmin userId={UserId} tenant={CallerTenantId} attempted " +
                "cross-tenant access to resource owned by tenant={ResourceTenantId} at {Path}.",
                ctx.UserId,
                ctx.TenantId.Value,
                resourceTenantId,
                httpContext?.Request.Path.Value);

            // BLK-COMP-01: Emit canonical security.governance.denial audit event so that
            // every cross-tenant access attempt is reconstructable under SOC 2 / HIPAA audit.
            // Fire-and-observe (discard) — never gates the deny response on audit success.
            _ = GetAuditClient(httpContext)?.IngestAsync(new IngestAuditEventRequest
            {
                EventType     = "security.governance.denial",
                EventCategory = EventCategory.Security,
                SourceSystem  = "platform",
                SourceService = "admin-tenant-scope",
                Visibility    = VisibilityScope.Platform,
                Severity      = SeverityLevel.Warn,
                OccurredAtUtc = DateTimeOffset.UtcNow,
                Action        = "CrossTenantAccessDenied",
                Description   = "TenantAdmin attempted cross-tenant access to a resource " +
                                 "owned by a different tenant.",
                Outcome       = "denied",
                Actor = new AuditEventActorDto
                {
                    Type = ActorType.User,
                    Id   = ctx.UserId?.ToString(),
                },
                Scope = new AuditEventScopeDto
                {
                    ScopeType = ScopeType.Tenant,
                    TenantId  = ctx.TenantId.Value.ToString(),
                    UserId    = ctx.UserId?.ToString(),
                },
                CorrelationId = httpContext?.Items["CorrelationId"]?.ToString(),
                Metadata      = JsonSerializer.Serialize(new
                {
                    callerTenantId   = ctx.TenantId.Value,
                    resourceTenantId = resourceTenantId,
                    path             = httpContext?.Request.Path.Value,
                }),
                Tags = ["security", "governance", "cross-tenant", "denial"],
            });

            return Results.Forbid();
        }

        return null; // Same tenant — access granted.
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Internal helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static ILogger? GetLogger(HttpContext? httpContext)
    {
        if (httpContext is null) return null;
        var factory = httpContext.RequestServices
            .GetService(typeof(ILoggerFactory)) as ILoggerFactory;
        return factory?.CreateLogger(LogCategory);
    }

    /// <summary>
    /// BLK-COMP-01: Optional audit client resolution via service locator.
    /// Returns null when the host service has not registered IAuditEventClient —
    /// callers must use null-conditional operators (?.IngestAsync(...)).
    /// </summary>
    private static IAuditEventClient? GetAuditClient(HttpContext? httpContext)
    {
        if (httpContext is null) return null;
        return httpContext.RequestServices.GetService(typeof(IAuditEventClient)) as IAuditEventClient;
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// AdminScopeResult
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// BLK-GOV-02: Resolution result from <see cref="AdminTenantScope"/>.
/// </summary>
public readonly struct AdminScopeResult
{
    private AdminScopeResult(bool isError, Guid? tenantId, bool isPlatformWide, IResult? error)
    {
        IsError        = isError;
        TenantId       = tenantId;
        IsPlatformWide = isPlatformWide;
        Error          = error;
    }

    /// <summary>True when scope resolution failed and <see cref="Error"/> must be returned immediately.</summary>
    public bool IsError { get; }

    /// <summary>
    /// Resolved tenant scope.
    /// <c>null</c> only when <see cref="IsPlatformWide"/> is <c>true</c>
    /// (PlatformAdmin operating without a tenant filter).
    /// Always non-null for SingleTenant mode.
    /// </summary>
    public Guid? TenantId { get; }

    /// <summary>True when a PlatformAdmin caller is operating without tenant restriction.</summary>
    public bool IsPlatformWide { get; }

    /// <summary>The IResult to return to the caller when <see cref="IsError"/> is <c>true</c>.</summary>
    public IResult? Error { get; }

    internal static AdminScopeResult Success(Guid? tenantId, bool isPlatformWide) =>
        new(false, tenantId, isPlatformWide, null);

    internal static AdminScopeResult MissingTenantId() =>
        new(true, null, false,
            Results.BadRequest(new
            {
                error = "PlatformAdmin must supply ?tenantId=<guid>. " +
                        "This endpoint operates on a single tenant at a time.",
            }));
}
