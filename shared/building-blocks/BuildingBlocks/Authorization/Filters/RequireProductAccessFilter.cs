using System.Text.Json;
using BuildingBlocks.Exceptions;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Authorization.Filters;

public sealed class RequireProductAccessFilter : IEndpointFilter
{
    private readonly string _productCode;

    public RequireProductAccessFilter(string productCode)
    {
        _productCode = productCode;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var user = httpContext.User;

        if (user.Identity?.IsAuthenticated != true)
            return Results.Unauthorized();

        var userId = user.FindFirst("sub")?.Value;
        var tenantId = user.FindFirst("tenant_id")?.Value;
        var accessVersion = user.FindFirst("access_version")?.Value;
        var path = httpContext.Request.Path.Value;
        var method = httpContext.Request.Method;

        if (user.IsTenantAdminOrAbove())
        {
            LogAuthzDecision(httpContext, "ALLOW", userId, tenantId, path, method,
                _productCode, null, "AdminBypass", accessVersion);
            return await next(context);
        }

        if (!user.HasProductAccess(_productCode))
        {
            LogAuthzDecision(httpContext, "DENY", userId, tenantId, path, method,
                _productCode, null, "NoProductAccess", accessVersion);

            EmitProductAccessDenied(httpContext, userId, tenantId, _productCode, "NoProductAccess", path, method);

            return ProductAccessDeniedResult.Create(
                ProductAccessDeniedException.NoProductAccess(_productCode));
        }

        LogAuthzDecision(httpContext, "ALLOW", userId, tenantId, path, method,
            _productCode, null, "ProductClaim", accessVersion);

        return await next(context);
    }

    private static void LogAuthzDecision(
        HttpContext ctx, string result, string? userId, string? tenantId,
        string? path, string method, string product, string? requiredRole,
        string source, string? accessVersion)
    {
        var logger = ctx.RequestServices.GetService(typeof(ILogger<RequireProductAccessFilter>)) as ILogger;
        if (logger == null) return;

        if (result == "DENY")
        {
            logger.LogWarning(
                "AuthzDecision: result={Result} user={UserId} tenant={TenantId} method={Method} endpoint={Path} product={Product} source={Source} accessVersion={AccessVersion}",
                result, userId, tenantId, method, path, product, source, accessVersion);
        }
        else
        {
            logger.LogInformation(
                "AuthzDecision: result={Result} user={UserId} tenant={TenantId} method={Method} endpoint={Path} product={Product} source={Source} accessVersion={AccessVersion}",
                result, userId, tenantId, method, path, product, source, accessVersion);
        }
    }

    // ── Canonical audit emit ───────────────────────────────────────────────────

    /// <summary>
    /// Emits a <c>security.product.access.denied</c> canonical audit event.
    ///
    /// Fail-safe: <see cref="IAuditEventClient"/> is resolved optionally from DI.
    /// If it is not registered in the service container, this method returns without
    /// throwing. The Task is discarded (fire-and-observe). The denial flow is never
    /// gated on audit publish success.
    /// </summary>
    private static void EmitProductAccessDenied(
        HttpContext httpContext,
        string?     userId,
        string?     tenantId,
        string      productCode,
        string      denialReason,
        string?     endpoint,
        string      method)
    {
        var auditClient = httpContext.RequestServices.GetService(typeof(IAuditEventClient)) as IAuditEventClient;
        if (auditClient is null) return;

        var now = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "security.product.access.denied",
            EventCategory = EventCategory.Security,
            SourceSystem  = "authorization",
            SourceService = "require-product-access-filter",
            Visibility    = VisibilityScope.Tenant,
            Severity      = SeverityLevel.Warn,
            OccurredAtUtc = now,
            Scope = new AuditEventScopeDto
            {
                ScopeType = ScopeType.Tenant,
                TenantId  = tenantId,
            },
            Actor = new AuditEventActorDto
            {
                Id   = userId,
                Type = ActorType.User,
                Name = userId,
            },
            Entity      = new AuditEventEntityDto { Type = "Product", Id = productCode },
            Action      = "ProductAccessDenied",
            Description = $"Product access denied for product '{productCode}' — reason: {denialReason}.",
            Outcome     = "Failure",
            Metadata    = JsonSerializer.Serialize(new
            {
                productCode,
                denialReason,
                endpoint,
                method,
            }),
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(
                now, "authorization", "security.product.access.denied",
                $"{userId}:{productCode}"),
            Tags = ["security", "access-denied", "product"],
        });
    }
}

