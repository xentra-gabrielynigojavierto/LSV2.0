# LS-NOTIF-SMS-013 — SMS Cost and Billing Analytics

**Status:** Implementation complete  
**Date:** 2026-05-09  
**Author:** Platform Engineering

---

## 1. Objective

Add SMS cost tracking, provider spend visibility, tenant usage allocation, and operational billing analytics inside Notification Service and Control Center. Platform operators gain insight into SMS spend, provider efficiency, tenant attribution, retry/failure spend, and usage trends.

This is **operational cost analytics only** — not an invoicing engine, payment processor, tax calculator, or external accounting integration. Estimated cost values are configurable assumptions; they are not invoice-grade billing data.

---

## 2. Initial Codebase Analysis

### 2.1 NotificationAttempt Entity — Existing Fields

File: `Notifications.Domain/NotificationAttempt.cs`

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `TenantId` | `Guid?` | Tenant attribution — available ✓ |
| `NotificationId` | `Guid` | Parent notification |
| `Channel` | `string` | "sms" / "email" — cost gated on "sms" |
| `Provider` | `string` | "twilio" etc — available ✓ |
| `Status` | `string` | sent/delivered/failed/dead_letter/... |
| `AttemptNumber` | `int` | Retry/failover count — available ✓ |
| `ProviderMessageId` | `string?` | Twilio SID — available ✓ |
| `ProviderOwnershipMode` | `string?` | "tenant" / "platform" — available ✓ |
| `ProviderConfigId` | `Guid?` | Opaque config reference — available ✓ |
| `FailureCategory` | `string?` | Error classification — available ✓ |
| `IsFailover` | `bool` | True for failover/retry hop — available ✓ |
| `CompletedAt` | `DateTime?` | When attempt finished |
| `CreatedAt` | `DateTime` | Attempt start time |
| **Reconciliation fields (LS-NOTIF-SMS-007)** | — | Added in migration 20260509000001 |
| `LastReconciliationOutcome` | `string?` | outcome code |
| `LastReconciledAt` | `DateTime?` | timestamp |
| `LastReconciliationErrorCode` | `string?` | error code |
| `LastReconciliationProviderStatus` | `string?` | raw provider status |
| `LastReconciliationNormalizedStatus` | `string?` | normalized status |
| `ReconciliationAttemptCount` | `int` | count |

**Assessment:** `NotificationAttempt` is the correct carrier for cost metadata — it already owns all required attribution fields. Adding cost fields here is less disruptive than a separate table.

### 2.2 SMS Send Path

File: `NotificationService.cs` → `ExecuteSendLoopAsync()`

The send loop:
1. Creates a `NotificationAttempt` with status "sending"
2. Resolves the SMS provider adapter via `ISmsProviderRuntimeResolver`
3. Calls `runtimeCtx.Adapter.SendAsync()`
4. On **success**: sets `attempt.Status = "sent"`, sets `ProviderMessageId`, calls `UpdateAsync()`
5. On **failure**: sets `attempt.Status = "failed"`, records failure category

Cost recording hook: immediately after `await _attemptRepo.UpdateAsync(attempt)` on the success path (line ~942), before the audit call. This is the correct point — only record estimated cost when the provider has accepted the message.

### 2.3 Reconciliation Path

File: `SmsReconciliationService.cs`

The vendor lookup (`GetMessageStatusAsync`) returns a `SmsMessageStatusResult` that contains:
- `Success`, `ProviderStatus`, `NormalizedStatus`, `ErrorCode`, `ErrorMessage`, `Retryable`

**Finding:** No price/cost fields are present in the current `SmsMessageStatusResult`. Twilio's message lookup API does return a `Price` field, but the current adapter does not parse it. Actual provider cost from reconciliation is therefore a **known gap** for this implementation. The `ActualCostAmount` field is persisted on the entity for future use when the Twilio adapter is extended.

### 2.4 Existing Billing Infrastructure

- `TenantBillingPlan`, `TenantBillingRate` entities exist for platform billing plans
- `BillingEndpoints.cs` exposes billing plan/rate CRUD
- These are **separate** from SMS cost analytics and not modified

### 2.5 SmsDashboard Pattern (LS-NOTIF-SMS-008)

