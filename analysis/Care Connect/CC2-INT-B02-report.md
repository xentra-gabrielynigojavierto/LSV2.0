# CC2-INT-B02 Report

**Task:** Gateway + Audit + Monitoring Wiring for CareConnect  
**Author:** Agent  
**Date:** April 21, 2026  
**Status:** COMPLETE

---

## 1. Summary

CC2-INT-B02 audits and hardens the integration layer between CareConnect and the LegalSynq platform. Five areas are covered:

| Part | Scope | Outcome |
|------|-------|---------|
| A | Gateway routing audit | No structural changes needed — 4 correct YARP routes confirmed; 1 cosmetic order-collision documented |
| B | Audit event alignment + CorrelationId/RequestId propagation | `IHttpContextAccessor` injected into all 4 audit-emitting services; 13 `RequestId` fields populated across all audit events |
| C | Monitoring / health check strengthening | `/health` endpoint upgraded from static stub to live DB connectivity probe |
| D | Internal endpoint hardening | `InternalProvisionEndpoints` migrated from custom `X-Internal-Service-Token` header pattern to platform `ServiceToken` bearer (`AddServiceTokenBearer` + `ServiceOnly` policy) |
| E | Correlation propagation + build verification | All 4 modified services compile clean; 0 warnings after changes |
| F | Runtime fix — JwtBearer version alignment | `CareConnect.Api.csproj` JwtBearer upgraded `8.0.8` → `8.0.*`; startup `FileNotFoundException` eliminated; `/health` verified live returning `{"status":"healthy","db":"connected"}` |

**Files changed:** 11 source files + csproj, 1 appsettings.json, 1 new report.

---

## 2. Part A — Gateway Routing Audit

### 2.1 CareConnect Routes Verified

```
Route                      Order  AuthPolicy   Path pattern
─────────────────────────────────────────────────────────────
careconnect-service-health    20  Anonymous    /careconnect/health
careconnect-service-info      21  Anonymous    /careconnect/info
careconnect-internal-block    22  Deny         /careconnect/internal/{**catch-all}
careconnect-protected        120  (default)    /careconnect/{**catch-all}
```

**Assessment:**
- Route ordering is correct — the Deny block (Order 22) fires before the catch-all (Order 120) for any `/careconnect/internal/*` request.
- `Deny` policy: YARP applies ASP.NET Core authorization middleware before proxying. Requests to `/careconnect/internal/**` are rejected with 403 at the gateway. The CareConnect service never receives them from external callers.
- The catch-all `careconnect-protected` carries no explicit `AuthorizationPolicy` which means ASP.NET Core's default (fallback) policy applies. The CareConnect service applies its own auth middleware — per-endpoint policies are the source of truth downstream.
- Cluster destination `http://localhost:5003` matches CareConnect port in `appsettings.json` (`"Urls": "http://0.0.0.0:5003"`). ✅

**No changes to Gateway routing were required.**

### 2.2 Issues Found (Cosmetic / Non-Blocking)

**GAP-B02-001 — Monitoring route order collision with Documents routes**

The monitoring route block uses orders 50–58, colliding with Documents routes at 50–53:

```
monitoring-service-health  Order 50  /monitoring/health
documents-service-health   Order 50  /documents/health   ← same order number
monitoring-entities-read   Order 51  /monitoring/monitoring/entities
documents-access-tokens    Order 51  /documents/access/**
```

Since the path patterns are fully disjoint (`/monitoring/**` vs `/documents/**`), YARP resolves matches by longest-prefix first and order is only the tiebreaker. **No requests are mis-routed.** However, this non-sequential ordering is a maintenance concern that should be cleaned up in a dedicated gateway refactor task. Does not affect CareConnect.

---

## 3. Part B — Audit Event Wiring + CorrelationId Propagation

### 3.1 Pre-Change Audit Coverage

Audit events were already emitted across 4 CareConnect services:

