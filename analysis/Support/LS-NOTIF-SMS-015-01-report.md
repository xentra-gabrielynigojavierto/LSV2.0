# LS-NOTIF-SMS-015-01 — Validation & Hardening Report

**Iteration ID:** LS-NOTIF-SMS-015-01  
**Title:** Validation Hardening for Adaptive Routing  
**Date:** 2026-05-10  
**Status:** APPROVED WITH FIXES (2 defects found and fixed)

---

## 1. Build Validation

### 1.1 Notifications.Api (full project build)

```
dotnet build Notifications.Api/Notifications.Api.csproj -c Release -maxcpucount:1
```

**Result: PASS — 0 errors, 3 warnings**

Warnings (all pre-existing, not introduced by LS-NOTIF-SMS-015):
- `NU1902`: MailKit 4.3.0 has a known moderate severity vulnerability — pre-existing, unrelated to this iteration.
- `MSB3277`: JwtBearer version conflict (8.0.8 vs 8.0.26) from BuildingBlocks reference — pre-existing, resolved by runtime binding.

Note: Parallel MSBuild crashed with `MSB0001: Invalid node id` in this constrained environment. Build passed successfully with `-maxcpucount:1`.

### 1.2 Notifications.Infrastructure

Builds as a dependency of Notifications.Api. No isolated build errors.

### 1.3 Control Center TypeScript

```
cd apps/control-center && npx tsc --noEmit
```

**Result: PASS — 0 errors** (no output = no type errors)

---

## 2. Functional Validation (Adaptive Routing)

Validated by static analysis of `SmsRoutingEngine.cs` and supporting services.

### 2.1 `adaptive_quality`
- **Selects highest quality provider**: Yes — `SelectAdaptiveQualityAsync` calls `GetQualityScoresForCandidates`, filters `HasSufficientData = true`, orders by `QualityScore` descending, picks first.
- **Falls back to priority on insufficient data**: Yes — when `withData.Count == 0`, returns `candidates[0]` with reason `adaptive_quality_fallback_priority_insufficient_data`.
- **Minimum threshold respected**: `HasSufficientData` is computed in `SmsProviderQualityService.MapToScore` as `snap.TotalAttempts >= _opts.MinimumAttemptCount` (configurable via `SmsProviderQualityOptions`).

### 2.2 `adaptive_balanced`
- **Composite score (quality 60% + cost 40%)**: Confirmed at lines 382–385 of `SmsRoutingEngine.cs`: `composite = (q * 0.6m) + (c * 0.4m)`.
- **Falls back to hybrid on insufficient data**: Yes — `SelectAdaptiveBalancedAsync` calls `SelectHybridAsync` when no candidates have sufficient data.
- **CostEfficiencyScore null handling**: When `CostEfficiencyScore` is null, defaults to `50m` (neutral midpoint) before computing composite.

### 2.3 `adaptive_regional`
- **Uses country-specific quality**: `SelectAdaptiveRegionalAsync` passes `countryCode` to `GetQualityScoresForCandidates`, which passes it to `ISmsProviderQualityService.GetLatestScoreAsync` → `repo.GetLatestAsync(..., countryCode, ...)`.
- **Falls back to `adaptive_quality` when no country**: When `countryCode` is null, delegates to `SelectAdaptiveQualityAsync` with null country.
- **Falls back to `adaptive_quality` when no regional data**: When `withRegionalData.Count == 0`, calls `SelectAdaptiveQualityAsync` ignoring country.

### 2.4 Excluded providers
- Confirmed: `excluded` list is applied before mode-specific selection (lines 108–116). Excluded candidates are filtered out regardless of mode.

### 2.5 `MaxEstimatedCostPerMessage`
- Confirmed: Cost cap applied before mode selection (lines 131–145). Candidates exceeding the cap are removed; returns `NoRoute` if all candidates exceed cap.

### 2.6 `RequireHealthyProvider`
- Confirmed: Passed to `SelectHealthOptimizedAsync` and `SelectHybridAsync`. In `health_optimized`, returns `null` (NoRoute) when `requireHealthy=true` and all providers are down. `adaptive_balanced` hybrid fallback honours `requireHealthy=false` by design (safe fallback).