public sealed class RequireProductRoleFilter : IEndpointFilter
{
    private readonly string _productCode;
    private readonly IReadOnlyList<string> _requiredRoles;

    public RequireProductRoleFilter(string productCode, IReadOnlyList<string> requiredRoles)
    {
        _productCode = productCode;
        _requiredRoles = requiredRoles;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var user = httpContext.User;

        if (user.Identity?.IsAuthenticated != true)
            return Results.Unauthorized();

        var userId = user.FindFirst("sub")?.Value;
        var tenantId = user.FindFirst("tenant_id")?.Value;
        var accessVersion = user.FindFirst("access_version")?.Value;
        var path = httpContext.Request.Path.Value;
        var method = httpContext.Request.Method;
        var rolesStr = string.Join(",", _requiredRoles);

        if (user.IsTenantAdminOrAbove())
        {
            LogAuthzDecision(httpContext, "ALLOW", userId, tenantId, path, method,
                _productCode, rolesStr, "AdminBypass", accessVersion);
            return await next(context);
        }

        if (!user.HasProductAccess(_productCode))
        {
            LogAuthzDecision(httpContext, "DENY", userId, tenantId, path, method,
                _productCode, rolesStr, "NoProductAccess", accessVersion);

            EmitProductDenied(httpContext, userId, tenantId, _productCode, rolesStr, "NoProductAccess", path, method, "security.product.access.denied");

            return ProductAccessDeniedResult.Create(
                ProductAccessDeniedException.NoProductAccess(_productCode));
        }

        if (!user.HasProductRole(_productCode, _requiredRoles))
        {
            LogAuthzDecision(httpContext, "DENY", userId, tenantId, path, method,
                _productCode, rolesStr, "InsufficientRole", accessVersion);

            EmitProductDenied(httpContext, userId, tenantId, _productCode, rolesStr, "InsufficientRole", path, method, "security.product.role.denied");

            return ProductAccessDeniedResult.Create(
                ProductAccessDeniedException.InsufficientProductRole(_productCode, _requiredRoles));
        }

        LogAuthzDecision(httpContext, "ALLOW", userId, tenantId, path, method,
            _productCode, rolesStr, "RoleClaim", accessVersion);

        return await next(context);
    }

    private static void LogAuthzDecision(
        HttpContext ctx, string result, string? userId, string? tenantId,
        string? path, string method, string product, string? requiredRoles,
        string source, string? accessVersion)
    {
        var logger = ctx.RequestServices.GetService(typeof(ILogger<RequireProductRoleFilter>)) as ILogger;
        if (logger == null) return;

        if (result == "DENY")
        {
            logger.LogWarning(
                "AuthzDecision: result={Result} user={UserId} tenant={TenantId} method={Method} endpoint={Path} product={Product} requiredRoles=[{RequiredRoles}] source={Source} accessVersion={AccessVersion}",
                result, userId, tenantId, method, path, product, requiredRoles, source, accessVersion);
        }
        else
        {
            logger.LogInformation(
                "AuthzDecision: result={Result} user={UserId} tenant={TenantId} method={Method} endpoint={Path} product={Product} requiredRoles=[{RequiredRoles}] source={Source} accessVersion={AccessVersion}",
                result, userId, tenantId, method, path, product, requiredRoles, source, accessVersion);
        }
    }

