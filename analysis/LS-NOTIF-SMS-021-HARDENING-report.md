# LS-NOTIF-SMS-021-HARDENING — Governance Release Hardening, Approval Role Enforcement, and Test Suite Alignment

**Status:** COMPLETE  
**Migration:** `20260512000007_AddSmsGovernanceReleaseHardening`  
**Depends on:** LS-NOTIF-SMS-021 (`20260512000006_AddSmsGovernanceReleaseManagement`)

---

## Summary

Hardens the LS-NOTIF-SMS-021 governance release pipeline with production-grade concurrency control, approval role enforcement, retry/backoff semantics, and audit integrity validation. Reconciles pre-existing test stub failures in `Notifications.Tests` and `CareConnect.Tests` introduced by constructor signature drift across prior SMS governance milestones.

---

## Changes Delivered

### 1. Domain — `SmsGovernanceReleasePackage`

Added 8 hardening fields:

| Field | Type | Purpose |
|---|---|---|
| `ActivationLockId` | `Guid?` | Identifies the active activation lock (null = unlocked) |
| `ActivationLockAcquiredAt` | `DateTime?` | When lock was acquired |
| `ActivationLockExpiresAt` | `DateTime?` | Lock expiry — stale locks are forcibly expired |
| `ActivationLockedBy` | `string?` | Actor/worker that holds the lock |
| `ActivationAttemptCount` | `int` | Cumulative activation attempt count |
| `LastActivationAttemptAt` | `DateTime?` | Timestamp of last attempt |
| `NextActivationRetryAt` | `DateTime?` | Backoff gate — worker skips until this time |
| `LastActivationFailureReason` | `string?` | Truncated last failure reason (max 500 chars) |

### 2. Domain — `ReleaseAuditEventTypes` (new event constants)

| Constant | Value |
|---|---|
| `ApprovalRoleMismatch` | `"approval_role_mismatch"` |
| `ActivationLockAcquired` | `"activation_lock_acquired"` |
| `ActivationLockReleased` | `"activation_lock_released"` |
| `ActivationLockFailed` | `"activation_lock_failed"` |
| `ActivationRetryScheduled` | `"activation_retry_scheduled"` |
| `IntegrityCheckFailed` | `"integrity_check_failed"` |

### 3. Options — `SmsGovernanceReleaseManagementOptions`

| Option | Default | Purpose |
|---|---|---|
| `EnforceApprovalRoles` | `true` | Gate approvals by declared role |
| `AllowPlatformAdminApprovalFallback` | `true` | PlatformAdmin bypasses role gate |
| `ActivationRetryLimit` | `3` | Max attempts before terminal failure |
| `ActivationRetryBackoffMinutes` | `10` | Linear backoff base (minutes × attemptCount) |
| `ActivationLockTimeoutMinutes` | `10` | Stale lock forcible expiry |
| `MaxScheduledReleasesPerCycle` | `10` | Worker poll cap per cycle |

### 4. New Interface — `ISmsGovernanceReleaseIntegrityService`

Three read-only operations:
- `ValidateReleaseItemsAsync(Guid, CancellationToken)` → `ReleaseValidationReport`
- `ValidateReleaseIntegrityAsync(Guid, CancellationToken)` → `ReleaseIntegrityReport`
- `GetActivationLockStatusAsync(Guid, CancellationToken)` → `ReleaseActivationLockStatus`

### 5. New Service — `SmsGovernanceReleaseIntegrityService`

- **Item validation:** checks max-item cap, validates all entity types and action types, detects duplicates.
- **Integrity:** verifies that each state transition has a corresponding audit event; flags orphaned or inconsistent states.
- **Lock status:** reads current lock fields and computes `IsExpired` flag based on `ActivationLockExpiresAt`.

All three operations are read-only — no state is mutated during diagnostics.

### 6. Approval Workflow Service — Role Enforcement

When `EnforceApprovalRoles = true`:
- Compares `request.DecidedByRole` against `pendingRequest.ApproverRole`.
- If mismatch and `AllowPlatformAdminApprovalFallback = false` → returns fail + records `approval_role_mismatch` audit event + calls `SaveChanges()` before early return.
- If `AllowPlatformAdminApprovalFallback = true` and `request.DecidedByRole == "PlatformAdmin"` → bypass allowed.

Same logic applies symmetrically to `RejectAsync`.

### 7. Release Service — Concurrency Locking + Retry Tracking

**Activation lock (optimistic concurrency):**
- Before activation, attempts to acquire lock via DB update with predicate: `ActivationLockId IS NULL OR ActivationLockExpiresAt < now`.
- On success: sets `ActivationLockId = Guid.NewGuid()`, `ActivationLockAcquiredAt`, `ActivationLockExpiresAt = now + LockTimeoutMinutes`, `ActivationLockedBy = requestedBy`; records `activation_lock_acquired` audit event.
- On failure to acquire: returns `Fail("Another activation is in progress.")` + records `activation_lock_failed` audit event.
- After success or failure: releases lock (sets all lock fields to null); records `activation_lock_released` audit event.