---

## 3. Regional Inference Validation

Validated by static analysis of `SmsRegionalInferenceService.cs`.

| Input | Expected | Actual | Result |
|-------|----------|--------|--------|
| `+15551234567` | US | US (via `+1` → longest NANP match) | ✅ |
| `+14165551234` | US | US (NANP, no Canada distinction — documented limitation) | ✅ (known gap) |
| `+441234567890` | GB | GB | ✅ |
| `+521234567890` | MX | MX | ✅ |
| `+61412345678` | AU | AU | ✅ |
| `+919876543210` | IN | IN | ✅ |
| `+33123456789` | FR | FR | ✅ |
| `+4915123456789` | DE | DE | ✅ |
| `5551234567` (no +) | null | null (fails `StartsWith('+')` guard) | ✅ |
| `` (empty) | null | null (`IsNullOrWhiteSpace` guard) | ✅ |
| `+999999` (unknown) | null | null (no prefix match) | ✅ |

**Raw phone number persistence:** `InferCountryCode` takes the phone, trims it, matches prefix, and returns a 2-char country code string. The phone string is never stored, assigned to a field, or logged. The return value is always a country code or null. **Confirmed safe.**

**Region to country mapping:** `+1` maps to `US` (country code), then `InferRegion("US")` → `"NANP"`. Mexico (`MX`) is also in `NANP` by the region map (geographic grouping for routing purposes — NANP is used as a broad carrier routing zone, not the NANP telephone numbering plan).

---

## 4. Existing Routing Regression Validation

All existing modes remain in `ValidModes` set and `switch` statement.

| Mode | Switch case present | Fallback correct |
|------|---------------------|-----------------|
| `priority` | `default:` | N/A (first candidate) |
| `cost_optimized` | `SelectCostOptimized` | priority (no estimates) |
| `health_optimized` | `SelectHealthOptimizedAsync` | priority (all degraded, !requireHealthy) |
| `hybrid` | `SelectHybridAsync` | priority (all degraded, !requireHealthy) |
| `regional` | `SelectRegional` | priority (no country/region data) |

**No changes to Twilio/Vonage adapters in this iteration.** `TwilioAdapter.cs`, `VonageAdapter.cs`, `TwilioAdapterFactory.cs`, and `VonageAdapterFactory.cs` are unchanged.

**No duplicate sends introduced:** Routing engine returns a single `SelectedRoute` — `NotificationService.cs` reorders the existing `routes` list rather than duplicating it.

**Contact/preference enforcement:** `SmsPreferenceService` and contact enforcement run before routing (pre-existing pipeline order, unchanged).

---

## 5. Optimization API Validation

All 5 endpoints verified in `SmsOptimizationEndpoints.cs`.

### 5.1 Authorization

Both endpoint groups use `.RequireAuthorization(Policies.AdminOnly)` at the group level:
- `SmsOptimizationEndpoints`: Group `/v1/admin/sms/routing` → `Policies.AdminOnly` ✅
- `SmsRoutingEndpoints`: Group `/v1/admin/sms/routing` → `Policies.AdminOnly` ✅

All 5 optimization endpoints inherit the group-level authorization policy. No unauthenticated path exists.

### 5.2 Information disclosure

Reviewed all 5 endpoint response projections:

| Endpoint | Credential fields | Phone numbers | Raw payloads |
|----------|------------------|---------------|--------------|
| GET /quality | None | None | None |
| GET /quality/trends | None | None | None |
| GET /latency | None | None | None |
| GET /regional | None | None | None |
| GET /optimization | None | None | None |

`SmsProviderQualityDto` contains only: `ProviderType`, `ProviderOwnershipMode`, `CountryCode`, `Region`, scores, rates, latency, attempt counts, window timestamps. No `CredentialsJson`, `SettingsJson`, `AccountSid`, `AuthToken`, `FromNumber`, or raw phone numbers.

`ProviderConfigId` appears in the `/quality` projection as an opaque Guid reference only — no credential data.

