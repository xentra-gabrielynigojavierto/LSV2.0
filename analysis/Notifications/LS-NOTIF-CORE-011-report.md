# LS-NOTIF-CORE-011 Report

## Summary

Implemented full async reliability for the Notifications microservice. Both background workers that were previously stubs (`NotificationWorker`, `StatusSyncWorker`) now execute real work. A delivery state machine with retry scheduling and dead-letter handling was added. All changes are additive; no existing API contracts or endpoints were modified.

---

## Worker Implementation

### NotificationWorker
- **Interval**: every 30 seconds  
- **Logic**: queries all `retrying` notifications whose `NextRetryAt <= UtcNow`, batched at 10 per cycle; calls `INotificationService.ProcessAutoRetryAsync(id)` for each
- **Error handling**: per-notification try/catch so a single failure does not cancel the batch; outer try/catch catches unexpected infra errors and resumes next cycle
- **File**: `Notifications.Infrastructure/Workers/NotificationWorker.cs`

### StatusSyncWorker
- **Interval**: every 5 minutes (30 s staggered start)  
- **Logic**: calls `INotificationService.ReconcileStalledAsync()` which finds all `processing` notifications whose `UpdatedAt < UtcNow - 5 min`, batched at 20; schedules them for retry or moves to dead-letter depending on `RetryCount vs MaxRetries`
- **File**: `Notifications.Infrastructure/Workers/StatusSyncWorker.cs`

### ProviderHealthWorker
Pre-existing, fully implemented. Unchanged.

---

## Retry Engine

### RetryCount / MaxRetries
Two new fields on `Notification` domain entity:
- `RetryCount` (int, default 0) — incremented on each failed delivery cycle
- `MaxRetries` (int, default 3) — configurable per notification at record level; defaults to 3

### Backoff Schedule (incremental)
| RetryCount after failure | NextRetryAt delay |
|---|---|
| 1 | +1 minute |
| 2 | +5 minutes |
| 3+ | +30 minutes |

### Retry eligibility
Only retried if `FailureCategory == "retryable_provider_failure"` (i.e. routes existed but all failed). `auth_config_failure` (no routes configured) immediately terminates as `failed` — no retry is useful without route configuration.

### Operator-triggered retry (`RetryAsync`)
Unchanged. Still allows manual retry of a `failed` notification. The existing manual retry path uses the same `ExecuteSendLoopAsync` which now also applies the retry scheduling logic on further failure.

---

## State Machine

```
accepted
  └─► processing (template rendered, entering send loop)
        ├─► sent          (provider accepted)
        ├─► retrying      (routes exhausted, retries remaining, NextRetryAt set)
        │     └─► processing (worker picks up, clears NextRetryAt)
        │           ├─► sent
        │           ├─► retrying (repeat)
        │           └─► dead-letter (RetryCount >= MaxRetries)
        └─► failed        (no routes configured — auth_config_failure)

blocked  (contact suppression / rate limit — never retried)
partial  (fan-out: some succeeded, some failed)
dead-letter  (RetryCount exhausted — surfaces in /issues endpoint)
```

---

## Webhook Handling

No changes required. `WebhookIngestionService` and `DeliveryStatusService` were already implemented and continue to handle provider-push status updates correctly. Webhook events update attempt and notification status independently of the retry scheduler.

---

## Files Changed

| File | Change |
|---|---|
| `Notifications.Domain/Notification.cs` | Added `RetryCount`, `MaxRetries`, `NextRetryAt` |
| `Notifications.Infrastructure/Data/Configurations/NotificationConfiguration.cs` | EF mapping for new fields + `IX_Notifications_Status_NextRetryAt` index |
| `Notifications.Infrastructure/Data/Migrations/20260419000001_AddRetryFields.cs` | New migration: 3 columns + index on `ntf_Notifications` |
| `Notifications.Infrastructure/Data/Migrations/NotificationsDbContextModelSnapshot.cs` | Snapshot updated with new properties and index |
| `Notifications.Application/Interfaces/INotificationRepository.cs` | Added `GetEligibleForRetryAsync` and `GetStalledProcessingAsync` |
| `Notifications.Infrastructure/Repositories/NotificationRepository.cs` | Implemented both new query methods |
| `Notifications.Application/Interfaces/INotificationService.cs` | Added `ProcessAutoRetryAsync` and `ReconcileStalledAsync` |
| `Notifications.Infrastructure/Services/NotificationService.cs` | Modified `ExecuteSendLoopAsync` failure path; added `ProcessAutoRetryAsync`, `ReconcileStalledAsync`, `ComputeNextRetryAt`, `CreateDeadLetterIssueAsync`; updated stats and event synthesis |
| `Notifications.Infrastructure/Workers/NotificationWorker.cs` | Full implementation (was stub) |
| `Notifications.Infrastructure/Workers/StatusSyncWorker.cs` | Full implementation (was stub) |

---

## Validation Performed

- **Build**: `dotnet build Notifications.Api.csproj` — 0 errors, 3 pre-existing warnings (MailKit advisory, JwtBearer version conflict — both pre-existing)
- **State transitions** reviewed end-to-end in code:
  - New notification → `accepted` → `processing` → `retrying` (on failure) → `processing` (worker pickup) → `dead-letter` after MaxRetries
  - Happy path: `accepted` → `processing` → `sent`
  - No-routes path: `accepted` → `processing` → `failed`
- **Worker queries** use indexed columns: `Status + NextRetryAt` index added for `GetEligibleForRetryAsync`; `UpdatedAt` already in the table for `GetStalledProcessingAsync`
- **Dead-letter issues**: `DeliveryIssue` created with `IssueType = "max_retries_exhausted"` using `CreateIfNotExistsAsync` (idempotent, safe to call multiple times)
- **Stats**: `QueuedCount` now includes `retrying`; `FailedCount` now includes `dead-letter`
- **Events timeline**: `retrying` and `dead-letter` now synthesized in `GetEventsAsync`

---

## Remaining Gaps

1. **`MaxRetries` is not configurable per-tenant** — currently always defaults to 3. A future enhancement could read `MaxRetries` from tenant configuration.
2. **Retry backoff is simple incremental, not exponential** — chosen for simplicity; upgrade to exponential if higher volume warrants it.
3. **Webhook-driven status does not re-trigger retry scheduling** — if a provider sends a `failed` webhook for an already-`sent` notification, the retry engine is not involved (by design, webhooks update status directly).
4. **No jitter on retry delay** — concurrent notifications failing at the same time will all retry together; add `±10 s` jitter if thundering-herd becomes an issue.
5. **`GetStalledProcessingAsync` threshold is hardcoded** (5 min) — could be promoted to configuration.

---

## Risks / Follow-Up Recommendations

| Risk | Severity | Recommendation |
|---|---|---|
| Migration applied while service is running may briefly expose `RetryCount` column as NULL to old code | Low | New columns have DB-level defaults (0, 3) — safe for rolling deploys |
| `RetryAsync` (operator) does not reset `RetryCount` — a manual retry after dead-letter is blocked | Medium | Consider whether operator retry should reset `RetryCount` to allow re-entry into auto-retry cycle |
| StatusSyncWorker catches stalled notifications at exactly the same time as NotificationWorker in some timing windows | Low | Both workers check status before acting; `status == "retrying"` guard in `ProcessAutoRetryAsync` prevents double-dispatch |
| Dead-letter notifications remain in DB indefinitely | Low | Future archival/purge policy recommended |
