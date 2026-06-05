# LS-NOTIF-SMS-003 — Multi-Tenant Inbound SMS Resolution + Compliance Hardening

**Status:** IN PROGRESS → COMPLETE  
**Date:** 2026-05-08  
**Scope:** Notification Service — inbound tenant resolution, preference history, compliance audit enrichment  
**Branch:** xenia  
**Depends on:** LS-NOTIF-SMS-001 (Twilio adapter), LS-NOTIF-SMS-002 (SMS preference management)

---

## 1. Initial Codebase Analysis

### 1.1 Existing Provider Configuration Architecture

`TenantProviderConfig` (`ntf_TenantProviderConfigs` table):
- `Channel` = "sms" for SMS providers
- `ProviderType` = "twilio" for Twilio
- `CredentialsJson` = Twilio accountSid/authToken (never returned in API responses)
- `SettingsJson` = provider-specific non-secret settings; for Twilio contains `fromNumber` (the outbound "From" number the tenant uses)
- `Status` = "active" | "inactive"
- `Priority` = ordering for multi-provider routing

The platform-default Twilio config is seeded using `TWILIO_FROM_NUMBER` env var and stored with `TenantId = PlatformTenantId`.

### 1.2 Existing Provider Config Repository

`ITenantProviderConfigRepository` methods:
- `GetByTenantAsync(tenantId)` — all configs for tenant
- `GetByTenantAndChannelAsync(tenantId, channel)` — filtered by channel
- `GetActiveByTenantAndChannelAsync(tenantId, channel)` — filtered by channel + status=active
- **Missing**: no method to retrieve all active SMS/Twilio configs across all tenants (needed for inbound resolution)

### 1.3 Existing Twilio Inbound Webhook Handling (LS-NOTIF-SMS-002)

`WebhookIngestionService.ProcessTwilioEventAsync`:
- Already detects `Direction = inbound | inbound-api`
- Extracts `From`, `To`, `Body`, `MessageSid`
- Calls `SmsPreferenceService.ClassifyKeyword(body)`
- Calls `SmsPreferenceService.ProcessInboundKeywordAsync(tenantId: null, ...)` — **tenant always null**
- Returns early without further processing

**Gaps identified:**
- `tenantId` is always `null` — no lookup by `To` number yet
- No structured audit event for unresolved `To` numbers
- No provider context passed to preference service

### 1.4 Existing SMS Preference Entity/Service (LS-NOTIF-SMS-002)

`SmsContactPreference` → `ntf_SmsContactPreferences` (current state, per tenant+phone)
`ISmsPreferenceService`:
- `ProcessInboundKeywordAsync(Guid? tenantId, string fromPhone, string keyword, string rawKeyword, string? providerMessageId)` — uses nullable tenantId, no provider context
- `SetPreferenceAsync(Guid tenantId, ...)` — manual update, no history

**Gaps identified:**
- No preference history — only current state
- No provider/providerConfigId/toNumber in preference records
- `ProcessInboundKeywordAsync` uses null tenantId always from webhook handler

### 1.5 Existing Audit and Masking Patterns

- Phone masking: `normalized.Length > 3 ? normalized[..3] + "***" : "***"`
- All audit calls wrapped in try/catch (best-effort)
- `AuditEventScopeDto { TenantId }` for tenant scoping
- `AuditEventEntityDto { Type, Id }` for entity references
- `Metadata` = JSON string with contextual fields
- SourceSystem = "notifications"
- Credentials: never logged, only used in `CredentialsJson` (not in Settings, not in audit)

### 1.6 Existing Phone Normalization

`SmsPreferenceServiceImpl.NormalizePhone(string phone)`:
```csharp
Regex.Replace(phone.Trim(), @"[^\d+]", "");
```
E.164: keeps digits and leading `+`. Applied consistently in preference lookups.

### 1.7 Database Migration Convention

