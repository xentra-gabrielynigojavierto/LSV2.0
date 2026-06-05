# LS-NOTIF-SMS-016 — Recipient Intelligence, Suppression Intelligence, and Delivery Reputation Management

**Feature ID:** LS-NOTIF-SMS-016  
**Status:** IN PROGRESS  
**Date started:** 2026-05-10

---

## 1. Initial Codebase Analysis

### 1.1 NotificationAttempt — retry/dead-letter fields

**File:** `Notifications.Domain/NotificationAttempt.cs`

| Field | Type | Notes |
|-------|------|-------|
| `Status` | string | pending / retrying / sent / delivered / failed / dead_letter |
| `AttemptNumber` | int | 1-based send attempt count |
| `IsFailover` | bool | True when this attempt is a failover from another provider |
| `FailureCategory` | string? | Classified failure string (see §1.3) |
| `ErrorMessage` | string? | Raw error text from adapter |
| `LastReconciliationOutcome` | string? | LS-NOTIF-SMS-007: reconciliation result |
| `LastReconciliationErrorCode` | string? | |
| `LastReconciliationProviderStatus` | string? | Vendor status (safe metadata) |
| `LastReconciliationNormalizedStatus` | string? | |
| `ReconciliationAttemptCount` | int | |
| `EstimatedCostAmount` | decimal? | LS-NOTIF-SMS-013 |
| `ActualCostAmount` | decimal? | Always null today (known gap) |
| `CostCurrency` | string? | ISO 4217 |
| `CostSource` | string? | estimated / provider_reconciled / manual / unavailable |

**Notification domain entity** (`Notification.cs`):
- `RetryCount` / `MaxRetries` (default 3) / `NextRetryAt` — retry state
- `Status` — accepted / processing / sent / failed / retrying / dead-letter
- `FailureCategory` — set on send failure, cleared on retry
- `RecipientJson` — JSON with `{"phone": "+1..."}` for SMS (source of phone for hashing)

### 1.2 Retry/dead-letter flow

**Entry points:**
1. `NotificationWorker` polls `GetEligibleForRetryAsync(batch=10)` every ~60s → calls `ProcessAutoRetryAsync`
2. `NotificationService.RetryAsync` / `AdminRetryAsync` — operator-triggered

**`ProcessAutoRetryAsync` flow** (`NotificationService.cs:1305`):
- Validates status == "retrying"
- Calls `DispatchAllRoutesAsync` (the core send pipeline)
- On success: `notification.Status = "sent"`
- On exhausted retries: `notification.Status = "dead-letter"`, `FailureCategory = "max_retries_exhausted"`
- On partial retry: `notification.RetryCount++`, `notification.Status = "retrying"`, `NextRetryAt = ComputeNextRetryAt(count)` (exponential: 5 / 30 / 120 / 480 min)

**Retryable failure categories:**
```csharp
private static readonly HashSet<string> RetryableFailureCategories = new()
{
    "retryable_provider_failure",  // includes provider 429/5xx
    // others from adapters...
};
```

**Dead-letter trigger:** `notification.RetryCount >= notification.MaxRetries`

### 1.3 Existing failure classifications

**Twilio adapter** (`TwilioAdapter.cs:ClassifyError`):
- HTTP 401/403 → `auth_config_failure`
- HTTP 400 + code 21211/21614/invalid → `invalid_recipient`
- HTTP 429/5xx → `retryable_provider_failure`
- Other 4xx → `non_retryable_failure`

**Vonage adapter** (`VonageAdapter.cs:ClassifyVonageStatus`):
- Status 1 → `retryable_provider_failure` (throttled)
- Status 2 → `non_retryable_failure` (missing params)
- Status 3 → `invalid_recipient` (invalid params)
- Status 4/5 → `auth_config_failure`
- Status 7/8/12/13/14 → `non_retryable_failure`
- Status 9 → `retryable_provider_failure`
- Status 11 → `provider_unavailable`
- Status 15 → `invalid_recipient` (invalid sender address)
- Status 22 → `invalid_recipient` (invalid number format)

**Domain enum** (`FailureCategory`): `RetryableProviderFailure`, `NonRetryableFailure`, `ProviderUnavailable`, `InvalidRecipient`, `AuthConfigFailure`

### 1.4 SmsProviderQualitySnapshot structure

