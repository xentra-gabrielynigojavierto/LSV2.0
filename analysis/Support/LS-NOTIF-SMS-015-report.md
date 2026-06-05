# LS-NOTIF-SMS-015 — Regional Intelligence, Provider Quality Scoring, and Adaptive Routing Optimization

**Status:** COMPLETE  
**Last updated:** 2026-05-09

---

## 1. Objective

Enhance the LS-NOTIF-SMS-014 multi-provider SMS routing platform with:
- Provider quality snapshots calculated from local operational telemetry
- Regional/country inference from E.164 phone prefixes
- Three adaptive routing modes: `adaptive_quality`, `adaptive_balanced`, `adaptive_regional`
- Four optimization analytics admin APIs
- Control Center optimization visibility UI

All intelligence remains inside Notification Service. Control Center is a read-only consumer.

---

## 2. Existing Codebase Analysis

### 2.1 SMS Routing Engine (LS-NOTIF-SMS-014)
- **Interface:** `Notifications.Application/Interfaces/ISmsRoutingEngine.cs`
- **Implementation:** `Notifications.Infrastructure/Services/SmsRoutingEngine.cs`
- **Existing modes:** `priority`, `cost_optimized`, `health_optimized`, `hybrid`, `regional`
- `SmsRoutingRequest` carries `TenantId`, `NotificationId`, `CandidateRoutes`, `CountryCode?`, `Region?`
- `SmsRoutingDecisionResult` carries full selection metadata; `NoRoute()` factory for failure path
- Engine is `Scoped` — can safely inject scoped services

### 2.2 SmsRoutingPolicy
- `Notifications.Domain/SmsRoutingPolicy.cs`
- Fields: `RoutingMode`, `ExcludedProvidersJson`, `PreferredProvidersJson`, `MaxEstimatedCostPerMessage`, `RequireHealthyProvider`, `FallbackToPlatform`, `Priority`, `TenantId`
- Mode validation in `SmsRoutingEndpoints.cs` against `ValidRoutingModes` set — must be extended for new modes

### 2.3 SmsRoutingDecision
- `Notifications.Domain/SmsRoutingDecision.cs`
- Already has: `CountryCode?`, `Region?`, `HealthSnapshotJson?`
- New fields needed: `InferredCountryCode?`, `InferredRegion?`, `ProviderQualityScore?`, `AdaptiveScore?`, `AdaptiveInputsJson?`
- EF config: `Notifications.Infrastructure/Data/Configurations/SmsRoutingDecisionConfiguration.cs`
- Table: `ntf_SmsRoutingDecisions`

### 2.4 NotificationAttempt
- Status, CompletedAt, CreatedAt (latency = diff)
- EstimatedCostAmount, ActualCostAmount, CostCurrency, CostSource
- LastReconciliationOutcome, LastReconciledAt, ReconciliationAttemptCount
- AttemptNumber, IsFailover, Provider, ProviderConfigId, ProviderOwnershipMode, TenantId

### 2.5 Existing Workers
- All in `Notifications.Infrastructure/Workers/`, extend `BackgroundService`
- Use `IServiceScopeFactory` for scoped DB access
- Registered via `services.AddHostedService<T>()` in `DependencyInjection.cs`
- Config-driven enable/disable via `IConfiguration`

### 2.6 SmsCostAnalyticsOptions
- `Notifications.Application/Options/SmsCostAnalyticsOptions.cs` — existing options pattern to follow

### 2.7 Control Center SMS Routing Page
- `apps/control-center/src/app/notifications/sms-routing/page.tsx` — Server Component
- `apps/control-center/src/lib/sms-routing-api.ts` — API client
- `apps/control-center/src/components/sms-routing/routing-panel.tsx` — 4-tab panel
- Add 5th "Optimization" tab

---

## 3. Architecture Decisions

### Quality Score Formula
```
QualityScore = 100
    - (FailureRate    × FailurePenaltyWeight    × 100)
    - (RetryRate      × RetryPenaltyWeight      × 100)
    - (ReconFailRate  × ReconciliationPenaltyWeight × 100)
    + (DeliverySuccessRate × DeliverySuccessWeight × 100)
    - (HealthPenalty  × HealthPenaltyWeight     × 100)
```
Bounded: `Max(0, Min(100, score))`.  
Returns `InsufficientDataScore` (default 50) when `TotalAttempts < MinimumAttemptCount`.  
This is operational telemetry for optimization — not a contractual SLA.

### Regional Inference
Approximate E.164 prefix mapping (first-match longest prefix):
- +1 → US (NANP — may also include CA; limitation documented)
- +44 → GB, +52 → MX, +61 → AU, +91 → IN, +33 → FR, +49 → DE
Raw phone number is NEVER persisted. Only `InferredCountryCode` is stored in routing decision.

### Adaptive Routing Fallback Chain
- `adaptive_quality` → priority (if insufficient data)
- `adaptive_balanced` → hybrid → priority (if insufficient data)
- `adaptive_regional` → adaptive_quality → priority (if no regional/quality data)