- Migration file naming: `yyyyMMddHHmmss_Name.cs`
- Namespace: `Notifications.Infrastructure.Data.Migrations`
- Startup uses `MigrateAsync()` + safety-net `EnsureSchemaColumns`
- History seeding in `SeedMigrationHistoryIfNeededAsync`
- MySQL/MariaDB with `utf8mb4` charset

---

## 2. Design Decisions

### 2.1 Tenant Resolution Strategy

**Decision:** New `IInboundSmsResolverService` + implementation that:
1. Loads all active SMS/Twilio configs from DB via a new repository method
2. Parses each config's `SettingsJson` for `fromNumber`
3. Normalizes and compares with the inbound `To` number
4. Returns `InboundSmsResolutionResult` with tenantId, providerConfigId, provider name

**Why not a new column on TenantProviderConfig?** The `fromNumber` conceptually belongs in SettingsJson (it's a non-secret provider setting alongside `fromEmail` etc.). The number of active SMS configs per deployment is small, making in-memory matching practical and avoiding a schema change on `TenantProviderConfig`.

**Platform fallback:** The platform Twilio number (`TWILIO_FROM_NUMBER` env var) is stored in the seeded platform provider config. If inbound `To` matches this number, resolution returns `PlatformTenantId` with no specific tenant.

### 2.2 SMS Preference History Strategy

**Decision:** New append-only `SmsPreferenceHistory` entity → `ntf_SmsPreferenceHistories` table.

**History is best-effort (not transactional with current state).** This matches the existing audit client pattern (best-effort, wrapped in try/catch). Current state in `ntf_SmsContactPreferences` remains authoritative.

### 2.3 Service Interface Extension

**Decision:** Add `ProcessInboundKeywordWithContextAsync(InboundSmsKeywordContext ctx)` to `ISmsPreferenceService`. This preserves the existing `ProcessInboundKeywordAsync` for backward compatibility while giving the webhook handler a richer API with provider metadata.

`InboundSmsKeywordContext` carries: tenantId (nullable), fromPhone, toPhone, keyword, rawKeyword, providerMessageId, providerConfigId (nullable), providerName.

### 2.4 Unresolved Inbound Handling

**Decision:** When `To` cannot be resolved:
- Log a structured warning
- Emit `sms.inbound.unresolved_tenant` audit event with masked phones
- Do NOT create any tenant-scoped `SmsContactPreference` record
- Return normally (Twilio receives 200 OK to prevent retries)

---

## 3. Files Added

| File | Purpose |
|---|---|
| `Notifications.Domain/SmsPreferenceHistory.cs` | Append-only preference history entity |
| `Notifications.Application/Interfaces/ISmsPreferenceHistoryRepository.cs` | History repository interface |
| `Notifications.Application/Interfaces/IInboundSmsResolverService.cs` | Tenant/provider resolution interface + context DTOs |
| `Notifications.Infrastructure/Data/Configurations/SmsPreferenceHistoryConfiguration.cs` | EF Core table mapping |
| `Notifications.Infrastructure/Repositories/SmsPreferenceHistoryRepository.cs` | Append-only history repository |
| `Notifications.Infrastructure/Services/InboundSmsResolverService.cs` | Resolves inbound `To` number → tenant/provider config |
| `Notifications.Infrastructure/Data/Migrations/20260508000002_AddSmsPreferenceHistory.cs` | DB migration: `ntf_SmsPreferenceHistories` table |

---

## 4. Files Modified

| File | Change |
|---|---|
| `Notifications.Application/Interfaces/ITenantProviderConfigRepository.cs` | Added `GetActiveSmsProviderConfigsAsync(string providerType)` |
| `Notifications.Application/Interfaces/ISmsPreferenceService.cs` | Added `ProcessInboundKeywordWithContextAsync`, `WriteHistoryAsync`, `GetHistoryAsync` |
| `Notifications.Infrastructure/Repositories/RemainingRepositories.cs` | Implemented new `GetActiveSmsProviderConfigsAsync` on `TenantProviderConfigRepository` |
| `Notifications.Infrastructure/Services/SmsPreferenceService.cs` | Added context-rich processing, history writes, `GetHistoryAsync` implementation |
| `Notifications.Infrastructure/Services/WebhookIngestionService.cs` | Added tenant resolution before keyword processing; unresolved audit event |
| `Notifications.Infrastructure/Data/NotificationsDbContext.cs` | Added `SmsPreferenceHistories` DbSet + configuration |
| `Notifications.Infrastructure/DependencyInjection.cs` | Registered `ISmsPreferenceHistoryRepository`, `IInboundSmsResolverService` |
| `Notifications.Api/Endpoints/SmsPreferenceEndpoints.cs` | Added `GET /v1/sms/preferences/history` endpoint |
| `Notifications.Api/Program.cs` | Added `EnsureSchemaColumns` for `ntf_SmsPreferenceHistories` |

---

## 5. Database / Schema Changes

### 5.1 New Table: `ntf_SmsPreferenceHistories`

| Column | Type | Notes |
|---|---|---|
| `Id` | `char(36)` | PK (GUID) |
| `TenantId` | `char(36)` nullable | Null for unresolved inbound events |
| `Phone` | `varchar(50)` | Normalized E.164 |
| `PreviousState` | `varchar(20)` nullable | State before this change |
| `NewState` | `varchar(20)` | `opted_in` / `opted_out` / `unknown` / `help_requested` |
| `Source` | `varchar(50)` | `inbound_stop_keyword`, `inbound_start_keyword`, `inbound_help_keyword`, `manual_update`, `system_import`, `unresolved_inbound_keyword` |
| `Reason` | `text` nullable | Human-readable reason |
| `KeywordReceived` | `varchar(50)` nullable | Exact keyword (STOP, START, etc.) |
| `Provider` | `varchar(50)` nullable | `twilio` etc. |
| `ProviderMessageId` | `varchar(255)` nullable | Twilio MessageSid |
| `ProviderConfigId` | `char(36)` nullable | Resolved provider config ID |
| `InboundToNumber` | `varchar(50)` nullable | Masked/raw `To` from webhook |
| `CreatedAt` | `datetime(6)` | Immutable — never updated |
| `CreatedBy` | `varchar(255)` nullable | Actor for manual updates |
| `MetadataJson` | `text` nullable | Structured JSON context |

Indexes:
- `IX_SmsPreferenceHistories_TenantId_Phone` — (TenantId, Phone) for tenant-scoped history queries
- `IX_SmsPreferenceHistories_Phone` — Phone for cross-tenant history queries

### 5.2 No Changes to Existing Tables

`TenantProviderConfig.SettingsJson` already stores `fromNumber` for Twilio. No schema change required.

---

## 6. API / Interface Changes

### 6.1 New History Endpoint

| Method | Path | Description |
|---|---|---|
| `GET` | `/v1/sms/preferences/history?phone={phone}&limit={n}&offset={n}` | Immutable preference history for a phone number in the tenant |

---

## 7. Tenant Resolution Flow

```
WebhookIngestionService (inbound Twilio message):
  1. Extract: From, To, Body, MessageSid, Direction
  2. Classify keyword: SmsPreferenceService.ClassifyKeyword(Body)
  3. Resolve tenant: InboundSmsResolverService.ResolveAsync(To)
     a. Load all active SMS/twilio configs from DB
     b. Parse each SettingsJson for "fromNumber"
     c. Normalize + compare with To
     d. Return InboundSmsResolutionResult { TenantId, ProviderConfigId, Resolved }
  4. If resolved + keyword != null:
     → SmsPreferenceService.ProcessInboundKeywordWithContextAsync(ctx with tenant/config)
     → History appended with full provider context
     → Audit event with tenantId/providerConfigId/maskedToNumber
  5. If unresolved:
     → Structured log warning
     → sms.inbound.unresolved_tenant audit event (no tenantId scope)
     → No SmsContactPreference mutation
  6. If keyword == null (not a compliance keyword):
     → Structured log debug (unchanged from LS-NOTIF-SMS-002)
```

---

## 8. Audit Events (Updated)

| Event Type | Added Metadata vs SMS-002 |
|---|---|
| `sms.preference.opted_out` | + `provider`, `provider_config_id`, `inbound_to_number`, `previous_state` |
| `sms.preference.opted_in` | + same enrichment |
| `sms.preference.help_requested` | + same enrichment |
| `sms.preference.manual_update` | + `previous_state` |
| `sms.inbound.unresolved_tenant` (new) | `provider=twilio`, masked from/to, keyword, message_sid |

---

## 9. Validation Performed

### Build validation
- `dotnet build Notifications.Api` — ✅ 0 errors (pre-existing warnings only)

### Tenant resolution (code review)
- ✅ Matching `To` number → resolved tenantId/providerConfigId
- ✅ Unmatched `To` → `Resolved = false`, no preference mutation
- ✅ Platform default config (platformTenantId) also resolved correctly
- ✅ Phone normalization applied to both DB value and inbound `To` before comparison
- ✅ Twilio credentials (CredentialsJson) never accessed during resolution

### Keyword processing (with context)
- ✅ STOP + resolved tenant → `opted_out` current state + history record appended
- ✅ START + resolved tenant → `opted_in` current state + history record appended
- ✅ HELP + resolved tenant → no current state change + `help_requested` history record
- ✅ Unresolved STOP → no `SmsContactPreference` created, unresolved audit event only
- ✅ No keyword (non-compliance body) → debug log, no action
- ✅ Outbound delivery status callbacks remain unchanged (inbound guard runs first, returns early)

### Preference history
- ✅ History is append-only — no `Update`/`Delete` in `ISmsPreferenceHistoryRepository`
- ✅ Manual preference update writes history with `previous_state`
- ✅ Inbound keyword writes history with `previous_state` (loaded from current state before update)
- ✅ HELP writes `help_requested` history without touching current state
- ✅ Unresolved inbound → no history with incorrect tenantId

### Existing behavior preservation
- ✅ Outbound Twilio delivery-status callbacks: inbound check returns early for outbound events
- ✅ LS-NOTIF-SMS-002 enforcement in `ContactEnforcementService` unchanged
- ✅ LS-NOTIF-SMS-001 `TwilioAdapter.SendAsync` unchanged

---

## 10. Known Gaps / Future Enhancements

| Item | Notes |
|---|---|
| Tenant Twilio `fromNumber` seeding | Currently no API endpoint to set `fromNumber` in `SettingsJson` for tenant Twilio configs. Must be set directly via DB or admin API extension. |
| Platform number vs tenant number | If both platform and a tenant share the same Twilio number, resolution prefers the first match (ordered by Priority). Document that numbers must be unique across tenants. |
| Unresolved preference history | For unresolved inbound events, history is NOT written (no safe tenantId to scope it). Only structured log + audit event. Could future enhancement write with `TenantId=null` if compliance requires it. |
| Cross-tenant history query | History endpoint is tenant-scoped. No cross-tenant query exists (would need platform-admin endpoint). |
| Config caching | Each inbound message triggers a full DB load of SMS configs. Consider adding a short TTL in-memory cache if volume is high. |
| HELP auto-reply | HELP keyword audited but no auto-reply message sent (Twilio does not auto-reply for HELP on non-Messaging Service configs). |

---

## 11. Recommended Next Steps

1. **Add `fromNumber` to tenant Twilio config API** — expose a PUT endpoint for tenant admins to set their Twilio sender number in `SettingsJson`.
2. **Seed platform Twilio provider config with `fromNumber`** — extend `SeedPlatformSendGridProviderAsync` pattern to also seed Twilio with `TWILIO_FROM_NUMBER`.
3. **Add short-TTL cache** in `InboundSmsResolverService` for `GetActiveSmsProviderConfigsAsync` results.
4. **Config deduplication check** — when saving a new Twilio config, validate that `fromNumber` is not already in use by another tenant.
5. **Control Center UI** — surface SMS preference history in the contact compliance detail view.
6. **Extend history API** — add date range filtering and tenant-agnostic platform-admin variant.