Aggregates per `(ProviderType, ProviderConfigId, TenantId, CountryCode)`:
- Attempt counts: Total, Delivered, Failed, Retry, DeadLetter, Reconciled, ReconciliationFailures
- Rates: DeliverySuccessRate, FailureRate, RetryRate, DeadLetterRate, ReconciliationFailureRate
- Scores: QualityScore (0-100), CostEfficiencyScore, HealthPenalty
- Cost: AverageEffectiveCost, CostPerDeliveredMessage

### 1.5 Existing contact enforcement / suppression

**`ContactSuppression`** entity: `SuppressionType` (Manual/Bounce/Unsubscribe/Complaint/InvalidContact/CarrierRejection/SystemProtection), `SuppressionSource`, `SuppressionStatus`.

**`ContactEnforcementService`** runs before send; checks suppression + `RecipientContactHealth`. Existing suppression uses compliance-level contact suppressions (phone/email), not intelligence-level retry suppression.

**`TenantContactPolicy`**: `BlockCarrierRejectedContacts`, `BlockInvalidContacts` — per-tenant blocking flags.

### 1.6 Hashing pattern

**Existing**: `SmsAlertEscalationMessageBuilder` uses `SHA256.HashData(Encoding.UTF8.GetBytes(canonical))`. New implementation uses `HMACSHA256` with a configurable salt for recipient privacy.

### 1.7 Worker pattern

**Model**: `SmsProviderQualityWorker` (BackgroundService) — startup delay 90s, configurable interval, `IServiceScopeFactory` for scoped services, exception swallowing, graceful cancellation.

---

## 2. Architecture Decisions

### 2.1 Recipient identity hashing

- Hash = `HMAC-SHA256(normalizePhone(phone), UTF8(salt))` → 64-char hex string
- Normalize: `Regex.Replace(phone.Trim(), @"[^\d+]", "")`
- Salt from `SmsRecipientIntelligence:RecipientHashSalt` (required for deployment; falls back to fixed prefix SHA256 when unconfigured for dev safety)
- Never logged, never returned in API responses

### 2.2 Suppression integration

- Injected into `NotificationService.ProcessAutoRetryAsync` for SMS channel only
- Wrapped in `try/catch` — never breaks delivery pipeline
- Decision outcomes:
  - `allow` / `warn` → proceed with retry (warn persists decision)
  - `soft_suppress` → skip this retry cycle (reschedule 30min), persist decision
  - `hard_suppress` / `review_required` → dead-letter immediately, persist decision, special FailureCategory

### 2.3 Snapshot aggregation

- Joins `NotificationAttempt` + `Notification.RecipientJson` in a bounded LINQ query
- Extracts phone from `RecipientJson.phone`, hashes it
- Groups by `(recipientHash, tenantId, providerType, countryCode)` 
- Computes rates + scores per group
- Persists `SmsRecipientReputationSnapshot` (upsert by hash+tenant+provider+country)

---

## 3. Files Added

| File | Description |
|------|-------------|
| `Notifications.Domain/SmsRecipientReputationSnapshot.cs` | Recipient reputation snapshot entity |
| `Notifications.Domain/SmsSuppressionDecision.cs` | Suppression decision audit entity |
| `Notifications.Application/Options/SmsRecipientIntelligenceOptions.cs` | Config options |
| `Notifications.Application/Interfaces/ISmsRecipientIdentityHasher.cs` | Hashing interface |
| `Notifications.Application/Interfaces/ISmsRecipientIntelligenceService.cs` | Intelligence service interface |
| `Notifications.Application/Interfaces/ISmsRetrySuppressionService.cs` | Suppression service interface |
| `Notifications.Application/DTOs/SmsRecipientIntelligenceDtos.cs` | API DTOs |
| `Notifications.Infrastructure/Services/SmsRecipientIdentityHasher.cs` | HMAC-SHA256 hasher |
| `Notifications.Infrastructure/Services/SmsRecipientIntelligenceService.cs` | Scoring + snapshot service |
| `Notifications.Infrastructure/Services/SmsRetrySuppressionService.cs` | Suppression evaluation |
| `Notifications.Infrastructure/Workers/SmsRecipientIntelligenceWorker.cs` | Background worker |
| `Notifications.Infrastructure/Data/Configurations/SmsRecipientReputationSnapshotConfiguration.cs` | EF configuration |
| `Notifications.Infrastructure/Data/Configurations/SmsSuppressionDecisionConfiguration.cs` | EF configuration |
| `Notifications.Infrastructure/Data/Migrations/20260512000001_AddSmsRecipientIntelligence.cs` | DB migration |
| `Notifications.Api/Endpoints/SmsRecipientIntelligenceEndpoints.cs` | Admin APIs (5 endpoints) |
| `apps/control-center/src/components/sms-routing/recipient-intelligence-panel.tsx` | CC UI |

