# LS-NOTIF-SMS-004 — Outbound SMS Vendor Log Reconciliation

**Status:** IN PROGRESS → COMPLETE  
**Date:** 2026-05-08  
**Scope:** Notification Service — SMS vendor status lookup, reconciliation service, batch worker, manual API  
**Depends on:** LS-NOTIF-SMS-001 (TwilioAdapter), LS-NOTIF-SMS-002/003 (preference compliance)

---

## 1. Initial Codebase Analysis

### 1.1 Existing SMS Provider Abstraction

`ISmsProviderAdapter` (in `IEmailProviderAdapter.cs`):
- `string ProviderType { get; }` 
- `Task<bool> ValidateConfigAsync()`
- `Task<SmsSendResult> SendAsync(SmsSendPayload payload)`
- `Task<ProviderHealthResult> HealthCheckAsync()`

**Gap**: No status lookup method. This is intentional — adding one directly would require all future SMS adapters to implement it. Solution: add separate `ISmsProviderStatusLookup` capability interface. Callers check `adapter is ISmsProviderStatusLookup` to opt into the capability.

### 1.2 Existing TwilioAdapter

`TwilioAdapter : ISmsProviderAdapter`:
- Implements `SendAsync`, `HealthCheckAsync`, `ValidateConfigAsync`
- Uses `_accountSid`, `_authToken`, `_defaultFromNumber`
- Uses `IHttpClientFactory.CreateClient("Twilio")` with 10s timeout on send, 8s on health check
- Existing error classification: `ClassifyError(statusCode, body)` in TwilioAdapter
- Phone masking: caller responsibility (not in adapter)

Twilio Message status lookup URL: `https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Messages/{messageSid}.json`

### 1.3 Existing NotificationAttempt Model

`NotificationAttempt`:
- `Channel` — "sms" | "email" | "in_app"
- `Provider` — "twilio" etc.
- `ProviderMessageId` — Twilio MessageSid (nullable — not set if send failed before getting SID)
- `Status` — `pending`, `sending`, `sent`, `failed`
- `FailureCategory`, `ErrorMessage` — failure context
- `TenantId`, `NotificationId`, `AttemptNumber`

**Terminal statuses** (from `DeliveryStatusService.AttemptTerminal`): `"delivered"`, `"failed"`  
**Non-terminal statuses for reconciliation**: `"pending"`, `"sending"`, `"sent"`, `"queued"`, `"processing"`, `"retrying"`

### 1.4 Existing Delivery Status Service

`DeliveryStatusService.UpdateAttemptFromEventAsync(Guid attemptId, string normalizedEventType)`:
- Already enforces terminal state protection: skips if attempt is already in `AttemptTerminal`
- Maps normalized event type → attempt status:
  - `accepted`/`queued` → `sending`
  - `sent` → `sent`
  - `delivered` → `sent`
  - `failed`/`undeliverable`/`bounced`/`rejected` → `failed`

`DeliveryStatusService.UpdateNotificationFromEventAsync(Guid notificationId, string normalizedEventType)`:
- Maps `sent`/`delivered` → notification `sent`; `failed`/etc → notification `failed`

**Key insight**: Reconciliation reuses these methods to avoid duplicating terminal-state protection logic.

### 1.5 Existing Worker Pattern

Three workers follow the same `BackgroundService` pattern:
- `IServiceScopeFactory.CreateScope()` → resolve scoped services
- `CancellationToken` passed throughout
- Configurable interval, startup stagger
- `_logger.LogError` on exception (no crash)

`StatusSyncWorker` already calls `notifService.ReconcileStalledAsync()` every 5 minutes. The SMS reconciliation worker follows the same pattern at a configurable interval (default: 15 minutes, **disabled by default**).

### 1.6 Existing Audit Patterns

- All audit calls wrapped in try/catch (best-effort)
- `SourceSystem = "notifications"`, `Outcome = "success"` | `"warning"` | `"failure"`
- `Scope = new AuditEventScopeDto { TenantId }` for tenant scoping
- `Metadata` = JSON string
- No phone numbers in audit metadata (masking convention)

### 1.7 Twilio Status → Normalized Internal Mapping

| Twilio `status` | Normalized (LS-004) | Normalized event for DeliveryStatusService |
|---|---|---|
| `queued`, `accepted`, `scheduled` | `queued` | `queued` |
| `sending` | `processing` | `queued` |
| `sent` | `sent` | `sent` |
| `delivered` | `delivered` | `delivered` |
| `undelivered` | `failed` | `failed` |
| `failed` | `failed` | `failed` |
| `canceled` | `failed` | `failed` |

---

## 2. Design Decisions

