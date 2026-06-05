# Gateway & Request-Context Propagation Model

**Platform:** LegalSynq microservices monorepo  
**Gateway:** YARP reverse proxy, port 5000  
**Services:** Identity (5001), Fund (5002), CareConnect (5003), Lien (5004, future)  
**Shared library:** `BuildingBlocks` (`shared/building-blocks/BuildingBlocks/`)  
**Date:** 2026-03-28

---

## Current State Baseline

| Layer | What exists today |
|---|---|
| **Gateway** | YARP + JWT Bearer validation; routes to 3 services; no tenant resolution from Host; no CorrelationId injection; no header stripping |
| **JWT** | Emits: `sub`, `email`, `jti`, `tenant_id`, `tenant_code`, `ClaimTypes.Role`, `org_id`, `org_type`, `product_roles[]` |
| **ICurrentRequestContext** | Reads `ClaimsPrincipal` via `IHttpContextAccessor`; exposes `UserId`, `TenantId`, `TenantCode`, `OrgId`, `OrgType`, `Roles`, `ProductRoles`, `IsPlatformAdmin` |
| **Capabilities** | Resolved per-request from `product_roles` via `ICapabilityService` + `IMemoryCache`; NOT stored in JWT |
| **TenantDomains** | Domain entity and DB table exist; unique index on `Domain`; NOT yet wired into gateway flow |

---

## 1. Gateway Responsibility Model

### Gateway Is Responsible For

| Responsibility | Detail |
|---|---|
| **Token validation** | Verify JWT signature, issuer, audience, expiry (`ClockSkew = TimeSpan.Zero`). Reject invalid tokens before any request reaches a downstream service. |
| **Tenant resolution from Host** | Extract subdomain from `Host` header → query `TenantDomains` table → resolve `TenantId` + `TenantCode`. Attach resolved values as forwarded headers. |
| **CorrelationId generation** | Generate a UUID `X-Correlation-Id` if the request does not carry one. Pass it downstream. |
| **Sensitive header stripping** | Strip any inbound `X-Tenant-Id`, `X-Org-Id`, `X-Forwarded-User` headers before forwarding. Clients must not be able to inject context. |
| **Route-level authorization policy** | Apply YARP route authorization policies (`Anonymous` for auth/health, `RequireAuthorization()` for protected routes). Do NOT enforce capability-level checks here. |
| **Access logging** | Log every request with `CorrelationId`, resolved `TenantId`, `UserId` (from JWT `sub`), route, and upstream response status. |

### Gateway Is NOT Responsible For

| Not the gateway's job | Who does it |
|---|---|
| Capability enforcement | Downstream services via `ICapabilityService.HasCapabilityAsync()` |
| Organization-level access checks | Downstream services via participant `OrgId` predicates |
| Business rule validation | Downstream application service layer |
| Token issuance | Identity service (`POST /api/auth/login`) |
| Tenant creation or domain registration | Identity service |

### Downstream Services Are Responsible For

| Responsibility | Mechanism |
|---|---|
| Reading caller context | `ICurrentRequestContext` (from `BuildingBlocks.Context`) |
| Capability enforcement | `AuthorizationService.RequireCapabilityAsync(ctx, CapabilityCodes.X)` |
| Org-participant checks | `WHERE SellingOrganizationId = ctx.OrgId` (or equivalent) |
| Optional tenant partitioning | `WHERE TenantId = ctx.TenantId` only when the record is tenant-owned (not cross-org) |
| Audit field population | `CreatedByUserId = ctx.UserId`, `UpdatedByUserId = ctx.UserId` |

---

## 2. Tenant Resolution Flow

### Production Path (subdomain-based)

