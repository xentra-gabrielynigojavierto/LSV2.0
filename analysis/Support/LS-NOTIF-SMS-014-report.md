# LS-NOTIF-SMS-014 — Multi-Provider SMS Expansion and Intelligent Routing

**Status:** COMPLETE  
**Last updated:** 2026-05-09

---

## 1. Initial Codebase Analysis

LS-NOTIF-SMS-014 expands the Notification Service beyond Twilio into a resilient multi-provider SMS platform with intelligent provider selection, dynamic failover, cost-aware routing, health-aware routing, and routing decision persistence. This report documents findings, decisions, and implementation details.

---

## 2. Existing SMS Provider Abstraction Findings

**File:** `Notifications.Application/Interfaces/IEmailProviderAdapter.cs`

Interfaces found:

```csharp
public interface ISmsProviderAdapter
{
    string ProviderType { get; }
    Task<bool> ValidateConfigAsync();
    Task<SmsSendResult> SendAsync(SmsSendPayload payload);
    Task<ProviderHealthResult> HealthCheckAsync();
}
```

`SmsSendPayload` — `To`, `From?`, `Body`  
`SmsSendResult` — `Success`, `ProviderMessageId?`, `Failure?`  
`ProviderFailure` — `Category`, `ProviderCode?`, `Message`, `Retryable`  
`ProviderHealthResult` — `Status`, `LatencyMs?`

`ISmsProviderStatusLookup` is a separate opt-in interface in `ISmsReconciliationService.cs` — providers only implement it if they support active status polling (TwilioAdapter does; new adapters may not).

---

## 3. Existing Twilio Adapter/Runtime Resolver Findings

**TwilioAdapter** (`Infrastructure/Providers/Adapters/TwilioAdapter.cs`):
- Implements `ISmsProviderAdapter` + `ISmsProviderStatusLookup`
- Direct HTTP via `HttpClient` — no SDK dependency
- `ProviderType` = `"twilio"`
- Send: `POST https://api.twilio.com/2010-04-01/Accounts/{sid}/Messages.json`
- Health: `GET https://api.twilio.com/2010-04-01/Accounts/{sid}.json`
- Status lookup: `GET https://api.twilio.com/2010-04-01/Accounts/{sid}/Messages/{messageSid}.json`
- Error categories: `auth_config_failure`, `invalid_recipient`, `retryable_provider_failure`, `provider_unavailable`, `non_retryable_failure`

**ITwilioAdapterFactory / TwilioAdapterFactory**:
- Parses `TenantProviderConfig.CredentialsJson` → `accountSid`, `authToken`
- Parses `TenantProviderConfig.SettingsJson` → `fromNumber`
- Returns `ISmsProviderAdapter` (actually `TwilioAdapter`)

**SmsProviderRuntimeResolver** (`Infrastructure/Services/SmsProviderRuntimeResolver.cs`):
- Send-time: routes with `TenantProviderConfigId` → calls `BuildAdapter(providerType, config)`
- Build-time switch: `"twilio" => _twilioFactory.CreateFromConfig(config)` — only Twilio supported
- No config ID → platform adapter
- Reconciliation-time: same logic, loads config from DB

**Key integration point:** `BuildAdapter()` switch statement in `SmsProviderRuntimeResolver` must be generalized to a factory registry for multi-provider support.

---

## 4. Existing ProviderRoutingService Findings

**File:** `Infrastructure/Services/ProviderRoutingService.cs`

```csharp
private static readonly Dictionary<string, string[]> PlatformProviderPriority = new()
{
    ["email"] = new[] { "sendgrid", "smtp" },
    ["sms"] = new[] { "twilio" }
};
```

Platform SMS: Twilio only. Routes are returned as ordered `List<ProviderRoute>`.

`ProviderRoute` fields: `ProviderType`, `OwnershipMode` (`platform`/`tenant`), `TenantProviderConfigId?`, `IsFailover`, `IsPlatformFallback`

The routing engine (LS-NOTIF-SMS-014) receives these candidate routes and reorders/selects the best one before the send loop begins.

---

## 5. Existing TenantProviderConfig Model Findings

**File:** `Notifications.Domain/TenantProviderConfig.cs`

Fields: `Id`, `TenantId`, `Channel`, `ProviderType`, `DisplayName`, `CredentialsJson`, `SettingsJson`, `Status`, `ValidationStatus`, `ValidationMessage`, `LastValidatedAt`, `HealthStatus`, `LastHealthCheckAt`, `HealthCheckLatencyMs`, `Priority`, `CreatedAt`, `UpdatedAt`