    /// <summary>
    /// Emits a canonical denied-access event (product access or role insufficiency).
    /// Fire-and-observe, IAuditEventClient resolved optionally.
    /// </summary>
    private static void EmitProductDenied(
        HttpContext httpContext,
        string?     userId,
        string?     tenantId,
        string      productCode,
        string      requiredRoles,
        string      denialReason,
        string?     endpoint,
        string      method,
        string      eventType)
    {
        var auditClient = httpContext.RequestServices.GetService(typeof(IAuditEventClient)) as IAuditEventClient;
        if (auditClient is null) return;

        var now = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = eventType,
            EventCategory = EventCategory.Security,
            SourceSystem  = "authorization",
            SourceService = "require-product-role-filter",
            Visibility    = VisibilityScope.Tenant,
            Severity      = SeverityLevel.Warn,
            OccurredAtUtc = now,
            Scope = new AuditEventScopeDto
            {
                ScopeType = ScopeType.Tenant,
                TenantId  = tenantId,
            },
            Actor = new AuditEventActorDto
            {
                Id   = userId,
                Type = ActorType.User,
                Name = userId,
            },
            Entity      = new AuditEventEntityDto { Type = "Product", Id = productCode },
            Action      = denialReason == "InsufficientRole" ? "ProductRoleDenied" : "ProductAccessDenied",
            Description = denialReason == "InsufficientRole"
                ? $"Product role access denied for product '{productCode}' — required roles: [{requiredRoles}]."
                : $"Product access denied for product '{productCode}' — reason: {denialReason}.",
            Outcome     = "Failure",
            Metadata    = JsonSerializer.Serialize(new
            {
                productCode,
                requiredRoles,
                denialReason,
                endpoint,
                method,
            }),
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(
                now, "authorization", eventType,
                $"{userId}:{productCode}:{denialReason}"),
            Tags = ["security", "access-denied", "product", "role"],
        });
    }
}

public sealed class RequireOrgProductAccessFilter : IEndpointFilter
{
    private readonly string _productCode;

    public RequireOrgProductAccessFilter(string productCode)
    {
        _productCode = productCode;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var user = httpContext.User;

        if (user.Identity?.IsAuthenticated != true)
            return Results.Unauthorized();

        var userId = user.FindFirst("sub")?.Value;
        var tenantId = user.FindFirst("tenant_id")?.Value;
        var accessVersion = user.FindFirst("access_version")?.Value;
        var path = httpContext.Request.Path.Value;
        var method = httpContext.Request.Method;

        if (user.IsTenantAdminOrAbove())
        {
            LogAuthzDecision(httpContext, "ALLOW", userId, tenantId, path, method,
                _productCode, "AdminBypass", accessVersion);
            return await next(context);
        }

        if (!user.HasProductAccess(_productCode))
        {
            LogAuthzDecision(httpContext, "DENY", userId, tenantId, path, method,
                _productCode, "NoProductAccess", accessVersion);

            EmitProductAccessDenied(httpContext, userId, tenantId, _productCode, "NoProductAccess", path, method);

            return ProductAccessDeniedResult.Create(
                ProductAccessDeniedException.NoProductAccess(_productCode));
        }

        var orgIdClaim = user.FindFirst("org_id")?.Value;
        if (!Guid.TryParse(orgIdClaim, out var userOrgId))
        {
            LogAuthzDecision(httpContext, "DENY", userId, tenantId, path, method,
                _productCode, "OrgContextMissing", accessVersion);

            return ProductAccessDeniedResult.Create(
                "ORG_CONTEXT_MISSING",
                "Organization context is required for this operation.",
                _productCode);
        }

        httpContext.Items["ProductAuth:OrgId"] = userOrgId;
        httpContext.Items["ProductAuth:ProductCode"] = _productCode;

        LogAuthzDecision(httpContext, "ALLOW", userId, tenantId, path, method,
            _productCode, "OrgProductClaim", accessVersion);

        return await next(context);
    }