| Service | IngestAsync calls | Pattern |
|---------|------------------|---------|
| `ReferralService` | 9 | Fire-and-forget (`_ = ...`) |
| `AppointmentService` | 2 | Fire-and-forget |
| `ActivationRequestService` | 1 | fire-and-return (`return _auditClient.IngestAsync(...)`) |
| `AutoProvisionService` | 1 | Awaited inside Task.Run |

All events included: `EventType`, `EventCategory`, `SourceSystem`, `SourceService`, `Visibility`, `Severity`, `OccurredAtUtc`, `Scope`, `Actor`, `Entity`, `Action`, `Description`, `Outcome`, `Metadata`, `IdempotencyKey`, `Tags`.

**Missing:** `CorrelationId`, `RequestId`, `SessionId` — none were populated.

### 3.2 Changes Made

**All 4 services now inject `IHttpContextAccessor`** and populate `RequestId` in every audit event:

```csharp
// Constructor change (same pattern in all 4 services)
private readonly IHttpContextAccessor _httpContextAccessor;

public ReferralService(
    ...
    IHttpContextAccessor httpContextAccessor,
    ...)
{
    ...
    _httpContextAccessor = httpContextAccessor;
}

// Audit event change (applied to all 13 IngestAsync calls)
_ = _auditClient.IngestAsync(new IngestAuditEventRequest
{
    ...
    RequestId      = _httpContextAccessor.HttpContext?.TraceIdentifier,
    IdempotencyKey = IdempotencyKey.For(...),
    Tags           = [...],
});
```

**Propagation count:**

| Service | Events updated |
|---------|---------------|
| `ReferralService` | 9 |
| `AppointmentService` | 2 |
| `ActivationRequestService` | 1 |
| `AutoProvisionService` | 1 |
| **Total** | **13** |

`HttpContext.TraceIdentifier` is the ASP.NET Core request trace ID (set by the framework, format: `{ConnectionId}:{RequestNumber}`). This is null-safe: if called outside an HTTP context (background work), the field is simply omitted.

### 3.3 Remaining Gaps

**GAP-B02-002 — `CorrelationId` not yet propagated from upstream headers**

`RequestId` (TraceIdentifier) is now populated, but `CorrelationId` remains null. True end-to-end correlation requires extracting an upstream `X-Correlation-ID` header set by the API Gateway (or client) and threading it through. This requires a correlation middleware and a shared `ICorrelationContext` abstraction. Recommended as a follow-up task scoped to all platform services together.

**GAP-B02-003 — `SourceService` is inconsistent across events**

Some events set `SourceService = "referral-api"` while others set `"referral-service"`. The `AuditClient` config already sets `"SourceService": "referral-api"` as the default. Recommend standardising to `"referral-api"` across all events in a cleanup pass.

---

## 4. Part C — Monitoring / Health Check Strengthening

### 4.1 Pre-Change Health Endpoint

```csharp
// Before: static stub — always returns healthy regardless of DB state
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();
```

This endpoint reported `healthy` even if the MySQL database was unreachable, making it useless for the platform monitoring service's liveness checks.

### 4.2 Change Made

```csharp
// After: live DB probe using EF Core CanConnectAsync (SELECT 1)
app.MapGet("/health", async (CareConnectDbContext db, CancellationToken ct) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync(ct);
        var dbStatus   = canConnect ? "connected" : "unreachable";
        return canConnect
            ? Results.Ok(new { status = "healthy", db = dbStatus })
            : Results.Json(new { status = "degraded", db = dbStatus },
                statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception)
    {
        return Results.Json(new { status = "degraded", db = "error" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).AllowAnonymous();
```

**Behaviour:**
- DB reachable → `200 { status: "healthy", db: "connected" }`
- DB unreachable → `503 { status: "degraded", db: "unreachable" }`
- Exception → `503 { status: "degraded", db: "error" }`

The 503 response code enables the platform monitoring service and orchestrators (Docker/Kubernetes) to properly detect failures. The check is lightweight (`CanConnectAsync` executes a `SELECT 1` with no query overhead).

