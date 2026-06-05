# LS-NOTIF-SMS-017 — SMS Governance Policies, Compliance Controls, and Operational Guardrails

**Status:** COMPLETE  
**Date:** 2026-05-10  
**Service:** `apps/services/notifications` (Notifications.Api / .Application / .Domain / .Infrastructure)  
**Control Center:** `apps/control-center/src/`

---

## Overview

LS-NOTIF-SMS-017 implements a multi-layer SMS governance engine that intercepts every outbound SMS (initial send and auto-retry) and evaluates it against a configurable set of tenant-scoped or platform-wide governance policies before any message reaches a provider. The engine is non-blocking: evaluation errors degrade to allow so they can never stall the delivery pipeline.

---

## Architecture

### Pipeline Position

```
ContactEnforcement
      ↓
ProviderRouting + SmsRoutingEngine   (LS-014)
      ↓
[LS-017] EvaluatePreSendAsync        ← NEW: blocks, delays, or allows
      ↓
ExecuteSendLoopAsync
      ↓  (on retry)
SmsRetrySuppressionService           (LS-016)
      ↓
[LS-017] EvaluateRetryAsync          ← NEW: blocks, delays, or allows retry
      ↓
ExecuteSendLoopAsync
```

### Decision Outcomes

| Outcome | Notification State | Effect |
|---|---|---|
| `allow` | unchanged | Pipeline continues |
| `delay` | `retrying`, `NextRetryAt` = policy window | Deferred to next allowed window |
| `throttle` | `retrying`, `NextRetryAt` = +30m | Deferred for rate-limit cooldown |
| `block` | `dead-letter` | Moved to dead-letter, issue created |
| `review_required` | `dead-letter` | Moved to dead-letter (manual review) |

---

## Domain Layer

### `SmsGovernancePolicy` (`Notifications.Domain`)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK |
| `TenantId` | `Guid?` | Null = platform-wide policy |
| `Name` | `string(200)` | Human-readable label |
| `PolicyType` | `string(50)` | See policy types below |
| `Enabled` | `bool` | Default true |
| `Priority` | `int` | Lower = evaluated first (default 100) |
| `PolicyJson` | `string` (text) | Type-specific configuration |
| `EmergencyOverrideAllowed` | `bool` | Allows override in emergency mode |
| `CreatedAt/UpdatedAt` | `DateTime` | Audit timestamps |
| `CreatedBy/UpdatedBy` | `string?` | Operator identity |

### `SmsGovernanceDecision` (`Notifications.Domain`)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK |
| `NotificationId` | `Guid?` | Links to notification |
| `AttemptId` | `Guid?` | Links to specific attempt |
| `TenantId` | `Guid?` | Tenant context |
| `PolicyId` | `Guid?` | Which policy fired |
| `ProviderConfigId` | `Guid?` | Provider targeted |
| `PolicyType` | `string(50)` | Policy type that fired |
| `DecisionType` | `string(30)` | `allow/delay/throttle/block/review_required/override_allowed` |
| `ReasonCode` | `string(60)` | Machine-readable reason |
| `ProviderType` | `string(100)?` | Provider name |
| `CountryCode` | `string(10)?` | Inferred destination country |
| `Region` | `string(50)?` | Inferred region |
| `EffectiveAt` | `DateTime?` | When delay/throttle expires |
| `DecisionMetadataJson` | `text?` | Extra context |
| `CreatedAt` | `DateTime` | Decision timestamp |

---

## Policy Types

### `quiet_hours`
```json
{
  "timezone": "America/New_York",
  "quietStart": "21:00",
  "quietEnd": "08:00",
  "daysOfWeek": ["Monday","Tuesday","Wednesday","Thursday","Friday","Saturday","Sunday"],
  "action": "delay",
  "nextAllowedWindow": true
}
```
Calculates `nextAllowedWindow` (next UTC boundary after quiet period) and delays until then.

### `geographic_restriction`
```json
{
  "allowedCountries": ["US","CA"],
  "blockedCountries": [],
  "action": "block"
}
```
Infers country from E.164 phone prefix. Blocks or allow-lists by ISO 3166-1 alpha-2.

### `rate_limit`
```json
{
  "windowMinutes": 60,
  "maxMessages": 500,
  "scope": "tenant",
  "action": "throttle"
}
```
Counts decisions in rolling window per tenant. Throttles when exceeded.

### `provider_governance`
```json
{
  "allowedProviders": ["twilio","vonage"],
  "blockedProviders": [],
  "action": "block"
}
```
Enforces provider allow/block lists at the platform or tenant level.

### `retry_governance`
```json
{
  "maxRetriesPerNotification": 3,
  "blockAfterDeadLetters": 2,
  "action": "review_required"
}
```
Caps total retry attempts per notification and blocks after repeated dead-letter events.

### `escalation_guardrail`
```json
{
  "maxEscalationsPerHour": 20,
  "maxEscalationsPerAlertTypePerHour": 5,
  "action": "throttle"
}
```
Rate-limits operational alert SMS escalations to prevent alert storms.

---

