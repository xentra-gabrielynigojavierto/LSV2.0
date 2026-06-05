# LS-NOTIF-SMS-005 — Tenant-Scoped SMS Provider Runtime Resolution

**Status:** IN PROGRESS → COMPLETE  
**Date:** 2026-05-08  
**Scope:** Notification Service — tenant-owned Twilio adapter resolution for send + reconciliation  
**Depends on:** LS-NOTIF-SMS-001 (TwilioAdapter), LS-NOTIF-SMS-004 (SmsReconciliationService)

---

## 1. Initial Codebase Analysis

### 1.1 TenantProviderConfig Domain Model

`TenantProviderConfig`:
- `Id`, `TenantId`, `Channel`, `ProviderType`
- `CredentialsJson` — `{"accountSid": "AC...", "authToken": "..."}` for Twilio
- `SettingsJson` — `{"fromNumber": "+15551234567"}` for Twilio
- `Status` — `"active"` | `"inactive"` | other
- `Priority`, `ValidationStatus`, `HealthStatus`

### 1.2 Existing TenantProviderConfigRepository

`ITenantProviderConfigRepository`:
- `GetByIdAsync(Guid id)` — cross-tenant by ID
- `FindByIdAndTenantAsync(Guid id, Guid tenantId)` — tenant-scoped by config ID
- `GetActiveByTenantAndChannelAsync(Guid tenantId, string channel)` — all active configs for tenant/channel
- `GetActiveSmsProviderConfigsAsync(string providerType)` — all active SMS configs (used by LS-NOTIF-SMS-003)

### 1.3 Provider Routing / ProviderRoute

`ProviderRoute`:
- `ProviderType` — e.g., `"twilio"`
- `OwnershipMode` — `"platform"` | `"tenant"`
- `TenantProviderConfigId` — `Guid?` — set for tenant-owned routes, null for platform routes
- `IsFailover` — bool
- `IsPlatformFallback` — bool

`ProviderRoutingService.ResolveRoutesAsync` already correctly:
- Loads tenant channel provider settings
- For `tenant_managed` mode: resolves primary tenant config, optional failover, optional platform fallback
- For non-tenant mode: returns platform routes only
- **Already sets `TenantProviderConfigId` on tenant routes**

### 1.4 TwilioAdapter Construction

`TwilioAdapter(string accountSid, string authToken, string defaultFromNumber, HttpClient http, ILogger<TwilioAdapter> logger)`:
- All 5 parameters required
- Platform adapter constructed in `DependencyInjection.cs` from `TWILIO_ACCOUNT_SID`, `TWILIO_AUTH_TOKEN`, `TWILIO_FROM_NUMBER` env vars
- Registered as scoped `ISmsProviderAdapter`

### 1.5 NotificationService SMS Send Loop

**Critical gap:** `NotificationService` already receives routes with `TenantProviderConfigId` but always calls `_twilioAdapter.SendAsync(...)` (the injected platform singleton) for ALL SMS sends, ignoring tenant ownership.

The send block (lines 902–910):
```csharp
else  // sms
{
    var result = await _twilioAdapter.SendAsync(new SmsSendPayload { ... });
    ...
}
```

`_twilioAdapter` is the platform singleton. Tenant-owned configs in `route.TenantProviderConfigId` are currently stored on attempts but never used for the actual send.

### 1.6 NotificationAttempt Provider Metadata

