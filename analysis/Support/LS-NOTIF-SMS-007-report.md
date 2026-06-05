# LS-NOTIF-SMS-007 — Persisted SMS Reconciliation Outcome Tracking

## 1. Initial Codebase Analysis

LS-NOTIF-SMS-007 adds persisted reconciliation tracking fields to `NotificationAttempt` so SMS activity queries can filter and summarise reconciliation state without consulting audit events or inferring from delivery status alone.

All work stays inside the Notification Service boundary. No other service is modified.

## 2. Existing NotificationAttempt Findings

`NotificationAttempt` (20 existing fields) stores delivery-lifecycle data but has no reconciliation tracking columns. The entity is mapped by `NotificationAttemptConfiguration` inside `NotificationConfiguration.cs` (shared configuration file). The table is `ntf_NotificationAttempts`.

Existing update patterns:
- `UpdateAsync` — full entity update
- `UpdateStatusAsync` — targeted status-only update (used by DeliveryStatusService)

No optimistic concurrency is in use; plain `SaveChangesAsync` is the pattern.

## 3. Existing SmsReconciliationService Findings

`SmsReconciliationService.ReconcileAttemptAsync` has two entry modes — manual (public API) and batch (background worker). Every code path through the method has a loaded `NotificationAttempt` except `OutcomeAttemptNotFound` which is resolved at the public-method level before `ReconcileAttemptAsync` is called.

The three public entry points (`ReconcileByAttemptIdAsync`, `ReconcileByProviderMessageIdAsync`, `ReconcileStalePendingAsync`) all call `ReconcileAttemptAsync` and receive a `SmsReconciliationResult`. The result already carries `Outcome`, `ErrorCode`, `VendorStatus`, and `NormalizedVendorStatus` — exactly what the tracking repository method needs. This means tracking can be added as a best-effort call at each public entry point after `ReconcileAttemptAsync` returns, without rewriting the internal flow.

Existing audit events remain unchanged.

## 4. Existing SMS Activity API/Query Findings

`SmsActivityQuery` (LS-NOTIF-SMS-006) has 10 filter fields. Repository: `SmsActivityRepository.BuildBaseQuery` builds a composable EF LINQ query with a LEFT JOIN to `Notifications`. `SmsActivityRawRecord` is a positional record (17 parameters); `SmsActivityItemDto` has 17 response fields.

## 5. Existing Summary API Findings

`SmsActivitySummaryDto` has 10 aggregate fields. `SummarizeAsync` fetches `Status`, `ProviderOwnershipMode`, `CreatedAt` for all matching rows and counts in-memory. Extending it to include reconciliation counts requires adding `LastReconciliationOutcome` and `ReconciliationAttemptCount` to the aggregate projection.

## 6. Existing Audit/Event Findings

`SmsReconciliationService.AuditAsync` emits non-blocking audit events for all reconciliation outcomes. These events are preserved unchanged — the new tracking columns are additive operational metadata, separate from the audit trail.

## 7. Files Added

| File | Purpose |
|------|---------|
| `Notifications.Infrastructure/Data/Migrations/20260509000001_AddSmsReconciliationTracking.cs` | EF migration adding 6 reconciliation tracking columns to `ntf_NotificationAttempts` |

## 8. Files Modified

| File | Change |
|------|--------|
| `Notifications.Domain/NotificationAttempt.cs` | Added 6 reconciliation tracking fields |
| `Notifications.Infrastructure/Data/Configurations/NotificationConfiguration.cs` | Added EF property configs for 6 new fields |
| `Notifications.Application/Interfaces/INotificationAttemptRepository.cs` | Added `UpdateReconciliationTrackingAsync` |
| `Notifications.Infrastructure/Repositories/NotificationAttemptRepository.cs` | Implemented `UpdateReconciliationTrackingAsync` |
| `Notifications.Infrastructure/Services/SmsReconciliationService.cs` | Added `TryPersistTrackingAsync` helper; added best-effort tracking call at each public reconciliation entry point |
| `Notifications.Application/DTOs/SmsActivityDtos.cs` | Extended `SmsActivityQuery` (5 new filter fields), `SmsActivityRawRecord` (6 new fields), `SmsActivityItemDto` (6 new response fields), `SmsActivitySummaryDto` (7 new reconciliation counts) |
| `Notifications.Infrastructure/Repositories/SmsActivityRepository.cs` | Added reconciliation filter predicates; extended projection and summary aggregate |
| `Notifications.Infrastructure/Services/SmsActivityService.cs` | Updated `MapToDto` to include reconciliation fields |
| `Notifications.Api/Endpoints/SmsActivityEndpoints.cs` | Added 5 reconciliation filter query parameters to tenant + admin endpoints |
| `Notifications.Api/Program.cs` | Added 6 new columns to startup safety-net (`EnsureNotificationsSchemaColumnsAsync`) |

## 9. Database / Schema / Config Changes

**New columns on `ntf_NotificationAttempts`:**