```
Client: GET https://lawfirm-alpha.legalsynq.com/careconnect/api/referrals
                         │
                         ▼
Gateway (port 5000)
  ┌─ TenantResolutionMiddleware ──────────────────────────────────────┐
  │  1. Extract Host header → "lawfirm-alpha.legalsynq.com"          │
  │  2. Parse subdomain → "lawfirm-alpha"                            │
  │  3. Lookup: SELECT TenantId FROM TenantDomains                   │
  │             WHERE Domain = 'lawfirm-alpha.legalsynq.com'         │
  │  4. If not found → 400 Bad Request {"error": "unknown_tenant"}   │
  │  5. If Tenant.IsActive = false → 403 Forbidden                   │
  │  6. Attach headers to forwarded request:                          │
  │       X-Resolved-Tenant-Id:   <uuid>                             │
  │       X-Resolved-Tenant-Code: LAWFIRM_ALPHA                      │
  └───────────────────────────────────────────────────────────────────┘
  ┌─ JWT Bearer Validation ───────────────────────────────────────────┐
  │  7. Validate token signature (HMAC-SHA256, same signing key)      │
  │  8. Validate issuer = "legalsynq-identity"                        │
  │  9. Validate audience = "legalsynq-platform"                      │
  │  10. Validate expiry (ClockSkew = TimeSpan.Zero)                  │
  │  11. Cross-check: JWT claim tenant_id == X-Resolved-Tenant-Id     │
  │      Mismatch → 401 Unauthorized                                  │
  └───────────────────────────────────────────────────────────────────┘
  ┌─ CorrelationId Middleware ─────────────────────────────────────────┐
  │  12. If X-Correlation-Id header present → validate UUID format    │
  │       If absent or invalid → generate new UUID                    │
  │  13. Set X-Correlation-Id on response and forwarded request       │
  └───────────────────────────────────────────────────────────────────┘
  ┌─ YARP Reverse Proxy ───────────────────────────────────────────────┐
  │  14. Strip inbound X-Tenant-Id / X-Org-Id / X-Forwarded-User      │
  │  15. Forward validated request + JWT Authorization header          │
  │      + X-Resolved-Tenant-Id + X-Correlation-Id to upstream        │
  └───────────────────────────────────────────────────────────────────┘
```

### Dev / Local Path (no subdomain)

In local development (`localhost:5000`), no subdomain is present. The gateway falls back to reading an `X-Tenant-Code` header (dev-only; stripped in production) or skips tenant resolution entirely if `ASPNETCORE_ENVIRONMENT = Development`. The JWT `tenant_id` claim is trusted directly in this mode.

```csharp
// Gateway TenantResolutionMiddleware pseudocode
if (env.IsProduction())
{
    // strict subdomain resolution — errors on mismatch
}
else if (env.IsDevelopment())
{
    // accept X-Tenant-Code header as fallback; log a warning
    var code = request.Headers["X-Tenant-Code"].FirstOrDefault();
    if (code is not null) { /* soft-resolve from TenantDomains by Code */ }
    // else skip; JWT claims are trusted without cross-check
}
```

### Login Path (always TenantCode-based)

The Identity service login endpoint (`POST /identity/api/auth/login`) is anonymous at the gateway. It accepts `tenantCode` in the request body and resolves the tenant by code, not subdomain. This is the only place where code-based resolution is the primary mechanism.

```json
POST /identity/api/auth/login
{ "tenantCode": "LAWFIRM_ALPHA", "email": "user@example.com", "password": "..." }
```

The resulting JWT embeds `tenant_id` and `tenant_code`. All subsequent requests use subdomain resolution to cross-check these values.

---

## 3. Request Context Contract

### Canonical Downstream Context Fields

| Field | Source | Claim / Header |
|---|---|---|
| `UserId` | JWT | `sub` |
| `Email` | JWT | `email` |
| `TenantId` | JWT | `tenant_id` |
| `TenantCode` | JWT | `tenant_code` |
| `OrgId` | JWT | `org_id` |
| `OrgType` | JWT | `org_type` |
| `ProductRoles` | JWT | `product_roles` (multi-value) |
| `Roles` | JWT | `ClaimTypes.Role` (system roles) |
| `IsPlatformAdmin` | Derived | `Roles.Contains("PlatformAdmin")` |
| `CorrelationId` | Gateway-generated | `X-Correlation-Id` header |
| `IsAuthenticated` | Derived | `ClaimsPrincipal.Identity.IsAuthenticated` |

### Fields from JWT vs. Gateway-Generated