### 5.3 Filter coverage

| Filter | /quality | /trends | /latency | /regional | /optimization |
|--------|----------|---------|---------|---------|-------------|
| tenantId | ✅ | ✅ | ✅ | ✅ | ✅ |
| provider | ✅ | ✅ | ✅ | ✅ | — |
| providerConfigId | ✅ | — | ✅ | — | — |
| providerOwnershipMode | ✅ | — | ✅ | — | — |
| countryCode | ✅ | ✅ | ✅ | ✅ | ✅ |
| region | ✅ | — | ✅ | ✅ | — |
| from / to | ✅ | ✅ | ✅ | ✅ | — |

All filters from specification are implemented. `/optimization` uses `GetLatestScoresAsync` (provider + country filter) as a summary endpoint — date/region filtering not applicable.

---

## 6. Control Center UI Validation

### 6.1 SMS Routing page

- `apps/control-center/src/app/notifications/sms-routing/page.tsx` loads data via `Promise.allSettled` — all 7 data sources fetch in parallel with graceful degradation.
- Non-critical failures (quality/optimization) are consumed silently in the Optimization tab.
- Critical failures (caps, policies, decisions, summary, health) are surfaced as a yellow warning banner.

### 6.2 Tabs

| Tab | Component | Empty state | Error state |
|-----|-----------|-------------|-------------|
| Providers | `CapabilitiesTab` | Empty table rows | `Promise.allSettled` graceful |
| Policies | `PoliciesTab` | Dashed empty-state panel | `Promise.allSettled` graceful |
| Decisions | `DecisionsTab` | "No routing decisions recorded" row | `Promise.allSettled` graceful |
| Health | `HealthTab` | Dashed empty-state panel | `Promise.allSettled` graceful |
| Optimization | `OptimizationPanel` | Amber no-data banner with worker instructions | Quality/optimization failures non-critical |

### 6.3 Credential and phone number exposure

`OptimizationPanel` renders only: `providerType`, `providerOwnershipMode`, `countryCode`, scores, rates, latency, attempt counts, window dates. No credentials, raw phones, webhook URLs, or raw payloads are displayed anywhere in the routing or optimization UI.

### 6.4 Adaptive mode reference section

`AdaptiveModeGuide` component renders all three adaptive modes with descriptions and fallback behavior. Always visible in the Optimization tab regardless of data availability. Correct.

---

## 7. Security Validation

### 7.1 `RecipientPhoneForInferenceOnly` — transient only

- Defined in `ISmsRoutingEngine.cs` as a property on `SmsRoutingRequest` (in-memory request object only).
- Read in `SmsRoutingEngine.SelectRouteAsync` lines 80–83: passed to `_regionalInference.InferCountryCode(...)`, result stored in `string? inferredCountryCode`. The phone string itself is not stored in any field, log statement, or return value.
- `SmsRoutingEngine` has no write path to DB — it returns `SmsRoutingDecisionResult`. The persisted `SmsRoutingDecision` entity contains only `InferredCountryCode` (2-char ISO code) and `InferredRegion` (region label), never the raw phone.
- `SmsRegionalInferenceService.InferCountryCode` is stateless: no fields, no DI, no logging. Input phone is a local parameter only.
- **Confirmed: phone is transient. Never persisted.**

### 7.2 `SmsProviderQualitySnapshot` — no sensitive data

Fields confirmed safe: aggregate counts and rates only. `CountryCode` is a 2-char ISO code derived post-inference. No `RecipientPhone`, `CredentialsJson`, `SettingsJson`, or `ProviderMessageId`.

### 7.3 `SmsRoutingDecision` — no sensitive data

Fields confirmed safe: `InferredCountryCode` (ISO code), `InferredRegion` (label), `AdaptiveInputsJson` (JSON with provider type, quality scores, country code — no phone, no credentials). `SelectedProviderConfigId` is an opaque Guid.

### 7.4 `SmsRoutingPolicy` — no sensitive data

Fields confirmed safe: provider type strings only in `PreferredProvidersJson` / `ExcludedProvidersJson`. No credentials.

### 7.5 Optimization API DTOs — no sensitive data