### RecipientPhone in Routing Request
`SmsRoutingRequest.RecipientPhoneForInferenceOnly` — transient, never persisted, never logged.
Used only to infer country code inside the routing engine before route selection.

---

## 4. Files Added

| File | Purpose |
|---|---|
| `Notifications.Domain/SmsProviderQualitySnapshot.cs` | Quality snapshot entity |
| `Notifications.Application/Options/SmsProviderQualityOptions.cs` | Quality scoring config |
| `Notifications.Application/Interfaces/ISmsRegionalInferenceService.cs` | Regional inference interface |
| `Notifications.Application/Interfaces/ISmsProviderQualityRepository.cs` | Repository interface |
| `Notifications.Application/Interfaces/ISmsProviderQualityService.cs` | Quality service interface |
| `Notifications.Application/DTOs/SmsQualityDtos.cs` | API response DTOs |
| `Notifications.Infrastructure/Data/Configurations/SmsProviderQualitySnapshotConfiguration.cs` | EF config |
| `Notifications.Infrastructure/Services/SmsRegionalInferenceService.cs` | E.164 prefix inference |
| `Notifications.Infrastructure/Services/SmsProviderQualityService.cs` | Quality calculation |
| `Notifications.Infrastructure/Repositories/SmsProviderQualityRepository.cs` | Quality repo |
| `Notifications.Infrastructure/Workers/SmsProviderQualityWorker.cs` | Background worker |
| `Notifications.Api/Endpoints/SmsOptimizationEndpoints.cs` | 4 admin optimization APIs |
| `Notifications.Infrastructure/Data/Migrations/20260511000003_AddSmsQualityAndAdaptiveRouting.cs` | Schema migration |
| `apps/control-center/src/components/sms-routing/optimization-panel.tsx` | CC optimization tab |

---

## 5. Files Modified

| File | Change |
|---|---|
| `Notifications.Domain/SmsRoutingDecision.cs` | Add InferredCountryCode, InferredRegion, ProviderQualityScore, AdaptiveScore, AdaptiveInputsJson |
| `Notifications.Application/Interfaces/ISmsRoutingEngine.cs` | Add RecipientPhoneForInferenceOnly to SmsRoutingRequest; add adaptive metadata to SmsRoutingDecisionResult |
| `Notifications.Infrastructure/Data/Configurations/SmsRoutingDecisionConfiguration.cs` | Map new columns |
| `Notifications.Infrastructure/Services/SmsRoutingEngine.cs` | Add 3 adaptive routing modes; inject ISmsRegionalInferenceService + ISmsProviderQualityService |
| `Notifications.Infrastructure/Data/NotificationsDbContext.cs` | Add SmsProviderQualitySnapshots DbSet |
| `Notifications.Infrastructure/DependencyInjection.cs` | Register new services + worker |
| `Notifications.Api/Program.cs` | Map SmsOptimizationEndpoints |
| `Notifications.Api/Endpoints/SmsRoutingEndpoints.cs` | Extend ValidRoutingModes |
| `Notifications.Infrastructure/Data/Migrations/NotificationsDbContextModelSnapshot.cs` | Add quality snapshot table |
| `apps/control-center/src/lib/sms-routing-api.ts` | Add 4 optimization API client functions |
| `apps/control-center/src/app/notifications/sms-routing/page.tsx` | Fetch optimization data, pass to panel |
| `apps/control-center/src/components/sms-routing/routing-panel.tsx` | Add Optimization tab |

---

## 6. Database / Schema Changes

### New table: `ntf_SmsProviderQualitySnapshots`
| Column | Type | Notes |
|---|---|---|
| Id | char(36) PK | |
| ProviderType | varchar(100) | e.g. "twilio", "vonage" |
| ProviderConfigId | char(36) nullable | opaque Guid |
| ProviderOwnershipMode | varchar(30) nullable | |
| TenantId | char(36) nullable | null = platform-level |
| Region | varchar(50) nullable | |
| CountryCode | varchar(10) nullable | |
| WindowStart | datetime(6) | |
| WindowEnd | datetime(6) | |
| TotalAttempts | int | |
| DeliveredAttempts | int | |
| FailedAttempts | int | |
| RetryAttempts | int | |
| DeadLetterAttempts | int | |
| ReconciledAttempts | int | |
| ReconciliationFailures | int | |
| AverageLatencyMs | decimal(18,4) nullable | |
| DeliverySuccessRate | decimal(5,4) | 0-1 |
| FailureRate | decimal(5,4) | 0-1 |
| RetryRate | decimal(5,4) | 0-1 |
| DeadLetterRate | decimal(5,4) | 0-1 |
| ReconciliationFailureRate | decimal(5,4) | 0-1 |
| AverageEffectiveCost | decimal(18,8) nullable | |
| CostPerDeliveredMessage | decimal(18,8) nullable | |
| QualityScore | decimal(5,2) | 0-100 |
| CostEfficiencyScore | decimal(5,2) nullable | 0-100 |
| HealthPenalty | decimal(5,4) | 0-1 |
| CalculatedAt | datetime(6) | UTC |