    private static void LogAuthzDecision(
        HttpContext ctx, string result, string? userId, string? tenantId,
        string? path, string method, string product,
        string source, string? accessVersion)
    {
        var logger = ctx.RequestServices.GetService(typeof(ILogger<RequireOrgProductAccessFilter>)) as ILogger;
        if (logger == null) return;

        if (result == "DENY")
        {
            logger.LogWarning(
                "AuthzDecision: result={Result} user={UserId} tenant={TenantId} method={Method} endpoint={Path} product={Product} source={Source} accessVersion={AccessVersion}",
                result, userId, tenantId, method, path, product, source, accessVersion);
        }
        else
        {
            logger.LogInformation(
                "AuthzDecision: result={Result} user={UserId} tenant={TenantId} method={Method} endpoint={Path} product={Product} source={Source} accessVersion={AccessVersion}",
                result, userId, tenantId, method, path, product, source, accessVersion);
        }
    }

    /// <summary>
    /// Emits <c>security.product.access.denied</c> for NoProductAccess denials.
    /// OrgContextMissing is a configuration issue, not a user access denial, so it is excluded.
    /// </summary>
    private static void EmitProductAccessDenied(
        HttpContext httpContext,
        string?     userId,
        string?     tenantId,
        string      productCode,
        string      denialReason,
        string?     endpoint,
        string      method)
    {
        var auditClient = httpContext.RequestServices.GetService(typeof(IAuditEventClient)) as IAuditEventClient;
        if (auditClient is null) return;

        var now = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "security.product.access.denied",
            EventCategory = EventCategory.Security,
            SourceSystem  = "authorization",
            SourceService = "require-org-product-access-filter",
            Visibility    = VisibilityScope.Tenant,
            Severity      = SeverityLevel.Warn,
            OccurredAtUtc = now,
            Scope = new AuditEventScopeDto
            {
                ScopeType = ScopeType.Tenant,
                TenantId  = tenantId,
            },
            Actor = new AuditEventActorDto
            {
                Id   = userId,
                Type = ActorType.User,
                Name = userId,
            },
            Entity      = new AuditEventEntityDto { Type = "Product", Id = productCode },
            Action      = "ProductAccessDenied",
            Description = $"Org-scoped product access denied for product '{productCode}' — reason: {denialReason}.",
            Outcome     = "Failure",
            Metadata    = JsonSerializer.Serialize(new
            {
                productCode,
                denialReason,
                endpoint,
                method,
            }),
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(
                now, "authorization", "security.product.access.denied",
                $"{userId}:{productCode}"),
            Tags = ["security", "access-denied", "product", "org"],
        });
    }
}

public sealed class RequirePermissionFilter : IEndpointFilter
{
    private readonly string _permissionCode;
    private readonly string[]? _fallbackRoles;

    public RequirePermissionFilter(string permissionCode, string[]? fallbackRoles = null)
    {
        _permissionCode = permissionCode;
        _fallbackRoles = fallbackRoles;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var user = httpContext.User;

        if (user.Identity?.IsAuthenticated != true)
            return Results.Unauthorized();

        var userId = user.FindFirst("sub")?.Value;
        var tenantId = user.FindFirst("tenant_id")?.Value;
        var accessVersion = user.FindFirst("access_version")?.Value;
        var path = httpContext.Request.Path.Value;
        var method = httpContext.Request.Method;

        if (user.IsTenantAdminOrAbove())
        {
            LogPermissionDecision(httpContext, "ALLOW", userId, tenantId, path, method,
                _permissionCode, "AdminBypass", accessVersion);
            return await next(context);
        }

        if (user.HasPermission(_permissionCode))
        {
            var policyResult = await EvaluatePoliciesIfEnabled(httpContext, user, _permissionCode);
            if (policyResult != null && !policyResult.Allowed)
            {
                LogPermissionDecision(httpContext, "DENY", userId, tenantId, path, method,
                    _permissionCode, $"PolicyDenied:{policyResult.Reason}", accessVersion);

                EmitPermissionDenied(httpContext, userId, tenantId, _permissionCode,
                    $"PolicyDenied:{policyResult.Reason}", path, method, "security.permission.policy.denied");

                return ProductAccessDeniedResult.Create(
                    "POLICY_DENIED",
                    $"Permission '{_permissionCode}' denied by policy: {policyResult.Reason}",
                    ExtractProductCode(_permissionCode) ?? "");
            }

            LogPermissionDecision(httpContext, "ALLOW", userId, tenantId, path, method,
                _permissionCode, policyResult != null ? "PermissionClaim+PolicyPass" : "PermissionClaim", accessVersion);
            return await next(context);
        }

        if (_fallbackRoles is { Length: > 0 } && IsRoleFallbackEnabled(httpContext))
        {
            var productCode = ExtractProductCode(_permissionCode);
            if (productCode != null && user.HasProductRole(productCode, _fallbackRoles))
            {
                LogPermissionDecision(httpContext, "ALLOW", userId, tenantId, path, method,
                    _permissionCode, "RoleFallback", accessVersion);
                return await next(context);
            }
        }

        LogPermissionDecision(httpContext, "DENY", userId, tenantId, path, method,
            _permissionCode, "MissingPermission", accessVersion);

        EmitPermissionDenied(httpContext, userId, tenantId, _permissionCode,
            "MissingPermission", path, method, "security.permission.denied");

        return ProductAccessDeniedResult.Create(
            ProductAccessDeniedException.MissingPermission(_permissionCode));
    }

