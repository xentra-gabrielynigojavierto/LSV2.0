# BLK-CC-02 Report

## 1. Summary

**Block:** CareConnect Onboarding Reliability & Recovery (Phase 2)
**Status:** Complete
**Date:** 2026-04-23
**Window:** TENANT-STABILIZATION 2026-04-23 → 2026-05-07

Eliminates the partial-failure gap documented in BLK-CC-01. Provider onboarding is now
resumable: if Tenant service provisioning succeeds but Identity membership assignment
fails, the provider record retains the pending tenant state so a retry can skip
re-provisioning and go straight to Identity assignment.

---

## 2. Failure Window Analysis

### Exact partial-failure gap (BLK-CC-01 documented)

1. `ProviderOnboardingService.ProvisionToTenantAsync` calls `_tenantClient.ProvisionAsync`
   → Tenant service creates the tenant → returns `TenantId, TenantCode, Subdomain`
2. Calls `_identityMembership.AssignTenantAsync` → **fails** (network, timeout, 5xx)
3. `ProviderOnboardingService` throws `ProviderOnboardingException(IdentityServiceFailed)`
4. No pending state is saved — the provider record is unchanged (still COMMON_PORTAL)
5. Provider retries → `ProvisionToTenantAsync` called again with the same `tenantCode`
6. Tenant service returns **409 CODE_TAKEN** → `ProviderOnboardingException(TenantCodeUnavailable)`
7. **Provider is permanently blocked** — cannot complete onboarding without manual cleanup

### Exact methods/services involved

| Location | Method | Risk |
|---|---|---|
| `ProviderOnboardingService` | `ProvisionToTenantAsync` | Didn't save pending state before Identity call |
| `HttpTenantServiceClient` | `ProvisionAsync` | Non-idempotent — same code = 409 on retry |
| `HttpIdentityMembershipClient` | `AssignTenantAsync` | Idempotent via BLK-ID-02 — safe to retry |
| `Provider` (domain entity) | (no recovery fields) | No state persisted for retry |

---

## 3. Recovery State Model

### New fields on `Provider` (domain entity)

| Field | Type | Default | Purpose |
|---|---|---|---|
| `PendingTenantId` | `Guid?` | null | TenantId returned by Tenant service; retained until onboarding completes |
| `PendingTenantCode` | `string?` | null | TenantCode returned by Tenant service |
| `PendingTenantSubdomain` | `string?` | null | Subdomain returned by Tenant service |
| `TenantOnboardingStatus` | `string` | "None" | Current onboarding status (see values below) |
| `LastOnboardingError` | `string?` | null | Last failure reason (for ops visibility) |
| `LastOnboardingAttemptAtUtc` | `DateTime?` | null | When the last attempt was made |

### TenantOnboardingStatus values

- `None` — no onboarding attempted
- `ProvisioningStarted` — attempt in progress (set before Tenant service call)
- `TenantProvisioned` — Tenant service succeeded; pending Identity assignment
- `Completed` — both steps succeeded; provider at TENANT stage
- `Failed` — last Identity assignment failed; pending state retained for retry

### Why this is sufficient

- CareConnect owns only the retry state — not canonical tenant data
- The actual tenant record is owned by Tenant service
- Identity membership is idempotent (BLK-ID-02 safe to call twice)
- No separate workflow engine needed

---

## 4. Service Flow Changes

### Normal flow (no pending state)

```
1. Validate: provider exists + COMMON_PORTAL
2. provider.BeginOnboarding() → Status=ProvisioningStarted, records attempt time → save
3. _tenantClient.ProvisionAsync(name, code)
   → On CODE_TAKEN (no pending state): surface conflict (HTTP 409)
   → On null (infra failure): RecordOnboardingFailure → save → throw retryable 503
4. provider.RecordTenantProvisioned(tenantId, code, subdomain) → Status=TenantProvisioned → save
5. _identityMembership.AssignTenantAsync(userId, tenantId, ["TenantAdmin"])
   → On null (failure): RecordOnboardingFailure → save → throw retryable 503
6. provider.CompleteOnboarding(tenantId) → AccessStage=TENANT, clears pending, Status=Completed → save
7. Return ProviderOnboardingResult (IsResumed=false)
```

### Retry/resume flow (pending state exists — PendingTenantId != null)

```
1. Validate: provider exists + COMMON_PORTAL
2. Detect pending state: PendingTenantId != null
3. Log: "Resuming onboarding from pending state TenantId={X}"
4. SKIP _tenantClient.ProvisionAsync entirely
5. provider.BeginOnboarding() → updates attempt time → save
6. _identityMembership.AssignTenantAsync(userId, PendingTenantId, ["TenantAdmin"])
   → On null (failure): RecordOnboardingFailure → save → throw retryable 503
7. provider.CompleteOnboarding(PendingTenantId) → AccessStage=TENANT, clears pending → save
8. Return ProviderOnboardingResult (IsResumed=true)
```

### Two-save pattern

Save 1 (before Identity call): persists `PendingTenantId/Code/Subdomain + Status=TenantProvisioned`
Save 2 (after completion): clears pending state, writes TENANT stage

If the process crashes between saves 1 and 2, the pending state is preserved. Next retry
resumes from save 1.

---

## 5. Identity Recovery Integration

- `AssignTenantAsync` (BLK-ID-02) is idempotent: if user is already in tenant, returns
  `AlreadyInTenant=true` without duplicating roles
- No new Identity user is ever created
- Retries call `AssignTenantAsync` with the same `(userId, tenantId, ["TenantAdmin"])` —
  safe to call multiple times

---

## 6. Frontend / UX Impact

### Changed