### 2.1 Capability Interface Pattern

`ISmsProviderStatusLookup` added alongside (not extending) `ISmsProviderAdapter`. `TwilioAdapter` implements both. `SmsReconciliationService` resolves `ISmsProviderAdapter` and casts to `ISmsProviderStatusLookup` — if not supported, returns `skipped_unsupported_provider`.

This keeps the abstraction clean: new SMS adapters that don't support pull-status don't need to implement it.

### 2.2 Reconciliation Outcome Determination

- Compare attempt status BEFORE and AFTER calling `DeliveryStatusService`. If the status changed → `updated`. If unchanged (because terminal protection blocked it, or vendor status same as local) → `no_change`.
- Never resend SMS. Never call `SendAsync` from reconciliation code.

### 2.3 Worker Configuration (Off By Default)

`SMS_RECONCILIATION_ENABLED=false` by default. Operators must explicitly enable it. This prevents unexpected Twilio API calls in environments where webhooks are reliable.

---

## 3. Files Added

| File | Purpose |
|---|---|
| `Notifications.Application/Interfaces/ISmsReconciliationService.cs` | Reconciliation service interface + result models + `ISmsProviderStatusLookup` + `SmsMessageStatusResult` |
| `Notifications.Infrastructure/Services/SmsReconciliationService.cs` | Full reconciliation implementation |
| `Notifications.Infrastructure/Workers/SmsReconciliationWorker.cs` | Periodic background worker |
| `Notifications.Api/Endpoints/SmsReconciliationEndpoints.cs` | `POST /v1/sms/reconciliation/*` endpoints |

---

## 4. Files Modified

| File | Change |
|---|---|
| `Notifications.Application/Interfaces/IEmailProviderAdapter.cs` | Added `ISmsProviderStatusLookup` interface + `SmsMessageStatusResult` model |
| `Notifications.Infrastructure/Providers/Adapters/TwilioAdapter.cs` | Implemented `ISmsProviderStatusLookup.GetMessageStatusAsync` |
| `Notifications.Application/Interfaces/INotificationAttemptRepository.cs` | Added `GetStaleSmsAttemptsAsync` |
| `Notifications.Infrastructure/Repositories/NotificationAttemptRepository.cs` | Implemented `GetStaleSmsAttemptsAsync` |
| `Notifications.Infrastructure/DependencyInjection.cs` | Registered `ISmsReconciliationService`, `SmsReconciliationWorker` (conditional) |
| `Notifications.Api/Program.cs` | Mapped `SmsReconciliationEndpoints` |

---

## 5. Database / Schema Changes

None. No new tables or columns required.

- `NotificationAttempt` already has all fields needed for reconciliation (`Channel`, `Provider`, `ProviderMessageId`, `Status`, `FailureCategory`, `ErrorMessage`)
- `NotificationAttempt.CompletedAt` already set on terminal state by `UpdateStatusAsync`

---

## 6. API / Interface Changes

### 6.1 New Endpoints

| Method | Path | Description |
|---|---|---|
| `POST` | `/v1/sms/reconciliation/attempts/{attemptId}` | Reconcile a specific attempt by ID |
| `POST` | `/v1/sms/reconciliation/provider-messages/{providerMessageId}` | Reconcile by Twilio MessageSid |
| `POST` | `/v1/sms/reconciliation/stale?limit={n}&olderThanMinutes={n}` | Batch reconcile stale pending attempts |

All require auth. No provider credentials in response. Attempt phone number not exposed in response.

---

## 7. Configuration Values

| Key | Default | Description |
|---|---|---|
| `SMS_RECONCILIATION_ENABLED` | `false` | Enable background reconciliation worker |
| `SMS_RECONCILIATION_INTERVAL_MINUTES` | `15` | Worker polling interval |
| `SMS_RECONCILIATION_STALE_AFTER_MINUTES` | `30` | Minimum age for stale attempt discovery |
| `SMS_RECONCILIATION_BATCH_SIZE` | `50` | Max attempts per reconciliation cycle |

---

## 8. Audit Events

| Event Type | Description |
|---|---|
| `sms.reconciliation.updated` | Local status updated based on vendor status |
| `sms.reconciliation.no_change` | Vendor status matches local — no update needed |
| `sms.reconciliation.lookup_failed` | Vendor status lookup failed |
| `sms.reconciliation.skipped` | Attempt skipped (no SID, not SMS, etc.) |
| `sms.reconciliation.batch_completed` | Batch reconciliation cycle completed |

---

## 9. Twilio Message Status Lookup

URL: `GET https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Messages/{messageSid}.json`  
Auth: HTTP Basic (`accountSid:authToken`)  
Timeout: 10 seconds  
Response fields used: `status`, `error_code`, `error_message`, `date_sent`, `date_updated`, `sid`