The SMS cost analytics implementation follows the exact same layered pattern:
- `SmsDashboardQuery` → `SmsCostQuery`
- `SmsDashboardRepository` → `SmsCostAnalyticsRepository`
- `SmsDashboardService` → `SmsCostAnalyticsService`
- `SmsDashboardEndpoints` → `SmsCostEndpoints`
- `sms-dashboard-api.ts` → `sms-cost-api.ts`

### 2.6 Control Center Existing Pattern

File: `apps/control-center/src/lib/sms-dashboard-api.ts`

Uses `notifClient` (server-side authenticated HTTP client). Same pattern used for cost analytics API client.

---

## 3. Files Added

### Notification Service (.NET)

| File | Purpose |
|---|---|
| `Notifications.Domain/NotificationAttempt.cs` | **Modified** — added 5 cost fields |
| `Notifications.Infrastructure/Data/Configurations/NotificationConfiguration.cs` | **Modified** — EF config for cost fields |
| `Notifications.Infrastructure/Data/Migrations/20260511000001_AddSmsCostFields.cs` | **New** — migration: 5 cost columns on ntf_NotificationAttempts |
| `Notifications.Infrastructure/Data/Migrations/NotificationsDbContextModelSnapshot.cs` | **Modified** — snapshot updated |
| `Notifications.Application/Options/SmsCostAnalyticsOptions.cs` | **New** — config class |
| `Notifications.Application/DTOs/SmsCostDtos.cs` | **New** — query + all response DTOs |
| `Notifications.Application/Interfaces/ISmsCostAnalyticsRepository.cs` | **New** — repository interface |
| `Notifications.Application/Interfaces/ISmsCostAnalyticsService.cs` | **New** — service interface |
| `Notifications.Infrastructure/Repositories/SmsCostAnalyticsRepository.cs` | **New** — EF aggregation (follows SmsDashboardRepository pattern) |
| `Notifications.Infrastructure/Services/SmsCostAnalyticsService.cs` | **New** — orchestration service |
| `Notifications.Api/Endpoints/SmsCostEndpoints.cs` | **New** — 6 admin-only endpoints |
| `Notifications.Infrastructure/DependencyInjection.cs` | **Modified** — register cost options, repo, service |
| `Notifications.Api/Program.cs` | **Modified** — map cost endpoints |
| `Notifications.Api/appsettings.json` | **Modified** — add SmsCostAnalytics section |
| `Notifications.Infrastructure/Services/NotificationService.cs` | **Modified** — record estimated cost after successful SMS send |
| `Notifications.Application/Interfaces/INotificationAttemptRepository.cs` | **Modified** — add `UpdateCostAsync` |
| `Notifications.Infrastructure/Repositories/NotificationAttemptRepository.cs` | **Modified** — implement `UpdateCostAsync` |

### Control Center (Next.js)

| File | Purpose |
|---|---|
| `apps/control-center/src/lib/sms-cost-api.ts` | **New** — TypeScript DTOs + server-side API client |
| `apps/control-center/src/app/notifications/sms-costs/page.tsx` | **New** — PlatformAdmin-only server page |
| `apps/control-center/src/components/sms-costs/cost-panel.tsx` | **New** — client component (filter + tables) |
| `apps/control-center/src/lib/nav.ts` | **Modified** — "SMS Costs" nav item added |

---

## 4. Database / Schema Changes

### Migration: 20260511000001_AddSmsCostFields

Adds 5 nullable columns to `ntf_NotificationAttempts`:

| Column | Type | Default | Notes |
|---|---|---|---|
| `EstimatedCostAmount` | `decimal(18,8)` | NULL | Configured per-provider estimate |
| `ActualCostAmount` | `decimal(18,8)` | NULL | From provider reconciliation (currently always NULL — known gap) |
| `CostCurrency` | `varchar(3)` | NULL | ISO 4217 (e.g. "USD") |
| `CostSource` | `varchar(30)` | NULL | "estimated" / "provider_reconciled" / "manual" / "unavailable" |
| `CostRecordedAt` | `datetime(6)` | NULL | UTC timestamp when cost was written |

**Safety:** All columns are nullable with no default. Pre-existing rows remain valid. No data migration needed.

---

## 5. API / Interface Changes

### New Notification Service Endpoints

All require `Policies.AdminOnly` (PlatformAdmin JWT role). All are read-only.