**From JWT (signed, tamper-proof):**
`UserId`, `Email`, `TenantId`, `TenantCode`, `OrgId`, `OrgType`, `ProductRoles`, `Roles`

**Gateway-generated (added per-request, not signed):**
`CorrelationId` — UUID generated by gateway correlation middleware; forwarded as `X-Correlation-Id` header; read by services from the HTTP request header

**Resolved at service layer (not in transit):**
Capabilities — resolved from `ProductRoles` via `ICapabilityService` + `IMemoryCache`; never stored in JWT or headers

### Updated `ICurrentRequestContext` Contract

```csharp
// shared/building-blocks/BuildingBlocks/Context/ICurrentRequestContext.cs
namespace BuildingBlocks.Context;

public interface ICurrentRequestContext
{
    bool IsAuthenticated { get; }

    // Identity
    Guid? UserId { get; }
    string? Email { get; }

    // Tenant (from JWT — cross-checked by gateway against Host header)
    Guid? TenantId { get; }
    string? TenantCode { get; }

    // Organization
    Guid? OrgId { get; }
    string? OrgType { get; }

    // Roles and product access
    IReadOnlyCollection<string> Roles { get; }
    IReadOnlyCollection<string> ProductRoles { get; }

    // Derived
    bool IsPlatformAdmin { get; }

    // Tracing (read from X-Correlation-Id header)
    string? CorrelationId { get; }
}
```

### Updated `CurrentRequestContext` Implementation

```csharp
// shared/building-blocks/BuildingBlocks/Context/CurrentRequestContext.cs
using System.Security.Claims;
using BuildingBlocks.Authorization;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Context;

public class CurrentRequestContext : ICurrentRequestContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentRequestContext(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    private HttpContext? Http => _httpContextAccessor.HttpContext;
    private ClaimsPrincipal? User => Http?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public Guid? UserId =>
        Guid.TryParse(User?.FindFirstValue("sub"), out var uid) ? uid : null;

    public string? Email => User?.FindFirstValue("email");

    public Guid? TenantId =>
        Guid.TryParse(User?.FindFirstValue("tenant_id"), out var tid) ? tid : null;

    public string? TenantCode => User?.FindFirstValue("tenant_code");

    public Guid? OrgId =>
        Guid.TryParse(User?.FindFirstValue("org_id"), out var oid) ? oid : null;

    public string? OrgType => User?.FindFirstValue("org_type");

    public IReadOnlyCollection<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList().AsReadOnly()
        ?? (IReadOnlyCollection<string>)Array.Empty<string>();

    public IReadOnlyCollection<string> ProductRoles =>
        User?.FindAll("product_roles").Select(c => c.Value).ToList().AsReadOnly()
        ?? (IReadOnlyCollection<string>)Array.Empty<string>();

    public bool IsPlatformAdmin =>
        Roles.Contains(Roles.PlatformAdmin, StringComparer.OrdinalIgnoreCase);

    // Read from forwarded header injected by gateway
    public string? CorrelationId =>
        Http?.Request.Headers["X-Correlation-Id"].FirstOrDefault();
}
```

---

## 4. Context Propagation Strategy

### Recommendation: JWT Claims as Canonical Source

Downstream services **must read context from the validated `ClaimsPrincipal`** (via `ICurrentRequestContext`), not from raw forwarded headers. Rationale:

1. The JWT is cryptographically signed — claims cannot be forged without the signing key.
2. `ClockSkew = TimeSpan.Zero` ensures expired tokens are always rejected.
3. Reading from headers would require services to individually trust and parse unsigned data.

The only exception is `CorrelationId`, which is a non-security tracing value injected by the gateway as `X-Correlation-Id`. Services read it from the HTTP header, not from the JWT.

### Header Stripping (Security Safeguard)

The gateway must strip the following headers from **inbound client requests** before forwarding:

```
X-Tenant-Id
X-Org-Id
X-Forwarded-User
X-User-Id
X-Platform-Admin
```

These headers must be considered attacker-controlled. Only headers that the gateway itself injects (after validation) should reach downstream services.

YARP configuration for header stripping:

```json
"Transforms": [
  { "PathRemovePrefix": "/careconnect" },
  { "RequestHeaderRemove": "X-Tenant-Id" },
  { "RequestHeaderRemove": "X-Org-Id" },
  { "RequestHeaderRemove": "X-Forwarded-User" },
  { "RequestHeaderRemove": "X-User-Id" },
  { "RequestHeaderRemove": "X-Platform-Admin" }
]
```

### Cross-Tenant Context

When a lien owner in Tenant B reads a lien created by a provider in Tenant A, the `tenant_id` in the JWT is Tenant B's ID. The downstream service must **not** filter by `tenant_id` for cross-tenant resources. The `org_id` in the JWT is what scopes access. Services that handle cross-tenant data must never use `ctx.TenantId` as a security predicate on records owned by another tenant.

---

## 5. Gateway Implementation Plan

### Middleware Pipeline (ordered)

```
[1] ExceptionHandlingMiddleware     — catch unhandled exceptions; return RFC 7807 ProblemDetails
[2] CorrelationIdMiddleware         — generate/validate X-Correlation-Id
[3] RequestLoggingMiddleware        — log request start with CorrelationId; no sensitive claims
[4] TenantResolutionMiddleware      — resolve TenantId from Host; attach X-Resolved-Tenant-Id
[5] UseAuthentication()             — validate JWT Bearer token
[6] UseAuthorization()              — enforce route-level policies
[7] TenantClaimCrossCheckMiddleware — compare JWT tenant_id vs X-Resolved-Tenant-Id (prod only)
[8] HeaderStrippingTransform        — YARP transforms strip spoofed inbound headers
[9] MapReverseProxy()               — forward to upstream with injected headers
```

### TenantResolutionMiddleware

```csharp
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWebHostEnvironment _env;

    public TenantResolutionMiddleware(
        RequestDelegate next, IServiceScopeFactory scopeFactory, IWebHostEnvironment env)
    {
        _next = next; _scopeFactory = scopeFactory; _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var host = context.Request.Host.Host; // e.g. "lawfirm-alpha.legalsynq.com"

        // Anonymous routes skip tenant resolution
        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await _next(context);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();

        var tenantDomain = await db.TenantDomains
            .Include(d => d.Tenant)
            .FirstOrDefaultAsync(d => d.Domain == host);

        if (tenantDomain is null)
        {
            if (_env.IsDevelopment())
            {
                // Dev fallback: skip subdomain check
                await _next(context);
                return;
            }
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "unknown_tenant" });
            return;
        }

        if (!tenantDomain.Tenant.IsActive)
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new { error = "tenant_suspended" });
            return;
        }

        // Attach for downstream use and for cross-check step [7]
        context.Items["resolved_tenant_id"]   = tenantDomain.TenantId;
        context.Items["resolved_tenant_code"] = tenantDomain.Tenant.Code;

        await _next(context);
    }
}
```

### TenantClaimCrossCheckMiddleware

```csharp
public class TenantClaimCrossCheckMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _env;

    public async Task InvokeAsync(HttpContext context)
    {
        // Only enforce in production; skip anonymous routes
        if (!_env.IsProduction()) { await _next(context); return; }

        var resolvedTenantId = context.Items["resolved_tenant_id"] as Guid?;
        if (resolvedTenantId is null) { await _next(context); return; }

        var jwtTenantIdClaim = context.User?.FindFirstValue("tenant_id");
        if (jwtTenantIdClaim is null || !Guid.TryParse(jwtTenantIdClaim, out var jwtTenantId))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "missing_tenant_claim" });
            return;
        }

        if (jwtTenantId != resolvedTenantId)
        {
            // Token issued for a different tenant's subdomain
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "tenant_mismatch" });
            return;
        }

        await _next(context);
    }
}
```

### CorrelationId Middleware

```csharp
public class CorrelationIdMiddleware
{
    private const string Header = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[Header].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId) || !Guid.TryParse(correlationId, out _))
            correlationId = Guid.NewGuid().ToString();

        context.Items[Header] = correlationId;
        context.Request.Headers[Header] = correlationId;   // pass downstream
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[Header] = correlationId;
            return Task.CompletedTask;
        });

        await _next(context);
    }
}
```