**Startup diagnostics (pre-existing, confirmed correct):**
- `MigrationCoverageProbe.RunAsync(db)` — validates EF model vs live schema ✅
- Providers/Facilities linkage health — logs orphan counts ✅

---

## 5. Part D — Internal Endpoint Hardening

### 5.1 Pre-Change State

`InternalProvisionEndpoints` used a **custom shared-secret header** for authentication:

```csharp
private const string InternalTokenHeader = "X-Internal-Service-Token";

routes.MapPost("/internal/provision-provider", ProvisionProvider)
    .AllowAnonymous();   // ← auth done manually inside handler

// Inside handler:
var configToken = httpContext.RequestServices
    .GetService<IConfiguration>()?["InternalServiceToken"];

if (string.IsNullOrEmpty(configToken))
    return Results.Problem(statusCode: 503);   // ← failed closed but opaque

var token = httpContext.Request.Headers[InternalTokenHeader].FirstOrDefault();
if (string.IsNullOrEmpty(token) || token != configToken)
    return Results.Unauthorized();
```

**Problems with this pattern:**
1. `InternalServiceToken` was never provisioned — the endpoint always returned 503 in production.
2. Custom header auth is not auditable through the platform auth middleware stack.
3. Inconsistent with every other service (Task, Flow, Liens) which use `ServiceToken` bearer.
4. `AllowAnonymous()` on the route bypassed ASP.NET Core auth middleware entirely.

### 5.2 Changes Made

**`Program.cs` — Added ServiceTokenBearer scheme + ServiceOnly policy:**

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => { ... })            // existing user JWT
    .AddServiceTokenBearer(builder.Configuration); // NEW — M2M service tokens

options.AddPolicy("ServiceOnly", policy =>
    policy
        .AddAuthenticationSchemes(ServiceTokenAuthenticationDefaults.Scheme)
        .RequireRole(ServiceTokenAuthenticationDefaults.ServiceRole));
```

The signing key is read from `FLOW_SERVICE_TOKEN_SECRET` environment variable (already provisioned). The `ServiceTokens` config section was added to `appsettings.json` for issuer/audience defaults:

```json
"ServiceTokens": {
    "Issuer": "legalsynq-service-tokens",
    "Audience": "flow-service"
}
```

**`InternalProvisionEndpoints.cs` — Migrated to service token bearer:**

```csharp
// After: clean, platform-standard auth
routes.MapPost("/internal/provision-provider", ProvisionProvider)
    .RequireAuthorization("ServiceOnly");

// Handler: no manual token check — auth middleware handles it
private static async Task<IResult> ProvisionProvider(
    ProvisionProviderRequest body,
    IProviderRepository providers,
    CancellationToken ct)
{
    // Validation only — auth already enforced by middleware
    ...
}
```

**Caller requirements:** Any service calling `POST /careconnect/internal/provision-provider` must:
1. Mint an HS256 service token via `IServiceTokenIssuer.IssueAsync(tenantId)`.
2. Set `Authorization: Bearer <token>` on the request.
3. The token must have `sub: "service:*"` and a `tenant_id`/`tid` claim.

The Identity service provisioning path (the expected caller) should adopt `AddServiceTokenIssuer` to mint these tokens. This is a follow-up task.

### 5.3 Security Improvement Summary

| Aspect | Before | After |
|--------|--------|-------|
| Auth mechanism | Custom `X-Internal-Service-Token` header | Platform HS256 JWT service token |
| Secret provision | `InternalServiceToken` (never set → 503) | `FLOW_SERVICE_TOKEN_SECRET` (provisioned) |
| Middleware integration | None (`AllowAnonymous`) | Full ASP.NET Core auth stack |
| Audit trail | None | Token sub/claims visible in request context |
| Gateway double-block | Gateway Deny policy | Gateway Deny policy (unchanged — defence in depth) |

---

## 6. Part E — Correlation Propagation + Build Verification

### 6.1 Build Results

All affected projects compiled successfully with 0 errors, 0 warnings:

```
CareConnect.Application  — build OK (ReferralService, AppointmentService,
                            ActivationRequestService, AutoProvisionService)