    private static async Task<Authorization.PolicyEvaluationResult?> EvaluatePoliciesIfEnabled(
        HttpContext httpContext, System.Security.Claims.ClaimsPrincipal user, string permissionCode)
    {
        if (!IsPolicyEvaluationEnabled(httpContext))
            return null;

        var evaluationService = httpContext.RequestServices.GetService(
            typeof(Authorization.IPolicyEvaluationService)) as Authorization.IPolicyEvaluationService;

        if (evaluationService == null)
            return null;

        var contextAccessor = httpContext.RequestServices.GetService(
            typeof(Authorization.IPolicyResourceContextAccessor)) as Authorization.IPolicyResourceContextAccessor;

        var resourceContext = contextAccessor?.GetResourceContext()
            ?? (httpContext.Items.TryGetValue("PolicyResourceContext", out var ctx)
                ? ctx as Dictionary<string, object?>
                : null);

        var result = await evaluationService.EvaluateAsync(
            user,
            permissionCode,
            resourceContext,
            httpContext);

        if (result != null)
        {
            LogPolicyDecisionSummary(httpContext, user, permissionCode, result);
        }

        return result;
    }

    private static void LogPolicyDecisionSummary(
        HttpContext httpContext, System.Security.Claims.ClaimsPrincipal user,
        string permission, Authorization.PolicyEvaluationResult result)
    {
        var logger = httpContext.RequestServices.GetService(typeof(ILogger<RequirePermissionFilter>)) as ILogger;
        if (logger == null) return;

        var userId = user.FindFirst("sub")?.Value;
        var tenantId = user.FindFirst("tenant_id")?.Value;
        var accessVersion = user.FindFirst("access_version")?.Value;
        var endpoint = httpContext.Request.Path.Value;

        if (!result.Allowed)
        {
            logger.LogWarning(
                "PolicyDecisionSummary: event=PolicyDecisionSummary userId={UserId} tenantId={TenantId} endpoint={Endpoint} permission={Permission} result=DENY reason={Reason} denyOverride={DenyOverride} denyPolicyCode={DenyPolicyCode} policiesEvaluated={PoliciesCount} elapsedMs={ElapsedMs} policyVersion={PolicyVersion} cacheHit={CacheHit} accessVersion={AccessVersion}",
                userId, tenantId, endpoint, permission, result.Reason,
                result.DenyOverrideApplied, result.DenyOverridePolicyCode,
                result.MatchedPolicies.Count, result.EvaluationElapsedMs,
                result.PolicyVersion, result.CacheHit, accessVersion);
        }
        else
        {
            logger.LogInformation(
                "PolicyDecisionSummary: event=PolicyDecisionSummary userId={UserId} tenantId={TenantId} endpoint={Endpoint} permission={Permission} result=ALLOW reason={Reason} policiesEvaluated={PoliciesCount} elapsedMs={ElapsedMs} policyVersion={PolicyVersion} cacheHit={CacheHit} accessVersion={AccessVersion}",
                userId, tenantId, endpoint, permission, result.Reason,
                result.MatchedPolicies.Count, result.EvaluationElapsedMs,
                result.PolicyVersion, result.CacheHit, accessVersion);
        }
    }