### Token Validation (Gateway `Program.cs`)

```csharp
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],          // "legalsynq-identity"
            ValidAudience = jwtSection["Audience"],      // "legalsynq-platform"
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(signingKey)),
            ClockSkew = TimeSpan.Zero                    // strict expiry
        };
    });
```

---

## 6. Downstream Service Consumption Pattern

### BuildingBlocks Shared Model

All services reference `shared/building-blocks/BuildingBlocks/` as a project reference. `ICurrentRequestContext` and `CurrentRequestContext` are the **only** mechanism services use to read caller identity. Services must never parse JWT claims directly or read request headers for security-sensitive values.

### Service Registration (all services, via DI)

```csharp
// In each service's Infrastructure/DependencyInjection.cs
services.AddHttpContextAccessor();
services.AddScoped<ICurrentRequestContext, CurrentRequestContext>();
// Capability resolution:
services.AddMemoryCache();
services.AddScoped<ICapabilityService, CapabilityService>();
services.AddScoped<AuthorizationService>();
```

### Endpoint Usage Pattern

```csharp
// Minimal API endpoint — canonical pattern
group.MapPost("/api/referrals", async (
    CreateReferralRequest request,
    ICurrentRequestContext ctx,
    IReferralService svc,
    AuthorizationService authz,
    CancellationToken ct) =>
{
    // 1. Confirm capability (resolved from product_roles via ICapabilityService)
    await authz.RequireCapabilityAsync(ctx, CapabilityCodes.ReferralCreate, ct);

    // 2. Extract required context — fail fast with 500 if claims missing
    var tenantId = ctx.TenantId   ?? throw new InvalidOperationException("tenant_id claim missing.");
    var orgId    = ctx.OrgId      ?? throw new InvalidOperationException("org_id claim missing.");
    var userId   = ctx.UserId     ?? throw new InvalidOperationException("sub claim missing.");

    // 3. Delegate to application service — no auth logic below this point
    var result = await svc.CreateAsync(tenantId, orgId, userId, request, ct);
    return Results.Created($"/api/referrals/{result.Id}", result);
})
.RequireAuthorization(Policies.CanReferCareConnect);  // coarse policy gate at route level
```

### Common Library vs. Per-Service Code

| Code | Location |
|---|---|
| `ICurrentRequestContext` / `CurrentRequestContext` | `BuildingBlocks.Context` — shared |
| `ICapabilityService` / `AuthorizationService` | `BuildingBlocks.Authorization` — shared |
| `CapabilityCodes` / `ProductRoleCodes` / `Policies` | `BuildingBlocks.Authorization` — shared |
| JWT validation parameters | Each service's `Program.cs` — duplicated but identical; consider moving to `BuildingBlocks.ServiceBase` |
| Capability DB queries (`CapabilityService` impl) | Each service's Infrastructure (needs DB access); common interface in BuildingBlocks |
| Business rules, org-participant checks | Per-service Application layer only |

---

## 7. Cross-Tenant Workflow Access Model

### The Core Rule

> **Downstream services must NEVER use `ctx.TenantId` as a security predicate on records that participate in cross-tenant workflows.**

`TenantId` is a storage partition key only. Org IDs are the access control keys.

### CareConnect: Provider in Tenant B reads a referral from Law Firm in Tenant A

```
Law Firm (Tenant A) creates referral:
  Referral.ReceivingOrganizationId = <provider-org-id>  ← provider is in Tenant B
  Referral.ReferringOrganizationId = <lawfirm-org-id>
  Referral.TenantId = <tenant-A-id>

Provider (Tenant B) queries:
  JWT claims: tenant_id = <tenant-B-id>, org_id = <provider-org-id>

  // WRONG — misses the referral because TenantId is Tenant A's
  WHERE TenantId = ctx.TenantId

  // CORRECT — org_id scopes access regardless of tenant
  WHERE ReceivingOrganizationId = ctx.OrgId
```

### SynqFund: Funder in Tenant B reads an application from Law Firm in Tenant A