CareConnect.Api          — build OK (Program.cs, InternalProvisionEndpoints.cs)
```

### 6.2 Correlation Wiring Summary

`RequestId` is now threaded from HTTP context into every audit event:

```
HTTP request (TraceIdentifier = "0HN1234:1")
  → ReferralService.CreateAsync(...)
    → _auditClient.IngestAsync(new IngestAuditEventRequest {
          RequestId = "0HN1234:1",   ← NEW
          IdempotencyKey = ...,
          ...
      })
      → POST http://localhost:5007/internal/audit/events
```

### 6.3 Platform Auth Alignment

| Scheme | Used By | CareConnect Support |
|--------|---------|---------------------|
| JwtBearer (HS256 user tokens) | User API calls | ✅ (existing) |
| ServiceToken (HS256 M2M tokens) | Internal service calls | ✅ (NEW — Part D) |

Both schemes now operate side-by-side via multi-scheme authentication. The `AuthenticatedUser` policy (used by most CareConnect endpoints) could be extended to accept both schemes (as done in the Task service) — deferred as follow-up.

---

## 7. Issues and Gaps

| ID | Severity | Description | Status |
|----|----------|-------------|--------|
| GAP-B02-001 | Low | Gateway monitoring routes use duplicate order numbers (50–58) colliding with Documents (50–53). No routing errors, cosmetic only. | Open |
| GAP-B02-002 | Medium | `CorrelationId` field in audit events is not yet populated. Requires cross-gateway `X-Correlation-ID` header propagation middleware. | Open |
| GAP-B02-003 | Low | `SourceService` value is inconsistent across audit events ("referral-api" vs "referral-service"). | Open |
| GAP-B02-004 | Medium | Identity service provisioning caller has not yet adopted `IServiceTokenIssuer` — must be updated to use Bearer token when calling `/internal/provision-provider`. Until updated, the endpoint will return 401. | Open — Caller-side task needed |
| GAP-B02-005 | Low | `AuthenticatedUser` policy does not include `ServiceTokenAuthenticationDefaults.Scheme`. Service-token callers cannot hit user-facing endpoints. Intentional for now but should be reviewed if M2M delegation is needed. | Documented |

---

## 8. Files Changed

| File | Change |
|------|--------|
| `CareConnect.Api/Program.cs` | Added `AddServiceTokenBearer`, `ServiceOnly` policy, enhanced `/health` endpoint with DB probe |
| `CareConnect.Api/Endpoints/InternalProvisionEndpoints.cs` | Migrated from `X-Internal-Service-Token` custom header to `RequireAuthorization("ServiceOnly")` |
| `CareConnect.Api/appsettings.json` | Added `ServiceTokens` section with default issuer/audience |
| `CareConnect.Application/Services/ReferralService.cs` | Added `IHttpContextAccessor`, `RequestId` in 9 audit events |
| `CareConnect.Application/Services/AppointmentService.cs` | Added `IHttpContextAccessor`, `RequestId` in 2 audit events |
| `CareConnect.Application/Services/ActivationRequestService.cs` | Added `IHttpContextAccessor`, `RequestId` in 1 audit event |
| `CareConnect.Application/Services/AutoProvisionService.cs` | Added `IHttpContextAccessor`, `RequestId` in 1 audit event |
| `CareConnect.Api/CareConnect.Api.csproj` | Upgraded `JwtBearer` pin from `8.0.8` → `8.0.*` to match `BuildingBlocks` resolution (8.0.26); eliminated `FileNotFoundException` crash at startup |
| `CareConnect.Tests/Application/ActivationQueueTests.cs` | Added `Mock<IHttpContextAccessor>().Object` to `ActivationRequestService` constructor call |
| `CareConnect.Tests/Application/AutoProvisionTests.cs` | Added `Mock<IHttpContextAccessor>().Object` to `AutoProvisionService` constructor call |
| `CareConnect.Tests/Application/ProviderActivationFunnelTests.cs` | Added `Mock<IHttpContextAccessor>().Object` to `ReferralService` constructor call |
