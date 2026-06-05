# BLK-GOV-02 — Governance Enforcement & Least-Privilege Model
## Security Review & Implementation Report

**Block:** BLK-GOV-02  
**Parent window:** TENANT-STABILIZATION (2026-04-23 → 2026-05-07)  
**Commit:** `91122b779a3cd567ca3a69b934ed46cc561351ff`  
**Preceded by:** BLK-GOV-01 (commit `744b5b3635b7c482a539a70a7a4247ab9a66a2ba`)  
**Status:** COMPLETE  
**Artifacts:**  
- Diff: `analysis/BLK-GOV-02-commit.diff.txt`  
- Summary: `analysis/BLK-GOV-02-commit-summary.md`

---

## 1. Objective

BLK-GOV-01 audited every admin endpoint and documented the existing tenant-scope enforcement model. BLK-GOV-02 acts on those findings: it centralizes enforcement in a reusable, testable helper (`AdminTenantScope`) and migrates all admin endpoints to use it, eliminating the per-file inline patterns that were hard to audit and easy to drift.

Secondary objective: fix three PlatformAdmin 500 gaps discovered during BLK-GOV-01 analysis on `ProviderAdminEndpoints.cs`.

---

## 2. Pre-Existing State (from BLK-GOV-01)

BLK-GOV-01 found the following inline pattern repeated independently across seven CareConnect admin endpoint files:

```csharp
// Pattern A — PlatformWide (appeared in 5 files)
Guid? scopeTenantId = ctx.IsPlatformAdmin
    ? null
    : (ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing."));

// Pattern B — SingleTenant (appeared in 2 files)
Guid scopeTenantId;
if (ctx.IsPlatformAdmin)
{
    if (!tenantId.HasValue)
        return Results.BadRequest(new { error = "..." });
    scopeTenantId = tenantId.Value;
}
else
{
    scopeTenantId = ctx.TenantId ?? throw new InvalidOperationException("...");
}

// Pattern C — CheckOwnership (appeared in 3 handlers, 2 files)
if (!ctx.IsPlatformAdmin)
{
    var callerTenantId = ctx.TenantId ?? throw ...;
    if (detail.TenantId != callerTenantId) return Results.Forbid();
}
```

**Deficiencies identified in BLK-GOV-01:**
1. Pattern A and C were correct but untestable (no unit-testable abstraction).
2. Pattern B was absent from three ProviderAdminEndpoints handlers that required it → PlatformAdmin 500 on those endpoints.
3. No structured logging on governance denials — silent failures in production.
4. No single place to patch or evolve enforcement logic.

---

## 3. Solution: `AdminTenantScope` Helper

### 3.1 Location

```
shared/building-blocks/BuildingBlocks/Authorization/AdminTenantScope.cs
```

Lives in `BuildingBlocks.Authorization` alongside `Policies.cs` and `RequireProductRoleFilter.cs`.

### 3.2 API Surface

```csharp
// Mode 1 — PlatformWide
// PlatformAdmin → TenantId = null (platform-wide view)
// TenantAdmin   → TenantId = ctx.TenantId (own-tenant scope)
AdminScopeResult AdminTenantScope.PlatformWide(ICurrentRequestContext ctx, HttpContext? http = null)

// Mode 2 — SingleTenant
// PlatformAdmin → TenantId = explicitTenantId (400 if null)
// TenantAdmin   → TenantId = ctx.TenantId (explicit param ignored for safety)
AdminScopeResult AdminTenantScope.SingleTenant(ICurrentRequestContext ctx, Guid? explicitTenantId, HttpContext? http = null)

// Mode 3 — CheckOwnership
// PlatformAdmin → null (allow)
// TenantAdmin   → null (allow) OR Results.Forbid() (deny cross-tenant)
IResult? AdminTenantScope.CheckOwnership(ICurrentRequestContext ctx, Guid resourceTenantId, HttpContext? http = null)
```

### 3.3 `AdminScopeResult` Struct

