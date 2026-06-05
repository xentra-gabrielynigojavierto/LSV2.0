# TENANT-B13 — Identity Physical Cleanup (Final Retirement)
## Analysis & Prerequisite Gate Report

**Date:** 2026-04-23T13:09:12Z
**Author:** Platform agent
**Status:** ❌ HARD STOP — Prerequisites not met. No code changes made.

---

## 1. Executive Summary

TENANT-B13 requires all migration blocks (B01–B12) to have been stable in production
for a **minimum of 2 weeks (recommended 30 days)** before any physical removal of
Identity's tenant-management code. A full prerequisite evaluation was performed against
the current runtime state.

**Result: 6 distinct blockers were identified. B13 work MUST NOT begin.**

No files were modified. This report documents every blocker with exact file paths, line
numbers, and the remediation required before B13 can be unlocked.

---

## 2. Prerequisite Checklist

| # | Prerequisite | Required | Actual | Status |
|---|---|---|---|---|
| 1 | Minimum stable runtime in new config | 2+ weeks | ~2 hours (B12 just completed) | ❌ FAIL |
| 2 | `TenantBrandingReadSource = Tenant` (logo/public route) | Tenant | Identity (default, no env var set) | ❌ FAIL |
| 3 | No hardcoded Identity branding reads | Zero | 3 active sites | ❌ FAIL |
| 4 | HybridFallback code paths eliminated | Gone | Still present (2 routes) | ❌ FAIL |
| 5 | Control Center off Identity for all tenant operations | Zero Identity calls | 3 active admin operations | ❌ FAIL |
| 6 | Identity fallback usage = 0 in metrics | 0 | Cannot confirm (no instrumentation) | ❌ FAIL |
| 7 | `TenantDualWriteEnabled` = false / removed | false | true (Identity appsettings) | ⚠️ WARN |
| 8 | All new tenants via Tenant service | Yes | B12 just completed — unverified at scale | ⚠️ WARN |

---

## 3. Blockers — Detail

### BLOCKER 1 — Runtime Duration (Critical)

**Requirement:** System running stably in new Tenant-source configuration for minimum
2 weeks before physical removal.

**Finding:** TENANT-B11 (admin reads to Tenant service) and TENANT-B12 (lifecycle
ownership to Tenant service) were both completed on **2026-04-23** — the same day as
this B13 analysis. The system has been running in the new configuration for
**approximately 2 hours**, not 2+ weeks.

**Risk if ignored:** Physical removal of Identity tenant code before the migration is
proven stable removes rollback capability. Any latent bug in Tenant service data
(missing records, schema gaps, incorrect field mappings) would result in a production
outage with no fallback.

**Resolution:** Run the system in production with all B11/B12 paths active. Revisit
B13 no sooner than **2026-05-07** (2 weeks from today) and ideally **2026-05-23** (30
days). Monitor Identity fallback trigger rate; it should remain zero throughout.

---

### BLOCKER 2 — `branding/logo/public` Route Defaults to Identity (Active)

**File:** `apps/web/src/app/api/branding/logo/public/route.ts`
**Line:** 18

```typescript
const READ_SOURCE = (process.env.TENANT_BRANDING_READ_SOURCE ?? 'Identity') as ReadSource;
```

**Finding:** `TENANT_BRANDING_READ_SOURCE` is **not set** in `apps/web/.env.local` or
any other env file. Therefore this route defaults to `'Identity'` at runtime, actively
calling:

```
GET /identity/api/tenants/current/branding
```

on every public logo request. This is a live Identity read dependency.

Note: The sibling route `tenant-branding/route.ts` correctly defaults to `'Tenant'`
(line 27). The logo/public route has an inconsistent default that was never updated.

**Resolution required before B13:**
1. Set `TENANT_BRANDING_READ_SOURCE=Tenant` in `apps/web/.env.local`.
2. Update the hardcoded default in `logo/public/route.ts` from `'Identity'` to
   `'Tenant'` to be safe-on-restart.
3. Verify zero traffic to `/identity/api/tenants/current/branding` for at least 7 days.

---

### BLOCKER 3 — Hardcoded Identity Branding URLs (3 Active Sites)

Three files call the Identity branding endpoint with no feature flag — they cannot be
switched without a code change:

**Site A — CareConnect route:**
- File: `apps/web/src/app/api/public/careconnect/[...path]/route.ts`
- Line: 35
- Code: `const brandingUrl = \`${GATEWAY_URL}/identity/api/tenants/current/branding\`;`
- Description: Hardcoded, no `READ_SOURCE` check.

**Site B — Public network API:**
- File: `apps/web/src/lib/public-network-api.ts`
- Line: 70
- Code: `` const url = `${GATEWAY_URL}/identity/api/tenants/current/branding`; ``
- Description: Hardcoded, no fallback.