## Application Layer

### `ISmsGovernancePolicyService`

```csharp
Task<SmsGovernanceDecisionResult> EvaluatePreSendAsync(
    SmsGovernanceEvaluationRequest request, CancellationToken ct);

Task<SmsGovernanceDecisionResult> EvaluateRetryAsync(
    SmsGovernanceEvaluationRequest request, CancellationToken ct);

Task<SmsGovernanceDecisionResult> EvaluateEscalationAsync(
    SmsGovernanceEvaluationRequest request, CancellationToken ct);

Task<SmsGovernanceDecisionResult> CheckRateLimitAsync(
    Guid tenantId, Guid? policyId, CancellationToken ct);

// CRUD
Task<Guid>   CreatePolicyAsync(CreateGovernancePolicyRequest r, CancellationToken ct);
Task<bool>   UpdatePolicyAsync(UpdateGovernancePolicyRequest r, CancellationToken ct);
Task<bool>   DisablePolicyAsync(Guid policyId, string? requestedBy, CancellationToken ct);
Task<IReadOnlyList<SmsGovernancePolicy>> GetPoliciesAsync(GovernancePolicyQuery q, CancellationToken ct);
Task<GovernanceDecisionPage>             GetDecisionsAsync(GovernanceDecisionQuery q, CancellationToken ct);
Task<GovernanceSummaryDto>               GetSummaryAsync(int windowHours, CancellationToken ct);
Task<RateLimitStatusDto>                 GetRateLimitStatusAsync(int windowMinutes, CancellationToken ct);
Task<GeoStatusDto>                       GetGeoStatusAsync(int windowHours, CancellationToken ct);
```

### `SmsGovernanceOptions`

```
SmsGovernance:
  Enabled: true
  DefaultTimezone: "UTC"
  DecisionAuditEnabled: true
  FailOpenOnEvaluationError: true
  MaxPolicyEvaluationMs: 100
  RateLimitWindowMinutes: 60
  EmergencyOverrideEnabled: false
```

---

## Infrastructure Layer

### `SmsGovernancePolicyService` (668 lines)

Six evaluator methods — one per policy type — called from a single `EvaluateAsync` dispatch loop:

| Evaluator | Key Logic |
|---|---|
| `EvaluateQuietHoursAsync` | Timezone-aware current time vs quiet window; computes next-allowed UTC boundary |
| `EvaluateGeographicAsync` | E.164 → country code; allow/block list match |
| `EvaluateRateLimitAsync` | Counts `SmsGovernanceDecision` rows in DB window; rolling count vs policy cap |
| `EvaluateProviderGovernanceAsync` | Matches `ProviderType` against allow/block lists |
| `EvaluateRetryGovernanceAsync` | Compares `RetryCount` to `maxRetriesPerNotification` |
| `EvaluateEscalationGuardrailAsync` | Counts escalation decisions in rolling window |

All evaluators:
- Parse `PolicyJson` defensively (returns `allow` on malformed JSON)
- Persist a `SmsGovernanceDecision` row for every non-allow result
- Always return a typed `SmsGovernanceDecisionResult`

---

## Database Schema

### Tables

**`ntf_SmsGovernancePolicies`**
7 columns + 3 indexes:
- `IX_ntf_SmsGovPolicies_Tenant_Type_Enabled` (TenantId, PolicyType, Enabled)
- `IX_ntf_SmsGovPolicies_Type_Enabled_Priority` (PolicyType, Enabled, Priority)
- `IX_ntf_SmsGovPolicies_UpdatedAt` (UpdatedAt)

**`ntf_SmsGovernanceDecisions`**
13 columns + 4 indexes:
- `IX_ntf_SmsGovDecisions_Tenant_Dt` (TenantId, CreatedAt)
- `IX_ntf_SmsGovDecisions_DecisionType_Dt` (DecisionType, CreatedAt)
- `IX_ntf_SmsGovDecisions_PolicyType_Dt` (PolicyType, CreatedAt)
- `IX_ntf_SmsGovDecisions_NotifId` (NotificationId)

**Migration:** `20260512000002_AddSmsGovernance`

---

## API Layer — Admin Endpoints (9 endpoints)

All routes under `/v1/admin/sms/governance`, require `PlatformAdmin` claim:

| Method | Route | Description |
|---|---|---|
| `GET` | `/policies` | List with filters: policyType, tenantId, enabled, page/pageSize |
| `POST` | `/policies` | Create new policy |
| `PUT` | `/policies/{id}` | Update policy name/json/priority/enabled |
| `POST` | `/policies/{id}/disable` | Soft-disable policy |
| `GET` | `/decisions` | List decisions with filters: tenant, decisionType, policyType, reasonCode, date range |
| `GET` | `/summary` | Aggregated KPIs: counts by decision type + policy type + top reason codes |
| `GET` | `/rate-limits` | Active rate limit policies + recent throttling activity |
| `GET` | `/geo` | Active geo restriction policies + blocked-by-country breakdown |
| `POST` | `/evaluate-test` | Dry-run evaluation against a test request (returns decision without persisting) |