**Retry tracking:**
- On activation failure: increments `ActivationAttemptCount`, sets `LastActivationAttemptAt`, truncates `LastActivationFailureReason` to 500 chars, calculates `NextActivationRetryAt = now + (backoffMinutes × attemptCount)`.
- If `ActivationAttemptCount >= ActivationRetryLimit`: sets permanent `activation_failed` state (no further retries).
- Records `activation_retry_scheduled` audit event when retry is scheduled.

**Item validation hardening (AddReleaseItemAsync):**
- Checks `MaxReleaseItems` cap before insertion.
- Validates `EntityType` and `ActionType` against known allowed sets.
- Detects duplicate entity+action combinations within the same release — rejects with `InvalidOperationException`.

### 8. Worker — Retry/Backoff Integration

`SmsGovernanceReleaseActivationWorker` now filters scheduled packages by:

```sql
NextActivationRetryAt IS NULL OR NextActivationRetryAt <= now
```

Skips packages that are not yet past their backoff window. Caps the cycle at `MaxScheduledReleasesPerCycle` (default 10).

### 9. New Hardening Endpoints

All require `PlatformAdmin` policy (`Policies.AdminOnly`):

| Method | Path | Returns |
|---|---|---|
| `GET` | `/v1/admin/sms/governance/releases/{id}/validation` | `ReleaseValidationReport` |
| `GET` | `/v1/admin/sms/governance/releases/{id}/integrity` | `ReleaseIntegrityReport` |
| `GET` | `/v1/admin/sms/governance/releases/{id}/locks` | `ReleaseActivationLockStatus` |

### 10. Migration `20260512000007_AddSmsGovernanceReleaseHardening`

Adds 8 new nullable columns to `ntf_SmsGovernanceReleasePackages`:

| Column | SQL Type |
|---|---|
| `ActivationLockId` | `char(36)` nullable |
| `ActivationLockAcquiredAt` | `datetime(6)` nullable |
| `ActivationLockExpiresAt` | `datetime(6)` nullable |
| `ActivationLockedBy` | `varchar(200)` nullable |
| `ActivationAttemptCount` | `int` not null default 0 |
| `LastActivationAttemptAt` | `datetime(6)` nullable |
| `NextActivationRetryAt` | `datetime(6)` nullable |
| `LastActivationFailureReason` | `varchar(500)` nullable |

No breaking schema changes. EF config uses `HasMaxLength` / `HasColumnType` matching domain semantics.

### 11. Test Suite Fixes

Three pre-existing test suite failures reconciled, caused by constructor signature drift from prior SMS governance milestones:

**Notifications.Tests — `NotificationServiceFailureCategoryTests.cs`:**
- Added `using Microsoft.Extensions.Options` and `using Notifications.Application.Options`.
- Added 6 new stub classes: `StubSmsProviderRuntimeResolver`, `StubSmsRoutingEngine`, `StubSmsRoutingDecisionRepository`, `StubSmsRetrySuppressionService`, `StubSmsGovernancePolicyService`, `StubSmsTemplateGovernanceService`.
- Updated `BuildService()` factory and one inline `new NotificationServiceImpl(...)` call to pass all 23 constructor parameters in correct order.
- `Notifications.Tests.csproj`: added `Microsoft.EntityFrameworkCore.InMemory` v8.0.2 (required by `SmsGovernanceReleaseTests`).

**Notifications.Tests — `SmsGovernanceReleaseTests.cs` (new file):**
- `StubVersioningService` corrected: fixed return types (`IReadOnlyList<RuleVersionDto>`, `IReadOnlyList<RulePackVersionDto>`, `RollbackResult`) and added 2 missing methods (`RollbackRuleAsync`, `RollbackRulePackAsync`).

**CareConnect.Tests — `ProviderReassignmentTests.cs`:**
- Added `Mock<IReferralAttachmentRepository>` and passed as named `activationRequests:` argument to `ReferralService` constructor.

**CareConnect.Tests — `ProviderActivationFunnelTests.cs`:**
- Same pattern: added `Mock<IReferralAttachmentRepository>` passed as positional `referralAttachments` argument following `IHttpContextAccessor`.

### 12. New Test File — `SmsGovernanceReleaseTests.cs`

Six xUnit `[Fact]` tests in `Notifications.Tests` (EF InMemory database, no mocking framework):