**Site C — Tenant branding provider:**
- File: `apps/web/src/providers/tenant-branding-provider.tsx`
- Line: 21 (comment) — actual call may be via `public-network-api.ts`
- Description: Component fetches from Identity branding path through provider.

**Resolution required before B13:**
Switch all 3 sites to `/tenant/api/v1/branding/by-subdomain/{subdomain}` (or the
appropriate Tenant service branding endpoint). Add `TENANT_BRANDING_READ_SOURCE` guard
where practical, else convert directly to Tenant path.

---

### BLOCKER 4 — HybridFallback Code Paths Still Present

**Files:**
- `apps/web/src/app/api/tenant-branding/route.ts` (lines 14, 32, 94, 159–179)
- `apps/web/src/app/api/branding/logo/public/route.ts` (lines 11, 22, 94–107)

The `HybridFallback` mode actively calls Identity when Tenant service fails or returns
no logo. Until this code is removed, Identity's branding endpoint cannot be safely
deleted — a Tenant service hiccup would auto-route traffic to Identity silently.

**Resolution required before B13:**
1. Confirm `HybridFallback` has zero activations in logs (search for
   `[tenant-branding] HybridFallback: Tenant fetch failed`).
2. Remove the `HybridFallback` branch from both routes.
3. Remove the `'Identity'` branch from both routes.
4. Collapse `ReadSource` type to `'Tenant'` only, or remove the abstraction entirely.

---

### BLOCKER 5 — Control Center Still Calls Identity for 3 Admin Operations

**File:** `apps/control-center/src/lib/control-center-api.ts`

Three BFF functions still route to Identity with no Tenant service equivalent:

| Function | Current endpoint | Tenant service replacement |
|---|---|---|
| `updateSessionSettings` | `PATCH /identity/api/admin/tenants/{id}/session-settings` | Not implemented in Tenant service |
| `retryProvisioning` | `POST /identity/api/admin/tenants/{id}/provisioning/retry` | Not implemented in Tenant service |
| `retryVerification` | `POST /identity/api/admin/tenants/{id}/verification/retry` | Not implemented in Tenant service |

These are **live, active calls** from the Control Center UI. They are not deprecated.
The Identity endpoints they target (`RetryProvisioning`, `RetryVerification`,
`UpdateTenantSessionSettings`) contain real business logic that does not exist in the
Tenant service yet.

Removing these Identity endpoints before Tenant service replacements exist would break
the Control Center's ability to manage provisioning retries and session configuration.

**Resolution required before B13:**
Implement a TENANT-B13-PRE block (or extend B12) to add:
- `PATCH /api/v1/admin/tenants/{id}/session-settings` on Tenant.Api (backed by
  `ITenantAdminService.UpdateSessionSettingsAsync`, which calls
  `IIdentityCompatAdapter` to set `SessionTimeoutMinutes` on Identity).
- `POST /api/v1/admin/tenants/{id}/provisioning/retry` on Tenant.Api — delegates to
  `IIdentityProvisioningAdapter.RetryProvisioningAsync` (Identity still owns DNS
  provisioning lifecycle).
- `POST /api/v1/admin/tenants/{id}/verification/retry` on Tenant.Api — delegates to
  `IIdentityProvisioningAdapter.RetryVerificationAsync`.
Switch control-center BFF to the new Tenant endpoints. Only then can the Identity
versions be deprecated+removed.

**Additional finding:** `apps/web/src/lib/control-center-api.ts` (the legacy web
control-center module) still has live calls to:
- `GET /identity/api/admin/tenants/{id}` (line 166)
- `POST /identity/api/admin/tenants/{id}/activate` (line 228)
- `POST /identity/api/admin/tenants/{id}/deactivate` (line 232)

These need to be audited and switched before removal.

---

### BLOCKER 6 — No Identity Fallback Metrics / Instrumentation

**Requirement (spec §3):** Identity fallback usage = 0 before proceeding.

**Finding:** There is no instrumentation collecting a "Identity fallback trigger count"
metric. Without telemetry, it is impossible to confirm that fallback is never being
invoked at runtime. The spec requires a positive confirmation of zero fallback usage,
not merely an absence of evidence.

**Resolution required before B13:**
Add structured log counters or Prometheus counters to the `HybridFallback` branches:
```typescript
console.warn('[tenant-branding] HybridFallback: Tenant fetch failed ...')
```
and confirm zero occurrences in production logs over the required stability window.

---

## 4. Warnings (Non-blocking, But Must Resolve)

### WARNING A — Dual-Write Still Enabled

- File: `apps/services/identity/Identity.Api/appsettings.json`
- Setting: `"TenantDualWriteEnabled": true`