    private static bool IsPolicyEvaluationEnabled(HttpContext ctx)
    {
        var config = ctx.RequestServices.GetService(typeof(Microsoft.Extensions.Configuration.IConfiguration))
            as Microsoft.Extensions.Configuration.IConfiguration;
        if (config == null) return false;
        return string.Equals(config["Authorization:EnablePolicyEvaluation"], "true", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRoleFallbackEnabled(HttpContext ctx)
    {
        var config = ctx.RequestServices.GetService(typeof(Microsoft.Extensions.Configuration.IConfiguration))
            as Microsoft.Extensions.Configuration.IConfiguration;
        if (config == null) return false;
        return string.Equals(config["Authorization:EnableRoleFallback"], "true", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractProductCode(string permissionCode)
    {
        var dotIndex = permissionCode.IndexOf('.');
        return dotIndex > 0 ? permissionCode[..dotIndex] : null;
    }

    private static void LogPermissionDecision(
        HttpContext ctx, string result, string? userId, string? tenantId,
        string? path, string method, string permission,
        string source, string? accessVersion)
    {
        var logger = ctx.RequestServices.GetService(typeof(ILogger<RequirePermissionFilter>)) as ILogger;
        if (logger == null) return;

        if (result == "DENY")
        {
            logger.LogWarning(
                "PermissionDecision: result={Result} user={UserId} tenant={TenantId} method={Method} endpoint={Path} permission={Permission} source={Source} accessVersion={AccessVersion}",
                result, userId, tenantId, method, path, permission, source, accessVersion);
        }
        else
        {
            logger.LogInformation(
                "PermissionDecision: result={Result} user={UserId} tenant={TenantId} method={Method} endpoint={Path} permission={Permission} source={Source} accessVersion={AccessVersion}",
                result, userId, tenantId, method, path, permission, source, accessVersion);
        }
    }

    /// <summary>
    /// Emits a <c>security.permission.denied</c> or <c>security.permission.policy.denied</c>
    /// canonical audit event when a permission check fails.
    ///
    /// Fail-safe: IAuditEventClient is resolved optionally from DI.
    /// Fire-and-observe: the Task is discarded; the denial response is never gated on audit success.
    /// </summary>
    private static void EmitPermissionDenied(
        HttpContext httpContext,
        string?     userId,
        string?     tenantId,
        string      permissionCode,
        string      denialReason,
        string?     endpoint,
        string      method,
        string      eventType)
    {
        var auditClient = httpContext.RequestServices.GetService(typeof(IAuditEventClient)) as IAuditEventClient;
        if (auditClient is null) return;

        var now = DateTimeOffset.UtcNow;
        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = eventType,
            EventCategory = EventCategory.Security,
            SourceSystem  = "authorization",
            SourceService = "require-permission-filter",
            Visibility    = VisibilityScope.Tenant,
            Severity      = SeverityLevel.Warn,
            OccurredAtUtc = now,
            Scope = new AuditEventScopeDto
            {
                ScopeType = ScopeType.Tenant,
                TenantId  = tenantId,
            },
            Actor = new AuditEventActorDto
            {
                Id   = userId,
                Type = ActorType.User,
                Name = userId,
            },
            Entity      = new AuditEventEntityDto { Type = "Permission", Id = permissionCode },
            Action      = "PermissionDenied",
            Description = $"Permission '{permissionCode}' denied — reason: {denialReason}.",
            Outcome     = "Failure",
            Metadata    = JsonSerializer.Serialize(new
            {
                permissionCode,
                denialReason,
                endpoint,
                method,
            }),
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(
                now, "authorization", eventType,
                $"{userId}:{permissionCode}"),
            Tags = ["security", "access-denied", "permission"],
        });
    }
}