| Method | Path | Description |
|---|---|---|
| GET | `/v1/admin/sms/costs/summary` | Platform-wide cost KPI aggregate |
| GET | `/v1/admin/sms/costs/trends` | Time-series cost trend (bucket: hour/day/week) |
| GET | `/v1/admin/sms/costs/providers` | Per-provider/config cost breakdown |
| GET | `/v1/admin/sms/costs/tenants` | Per-tenant cost breakdown |
| GET | `/v1/admin/sms/costs/failures` | Failure/retry cost breakdown |
| GET | `/v1/admin/sms/costs/export` | Export-ready JSON rows |

**Common query parameters** (all optional):
- `tenantId`, `provider`, `providerConfigId`, `providerOwnershipMode`
- `status`, `failureCategory`, `costSource`
- `from`, `to` (ISO-8601 UTC)
- `currency`, `bucket`

---

## 6. Configuration

### SmsCostAnalytics section in appsettings.json

```json
{
  "SmsCostAnalytics": {
    "Enabled": true,
    "DefaultCurrency": "USD",
    "DefaultEstimatedOutboundSmsCost": 0.0000,
    "TwilioEstimatedOutboundSmsCost": 0.0075,
    "FailedMessageCostPolicy": "count_estimated_when_provider_accepted",
    "RetryCostPolicy": "per_attempt"
  }
}
```

**Notes:**
- `TwilioEstimatedOutboundSmsCost = 0.0075` is used when provider = "twilio" and a message is accepted.
- `DefaultEstimatedOutboundSmsCost = 0.0000` means unknown-provider SMS defaults to CostSource = "unavailable" (no cost assumed).
- If `Enabled = false`, cost recording is skipped but analytics queries still work against historical data.

---

## 7. Cost Recording Semantics

### Effective cost rule (for analytics aggregation)

```
EffectiveCost = ActualCostAmount ?? EstimatedCostAmount ?? 0
```

Metadata field `CostSource` indicates reliability:
- `estimated` — provider-specific configured estimate was used
- `provider_reconciled` — actual cost from vendor billing API (not yet implemented)
- `manual` — operator-entered correction
- `unavailable` — no cost estimate configured; amount is NULL / zero

### Which attempts get cost recorded

- Channel = "sms" only
- On the **success path** (provider accepted the message) — `attempt.Status = "sent"`
- Retry hops: each attempt is costed independently (`RetryCostPolicy = "per_attempt"`)
- Failed attempts where provider never accepted: CostSource = "unavailable", no cost

### FailedMessageCostPolicy

`count_estimated_when_provider_accepted`: only attempts that reached the provider (have a ProviderMessageId) are costed. Pure local failures (auth_config_failure, no routes) are never costed.

---

## 8. Known Gaps / Issues

1. **Actual provider cost (ActualCostAmount):** The current Twilio adapter `SmsMessageStatusResult` does not parse Twilio's `Price` or `PriceUnit` response fields. `ActualCostAmount` is always NULL until the adapter is extended. The entity field and the `provider_reconciled` cost source are in place for future use.

2. **FX conversion:** Not implemented per spec. All cost values are single-currency (USD by default). Operators must configure the correct per-provider estimate if they use a non-USD pricing region.

3. **Historical backfill:** Pre-existing SMS attempts have NULL cost fields. Analytics will show them as "uncosted" unless a manual backfill is performed via the repository `UpdateCostAsync` method.

4. **Per-attempt cost recording is best-effort:** The cost recording call in NotificationService is wrapped in try/catch and never blocks or affects delivery semantics.

---

## 9. Validation Performed

- TypeScript: `npx tsc --noEmit` in `apps/control-center` — 0 errors
- .NET: `dotnet build Notifications.Api.csproj --no-restore` — Build succeeded
- All existing SMS operational APIs (LS-NOTIF-SMS-001 through LS-NOTIF-SMS-012) verified unmodified by the cost recording hook (additive only, non-blocking)
- Security: no credentials, ProviderMessageId mapping (safe SID), no phone numbers, no raw payloads in any cost analytics response

---

## 10. Non-Goals

- No invoicing engine
- No payment processing
- No tax calculation
- No external accounting integration
- No FX conversion
- No new SMS providers
- No change to inbound SMS handling
- No provider credentials in cost fields
- No raw phone numbers in any response
- No duplicate aggregation logic in Control Center