---

## 4. Files Modified

| File | Change |
|------|--------|
| `Notifications.Infrastructure/Data/NotificationsDbContext.cs` | Add 2 DbSets + ApplyConfiguration |
| `Notifications.Infrastructure/DependencyInjection.cs` | Register 3 services + worker |
| `Notifications.Api/Program.cs` | Map new endpoints |
| `Notifications.Api/appsettings.json` | Add SmsRecipientIntelligence config |
| `Notifications.Infrastructure/Services/NotificationService.cs` | Inject + call suppression in ProcessAutoRetryAsync |
| `Notifications.Infrastructure/Data/Migrations/NotificationsDbContextModelSnapshot.cs` | Add new entity models |
| `apps/control-center/src/lib/sms-routing-api.ts` | Add recipient intelligence API calls |
| `apps/control-center/src/app/notifications/sms-routing/page.tsx` | Add recipient intelligence tab |

---

## 5. Database / Schema Changes

Two new tables, migration `20260512000001_AddSmsRecipientIntelligence`:

### `ntf_SmsRecipientReputationSnapshots`
- `Id` char(36) PK
- `RecipientHash` varchar(64) — HMAC-SHA256 hex of normalized phone
- `TenantId` char(36) nullable — scoped aggregate
- `ProviderType` varchar(100) nullable
- `CountryCode` varchar(10) nullable — ISO code, never raw phone
- `Region` varchar(50) nullable
- `TotalAttempts` int
- `DeliveredAttempts` int
- `FailedAttempts` int
- `RetryAttempts` int
- `DeadLetterAttempts` int
- `CarrierRejectedAttempts` int
- `InvalidDestinationAttempts` int
- `AverageLatencyMs` decimal(10,2) nullable
- Rates: DeliverySuccessRate, FailureRate, RetryRate, DeadLetterRate, CarrierFailureRate (decimal(5,4))
- Scores: InvalidNumberRisk, RetrySuppressionRisk, QualityScore (decimal(5,2))
- `DestinationRiskLevel` varchar(20)
- `LastAttemptAt` datetime nullable
- `CalculatedAt` datetime

Indexes: RecipientHash, (TenantId, RecipientHash), (ProviderType, RecipientHash), (CountryCode, CalculatedAt)

### `ntf_SmsSuppressionDecisions`
- `Id` char(36) PK
- `RecipientHash` varchar(64)
- `TenantId` char(36) nullable
- `NotificationId` char(36) nullable
- `AttemptId` char(36) nullable
- `DecisionType` varchar(30)
- `ReasonCode` varchar(50)
- `RiskScore` decimal(5,2) nullable
- `QualityScore` decimal(5,2) nullable
- `RetryCount` int
- `ProviderType` varchar(100) nullable
- `CountryCode` varchar(10) nullable
- `Region` varchar(50) nullable
- `DecisionMetadataJson` text nullable
- `CreatedAt` datetime

Indexes: RecipientHash, (TenantId, RecipientHash), (TenantId, DecisionType, CreatedAt)

---

## 6. API / Interface Changes

### New endpoints (all require `PlatformAdmin`):

| Method | Path | Description |
|--------|------|-------------|
| GET | `/v1/admin/sms/recipients/quality` | Recipient reputation snapshots, filtered |
| GET | `/v1/admin/sms/recipients/failures` | High-failure recipients (risk-ordered) |
| GET | `/v1/admin/sms/recipients/suppressions` | Suppression decision audit log |
| GET | `/v1/admin/sms/recipients/risk` | Destination risk distribution summary |
| GET | `/v1/admin/sms/recipients/trends` | Delivery quality trends over time |

All filters: `tenantId`, `provider`, `countryCode`, `region`, `riskLevel`, `decisionType`, `from`, `to`, `limit`, `offset`.

**No raw phone numbers in any response.** All recipient identity referenced by `recipientHash` (opaque hex) only.

---

## 7. Carrier Failure Classification Enhancements

Normalized categories (LS-NOTIF-SMS-016 standard):