```csharp
public readonly struct AdminScopeResult
{
    public bool     IsError        { get; }  // true  → return Error immediately
    public Guid?    TenantId       { get; }  // null  → IsPlatformWide
    public bool     IsPlatformWide { get; }  // true  → PlatformAdmin, no tenant filter
    public IResult? Error          { get; }  // populated when IsError
}
```

### 3.4 Governance Denial Logging

All denial paths emit a `Warning`-level structured log entry via `ILoggerFactory` resolved from `HttpContext.RequestServices`. This follows the same service-locator pattern as `RequireProductRoleFilter`.

Log category: `BuildingBlocks.Authorization.AdminTenantScope`

Example log output:
```
[WARN] GovernanceDenial: PlatformAdmin userId=<guid> called single-tenant endpoint at
       /api/admin/providers/<id>/link-organization without supplying ?tenantId.
```

---

## 4. Endpoint Migration Map

| Endpoint Handler | File | Mode | Change |
|-----------------|------|------|--------|
| `GetMetricsAsync` | `AnalyticsEndpoints` | `PlatformWide` | Replaces inline ternary |
| `GetPerformanceAsync` | `PerformanceEndpoints` | `PlatformWide` | Replaces inline ternary |
| `GetDashboardAsync` | `AdminDashboardEndpoints` | `PlatformWide` | Replaces inline ternary |
| `GetBlockedProvidersAsync` | `AdminDashboardEndpoints` | `PlatformWide` | Replaces inline ternary |
| `GetAdminReferralsAsync` | `AdminDashboardEndpoints` | `PlatformWide` | Replaces ternary; preserves optional `?tenantId` narrow for PlatformAdmin |
| `GetPendingAsync` | `ActivationAdminEndpoints` | `PlatformWide` | Replaces inline `IsPlatformAdmin` branch |
| `GetByIdAsync` | `ActivationAdminEndpoints` | `CheckOwnership` | Replaces inline `if (detail.TenantId != callerTenantId) Forbid()` |
| `ApproveAsync` | `ActivationAdminEndpoints` | `CheckOwnership` | Same; prefetch only for non-PlatformAdmin |
| `LinkOrganizationAsync` | `ProviderAdminEndpoints` | `SingleTenant` | **Fixes 500**: adds `?targetTenantId`; PlatformAdmin now gets 400 if missing |
| `GetUnlinkedAsync` | `ProviderAdminEndpoints` | `SingleTenant` | **Fixes 500**: same |
| `BulkLinkOrganizationAsync` | `ProviderAdminEndpoints` | `SingleTenant` | **Fixes 500**: same |
| `ActivateForCareConnectAsync` | `ProviderAdminEndpoints` | `CheckOwnership` | Explicit cross-tenant ownership assertion added |
| `BackfillAppointmentOrgIdsAsync` | `AdminBackfillEndpoints` | `SingleTenant` | Replaces 6-line if/else; already had correct 400 path in BLK-GOV-01 |

---

## 5. Security Fixes

### 5.1 PlatformAdmin 500 on ProviderAdminEndpoints (Critical)

**Root cause:** Three handlers (`LinkOrganizationAsync`, `GetUnlinkedAsync`, `BulkLinkOrganizationAsync`) did not check `ctx.IsPlatformAdmin` before calling `ctx.TenantId ?? throw`. When a PlatformAdmin (who has no `tenant_id` claim) called these endpoints, the code unconditionally threw `InvalidOperationException`, producing a 500.

**Fix:** All three endpoints now use `AdminTenantScope.SingleTenant(ctx, targetTenantId, http)`. PlatformAdmin callers that omit `?targetTenantId` receive a structured `400 Bad Request` instead of a 500.

**API change:** Added `[FromQuery] Guid? targetTenantId` to `LinkOrganizationAsync`, `GetUnlinkedAsync`, and `BulkLinkOrganizationAsync`. This is a backward-compatible addition for TenantAdmin callers (parameter is ignored for them).

### 5.2 Silent Cross-Tenant Denials

**Root cause:** Previous `Results.Forbid()` returns were silent — no log entry was emitted. Security-relevant denials were invisible in production telemetry.