New providers (Vonage, Telnyx, etc.) use the same entity — `ProviderType` determines the factory.

---

## 6. Existing Reconciliation/Status Lookup Findings

**File:** `Infrastructure/Services/SmsReconciliationService.cs`

The reconciliation service already handles unsupported providers gracefully:

```csharp
// Guard: resolved adapter must support status lookup.
if (runtimeCtx.Adapter is not ISmsProviderStatusLookup statusLookup)
{
    return Skipped(SmsReconciliationResult.OutcomeSkippedUnsupportedProvider, ...);
}
```

**No changes needed** — new providers that don't implement `ISmsProviderStatusLookup` (e.g., Vonage) automatically skip reconciliation with structured outcome `skipped_unsupported_provider`.

---

## 7. Existing Cost Analytics Findings

**File:** `Notifications.Application/Options/SmsCostAnalyticsOptions.cs`

`GetEstimatedCost()` currently handles only `"twilio"` explicitly; other providers return `DefaultEstimatedOutboundSmsCost` (null by default).

**Change:** Added per-provider dictionary (`ProviderEstimates`) to `SmsCostAnalyticsOptions` and updated `GetEstimatedCost()` to check dictionary before falling back to `DefaultEstimatedOutboundSmsCost`. Config added to `appsettings.json`:

```json
"SmsCostAnalytics": {
  "ProviderEstimates": {
    "twilio": 0.0075,
    "vonage": 0.0065,
    "telnyx": 0.0055
  }
}
```

---

## 8. Existing Alert/Dashboard/Control Center Findings

- SMS operational alerts (`SmsOperationalAlert`), escalation policies, and dashboard APIs are unaffected
- Control Center already has SMS Dashboard, SMS Costs, SMS Escalation UI
- New Control Center route added: `/notifications/sms-routing` for routing policy management and decision visibility
- All existing CC routes and components preserved

---

## 9. Files Added

### Backend (Notification Service)

| File | Purpose |
|------|---------|
| `Notifications.Application/DTOs/SmsRoutingDtos.cs` | All DTOs: capability, routing policy, decision, request/response |
| `Notifications.Application/Interfaces/ISmsProviderCapabilityService.cs` | Provider capability registry interface |
| `Notifications.Application/Interfaces/ISmsProviderAdapterFactory.cs` | Generic factory interface for all SMS provider adapters |
| `Notifications.Application/Interfaces/ISmsRoutingEngine.cs` | Routing engine interface + request/result types |
| `Notifications.Application/Interfaces/ISmsRoutingPolicyRepository.cs` | Routing policy repository interface |
| `Notifications.Application/Interfaces/ISmsRoutingDecisionRepository.cs` | Routing decision repository interface |
| `Notifications.Domain/SmsRoutingPolicy.cs` | Domain entity for routing policies |
| `Notifications.Domain/SmsRoutingDecision.cs` | Domain entity for persisted routing decisions |
| `Notifications.Infrastructure/Providers/Adapters/VonageAdapter.cs` | Vonage SMS adapter (classic REST API) |
| `Notifications.Infrastructure/Providers/Adapters/VonageAdapterFactory.cs` | Factory creating VonageAdapter from TenantProviderConfig |
| `Notifications.Infrastructure/Services/SmsProviderCapabilityService.cs` | Static capability registry |
| `Notifications.Infrastructure/Services/SmsProviderAdapterRegistry.cs` | Factory registry (replaces BuildAdapter switch) |
| `Notifications.Infrastructure/Services/SmsRoutingEngine.cs` | Routing engine: priority, cost_optimized, health_optimized, hybrid, regional |
| `Notifications.Infrastructure/Repositories/SmsRoutingPolicyRepository.cs` | EF-based routing policy CRUD |
| `Notifications.Infrastructure/Repositories/SmsRoutingDecisionRepository.cs` | EF-based routing decision read/write |
| `Notifications.Infrastructure/Data/Configurations/SmsRoutingPolicyConfiguration.cs` | EF table config |
| `Notifications.Infrastructure/Data/Configurations/SmsRoutingDecisionConfiguration.cs` | EF table config |
| `Notifications.Infrastructure/Data/Migrations/20260511000002_AddSmsRouting.cs` | Migration: creates ntf_SmsRoutingPolicies + ntf_SmsRoutingDecisions |
| `Notifications.Api/Endpoints/SmsRoutingEndpoints.cs` | 9 admin routing API endpoints |
| `analysis/LS-NOTIF-SMS-014-report.md` | This report |