Error handling:
- `401/403` → `Success=false`, non-retryable auth failure
- `404` → `Success=false`, non-retryable `message_not_found`
- `429` → `Success=false`, retryable `provider_rate_limited`
- `5xx` / timeout → `Success=false`, retryable `provider_unavailable`

---

## 10. Status Precedence (Reconciliation)

| Local Status | Vendor Status | Action |
|---|---|---|
| `failed` (terminal) | any | Skipped (terminal protection in DeliveryStatusService) |
| `delivered` (terminal) | any | Skipped (terminal protection) |
| `sent` | `delivered` | Updated → `sent` attempt (no effective change) + audit |
| `sent` | `failed` | Updated → `failed` attempt |
| `sending`/`pending` | `delivered` | Updated → `sent` attempt |
| `sending`/`pending` | `failed` | Updated → `failed` attempt |
| any non-terminal | `sent` | Updated → `sent` attempt |
| any | same as local | No-change audit |

---

## 11. Validation Performed

### Build
- `dotnet build Notifications.Api` — ✅ 0 errors

### Provider status lookup (code review)
- ✅ Valid Twilio SID → HTTP GET to Messages resource with Basic auth
- ✅ Twilio `delivered` → normalized `delivered`
- ✅ Twilio `failed`/`undelivered`/`canceled` → normalized `failed`
- ✅ `404` → `message_not_found`, non-retryable
- ✅ `401/403` → auth failure, non-retryable
- ✅ `429` → retryable
- ✅ Timeout → retryable `provider_unavailable`
- ✅ Credentials not logged anywhere
- ✅ Phone not extracted or logged from vendor response

### Reconciliation (code review)
- ✅ Missing `ProviderMessageId` → `skipped_missing_provider_message_id`
- ✅ Non-SMS attempt → `skipped_not_sms`
- ✅ Provider adapter not implementing `ISmsProviderStatusLookup` → `skipped_unsupported_provider`
- ✅ Lookup failure → `vendor_lookup_failed`
- ✅ Terminal attempt → protected by `DeliveryStatusService`
- ✅ `ReconcileByAttemptIdAsync` and `ReconcileByProviderMessageIdAsync` work independently
- ✅ Batch `ReconcileStalePendingAsync` queries non-terminal SMS attempts older than threshold

### Worker (code review)
- ✅ `SMS_RECONCILIATION_ENABLED=false` → worker starts but immediately exits each cycle
- ✅ `CancellationToken` respected throughout
- ✅ Per-attempt failures don't stop the batch
- ✅ Batch audit event emitted at end of cycle

### Existing behavior preservation
- ✅ `TwilioAdapter.SendAsync` unchanged
- ✅ LS-NOTIF-SMS-002/003 preference enforcement unchanged
- ✅ Webhook status delivery path unchanged
- ✅ No SMS resend in reconciliation code

---

## 12. Known Gaps / Future Enhancements

| Item | Notes |
|---|---|
| Per-tenant Twilio adapter resolution | Currently uses the single platform Twilio adapter. Tenant-owned Twilio configs would need `InboundSmsResolverService`-style lookup by `ProviderConfigId` on the attempt. |
| Exponential backoff on rate limiting | Current implementation skips rate-limited lookups and emits audit. A future enhancement could implement jitter/backoff. |
| DeliveryIssue creation on vendor-confirmed failure | Reconciliation detects `failed` from vendor but does not currently create a `DeliveryIssue` record. Could call `IDeliveryIssueService.ProcessEventAsync` on `failed` outcome. |
| Reconciliation result persistence | Results are only logged and audited — not persisted to a reconciliation history table. |
| Webhook + reconciliation deduplication | Possible race condition if a webhook arrives during reconciliation. DeliveryStatusService's terminal protection handles this safely. |

---

## 13. Recommended Next Steps

1. **Enable in staging** — Set `SMS_RECONCILIATION_ENABLED=true` with `SMS_RECONCILIATION_STALE_AFTER_MINUTES=60` for a production trial.
2. **Create `DeliveryIssue` on vendor-confirmed failure** — Add call to `IDeliveryIssueService.ProcessEventAsync` in reconciliation service when vendor confirms `failed`.
3. **Per-tenant adapter resolution** — For tenants with their own Twilio numbers, look up `ProviderConfigId` from the attempt and build a tenant-scoped adapter.
4. **Reconciliation history table** — Persist `SmsReconciliationResult` records for audit queries.
5. **Rate-limit backoff** — Implement exponential backoff when Twilio returns 429 during batch.