---

## Pipeline Integration

### `NotificationService.cs` — Pre-Send Block (line ~933)

```
After: SMS routing engine selection
Before: provider send loop (contactValue, routes, subject etc.)
```

- Calls `EvaluatePreSendAsync` with tenantId, notificationId, phone, provider, retryCount=0, isRetry=false
- Block → dead-letter + `CreateDeadLetterIssueAsync`
- Delay/Throttle → `retrying` + `NextRetryAt` set to policy window
- Error → logs warning, continues (fail-open)

### `NotificationService.cs` — Retry Block (line ~1453)

```
After: LS-016 SmsRetrySuppressionService
Before: ExecuteSendLoopAsync
```

- Calls `EvaluateRetryAsync` with tenantId, notificationId, phone, providerUsed, retryCount, isRetry=true
- Same block/delay/error handling as pre-send

Both blocks are SMS-only (`notification.Channel == "sms"`) and null-safe on `_governanceService`.

---

## Control Center UI

### `apps/control-center/src/app/notifications/sms-governance/page.tsx`
Server Component. `requirePlatformAdmin` guard. Reads `platform_session` cookie for Bearer auth. Calls all 5 governance endpoints via `Promise.allSettled` (graceful degradation per panel).

### `apps/control-center/src/components/sms-governance/governance-panel.tsx` (590 lines)
Client Component. Five tabs:

| Tab | Content |
|---|---|
| **Overview** | KPI row (total/active/blocks/delays), decision-type breakdown, top reason codes, by-policy-type grid |
| **Policies** | Sortable table + inline create form with per-type JSON templates |
| **Decision Log** | Filterable table (decisionType + policyType filters), shows notification ID, country, provider |
| **Rate Limits** | Active rate-limit policies + recent throttling activity banner |
| **Geographic** | Geo restriction policies + blocked-by-country bar chart |

### `apps/control-center/src/lib/sms-governance-api.ts` (193 lines)
Client-side API layer. TypeScript types for all entities + 8 async functions matching the 9 admin endpoints.

---

## Wiring Summary

| File | Change |
|---|---|
| `NotificationsDbContext.cs` | +`DbSet<SmsGovernancePolicy>`, +`DbSet<SmsGovernanceDecision>`, +EF configurations |
| `DependencyInjection.cs` | `AddScoped<ISmsGovernancePolicyService, SmsGovernancePolicyService>()` + `SmsGovernanceOptions` binding |
| `Program.cs` | `SmsGovernanceEndpoints.MapSmsGovernanceAdminEndpoints(app)` |
| `appsettings.json` | `SmsGovernance` section with all 7 options |
| `NotificationsDbContextModelSnapshot.cs` | +128 lines for both entity snapshots |

---

## Security Notes

- All 9 admin endpoints require `PlatformAdmin` claim — no tenant-level exposure.
- `RecipientPhoneTransient` field name makes clear the value is used only for country inference and is never persisted in decision rows.
- `FailOpenOnEvaluationError: true` ensures governance evaluation errors never block legitimate SMS delivery.
- Emergency override flag (`EmergencyOverrideEnabled`) defaults to `false` and requires explicit platform operator action.

---

## Files Created / Modified

| File | Status | Lines |
|---|---|---|
| `Notifications.Domain/SmsGovernancePolicy.cs` | New | 44 |
| `Notifications.Domain/SmsGovernanceDecision.cs` | New | 46 |
| `Notifications.Application/Interfaces/ISmsGovernancePolicyService.cs` | New | 97 |
| `Notifications.Application/Options/SmsGovernanceOptions.cs` | New | 31 |
| `Notifications.Infrastructure/Services/SmsGovernancePolicyService.cs` | New | 668 |
| `Notifications.Infrastructure/Data/EntityConfigurations/SmsGovernancePolicyConfiguration.cs` | New | ~60 |
| `Notifications.Infrastructure/Data/EntityConfigurations/SmsGovernanceDecisionConfiguration.cs` | New | ~55 |
| `Notifications.Infrastructure/Data/Migrations/20260512000002_AddSmsGovernance.cs` | New | ~120 |
| `Notifications.Infrastructure/Data/NotificationsDbContext.cs` | Modified | +6 lines |
| `Notifications.Infrastructure/DependencyInjection.cs` | Modified | +5 lines |
| `Notifications.Infrastructure/Services/NotificationService.cs` | Modified | +115 lines (2 blocks) |
| `Notifications.Api/Endpoints/SmsGovernanceEndpoints.cs` | New | 337 |
| `Notifications.Api/appsettings.json` | Modified | +9 lines |
| `Notifications.Api/Program.cs` | Modified | +1 line |
| `Notifications.Infrastructure/Data/Migrations/NotificationsDbContextModelSnapshot.cs` | Modified | +128 lines |
| `apps/control-center/src/lib/sms-governance-api.ts` | New | 193 |
| `apps/control-center/src/components/sms-governance/governance-panel.tsx` | New | 590 |
| `apps/control-center/src/app/notifications/sms-governance/page.tsx` | New | 60 |