**Fix:** All denials from `CheckOwnership` and `SingleTenant`/`PlatformWide` error paths now emit a structured `Warning` log entry via `ILoggerFactory`.

---

## 6. What Was NOT Changed

The following endpoints were reviewed but not modified — their existing enforcement is correct:

| File | Reason |
|------|--------|
| `ProviderOnboardingEndpoints.cs` | Documented in BLK-GOV-01; access model is by-design (org onboarding uses a different scope) |
| `ActivationEndpoints.cs` (non-admin) | Not an admin endpoint; out of scope |

---

## 7. Test Coverage

### 7.1 New Tests

**File:** `shared/building-blocks/BuildingBlocks.Tests/BuildingBlocks.Tests/AdminTenantScopeTests.cs`

18 xunit tests using a `StubRequestContext` (no mocking library required):

| Category | Test | Expected |
|----------|------|----------|
| PlatformWide / PlatformAdmin | No tenantId claim | `IsPlatformWide=true, TenantId=null` |
| PlatformWide / PlatformAdmin | With tenantId claim | Still platform-wide (not narrowed by claim) |
| PlatformWide / TenantAdmin | With tenantId | `TenantId=ctx.TenantId` |
| PlatformWide / TenantAdmin | Missing tenantId claim | Throws `InvalidOperationException` |
| SingleTenant / PlatformAdmin | Supplies explicit tenantId | Success, `TenantId=explicit` |
| SingleTenant / PlatformAdmin | Missing explicit tenantId | `IsError=true`, `Error` is 400 |
| SingleTenant / TenantAdmin | Ignores explicit tenantId | `TenantId=ctx.TenantId` (not exploitable) |
| SingleTenant / TenantAdmin | No explicit tenantId | `TenantId=ctx.TenantId` |
| SingleTenant / TenantAdmin | Missing tenantId claim | Throws `InvalidOperationException` |
| CheckOwnership / PlatformAdmin | Any resourceTenantId | `null` (allow) |
| CheckOwnership / TenantAdmin | Same tenant | `null` (allow) |
| CheckOwnership / TenantAdmin | Cross-tenant | `IResult` (Forbid) |
| CheckOwnership / TenantAdmin | Missing tenantId claim | Throws `InvalidOperationException` |
| Standard user (regression) | PlatformWide | Not platform-wide |
| Standard user (regression) | SingleTenant ignores explicit | `TenantId=ctx.TenantId` |
| Standard user (regression) | CheckOwnership cross-tenant | Forbid |

### 7.2 Full Test Run

```
Passed!  - Failed: 0, Passed: 256, Skipped: 0, Total: 256, Duration: 436ms
```

No regressions.

---

## 8. Build Verification

```
BuildingBlocks  → Build succeeded. 0 Warning(s), 0 Error(s)
CareConnect.Api → Build succeeded. 0 Warning(s), 0 Error(s)
```

---

## 9. Governance Denial Visibility (Part G)

Structured log entries are now emitted for every governance denial:

| Scenario | Log Level | Category | Message |
|----------|-----------|----------|---------|
| PlatformAdmin missing `?tenantId` | Warning | `BuildingBlocks.Authorization.AdminTenantScope` | `GovernanceDenial: PlatformAdmin userId=... at {Path} without supplying ?tenantId.` |
| TenantAdmin cross-tenant (CheckOwnership) | Warning | same | `GovernanceDenial: TenantAdmin userId=... tenant=... cross-tenant resource owned by tenant=...` |
| TenantAdmin missing `tenant_id` claim | Warning | same | `GovernanceDenial: TenantAdmin userId=... has no tenant_id claim (...)` |

These integrate directly into any `ILogger`-compatible sink (Application Insights, Seq, console, etc.).

---

## 10. `PlatformAdmin` Scope Decision Model

The following decision table is now enforced uniformly by `AdminTenantScope`:

```
┌──────────────────────┬─────────────────┬─────────────────────────────┐
│ Caller               │ Mode            │ Resolved TenantId           │
├──────────────────────┼─────────────────┼─────────────────────────────┤
│ PlatformAdmin        │ PlatformWide    │ null (platform-wide)        │
│ PlatformAdmin        │ SingleTenant    │ ?targetTenantId or 400      │
│ PlatformAdmin        │ CheckOwnership  │ always allowed (null)       │
├──────────────────────┼─────────────────┼─────────────────────────────┤
│ TenantAdmin          │ PlatformWide    │ ctx.TenantId                │
│ TenantAdmin          │ SingleTenant    │ ctx.TenantId (explicit ignored) │
│ TenantAdmin          │ CheckOwnership  │ null or Forbid()            │
├──────────────────────┼─────────────────┼─────────────────────────────┤
│ Any (missing claim)  │ any             │ InvalidOperationException   │
└──────────────────────┴─────────────────┴─────────────────────────────┘
```

---

## 11. Files Changed

| File | Type |
|------|------|
| `shared/building-blocks/BuildingBlocks/Authorization/AdminTenantScope.cs` | New |
| `shared/building-blocks/BuildingBlocks.Tests/BuildingBlocks.Tests/AdminTenantScopeTests.cs` | New |
| `apps/services/careconnect/CareConnect.Api/Endpoints/AnalyticsEndpoints.cs` | Updated |
| `apps/services/careconnect/CareConnect.Api/Endpoints/PerformanceEndpoints.cs` | Updated |
| `apps/services/careconnect/CareConnect.Api/Endpoints/AdminDashboardEndpoints.cs` | Updated |
| `apps/services/careconnect/CareConnect.Api/Endpoints/ActivationAdminEndpoints.cs` | Updated |
| `apps/services/careconnect/CareConnect.Api/Endpoints/ProviderAdminEndpoints.cs` | Updated |
| `apps/services/careconnect/CareConnect.Api/Endpoints/AdminBackfillEndpoints.cs` | Updated |

Diff: `analysis/BLK-GOV-02-commit.diff.txt` (539 lines)

---

## 12. Risk Assessment

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| TenantAdmin sees more data than before | Very Low | `PlatformWide` mode preserves existing `ctx.TenantId` scoping |
| PlatformAdmin loses access | Very Low | All three modes allow PlatformAdmin through |
| New `?targetTenantId` param breaks existing TenantAdmin clients | None | Param is optional and ignored for TenantAdmin |
| `?targetTenantId` vs `?tenantId` naming inconsistency | Low | `targetTenantId` is explicit and unambiguous in admin context |
| Missing `ILoggerFactory` in non-DI environments | Very Low | `GetLogger` returns null safely; logging is optional |

---

## 13. BLK-GOV-02 Completion Checklist

- [x] `AdminTenantScope` helper created with all three modes
- [x] `AdminScopeResult` struct with `IsError`, `TenantId`, `IsPlatformWide`, `Error`
- [x] Governance denial logging via `ILoggerFactory` (Part G)
- [x] `AnalyticsEndpoints.cs` migrated — `PlatformWide`
- [x] `PerformanceEndpoints.cs` migrated — `PlatformWide`
- [x] `AdminDashboardEndpoints.cs` migrated — `PlatformWide` ×3
- [x] `ActivationAdminEndpoints.cs` migrated — `PlatformWide` + `CheckOwnership` ×2
- [x] `ProviderAdminEndpoints.cs` migrated + PlatformAdmin 500 fixed — `SingleTenant` ×3 + `CheckOwnership` ×1
- [x] `AdminBackfillEndpoints.cs` migrated — `SingleTenant`
- [x] 18 xunit tests written covering all modes and caller types
- [x] 256 tests pass, 0 failures
- [x] `BuildingBlocks` build: 0 errors, 0 warnings
- [x] `CareConnect.Api` build: 0 errors, 0 warnings
- [x] Diff generated: `analysis/BLK-GOV-02-commit.diff.txt`
- [x] Summary written: `analysis/BLK-GOV-02-commit-summary.md`
- [x] Report complete: `analysis/BLK-GOV-02-report.md`

**BLK-GOV-02: COMPLETE**
