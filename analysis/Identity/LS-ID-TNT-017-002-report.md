# LS-ID-TNT-017-002 — User Access Audit: Implementation Report

**Status:** COMPLETE  
**Date:** 2026-04-19  
**Ticket:** Capture canonical audit events for access/auth surfaces beyond permission changes.

---

## Scope

Instrument denied product/permission access, session invalidation, and stale access-version token
rejection with canonical audit events flowing through the existing `IAuditEventClient` pipeline.
Maintain fail-safe non-blocking behavior, tenant isolation, and avoid noisy over-capture.

---

## Pre-existing Coverage (No Changes Required)

These events were already fully instrumented before this ticket:

| Event Type | Source | Trigger |
|---|---|---|
| `identity.user.login.succeeded` | `AuthService.LoginAsync` | Successful credential auth |
| `identity.user.login.failed` | `AuthService.EmitLoginFailed` | Bad credentials, unknown user/tenant |
| `identity.user.login.blocked` | `AuthService.EmitLockedLoginBlocked` | Locked account login attempt |
| `identity.user.logout` | `AuthEndpoints` | Explicit logout |
| `identity.user.password_reset_requested` | `AuthEndpoints` | Self-service password reset request |

---

## Gaps Closed by This Ticket

### 1. Product Access Denied (Authorization Filters)

**File changed:** `shared/building-blocks/BuildingBlocks/Authorization/Filters/RequireProductAccessFilter.cs`  
**Project change:** `BuildingBlocks.csproj` now references `LegalSynq.AuditClient` (optional resolution pattern).

Three filter classes now emit canonical denied-access events when their DENY branches fire:

#### `RequireProductAccessFilter` — new event: `security.product.access.denied`
- **Trigger:** User lacks the required product claim (e.g. `SYNQ_LIENS`).
- **Reason code:** `NoProductAccess`

#### `RequireProductRoleFilter` — two new events:
- `security.product.access.denied` (reason: `NoProductAccess`) — user lacks the product entirely.
- `security.product.role.denied` (reason: `InsufficientRole`) — user has the product but not the required role.

#### `RequireOrgProductAccessFilter` — new event: `security.product.access.denied`
- **Trigger:** Org-scoped product check fails (`NoProductAccess`).
- **Excluded:** `OrgContextMissing` — this is a configuration/setup issue, not a user access denial; still logged to `ILogger` only.

#### `RequirePermissionFilter` — two new events:
- `security.permission.denied` (reason: `MissingPermission`) — user lacks the permission claim.
- `security.permission.policy.denied` (reason: `PolicyDenied:<reason>`) — user has the claim but the ABAC policy engine denied it.

**Fail-safe pattern used** (identical to the existing `ILogger` resolution in these filters):
```csharp
var auditClient = httpContext.RequestServices.GetService(typeof(IAuditEventClient)) as IAuditEventClient;
if (auditClient is null) return; // graceful no-op if not registered

var now = DateTimeOffset.UtcNow;
_ = auditClient.IngestAsync(new IngestAuditEventRequest { ... }); // fire-and-observe
```

Services that do not register `IAuditEventClient` degrade silently. No new DI requirement is imposed on any consumer.

**Not instrumented** (intentional):
- `ALLOW` paths in all filters — to avoid high-volume noise from every authorized request.
  This is the ticket's explicit "selective successful protected access" guidance: for these
  cross-cutting shared filters, emitting on every authorized request would be prohibitively noisy
  (multiple events per page load). Deferred to a future analytics/sampling ticket.

---

### 2. Session Invalidation

**File changed:** `apps/services/identity/Identity.Application/Services/AuthService.cs`  
(within `GetCurrentUserAsync`, which serves `/api/auth/me`)

Two new `EmitXxx` helper methods added, following the exact same fire-and-observe pattern as
`EmitLoginFailed` and `EmitLockedLoginBlocked`.

#### `identity.session.invalidated`
- **Trigger:** `session_version` in JWT is older than the DB value (force-logout or account lock since last login).
- **Metadata:** `{ reason: "SessionVersionStale" }`
- **Severity:** `Warn`