### Control Center

| File | Purpose |
|------|---------|
| `apps/control-center/src/lib/sms-routing-api.ts` | CC API client for routing endpoints |
| `apps/control-center/src/app/notifications/sms-routing/page.tsx` | Server page — capabilities, policies, decisions |
| `apps/control-center/src/components/sms-routing/routing-panel.tsx` | Client panel with tabs |

---

## 10. Files Modified

### Backend

| File | Change |
|------|--------|
| `SmsCostAnalyticsOptions.cs` | Added `ProviderEstimates` dictionary + updated `GetEstimatedCost()` |
| `SmsProviderRuntimeResolver.cs` | Replaced `BuildAdapter()` switch with `ISmsProviderAdapterRegistry.BuildAdapter()` |
| `NotificationsDbContext.cs` | Added `SmsRoutingPolicies`, `SmsRoutingDecisions` DbSets + EF configs |
| `DependencyInjection.cs` | Registered: `SmsProviderAdapterRegistry`, `SmsProviderCapabilityService`, `SmsRoutingEngine`, `VonageAdapterFactory`, repositories, options |
| `NotificationService.cs` | Injected `ISmsRoutingEngine`; added SMS-channel routing engine call + decision persistence before send loop |
| `appsettings.json` | Added `SmsRouting` section + `ProviderEstimates` to SmsCostAnalytics |
| `Program.cs` | Registered `MapSmsRoutingEndpoints()` |
| `NotificationsDbContextModelSnapshot.cs` | Added snapshot entries for both new tables |

### Control Center

| File | Change |
|------|--------|
| `apps/control-center/src/lib/nav.ts` | Added SMS Routing nav item under Notifications section |

---

## 11. Database/Schema/Config Changes

### New Tables

**ntf_SmsRoutingPolicies**
| Column | Type | Notes |
|--------|------|-------|
| Id | char(36) | PK |
| TenantId | char(36)? | null = global/platform policy |
| Name | varchar(200) | |
| Enabled | tinyint(1) | default true |
| Region | varchar(50)? | optional regional match |
| CountryCode | varchar(10)? | optional country match |
| RoutingMode | varchar(30) | priority/cost_optimized/health_optimized/hybrid/regional |
| PreferredProvidersJson | text? | ordered preferred providers |
| ExcludedProvidersJson | text? | excluded providers |
| MaxEstimatedCostPerMessage | decimal(18,8)? | cost cap filter |
| RequireHealthyProvider | tinyint(1) | default false |
| FallbackToPlatform | tinyint(1) | default true |
| Priority | int | matching priority (lower = higher priority) |
| CreatedAt | datetime(6) | |
| UpdatedAt | datetime(6) | |
| CreatedBy | varchar(255)? | |
| UpdatedBy | varchar(255)? | |

**ntf_SmsRoutingDecisions**
| Column | Type | Notes |
|--------|------|-------|
| Id | char(36) | PK |
| TenantId | char(36)? | |
| NotificationId | char(36)? | |
| AttemptId | char(36)? | updated post-send |
| RoutingPolicyId | char(36)? | matched policy ID |
| RoutingMode | varchar(30) | |
| SelectedProvider | varchar(100) | |
| SelectedProviderConfigId | char(36)? | |
| ProviderOwnershipMode | varchar(30)? | |
| CandidateProvidersJson | text? | full candidate list at decision time |
| ExcludedProvidersJson | text? | |
| DecisionReason | varchar(500) | |
| EstimatedCostAmount | decimal(18,8)? | |
| CostCurrency | varchar(3)? | |
| HealthSnapshotJson | text? | reserved for future health gate data |
| Region | varchar(50)? | |
| CountryCode | varchar(10)? | |
| CreatedAt | datetime(6) | |

Indexes: `(TenantId, CreatedAt)`, `(NotificationId)`, `(RoutingPolicyId)`

---

## 12. API/Interface Changes

### New Endpoints (`/v1/admin/sms/routing/`)

| Method | Path | Description |
|--------|------|-------------|
| GET | `/capabilities` | All provider capability metadata |
| GET | `/policies` | List routing policies (paged, filtered) |
| GET | `/policies/{id}` | Single policy by ID |
| POST | `/policies` | Create routing policy |
| PUT | `/policies/{id}` | Update routing policy |
| POST | `/policies/{id}/disable` | Soft-disable policy |
| GET | `/decisions` | List routing decisions (read-only, paged, filtered) |
| GET | `/decisions/summary` | Aggregate routing decision statistics |
| GET | `/providers/health` | Provider health snapshot from existing ProviderHealth table |