```
Law Firm (Tenant A) creates application:
  Application.ReferringOrganizationId = <lawfirm-org-id>
  Application.ReceivingOrganizationId = <funder-org-id>   ← funder is in Tenant B
  Application.TenantId = <tenant-A-id>

Funder (Tenant B) queries:
  JWT claims: tenant_id = <tenant-B-id>, org_id = <funder-org-id>

  // CORRECT
  WHERE ReceivingOrganizationId = ctx.OrgId
```

### SynqLien: Lien Owner in Tenant B reads liens offered by Provider in Tenant A

```
Provider (Tenant A) lists lien:
  Lien.SellingOrganizationId = <provider-org-id>
  Lien.Status = 'Offered'
  Lien.TenantId = <tenant-A-id>

Lien Owner (Tenant B) browses marketplace:
  JWT claims: tenant_id = <tenant-B-id>

  // CORRECT — no tenant filter for the marketplace
  WHERE Status = 'Offered'                              (anonymous browse)

  // After purchase:
  WHERE BuyingOrganizationId = ctx.OrgId               (portfolio view)
```

### Where `TenantId` IS appropriate

```csharp
// Safe to use TenantId — fully tenant-owned resource
// Example: Identity service listing users in their tenant's admin panel
WHERE TenantId = ctx.TenantId AND UserId = <id>

// Safe: listing providers I manage (provider is tenant-scoped in CareConnect)
WHERE TenantId = ctx.TenantId AND Status = 'Active'
```

---

## 8. Authorization Boundary Rules

### Layer 1: Route-Level Policy (gateway + service Program.cs)

Coarse check — "does this caller have the right product role for this route group?"

```csharp
// Enforced by ASP.NET Core authorization middleware
app.MapGroup("/api/referrals")
   .RequireAuthorization(Policies.CanReferCareConnect);  // product_roles contains CARECONNECT_REFERRER

app.MapGroup("/api/applications")
   .RequireAuthorization(Policies.AuthenticatedUser);    // authenticated user, any org type
```

### Layer 2: Capability Check (per endpoint)

Fine-grained check — "does this caller have the exact capability for this operation?"

```csharp
await authz.RequireCapabilityAsync(ctx, CapabilityCodes.ReferralCreate, ct);
// Checks: product_roles → capabilities map via ICapabilityService
// Platform admins bypass all capability checks
```

### Layer 3: Participant Organization Check (per query/command)

Record-level check — "is this caller's org a participant in this specific record?"

```csharp
// After fetching the record:
if (lien.SellingOrganizationId != ctx.OrgId &&
    lien.BuyingOrganizationId  != ctx.OrgId &&
    lien.HoldingOrganizationId != ctx.OrgId &&
    !ctx.IsPlatformAdmin)
    throw new ForbiddenException("lien:read");
```

### Reusable Guard Pattern

```csharp
// BuildingBlocks.Authorization — reusable static guards
public static class OrgParticipantGuard
{
    public static void RequireParticipant(
        ICurrentRequestContext ctx,
        params Guid?[] participantOrgIds)
    {
        if (ctx.IsPlatformAdmin) return;

        var orgId = ctx.OrgId;
        if (orgId is null || !participantOrgIds.Any(id => id == orgId))
            throw new ForbiddenException("org_participant_required");
    }

    public static void RequireIsSellingOrg(ICurrentRequestContext ctx, Guid? sellingOrgId)
        => RequireParticipant(ctx, sellingOrgId);

    public static void RequireIsBuyingOrHoldingOrg(
        ICurrentRequestContext ctx, Guid? buyingOrgId, Guid? holdingOrgId)
        => RequireParticipant(ctx, buyingOrgId, holdingOrgId);
}
```

Usage:

```csharp
// In application service
var lien = await _liens.GetByIdAsync(id, ct)
    ?? throw new NotFoundException($"Lien '{id}' not found.");

OrgParticipantGuard.RequireParticipant(ctx,
    lien.SellingOrganizationId,
    lien.BuyingOrganizationId,
    lien.HoldingOrganizationId);
```

### What Must Always Be Checked (Service Layer Checklist)