#### `identity.access.version.stale`  
- **Trigger:** `access_version` in JWT is older than the DB value (permissions changed since last login; user must re-authenticate to get fresh claims).
- **Metadata:** `{ reason: "AccessVersionStale", tokenAccessVersion, currentAccessVersion }`
- **Severity:** `Warn`

Both events are emitted immediately before the `UnauthorizedAccessException` is thrown. They never gate the rejection response.

---

## Audit Event Catalogue (Complete Post-Ticket)

| Event Type | Category | Severity | Source System | Trigger |
|---|---|---|---|---|
| `identity.user.login.succeeded` | Security | Info | identity-service | Successful login |
| `identity.user.login.failed` | Security | Warn | identity-service | Failed login attempt |
| `identity.user.login.blocked` | Security | Warn | identity-service | Locked account login |
| `identity.user.logout` | Security | Info | identity-service | Explicit logout |
| `identity.user.password_reset_requested` | Security | Info | identity-service | Password reset request |
| `identity.session.invalidated` | Security | Warn | identity-service | Stale session_version (force-logout) |
| `identity.access.version.stale` | Security | Warn | identity-service | Stale access_version (perm change) |
| `security.product.access.denied` | Security | Warn | authorization | No product access in JWT |
| `security.product.role.denied` | Security | Warn | authorization | Insufficient product role |
| `security.permission.denied` | Security | Warn | authorization | Missing permission claim |
| `security.permission.policy.denied` | Security | Warn | authorization | ABAC policy evaluation denied |

---

## Architectural Decision: Optional Audit Client Resolution

`BuildingBlocks.csproj` did not previously reference `LegalSynq.AuditClient`.

**Decision:** Add it as a project reference, but resolve via `GetService` (not constructor injection).

**Rationale:**
- `LegalSynq.AuditClient` depends only on `Microsoft.Extensions.*` packages — it is lightweight and
  genuinely cross-cutting (audit is a universal concern, not domain-specific).
- Resolving optionally means no breaking change for services that choose not to register it.
- This avoids inventing an intermediate abstraction (`ISecurityAuditPublisher`) just to decouple
  a lightweight dependency.
- Consistent with the `ILogger` resolution pattern already used in the same filter classes.

---

## Build Verification

All four affected projects build cleanly with zero errors:

```
dotnet build shared/building-blocks/BuildingBlocks/BuildingBlocks.csproj       ✅ 0 errors
dotnet build Identity.Application/Identity.Application.csproj                  ✅ 0 errors
dotnet build Identity.Api/Identity.Api.csproj                                  ✅ 0 errors
dotnet build BuildingBlocks.Tests/BuildingBlocks.Tests.csproj                  ✅ 0 errors
```

---

## Files Changed

| File | Change |
|---|---|
| `shared/building-blocks/BuildingBlocks/BuildingBlocks.csproj` | Added `ProjectReference` to `LegalSynq.AuditClient` |
| `shared/building-blocks/BuildingBlocks/Authorization/Filters/RequireProductAccessFilter.cs` | Added `using` statements, `EmitXxx` helpers, and call sites in all four filter DENY branches |
| `apps/services/identity/Identity.Application/Services/AuthService.cs` | Added `EmitSessionInvalidated`, `EmitAccessVersionStale` helpers + call sites in `GetCurrentUserAsync` |

---

## Deferred (Out of Scope for This Ticket)

- **Successful protected access audit** — explicitly deferred. The ticket acknowledges "selective"
  capture is required to avoid noise. Cross-cutting filter ALLOW paths would fire multiple times per
  page load. Recommend a future sampling or first-access-per-session strategy.
- **Frontend auth guard denials** — `/access-denied` page redirects are Next.js-only and have no
  direct server-side audit pathway without a dedicated BFF audit call.
- **Batch/administrative session invalidation** — force-logout via Admin API increments
  `SessionVersion`; individual session invalidation is captured when `/auth/me` is next called.