| Column | Type | Default | Notes |
|--------|------|---------|-------|
| `LastReconciliationOutcome` | `varchar(100)` NULL | NULL | Last outcome constant |
| `LastReconciledAt` | `datetime(6)` NULL | NULL | UTC timestamp |
| `LastReconciliationErrorCode` | `varchar(100)` NULL | NULL | Safe error code string |
| `LastReconciliationProviderStatus` | `varchar(100)` NULL | NULL | Vendor status string only |
| `LastReconciliationNormalizedStatus` | `varchar(100)` NULL | NULL | Normalized status string |
| `ReconciliationAttemptCount` | `int` NOT NULL | 0 | Incremented per reconciliation |

No credentials, raw payloads, or phone numbers are stored in any of these columns.

**EF migration:** `20260509000001_AddSmsReconciliationTracking`

**Startup safety-net:** All 6 columns added to `EnsureNotificationsSchemaColumnsAsync` (idempotent `INFORMATION_SCHEMA` check + `ALTER TABLE`).

**Optional index** `IX_NotificationAttempts_Channel_TenantId_LastReconciliationOutcome` added to startup safety-net for reconciliation outcome filter queries.

## 10. API / Interface Changes

### New query filters (tenant + admin activity endpoints)

| Parameter | Type | Description |
|-----------|------|-------------|
| `lastReconciliationOutcome` | `string?` | Exact outcome code filter |
| `lastReconciliationErrorCode` | `string?` | Exact error code filter |
| `reconciledFrom` | `DateTime?` | Inclusive start of `LastReconciledAt` |
| `reconciledTo` | `DateTime?` | Inclusive end of `LastReconciledAt` |
| `hasBeenReconciled` | `bool?` | true = `ReconciliationAttemptCount > 0` |

### New activity response fields (`SmsActivityItemDto`)

| Field | Source |
|-------|--------|
| `lastReconciliationOutcome` | `LastReconciliationOutcome` |
| `lastReconciledAt` | `LastReconciledAt` |
| `lastReconciliationErrorCode` | `LastReconciliationErrorCode` |
| `lastReconciliationProviderStatus` | `LastReconciliationProviderStatus` |
| `lastReconciliationNormalizedStatus` | `LastReconciliationNormalizedStatus` |
| `reconciliationAttemptCount` | `ReconciliationAttemptCount` |

### New summary counts (`SmsActivitySummaryDto`)

| Field | Meaning |
|-------|---------|
| `reconciledTotal` | Any attempt where `ReconciliationAttemptCount > 0` |
| `reconciliationUpdated` | `LastReconciliationOutcome == "updated"` |
| `reconciliationNoChange` | `LastReconciliationOutcome == "no_change"` |
| `reconciliationLookupFailed` | `LastReconciliationOutcome == "vendor_lookup_failed"` |
| `reconciliationSkipped` | skipped_* and provider_message_not_found outcomes |
| `reconciliationProviderConfigFailed` | provider_config_* and resolution failure outcomes |
| `neverReconciled` | `ReconciliationAttemptCount == 0` |

### INotificationAttemptRepository

```csharp
Task UpdateReconciliationTrackingAsync(
    Guid attemptId,
    string outcome,
    string? errorCode,
    string? providerStatus,
    string? normalizedStatus,
    DateTime reconciledAt,
    CancellationToken ct = default);
```

## 11. Validation / Testing Performed

Build: `dotnet build Notifications.Api.csproj -c Release --no-restore` — EXIT:0, 0 errors.

Logical validation:
- All 12 outcome constants from LS-NOTIF-SMS-005 are handled in the tracking update flow.
- `OutcomeAttemptNotFound` never reaches `ReconcileAttemptAsync` — no tracking call made.
- `TryPersistTrackingAsync` is wrapped in try/catch — tracking failure logs but does not crash reconciliation.
- Audit events from LS-NOTIF-SMS-004 are not modified.
- `ReconciliationAttemptCount` is incremented via `a.ReconciliationAttemptCount++` (load + increment + save, no separate counter table).
- Delivery status (`Status`, `CompletedAt`) is not modified by `UpdateReconciliationTrackingAsync`.
- No provider credentials or raw payloads are stored in any new field.
- Phone masking in `SmsActivityService` unchanged.
- All existing LS-NOTIF-SMS-001..006 behavior preserved.

## 12. Known Gaps / Issues

- **Counter increment race**: `ReconciliationAttemptCount` is incremented by load-increment-save. Concurrent reconciliation on the same attempt (unusual but possible in batch scenarios) could result in a lost increment. Acceptable for this operational counter — exact accuracy is not required.
- **Summary counts in-memory**: `SummarizeAsync` fetches all matching rows and counts in-memory (consistent with SMS-006). At high volume this could be replaced with SQL `COUNT GROUP BY`.

## 13. Recommended Next Steps

- **Control Center UI**: Wire reconciliation tracking fields into the SMS activity dashboard.
- **Tenant portal**: Surface `lastReconciliationOutcome` in tenant SMS delivery log.
- **Alert rule**: Notify when `reconciliationLookupFailed` count exceeds a threshold.
- **SQL aggregation**: Replace in-memory summary counts with SQL `GROUP BY` for scale.