Indexes:
- `(ProviderType, CalculatedAt)`
- `(TenantId, ProviderType, CalculatedAt)`
- `(CountryCode, ProviderType, CalculatedAt)`
- `(ProviderConfigId, CalculatedAt)`

### Modified table: `ntf_SmsRoutingDecisions`
New nullable columns:
- `InferredCountryCode varchar(10)`
- `InferredRegion varchar(50)`
- `ProviderQualityScore decimal(5,2)`
- `AdaptiveScore decimal(5,2)`
- `AdaptiveInputsJson text`

---

## 7. API Changes

### New endpoints (all PlatformAdmin, read-only)
| Method | Path | Description |
|---|---|---|
| GET | `/v1/admin/sms/routing/quality` | Latest quality snapshots per provider |
| GET | `/v1/admin/sms/routing/quality/trends` | Quality score trends over time |
| GET | `/v1/admin/sms/routing/latency` | Latency analytics per provider |
| GET | `/v1/admin/sms/routing/regional` | Regional delivery performance |
| GET | `/v1/admin/sms/routing/optimization` | Optimization insight summary |

Common filters: `tenantId`, `provider`, `providerConfigId`, `providerOwnershipMode`, `countryCode`, `region`, `from`, `to`

---

## 8. Quality Score Calculation

### Delivery Success Rate
`DeliveredAttempts / TotalAttempts` — where status is "sent", "delivered", or provider confirms delivery.

### Failure Rate
`FailedAttempts / TotalAttempts` — "failed" + "dead_letter" statuses.

### Retry Rate
`RetryAttempts / TotalAttempts` — attempts with `AttemptNumber > 1` OR `IsFailover = true`.

### Reconciliation Failure Rate
`ReconciliationFailures / max(ReconciledAttempts, 1)`.

### Average Latency
`AVG(CompletedAt - CreatedAt)` in ms, where both are not null.

### Quality Score
```
QualityScore = clamp(
    (DeliverySuccessRate × 0.45 × 100)
    - (FailureRate       × 0.25 × 100)
    - (RetryRate         × 0.10 × 100)
    - (ReconFailRate     × 0.10 × 100)
    - (HealthPenalty     × 0.10 × 100)
  , 0, 100)
```
Weights from `SmsProviderQualityOptions`. `HealthPenalty` = 0 when healthy, 0.5 when degraded, 1.0 when down.

---

## 9. Adaptive Routing Mode Behavior

### adaptive_quality
1. Get latest quality snapshot for each candidate
2. Select candidate with highest QualityScore
3. Fallback: priority (if no snapshots or all below MinimumAttemptCount)

### adaptive_balanced
1. Get quality snapshot + estimated cost for each candidate
2. Composite score = (QualityScore/100 × 0.6) + (CostEfficiencyScore/100 × 0.4)
3. Select highest composite score
4. Fallback: hybrid → priority

### adaptive_regional
1. Infer country from RecipientPhoneForInferenceOnly
2. Get regional quality snapshot (ProviderType + CountryCode)
3. Select highest QualityScore for that country
4. Fallback: adaptive_quality → priority

---

## 10. Validation Results

### Build validation
- [ ] dotnet build Notifications.Infrastructure passes

### Functional checks
- [ ] Quality snapshot created from SMS attempts
- [ ] Zero attempts handled safely (returns InsufficientDataScore)
- [ ] Below MinimumAttemptCount returns default score
- [ ] Regional inference: +1 → US, +44 → GB, unknown → null
- [ ] adaptive_quality selects highest score
- [ ] adaptive_regional falls back when country unknown
- [ ] excluded providers not selected
- [ ] MaxEstimatedCostPerMessage respected
- [ ] Existing modes (priority, cost_optimized, health_optimized, hybrid, regional) unchanged
- [ ] No phone numbers stored in quality tables or decisions
- [ ] Admin authorization enforced on optimization endpoints
- [ ] Control Center optimization tab renders

---

## 11. Known Limitations

- E.164 prefix mapping is approximate (+1 covers NANP — US and Canada not distinguished)
- Quality scores are operational telemetry, not contractual SLA
- Adaptive routing uses historical telemetry only — no real-time provider calls during selection
- CostEfficiencyScore requires ActualCostAmount or EstimatedCostAmount — may be null for some providers
- Quality worker is disabled by default (`SmsProviderQuality:Enabled = false`)

---

## 12. Recommended Next Steps

- Add +1 sub-prefix NPA lookup for US/CA distinction
- Add provider regional config metadata for true geographic routing
- Build quality score trend visualization in Control Center
- Add quality-based alerting thresholds to existing alert worker