- `ProviderOnboardingResponse` gains `IsResumed` (bool) field
- Resume success response includes message: "We found an existing workspace setup. Setup is now complete."
- Identity-assignment failure now surfaces as: "Your workspace was created but membership setup is still pending. Retrying will complete the setup."

### Unchanged

- `GET /api/provider/onboarding/check-code` — no change
- `POST /api/provider/onboarding/provision-tenant` — same path, handles both fresh and retry
- HTTP 409 for true code conflicts (no pending state + CODE_TAKEN)
- HTTP 503 for retryable identity failures
- `GET /api/provider/onboarding/status` — no breaking change (COMMON_PORTAL flag still works)

---

## 7. Data Consistency / Idempotency

| Invariant | How enforced |
|---|---|
| One provider record only | `GetByIdentityUserIdAsync` — lookup before any changes |
| One canonical tenant | Pending state reuse prevents second `ProvisionAsync` call |
| One Identity user | `AssignTenantAsync` reuses existing `IdentityUserId` |
| No duplicate roles | BLK-ID-02 idempotency — skips existing assignments |
| Provider not TENANT until both steps succeed | `CompleteOnboarding` only called after `AssignTenantAsync` returns non-null |
| Pending state cleared only on success | `CompleteOnboarding` clears all pending fields atomically |

### Unavoidable temporary state

Between Save 1 (pending state written) and Save 2 (completion), the provider is at
COMMON_PORTAL with pending state. This is the intended recoverable window. Duration
is bounded by Identity service response time.

---

## 8. Changed Files

**Modified files:**
- `apps/services/careconnect/CareConnect.Domain/Provider.cs` — +6 recovery state fields, +4 domain methods
- `apps/services/careconnect/CareConnect.Infrastructure/Data/Configurations/ProviderConfiguration.cs` — map new fields
- `apps/services/careconnect/CareConnect.Application/Interfaces/IProviderOnboardingService.cs` — add `IsResumed` to result
- `apps/services/careconnect/CareConnect.Application/Services/ProviderOnboardingService.cs` — two-save resumable flow
- `apps/services/careconnect/CareConnect.Application/DTOs/ProviderOnboardingDtos.cs` — add `IsResumed` to response

**New files:**
- `apps/services/careconnect/CareConnect.Infrastructure/Data/Migrations/20260423230000_AddProviderOnboardingRecoveryState.cs`

**Updated files:**
- `apps/services/careconnect/CareConnect.Infrastructure/Data/Migrations/CareConnectDbContextModelSnapshot.cs` — add missing + new Provider fields

---

## 9. Methods / Endpoints Updated

### Domain methods added to `Provider`

- `BeginOnboarding()` — records attempt timestamp, sets ProvisioningStarted
- `RecordTenantProvisioned(tenantId, code, subdomain)` — stores pending state, sets TenantProvisioned
- `CompleteOnboarding(providerTenantId)` — calls MarkTenantProvisioned, clears pending, sets Completed
- `RecordOnboardingFailure(error)` — stores error, sets Failed, keeps pending state for retry

### Service methods changed

- `ProviderOnboardingService.ProvisionToTenantAsync` — full two-save resumable flow

### Result types changed

- `ProviderOnboardingResult` — adds `IsResumed` (bool)
- `ProviderOnboardingResponse` — adds `IsResumed` (bool)

---

## 10. GitHub Commits (MANDATORY)

| Commit | Description |
|--------|-------------|
| `ebd1cdf` | BLK-CC-02: Add Provider onboarding recovery state fields, migration, two-save resumable ProviderOnboardingService |

---

## 11. Validation Results

### Build

```
dotnet build CareConnect.Api/CareConnect.Api.csproj --no-restore -c Release
Build succeeded.  0 Warning(s)  0 Error(s)  Time Elapsed 00:00:08.87
```

### Changed files verified

| File | Change | Status |
|---|---|---|
| `CareConnect.Domain/Provider.cs` | +`TenantOnboardingStatuses` class; +6 recovery state properties; +4 domain methods | ✅ Compiles |
| `ProviderConfiguration.cs` | +6 EF property mappings + 2 indexes | ✅ Compiles |
| `20260423230000_AddProviderOnboardingRecoveryState.cs` | New migration — 6 columns + 2 indexes | ✅ Compiles |
| `CareConnectDbContextModelSnapshot.cs` | Added all missing Provider fields (AccessStage, Npi, IdentityUserId, timestamps, recovery state, indexes) | ✅ Compiles |
| `IProviderOnboardingService.cs` | `ProviderOnboardingResult` + `IsResumed` | ✅ Compiles |
| `ProviderOnboardingService.cs` | Full two-save resumable flow | ✅ Compiles |
| `ProviderOnboardingDtos.cs` | `ProviderOnboardingResponse` + `IsResumed` | ✅ Compiles |
| `ProviderOnboardingEndpoints.cs` | Resume-aware message + `IsResumed` passed to response | ✅ Compiles |

---

## 12. Issues / Gaps

**Phase 2 remaining gap:** If the user submits a DIFFERENT tenant code on retry while
pending state from a prior attempt exists, CareConnect reuses the pending (old) tenant.
This is by design — the old partial tenant cannot be abandoned without manual cleanup.
A future Phase 3 could add a support endpoint to force-reset pending state.

**Code conflict detection:** If `PendingTenantCode != null` and user submits a different
code, the service uses the pending code (ignores the new code). This is safe but may
surprise users. UX should surface the pending state so users know a code is already reserved.

---

## 13. GitHub Diff Reference

- **Commit ID:** `ebd1cdf1a9bddd70fd3b252410edb46f83fae59c`
- **Diff file:** `analysis/BLK-CC-02-commit.diff.txt`
- **Summary file:** `analysis/BLK-CC-02-commit-summary.md`