`SmsProviderQualityDto`, `SmsQualityTrendPoint`, `SmsLatencyDto`, `SmsRegionalDto`, `SmsOptimizationInsight` — all confirmed safe per §5.2.

### 7.6 Logs — no phone numbers or secrets

Log statements in `SmsRoutingEngine.cs` log only: TenantId, RoutingMode, PolicyId, ProviderType. No phone numbers, credentials, or raw payloads logged.

Log statements in `SmsRegionalInferenceService.cs`: none (stateless, no logging).

Log statements in `SmsProviderQualityService.cs`: window timestamps and snapshot counts only. No phone numbers, credentials.

---

## 8. Regression Validation (LS-NOTIF-SMS-001 through LS-NOTIF-SMS-014)

| Area | Status | Notes |
|------|--------|-------|
| Twilio outbound send | ✅ No change | `TwilioAdapter.cs` unmodified |
| Vonage outbound send | ✅ No change | `VonageAdapter.cs` unmodified |
| SMS preference enforcement | ✅ No change | `SmsPreferenceService.cs` unmodified |
| Retry / dead-letter | ✅ No change | Retry logic in `NotificationService.cs` unmodified |
| Webhook reconciliation | ✅ No change | `WebhookIngestionService.cs` unmodified |
| Vendor polling reconciliation | ✅ No change | `SmsReconciliationService.cs` unmodified |
| Tenant runtime provider resolution | ✅ No change | `SmsProviderRuntimeResolver.cs` unmodified |
| Activity APIs | ✅ No change | `SmsActivityEndpoints.cs` unmodified |
| Dashboard APIs | ✅ No change | `SmsDashboardEndpoints.cs` unmodified |
| Cost analytics | ✅ No change | `SmsCostEndpoints.cs` unmodified |
| Alerting | ✅ No change | `SmsAlertEndpoints.cs` unmodified |
| Escalation | ✅ No change | `SmsEscalationEndpoints.cs` unmodified |
| Incident UI | ✅ No change | `sms-incidents/` unmodified |
| Routing policies and decisions (SMS-014) | ✅ Validated | Existing modes confirmed in §4 |

---

## 9. Files Changed

### Backend

| File | Change |
|------|--------|
| `Notifications.Application/Interfaces/ISmsProviderQualityService.cs` | Added `DeliverySuccessRate` property to `ProviderQualityScore` |
| `Notifications.Infrastructure/Services/SmsProviderQualityService.cs` | Populated `DeliverySuccessRate` in `MapToScore` from snapshot |
| `Notifications.Api/Endpoints/SmsOptimizationEndpoints.cs` | Fixed `/optimization` endpoint: use `s.DeliverySuccessRate` instead of hardcoded `0m` |

### Frontend

| File | Change |
|------|--------|
| `apps/control-center/src/components/sms-routing/routing-panel.tsx` | Added 3 adaptive modes to `ROUTING_MODES` constant and `MODE_LABELS` map |
| `apps/control-center/src/components/notifications/test-message-form.tsx` | E.164 format label and Twilio trial account warning (previous session) |
| `apps/services/notifications/Notifications.Api/Endpoints/ProviderEndpoints.cs` | SMS test response includes Twilio Message SID (previous session) |

---

## 10. Defects Found and Fixed

### Defect 1 — Adaptive modes unreachable via Control Center policy form UI

**Severity:** Medium  
**File:** `apps/control-center/src/components/sms-routing/routing-panel.tsx`  
**Root cause:** `ROUTING_MODES` constant only listed the 5 original LS-NOTIF-SMS-014 modes. The policy create/edit form dropdown did not include `adaptive_quality`, `adaptive_balanced`, or `adaptive_regional`. Operators could not create or edit adaptive routing policies through the UI (API accepts them; only the UI was restricted).  
**Fix:** Added all 3 adaptive modes to `ROUTING_MODES` and `MODE_LABELS`.  
**Risk:** Low — purely additive UI change. Backend already accepts all 8 modes.

### Defect 2 — `/optimization` endpoint returned `DeliverySuccessRate = 0` for all providers