```
✓ Capability check (RequireCapabilityAsync)
✓ Participant organization check (OrgParticipantGuard or inline WHERE predicate)
✓ Record existence check (throw NotFoundException, not ForbiddenException, if record missing)
✗ Do NOT use TenantId as the sole access guard on cross-tenant records
✗ Do NOT skip capability check when IsPlatformAdmin (the bypass is in AuthorizationService already)
```

---

## 9. Observability and Audit

### Correlation IDs

```
Client request
  → Gateway injects X-Correlation-Id (UUID)
  → YARP forwards X-Correlation-Id to upstream
  → Service reads from ctx.CorrelationId (HttpContext.Request.Headers)
  → Service includes CorrelationId in all log messages (log scope)
  → Service returns X-Correlation-Id on response
  → Client uses CorrelationId for support tickets / distributed tracing
```

Log scope pattern in each service:

```csharp
using (_logger.BeginScope(new Dictionary<string, object>
{
    ["CorrelationId"] = ctx.CorrelationId ?? "none",
    ["TenantId"]      = ctx.TenantId?.ToString() ?? "none",
    ["UserId"]        = ctx.UserId?.ToString() ?? "none",
    ["OrgId"]         = ctx.OrgId?.ToString() ?? "none",
}))
{
    _logger.LogInformation("Creating referral for org {OrgId}", ctx.OrgId);
    // ...
}
```

### Audit Fields

Every `AuditableEntity` has:

```
CreatedByUserId  = ctx.UserId
UpdatedByUserId  = ctx.UserId
CreatedAtUtc     = DateTime.UtcNow
UpdatedAtUtc     = DateTime.UtcNow
```

`StatusHistory` records additionally store `ChangedByOrgId = ctx.OrgId` for cross-org audit trails.

### Logging Sensitive Claims Safely

```csharp
// NEVER log:
_logger.LogInformation("Login by {Email} with token {Token}", email, token);  // ❌

// Safe patterns:
_logger.LogInformation("User {UserId} (tenant {TenantId}) created referral {ReferralId}",
    ctx.UserId, ctx.TenantId, referral.Id);  // ✓ IDs only

_logger.LogInformation("Org {OrgId} ({OrgType}) purchased lien {LienId}",
    ctx.OrgId, ctx.OrgType, lien.Id);        // ✓ no PII

// Email: log only in Identity service audit logs, at Debug level, gated on env
if (_env.IsDevelopment())
    _logger.LogDebug("Login attempt for {Email}", email);  // ✓ dev only
```

### Log Fields by Layer

| Layer | Fields to always log |
|---|---|
| **Gateway** | `CorrelationId`, resolved `TenantId`, route, method, response status, latency ms |
| **Service (request)** | `CorrelationId`, `TenantId`, `UserId`, `OrgId`, `OrgType`, endpoint |
| **Service (domain event)** | `CorrelationId`, entity ID, status transition, `ChangedByUserId`, `ChangedByOrgId` |
| **Error** | Above + exception type (never stack trace to client) |

---

## 10. Risks and Failure Modes

### 10.1 Tenant Resolution Mismatch

**Risk:** The `TenantDomains` table is stale or the subdomain mapping is wrong — a request from `firm-a.legalsynq.com` resolves to `firm-b`'s TenantId.

**Mitigation:**
- `TenantDomains.Domain` has a unique index — no two tenants can claim the same domain.
- Domain changes require an admin operation through the Identity service (not direct DB writes).
- The cross-check in `TenantClaimCrossCheckMiddleware` (step [7]) rejects requests where `JWT.tenant_id ≠ resolved TenantId`. Even if the resolution table is wrong, the user's JWT (issued at login by their own tenant) will fail the cross-check.

### 10.2 Host Header Spoofing

**Risk:** An attacker sends a request with a forged `Host: admin.legalsynq.com` header to escalate tenant access.

**Mitigation:**
- In production, the platform is behind a load balancer / TLS terminator. Configure it to set the `Host` header from the TLS SNI value, not from the client's `Host` header.
- The cross-check (§10.1) means a spoofed host resolves to a different `TenantId` than the JWT's `tenant_id` → 401 rejected.
- Add `AllowedHosts` configuration in ASP.NET Core (`"AllowedHosts": "*.legalsynq.com"`) as an additional guard.