`NotificationAttempt` already has:
- `ProviderConfigId` — `Guid?` — set from `route.TenantProviderConfigId` (but currently refers to a platform route's null)
- `ProviderOwnershipMode` — already set from `route.OwnershipMode`

**No schema change needed** — these fields exist and are already populated from routing.

### 1.7 SmsReconciliationService (LS-NOTIF-SMS-004)

`SmsReconciliationService` injects `ISmsProviderAdapter _smsAdapter` (platform singleton) and casts it to `ISmsProviderStatusLookup`. This means reconciliation always uses platform Twilio credentials, even for attempts originally sent via a tenant-owned Twilio config. Fixed in LS-NOTIF-SMS-005.

### 1.8 DependencyInjection.cs

- `ISmsProviderAdapter` registered as scoped factory (reads `TWILIO_*` env vars at startup)
- `IHttpClientFactory` registered with `"Twilio"` named client

---

## 2. Design Decisions

### 2.1 Interface Placement

`ISmsProviderRuntimeResolver` and `ITwilioAdapterFactory` placed in `Application/Interfaces` — accessible to both Application (interface definition) and Infrastructure (implementation). No cross-layer violation.

### 2.2 Factory Pattern for Adapter Creation

`TwilioAdapterFactory` creates `TwilioAdapter` instances from `TenantProviderConfig` by parsing `CredentialsJson` and `SettingsJson`. Reuses existing `TwilioAdapter` implementation — no duplication of send/status logic.

### 2.3 Platform Adapter in Resolver

`SmsProviderRuntimeResolver` injects the registered `ISmsProviderAdapter` (platform singleton) alongside `ITwilioAdapterFactory`. For platform routes (no `TenantProviderConfigId`), returns the platform singleton. For tenant routes, calls factory.

### 2.4 NotificationService Change

`NotificationServiceImpl` adds `ISmsProviderRuntimeResolver _smsRuntimeResolver`. The existing `_twilioAdapter` field is retained (still used by `ProviderHealthWorker` which resolves it separately). The SMS send block calls the resolver to get the correct adapter for the current route.

### 2.5 SmsReconciliationService Change

Remove `ISmsProviderAdapter _smsAdapter` field; add `ISmsProviderRuntimeResolver _runtimeResolver`. In `ReconcileAttemptAsync`, resolve the correct adapter using `attempt.TenantId`, `attempt.Provider`, `attempt.ProviderConfigId`.

---

## 3. Twilio Provider Config JSON Shape

### CredentialsJson
```json
{
  "accountSid": "ACxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "authToken": "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
}
```

### SettingsJson
```json
{
  "fromNumber": "+15551234567"
}
```

---

## 4. Files Added

| File | Purpose |
|---|---|
| `Notifications.Application/Interfaces/ISmsProviderRuntimeResolver.cs` | `ISmsProviderRuntimeResolver` interface + `SmsProviderRuntimeContext` result model |
| `Notifications.Application/Interfaces/ITwilioAdapterFactory.cs` | Factory interface for creating `ISmsProviderAdapter` from `TenantProviderConfig` |
| `Notifications.Infrastructure/Providers/Adapters/TwilioAdapterFactory.cs` | Parses `CredentialsJson`/`SettingsJson`, constructs `TwilioAdapter` |
| `Notifications.Infrastructure/Services/SmsProviderRuntimeResolver.cs` | Resolver implementation — tenant config lookup, adapter construction, platform fallback |

---

## 5. Files Modified

| File | Change |
|---|---|
| `Notifications.Application/Interfaces/ISmsReconciliationService.cs` | Added 5 new provider config failure outcome constants |
| `Notifications.Infrastructure/Services/NotificationService.cs` | Inject `ISmsProviderRuntimeResolver`; replace platform singleton call with resolver for SMS send |
| `Notifications.Infrastructure/Services/SmsReconciliationService.cs` | Replace `ISmsProviderAdapter` singleton injection with `ISmsProviderRuntimeResolver`; resolve per-attempt adapter |
| `Notifications.Infrastructure/DependencyInjection.cs` | Register `ITwilioAdapterFactory`, `ISmsProviderRuntimeResolver` |

---

## 6. Database / Schema Changes

None. `NotificationAttempt.ProviderConfigId` and `ProviderOwnershipMode` already exist and are already populated from routing.

---

## 7. Send-Time Resolution Flow

```
NotificationServiceImpl.ExecuteSendAsync
  → ProviderRoutingService.ResolveRoutesAsync  (already tenant-aware)
  → foreach route:
      → ISmsProviderRuntimeResolver.ResolveForSendAsync(tenantId, providerType, route.TenantProviderConfigId)
          → if TenantProviderConfigId == null → return platform adapter
          → else: FindByIdAndTenantAsync → validate active → TwilioAdapterFactory.CreateFromConfig → validate → return
      → resolved.Adapter.SendAsync(payload)
```

---

## 8. Reconciliation-Time Resolution Flow

```
SmsReconciliationService.ReconcileAttemptAsync
  → ISmsProviderRuntimeResolver.ResolveForReconciliationAsync(attempt.TenantId, attempt.Provider, attempt.ProviderConfigId)
      → if ProviderConfigId == null → platform adapter (platform-owned attempt)
      → else: FindByIdAndTenantAsync → validate active → TwilioAdapterFactory.CreateFromConfig → return
  → resolved.Adapter as ISmsProviderStatusLookup → GetMessageStatusAsync
```

---

## 9. New Reconciliation Outcome Constants

| Constant | Value | Meaning |
|---|---|---|
| `OutcomeMissingProviderConfigContext` | `missing_provider_config_context` | No providerConfigId and no tenantId — cannot resolve adapter |
| `OutcomeProviderConfigNotFound` | `provider_config_not_found` | Config ID in attempt not found in DB |
| `OutcomeProviderConfigInactive` | `provider_config_inactive` | Config found but not active |
| `OutcomeProviderConfigInvalid` | `provider_config_invalid` | Config found but credentials/settings invalid |
| `OutcomeProviderRuntimeResolutionFailed` | `provider_runtime_resolution_failed` | Unexpected error during resolution |

---

## 10. Security Invariants

- Credentials in `TenantProviderConfig.CredentialsJson` are never logged, returned in API responses, or included in audit metadata
- `TwilioAdapterFactory` parses credentials only to pass to `TwilioAdapter` constructor — never returns them
- `SmsProviderRuntimeContext` contains the constructed adapter, not credentials
- Audit events include `provider_config_id` and `ownership_mode` only — no credentials

---

## 11. Validation Performed

### Build
- `dotnet build Notifications.Api` — ✅ 0 errors

### Code review (send-time)
- ✅ Tenant route with `TenantProviderConfigId` → loads tenant config → uses tenant credentials
- ✅ Platform route (no `TenantProviderConfigId`) → platform adapter returned
- ✅ Missing/inactive config → structured `ProviderFailure` → attempt marked `failed`, no send
- ✅ Invalid credentials (empty accountSid/authToken/fromNumber) → `ValidateConfigAsync()` returns false → structured failure
- ✅ Preference/suppression enforcement still executes before routing (unchanged)
- ✅ Retry/dead-letter semantics unchanged (failure path same as before)

### Code review (reconciliation-time)
- ✅ Attempt with `ProviderConfigId` → loads tenant config → tenant TwilioAdapter for status lookup
- ✅ Attempt without `ProviderConfigId` (platform) → platform adapter
- ✅ Config not found → `provider_config_not_found` outcome
- ✅ Config inactive → `provider_config_inactive` outcome
- ✅ No resend anywhere in reconciliation path
- ✅ Terminal state protection unchanged (in DeliveryStatusService)

### Existing behavior preservation
- ✅ LS-NOTIF-SMS-001: `TwilioAdapter.SendAsync` logic unchanged
- ✅ LS-NOTIF-SMS-002: preference enforcement happens before send, unchanged
- ✅ LS-NOTIF-SMS-003: inbound resolver unchanged
- ✅ LS-NOTIF-SMS-004: reconciliation batch/manual flow preserved; only adapter source changed

---

## 12. Known Gaps / Future Enhancements

| Item | Notes |
|---|---|
| Multi-provider SMS support | `SmsProviderRuntimeResolver.BuildAdapter` currently only supports `"twilio"`. New providers require adding a case + factory. |
| Inactive config retry after reactivation | If a tenant config becomes inactive after a batch of attempts, reconciliation returns `provider_config_inactive`. No automatic retry. |
| Provider config validation pre-send | `ValidateConfigAsync()` checks non-empty credentials but does not make a live API call. Full validation requires a health check call. |
| Tenant config caching | Every SMS send does a DB lookup for the tenant config. A short TTL cache would reduce DB load for high-volume tenants. |

---

## 13. Recommended Next Steps

1. **Activate tenant Twilio configs** — Provision `TenantProviderConfig` records for tenants with their own Twilio accounts; set `Status=active`.
2. **Extend to other SMS providers** — Add `ITwilioAdapterFactory`-equivalent factories for other SMS providers as they are onboarded.
3. **Cache tenant provider config** — Add a short TTL cache (e.g., 60s) in `SmsProviderRuntimeResolver` to avoid per-send DB lookups.
4. **ProviderHealthWorker tenant-aware** — Currently only health-checks the platform adapter; extend to check active tenant configs.
5. **Config validation health check** — Implement `ValidateConfigAsync` to optionally make a low-cost live API call.