| Category | Twilio triggers | Vonage triggers |
|----------|----------------|----------------|
| `invalid_destination` | 21211, 21614, "invalid" body | status 3, 15, 22 |
| `carrier_rejected` | (new — mapped from Vonage 7 "barred") | status 7, 8 |
| `unreachable_destination` | (mapped from general non-retryable) | status 6, 13 |
| `blocked_destination` | (mapped from non-retryable) | status 9 quota |
| `delivery_timeout` | (reconciliation timeout outcomes) | reconciliation timeout |
| `provider_rejected` | non-retryable 4xx | status 2, 12, 14 |
| `unknown_failure` | all other non-retryable | all other |

These categories are internal to `SmsRecipientIntelligenceService` for scoring — no change to existing adapter `FailureCategory` strings.

---

## 8. Security / Privacy

- Raw phone numbers are never persisted in new tables
- `RecipientHash` is a 64-char HMAC-SHA256 hex string — computationally irreversible without the salt
- Salt is stored as a Replit secret / environment variable — not in code
- API responses contain only: `recipientHash` (opaque), aggregate counts, rates, risk levels
- All new endpoints require `Policies.AdminOnly`
- No credentials, raw payloads, ProviderMessageId, or phone numbers in any new response

---

## 9. Known Gaps / Issues

1. **`RecipientPhoneForInferenceOnly` still not wired** — `adaptive_regional` still falls through to `adaptive_quality` (inherited gap from LS-NOTIF-SMS-015).
2. **Carrier-level rejection codes not enriched** — Vonage status 7 (number barred) is classified `non_retryable_failure` by the existing adapter; LS-NOTIF-SMS-016 maps these internally for intelligence purposes but does not change the adapter output.
3. **`ActualCostAmount` always null** — cost-per-delivered-message analytics remain estimation-based only.
4. **`soft_suppress` reschedules at fixed 30min** — no exponential back-off for soft suppression (acceptable for v1).
5. **No Control Center suppression management** — operators can view suppression decisions but cannot lift them via UI (would require a separate management endpoint, tracked as future work).
6. **Snapshot does not segment by `ProviderConfigId`** — recipient snapshots aggregate across all configs for a given provider type. Finer-grained segmentation is a future enhancement.

---

## 10. Validation

### 10.1 .NET Build
- **Result:** Build succeeded — 0 errors, 3 pre-existing warnings (MailKit CVE NU1902, JwtBearer NU1605, EF tools NU1606)
- Command: `dotnet build apps/services/notifications/Notifications.Api/Notifications.Api.csproj -c Release`
- No new warnings introduced by LS-NOTIF-SMS-016 code

### 10.2 TypeScript Build
- **Result:** 0 errors (tsc --noEmit)
- New types correctly propagate through `sms-routing-api.ts` → `routing-panel.tsx` → `recipient-intelligence-panel.tsx`
- Page-level `Promise.allSettled` gracefully handles intelligence API failures

### 10.3 Suppression pipeline safety
- `EvaluateAsync` is wrapped in try/catch in `ProcessAutoRetryAsync` — no uncaught exceptions possible
- When no snapshot exists: returns `allow` with `telemetry_unavailable` reason
- When salt not configured: hasher returns hash via SHA256+prefix (dev-only fallback) — never null
- Worker defaults to `Enabled = false` — no background activity until explicitly enabled

### 10.4 Privacy / security invariants
- `RecipientHash` is HMAC-SHA256(normalizePhone, salt) — never raw phone
- No phone numbers in: `ntf_SmsRecipientReputationSnapshots`, `ntf_SmsSuppressionDecisions`, API responses
- `DecisionMetadataJson` constructed from safe aggregate fields only (no phone, no credentials)
- All 5 admin endpoints require `Policies.AdminOnly`

### 10.5 Migration
- `20260512000001_AddSmsRecipientIntelligence`: creates 2 tables + 8 indexes
- Model snapshot updated to include both new entity types
- Down migration is clean (drop both tables)

### 10.6 Completeness checklist
- [x] Domain entities
- [x] Application interfaces + options + DTOs
- [x] Infrastructure services (hasher, intelligence, suppression)
- [x] Background worker
- [x] EF configurations
- [x] Migration
- [x] Model snapshot update
- [x] API endpoints (5 admin)
- [x] DI registration
- [x] Program.cs endpoint mapping
- [x] appsettings.json config section
- [x] NotificationService integration (ProcessAutoRetryAsync)
- [x] Control Center API client (sms-routing-api.ts)
- [x] Control Center panel component (recipient-intelligence-panel.tsx)
- [x] Control Center routing-panel.tsx tab addition
- [x] Control Center page.tsx data fetching