### 10.3 JWT Valid But Wrong Tenant Subdomain

**Risk:** A user from `firm-a.legalsynq.com` copies their JWT and sends a request to `firm-b.legalsynq.com`. The JWT is cryptographically valid but was issued for a different tenant.

**Mitigation:**
- `TenantClaimCrossCheckMiddleware` (step [7]) detects `JWT.tenant_id (Tenant A) ≠ resolved TenantId (Tenant B)` → 401 with `{"error": "tenant_mismatch"}`.
- In development mode, this check is relaxed but logged as a warning.

### 10.4 Missing Org Context

**Risk:** A user authenticated without an org (e.g., a recently-invited user who hasn't joined an org yet) calls an org-scoped endpoint. `ctx.OrgId` is null.

**Mitigation:**
- The JWT `org_id` claim is only included when the user has an org membership. Endpoints that require `ctx.OrgId` throw `InvalidOperationException("org_id claim missing.")` — surfaced as 500 by the exception middleware.
- **Better approach**: add an `OrgRequired` policy that fails with 403 instead of 500:

```csharp
options.AddPolicy("OrgRequired", policy =>
    policy.RequireClaim("org_id"));
```

- Apply `OrgRequired` policy to all endpoints that read `ctx.OrgId`.
- Document at the Identity service level that users without org membership cannot access product endpoints.

### 10.5 Service Accidentally Uses Tenant-Only Filtering

**Risk:** A developer adds `WHERE TenantId = ctx.TenantId` to a query on a cross-tenant resource (e.g., `Applications` or `Liens`). A funder in Tenant B sees no applications because the records belong to Tenant A.

**Mitigation:**
- Code review checklist item: "Does this query filter by `TenantId` on a record type that participates in cross-org workflows?"
- Lint rule: search for `ctx.TenantId` in repository implementations of `ApplicationRepository`, `LienRepository`; flag any direct equality filter.
- Integration test: seed an application under `TenantId = A`, query with `ctx.TenantId = B` but `ctx.OrgId = ReceivingOrganizationId`; assert the record IS returned.

### 10.6 Capability Cache Stale After Role Change

**Risk:** A user's product roles change (e.g., org's subscription is downgraded). The `ICapabilityService` caches the old capability set and the user retains access for the cache TTL.

**Mitigation:**
- Cache key includes the full sorted product-role set (not user ID), so role changes reflected in a new JWT immediately get a new cache key.
- JWT has a configurable expiry (default 60 minutes in production, 480 in dev). A role change requires re-login to get a new JWT with updated `product_roles`.
- For immediate revocation, implement short-lived JWTs (15 minutes) with refresh tokens — planned for Phase 2.

### 10.7 Gateway Unavailable — Direct Service Access

**Risk:** An internal consumer (another service) or attacker calls a service directly on port 5001–5003, bypassing the gateway's tenant resolution and header stripping.

**Mitigation:**
- In production, services must not be exposed on public ports. Only the gateway's port (5000 / 443) is public. Use a private VPC subnet or network policy.
- Each service independently validates the JWT (same `AddAuthentication` setup). A request without a valid JWT is rejected even if the gateway is bypassed.
- Capabilities and org-participant checks in the service layer are independent of the gateway and remain enforced.
- Add `X-Gateway-Secret` header validation in services as an additional internal layer (optional hardening — services verify the request was forwarded by the known gateway instance).

---

*Document status: Design complete. Implementation sequencing: (1) Add `CorrelationId` to `ICurrentRequestContext` + `CurrentRequestContext` in BuildingBlocks → (2) Add `CorrelationIdMiddleware` to gateway → (3) Add `TenantResolutionMiddleware` to gateway (reads from `TenantDomains` via a lightweight `GatewayDbContext`) → (4) Add `TenantClaimCrossCheckMiddleware` → (5) Add header-strip transforms to YARP config → (6) Add `OrgRequired` policy to BuildingBlocks → (7) Add `OrgParticipantGuard` to BuildingBlocks.Authorization → (8) Propagate log scopes to all service endpoints.*