Dual-write is not a blocker for B13 itself (it's a guard, not a dependency), but it
signals the system is still in a transitional state. B13's cleanup assumes dual-write
will be removed as part of the Identity simplification. Dual-write should be confirmed
unnecessary (no divergence observed) before it is turned off, and it should be turned
off before, not during, physical code removal.

**Resolution:** After the 2-week stability window, set `TenantDualWriteEnabled=false`,
run for 1 week, confirm no data divergence, then proceed to B13.

### WARNING B — Legacy `apps/web/src/lib/control-center-api.ts`

This file (`apps/web/src/lib/`) is a legacy control-center module (distinct from
`apps/control-center/src/lib/control-center-api.ts`). It still contains stubs and live
calls mixed together. The ownership and active usage of this file needs to be audited
before B13 to determine which calls are truly live vs. dead code.

---

## 5. Safe Work That Can Proceed Now

While B13's physical removals are blocked, the following preparatory work is safe and
recommended during the waiting period:

| Prep item | Risk | Priority |
|---|---|---|
| Fix `logo/public/route.ts` default from `'Identity'` to `'Tenant'` | Low | High |
| Set `TENANT_BRANDING_READ_SOURCE=Tenant` in `apps/web/.env.local` | Zero | High |
| Switch CareConnect + public-network-api branding to Tenant endpoint | Low | High |
| Add session-settings proxy to Tenant.Api (delegates to Identity) | Low | Medium |
| Add provisioning/verification retry proxy to Tenant.Api | Low | Medium |
| Add structured metrics to HybridFallback branches | Zero | Medium |
| Set `TenantDualWriteEnabled=false` after 2-week stability window | Low (with monitoring) | Medium |

These items eliminate the blockers incrementally and shorten the gate period. B13 can
be re-evaluated once all 6 blockers above are resolved AND the 2-week stability
window has elapsed.

---

## 6. B13 Unlock Criteria (Checklist)

The following must ALL be true before a future run of B13 is authorised to make
code changes:

- [ ] Date is no earlier than **2026-05-07** (2 weeks from B12 completion)
- [ ] `TENANT_BRANDING_READ_SOURCE=Tenant` confirmed in all environments
- [ ] `logo/public/route.ts` default changed to `'Tenant'`
- [ ] CareConnect + public-network-api branding switched to Tenant endpoint
- [ ] HybridFallback branches show zero activations in production logs for ≥7 days
- [ ] `updateSessionSettings` BFF switched to Tenant service endpoint
- [ ] `retryProvisioning` BFF switched to Tenant service endpoint
- [ ] `retryVerification` BFF switched to Tenant service endpoint
- [ ] Legacy `apps/web/src/lib/control-center-api.ts` Identity calls audited / removed
- [ ] `TenantDualWriteEnabled=false` confirmed stable for ≥7 days
- [ ] Zero Identity tenant endpoint traffic in gateway/proxy logs for ≥7 days

---

## 7. Files Confirmed Safe to Remove (When Gate Unlocks)

*Listed here for future reference only. Do not act on this list until the gate in §6
is fully cleared.*

**Identity.Api/Endpoints/AdminEndpoints.cs** — handlers to remove:
- `CreateTenant` (deprecated TENANT-B12)
- `UpdateEntitlement` (deprecated TENANT-B12)
- `ListTenants` (reads, once all consumers gone)
- `GetTenant` (reads, once all consumers gone)

**Identity.Api/Endpoints/TenantEndpoints.cs** — handlers to review:
- `GET /api/tenants/current/branding` — remove once all branding reads use Tenant service

**Identity.Domain/Tenant.cs** — fields to remove (with EF migration):
- Branding fields (`LogoDocumentId`, `LogoWhiteDocumentId`) — already write-through
  only; safe to drop once dual-write is off and column confirmed empty/unused
- Address fields — duplicated in Tenant service; can be dropped from Identity DB
  after confirmed stable

**Identity.Infrastructure/Services/** — services to remove:
- `SyncAdapter` / `ITenantSyncAdapter` — dual-write sync (once `TenantDualWriteEnabled` gone)

---

## 8. Decision Record

**TENANT-B13 is BLOCKED. No code changes were made during this session.**

The prerequisite gate is a safety mechanism, not a formality. The Tenant service
migration (B01–B12) was completed today. Removing Identity's tenant code before
observing 2+ weeks of stable production runtime would eliminate rollback capability
during the most likely window for latent bugs to surface.

The correct next step is to resolve the preparatory items in §5 (which carry low risk
and can begin immediately), then return to B13 after the stability window elapses and
the §6 checklist is complete.

---

*Report written: 2026-04-23T13:09:12Z*
*No files modified during B13 analysis. Codebase state is identical to the B12 checkpoint.*