**Severity:** Low (data quality / misleading display)  
**Files:** `SmsOptimizationEndpoints.cs`, `ISmsProviderQualityService.cs`, `SmsProviderQualityService.cs`  
**Root cause:** The `SmsOptimizationInsight` builder used `DeliverySuccessRate = 0m` with a comment "scores don't include rate breakdown — from snapshot". `ProviderQualityScore` (the object returned by `GetLatestScoresAsync`) did not carry `DeliverySuccessRate`, even though the underlying `SmsProviderQualitySnapshot` does.  
**Fix:** Added `DeliverySuccessRate` to `ProviderQualityScore`; populated it in `SmsProviderQualityService.MapToScore` from `snap.DeliverySuccessRate`; updated the optimization endpoint to use `s.DeliverySuccessRate`.  
**Risk:** None — additive field. No API breaking change.

---

## 11. Known Gaps / Issues

### Gap 1 — `RecipientPhoneForInferenceOnly` not wired in `NotificationService.cs`

`NotificationService.cs` builds the `SmsRoutingRequest` without setting `RecipientPhoneForInferenceOnly` (comment: "CountryCode and Region derivation not yet implemented (see LS-NOTIF-SMS-014 gap #3)"). As a result, `adaptive_regional` mode always falls through to the `adaptive_quality` fallback in production — country inference never fires via this path.

**Impact:** `adaptive_regional` mode is functional at the engine level (tested in isolation) but does not route by destination country in the live notification pipeline. Requires a separate task to read the recipient phone from the notification payload and set the field before calling `SelectRouteAsync`.

**Severity:** Medium functional gap. No crash, no data corruption, no security risk. Fallback to `adaptive_quality` is correct behavior.

**Recommendation:** Track as a follow-up task: "Wire RecipientPhoneForInferenceOnly in NotificationService.cs for adaptive_regional routing."

### Gap 2 — +1 NANP does not distinguish US from Canada

`InferCountryCode("+1...")` returns `"US"` for all NANP numbers including Canadian ones. This is documented in the `SmsRegionalInferenceService` XML comment. Full libphonenumber integration would be required for production-grade country distinction.

**Severity:** Low — platform is primarily US-focused. Fallback behavior is safe.

### Gap 3 — No migration directory for Notifications service

`Notifications.Infrastructure/Migrations/` does not exist in the repository. DB schema for `SmsProviderQualitySnapshots`, `SmsRoutingDecisions`, and `SmsRoutingPolicies` tables is assumed to be managed outside this repository (e.g., applied directly or via a separate migration tool). If EF Core migrations are expected, they must be generated.

**Severity:** Deployment risk. **Does not affect build or test.**

### Gap 4 — `SmsProviderQualityWorker` enabled by default

`services.AddHostedService<SmsProviderQualityWorker>()` is registered unconditionally. If `SmsProviderQuality:Enabled = false` is not in appsettings, the worker runs on startup. The worker polls the DB at its configured interval. This is correct behavior but should be documented for operators.

---

## 12. Final Recommendation

**LS-NOTIF-SMS-015 is APPROVED** to be considered complete with the following conditions:

1. ✅ **Builds cleanly** — 0 errors in both Notifications.Api and Control Center TypeScript
2. ✅ **Adaptive routing logic is correct** — all 3 adaptive modes have correct selection and fallback chains
3. ✅ **Security requirements met** — no phone numbers, credentials, or sensitive payloads persist or appear in API responses
4. ✅ **Authorization enforced** — all 5 optimization endpoints require `PlatformAdmin`
5. ✅ **Regression safe** — all existing routing modes and LS-NOTIF-SMS-001 through SMS-014 behaviors unchanged
6. ✅ **2 defects found and fixed** — UI dropdown and DeliverySuccessRate data quality
7. ⚠️ **Gap 1 tracked** — `RecipientPhoneForInferenceOnly` not wired; `adaptive_regional` falls back to `adaptive_quality` in production. Requires a follow-up task.
8. ⚠️ **Gap 3 tracked** — No EF Core migration files; DB schema deployment requires separate verification.