| Test | Validates |
|---|---|
| `ApproveAsync_RoleEnforced_ReturnsFail_WhenRoleMismatches` | Role enforcement blocks mismatched approver role |
| `ApproveAsync_PlatformAdminFallback_Succeeds_EvenWithMismatchedStageRole` | PlatformAdmin fallback bypasses role gate |
| `AddReleaseItemAsync_ThrowsInvalidOperation_WhenDuplicateEntityAction` | Duplicate item detection at add time |
| `ActivateAsync_ReturnsFail_WhenActivationLockHeld` | Concurrent activation guard returns fail |
| `ValidateReleaseIntegrityAsync_ReturnsInvalid_WhenCreatedEventMissing` | Integrity checker flags missing created audit event |
| `GetActivationLockStatusAsync_ReportsExpired_WhenLockPastExpiry` | Lock status correctly computes `IsExpired` flag |

---

## Files Created

| File | Purpose |
|---|---|
| `Notifications.Application/Interfaces/ISmsGovernanceReleaseIntegrityService.cs` | New interface |
| `Notifications.Infrastructure/Services/SmsGovernanceReleaseIntegrityService.cs` | New service implementation |
| `Notifications.Infrastructure/Data/Migrations/20260512000007_AddSmsGovernanceReleaseHardening.cs` | Schema migration (+8 columns) |
| `Notifications.Tests/SmsGovernanceReleaseTests.cs` | 6 hardening tests |

## Files Modified

| File | Change |
|---|---|
| `Notifications.Domain/SmsGovernanceReleasePackage.cs` | +8 hardening fields |
| `Notifications.Domain/SmsGovernanceReleaseAuditEvent.cs` | +6 new event type constants |
| `Notifications.Application/Options/SmsGovernanceReleaseManagementOptions.cs` | +6 new options |
| `Notifications.Infrastructure/Data/Configurations/SmsGovernanceReleasePackageConfiguration.cs` | +8 EF column mappings |
| `Notifications.Infrastructure/Services/SmsGovernanceApprovalWorkflowService.cs` | Role enforcement in `ApproveAsync` + `RejectAsync` |
| `Notifications.Infrastructure/Services/SmsGovernanceReleaseService.cs` | Concurrency lock + retry tracking + duplicate item check |
| `Notifications.Infrastructure/Workers/SmsGovernanceReleaseActivationWorker.cs` | `NextActivationRetryAt` filter + `MaxScheduledReleasesPerCycle` cap |
| `Notifications.Api/Endpoints/SmsGovernanceReleaseEndpoints.cs` | 3 new hardening endpoints |
| `Notifications.Infrastructure/DependencyInjection.cs` | Register `ISmsGovernanceReleaseIntegrityService` |
| `Notifications.Infrastructure/Data/Migrations/NotificationsDbContextModelSnapshot.cs` | Snapshot updated for 8 new columns |
| `Notifications.Api/appsettings.json` | 6 new governance options added |
| `Notifications.Tests/Notifications.Tests.csproj` | Added `Microsoft.EntityFrameworkCore.InMemory` v8.0.2 |
| `Notifications.Tests/NotificationServiceFailureCategoryTests.cs` | 6 new stubs + 2 updated constructor calls |
| `CareConnect.Tests/Application/ProviderReassignmentTests.cs` | Fixed constructor call (referral attachment repo) |
| `CareConnect.Tests/Application/ProviderActivationFunnelTests.cs` | Fixed constructor call (referral attachment repo) |

---

## Business Rules Enforced

1. An approver whose declared role does not match the stage's `ApproverRole` is blocked (unless PlatformAdmin fallback is enabled). The mismatch is always audited before early return.
2. Only one activation can run concurrently per release package — an optimistic lock with configurable expiry prevents phantom double-activations.
3. After `ActivationRetryLimit` failures, the release enters terminal `activation_failed` state and the worker permanently skips it.
4. The retry window (`NextActivationRetryAt`) prevents runaway worker retries; backoff is linear: `backoffMinutes × attemptCount`.
5. Duplicate entity+action combinations within the same release are rejected at item-add time (before any DB write).
6. All lock operations (acquire, release, fail) and role mismatches produce named audit events on the release audit trail.
7. The integrity service provides operator-visible, read-only diagnostics — no state mutation occurs during integrity or validation checks.

---

## Build Verification

All projects verified clean (`dotnet build`, 0 errors):

| Project | Exit Code | Errors |
|---|---|---|
| `Notifications.Infrastructure` | 0 | 0 |
| `Notifications.Api` | 0 | 0 |
| `Notifications.Tests` | 0 | 0 |
| `CareConnect.Tests` | 0 | 0 |

Warnings: 2 × NU1902 (MailKit known vulnerability — pre-existing, unrelated to this milestone) and 3 × CS8767 (nullability annotation hints, non-blocking).