All endpoints: `RequireAuthorization(Policies.AdminOnly)`.  
No credentials, credentials JSON, webhook URLs, or raw phone numbers exposed in any response.

### New Interfaces

- `ISmsProviderCapabilityService` — static registry: `GetCapability(string providerType)`, `GetAll()`
- `ISmsProviderAdapterFactory` — generic factory: `bool Supports(string providerType)`, `ISmsProviderAdapter CreateFromConfig(TenantProviderConfig config)`
- `ISmsProviderAdapterRegistry` — registry of factories: `ISmsProviderAdapter BuildAdapter(string providerType, TenantProviderConfig config)`
- `ISmsRoutingEngine` — `Task<SmsRoutingDecisionResult> SelectRouteAsync(SmsRoutingRequest request, CancellationToken ct)`
- `ISmsRoutingPolicyRepository` — CRUD for SmsRoutingPolicy
- `ISmsRoutingDecisionRepository` — write + read for SmsRoutingDecision

---

## 13. UI/Route Changes

### Control Center

**New route:** `/notifications/sms-routing`

Sections:
1. **Provider Capabilities** — table of all registered providers with capability badges
2. **Routing Policies** — table with create/edit/disable support (modal form)
3. **Routing Decisions** — recent routing decision log with provider/mode/reason columns
4. **Provider Health** — summary of live provider health from existing health endpoint

Nav: "SMS Routing" added under Notifications section with "NEW" badge.

---

## 14. Validation/Testing Performed

- [x] `dotnet build` — 0 errors
- [x] TypeScript check — exit code 0
- [x] New interfaces satisfy existing consumer call sites
- [x] TwilioAdapter unchanged — reconciliation behavior preserved
- [x] VonageAdapter: `OutcomeSkippedUnsupportedProvider` on reconciliation (no `ISmsProviderStatusLookup` implementation)
- [x] Routing engine priority mode preserves existing route order
- [x] Cost analytics `GetEstimatedCost()` — dictionary lookup with fallback to default
- [x] No credentials/settings in routing decision or capability response
- [x] All existing endpoints (SMS-001 through SMS-013) unchanged

---

## 15. Known Gaps/Issues

1. **Vonage status lookup not implemented** — Vonage uses webhook callbacks for status. Active pull-status requires Vonage Status API (different auth flow). Reconciliation auto-skips with `skipped_unsupported_provider`. Gap documented.
2. **Vonage health check not implemented** — No safe zero-cost health probe for Vonage classic REST API. `HealthCheckAsync()` returns `{ Status = "unknown" }`. Gap documented.
3. **Regional routing** — `CountryCode` derivation from recipient phone number is not implemented (requires phone number parsing library). Regional mode falls back to priority with reason `regional_fallback_no_country_data`.
4. **Actual provider cost** — `ActualCostAmount` remains null for all providers (requires provider billing API integration). `CostSource` = `"estimated"` for configured providers, `"unavailable"` otherwise.
5. **RoutingDecision.AttemptId** — linked post-send in the routing decision update. If the send fails before attempt creation, AttemptId remains null.
6. **Telnyx, Plivo, MessageBird, Sinch** — factory scaffolding not added in this iteration. Only Vonage (lowest risk, classic REST API) added as a second provider. Others can be added following the same `ISmsProviderAdapterFactory` pattern.
7. **Platform Vonage provider** — `PlatformProviderPriority["sms"]` remains `["twilio"]`. Vonage is only available via `TenantProviderConfig` (tenant-managed). Adding Vonage to platform requires env-var credentials registration.

---

## 16. Recommended Next Steps

1. Implement Vonage health check using `/v2/account/numbers` or a no-op ping
2. Implement `ISmsProviderStatusLookup` for Vonage via their Status API
3. Implement phone number → country code parsing for regional routing (e.g., `libphonenumber-csharp`)
4. Add Telnyx adapter (similar REST pattern, supports status lookup)
5. Surface routing decision data in the SMS Dashboard existing "Activity" tab
6. Add tenant-facing routing policy API (non-admin) for tenant self-service
7. Implement actual provider cost pull-reconciliation for Twilio (`Price` field in Twilio Status API response)

---

*Report auto-updated during implementation. See individual file headers for additional inline documentation.*
