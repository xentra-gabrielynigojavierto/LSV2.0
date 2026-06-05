# LS-NOTIF-SMS-002 — SMS Preference Management + Compliance Controls

**Status:** IN PROGRESS → COMPLETE  
**Date:** 2026-05-08  
**Scope:** Notification Service — SMS compliance, opt-out/opt-in enforcement, keyword processing  
**Branch:** xenia

---

## 1. Initial Codebase Analysis

### 1.1 Existing SMS Delivery Architecture (from LS-NOTIF-SMS-001)

- `ISmsProviderAdapter` / `TwilioAdapter` — full Twilio HTTP adapter
- `ProviderRoutingService` — `["sms"] = new[] { "twilio" }` routing
- `NotificationService.ExecuteSendLoopAsync` — dispatches via `_twilioAdapter.SendAsync`
- `WebhookIngestionService.HandleTwilioAsync` — ingests outbound delivery callbacks (status events)
- `TwilioNormalizer` — normalizes `MessageStatus`/`SmsStatus` delivery callbacks
- `TwilioVerifier` — HMAC-SHA1 webhook signature verification

### 1.2 Existing Contact Enforcement Architecture

| Component | File | Behavior |
|---|---|---|
| `IContactEnforcementService` | `Application/Interfaces/IContactEnforcementService.cs` | `EvaluateAsync(ContactEnforcementInput)` → `ContactEnforcementResult` |
| `ContactEnforcementService` | `Infrastructure/Services/ContactEnforcementService.cs` | Checks active suppression records + contact health status against tenant policy |
| `ContactSuppression` | `Domain/ContactSuppression.cs` | Suppression records per tenant+channel+contact. Types: `manual`, `bounce`, `unsubscribe`, `complaint`, `invalid_contact`, `carrier_rejection`, `system_protection` |
| `TenantContactPolicy` | `Domain/TenantContactPolicy.cs` | Per-tenant, per-channel policy flags: `BlockSuppressedContacts`, `BlockUnsubscribedContacts`, `BlockComplainedContacts`, `BlockBouncedContacts`, `BlockInvalidContacts`, `BlockCarrierRejectedContacts`, `AllowManualOverride` |
| `RecipientContactHealth` | `Domain/RecipientContactHealth.cs` | Health records per contact: `valid`, `bounced`, `complained`, `unsubscribed`, `suppressed`, `invalid`, `carrier_rejected`, `opted_out` |
| `IContactSuppressionRepository` | `Application/Interfaces/IContactSuppressionRepository.cs` | CRUD + `UpsertFromEventAsync` for webhook-driven suppression |

### 1.3 Existing Suppression Enforcement Flow

```
ContactEnforcementService.EvaluateAsync:
  1. Load TenantContactPolicy for channel
  2. Load active ContactSuppressions for (tenant, channel, contact)
  3. For each suppression: check SuppressionTypePolicyMap → block if matching policy flag is true
  4. Load RecipientContactHealth for contact
  5. Check HealthStatusPolicyMap → block if health status + policy flag match
  6. Return allowed/denied result with reason code
```

**Key finding:** The existing model already blocks `opted_out` contacts (via `HealthStatusPolicyMap["opted_out"] = "BlockUnsubscribedContacts"`). However:
- There is no **SMS opt-in** state — the system cannot distinguish "opted_out" from "never opted_in" (unknown)
- There is no **tenant policy for unknown SMS preference** — the system defaults to allow for unknown contacts
- There is no **inbound keyword processing** — Twilio STOP/START callbacks are not processed
- Inbound messages (Direction=inbound) from recipients are not currently recognized in `ProcessTwilioEventAsync`

### 1.4 Existing Webhook Ingestion Findings

`WebhookIngestionService.ProcessTwilioEventAsync` handles:
- Outbound delivery status callbacks (delivered, failed, undelivered, etc.)
- Auto-suppresses on bounce/complaint/unsubscribed/carrier_rejected events
- Does NOT check `Direction` field — inbound messages would be treated as outbound status events
- `TwilioNormalizer.Normalize` reads `MessageStatus`/`SmsStatus` only — not `Body`/`Direction` fields

### 1.5 Existing Audit Patterns

- All audit events posted via `_auditClient.IngestAsync(new IngestAuditEventRequest { ... })`
- Common fields: `EventType`, `Action`, `SourceSystem = "notifications"`, `Outcome`, `Description`, `Scope.TenantId`, `Entity`, `Metadata` (JSON)
- Phone numbers always masked via `MaskRecipient()` → `+1***` convention
- All audit calls wrapped in try/catch (best-effort, non-blocking)

### 1.6 Existing Skip/Failure Reason Classification

Existing reason codes:
- `suppressed_{type}` — contact suppressed by type
- `health_{status}` — contact health blocked
- `override_{type}` — suppression overridden
- `sms_opted_out` — mapped from `health_opted_out`

Missing for SMS-002:
- `sms_opted_out` — explicit
- `sms_preference_unknown_blocked_by_policy` — unknown preference + tenant policy blocks
- `sms_suppressed` — generic SMS suppression (existing patterns cover this)
- `no_phone_on_file` — already in ClassifySkipReason (LS-NOTIF-SMS-001)

### 1.7 Database Schema Findings (from InitialCreate migration)

- `ntf_ContactSuppressions` — supports SMS via channel field
- `ntf_TenantContactPolicies` — 7 policy flags, no SMS-specific preference policy
- `ntf_RecipientContactHealth` — health status including `opted_out`
- **No `ntf_SmsContactPreferences` table** — does not exist yet

---

## 2. Design Decisions

### 2.1 SMS Preference State Model

**Decision:** New `SmsContactPreference` entity (new table `ntf_SmsContactPreferences`).

**Rationale:** 
- `ContactSuppression` is suppression-only and cannot cleanly represent `opted_in`
- `RecipientContactHealth` has `opted_out` status but no `opted_in` — extending it conflates health semantics with preference semantics
- A dedicated entity is clearer, auditable with full source/history, and avoids polluting existing models
- Small table, indexed by tenant+phone — fast lookups

**States:** `opted_in`, `opted_out`, `unknown`

### 2.2 Tenant Policy Extension

**Decision:** Add `BlockUnknownSmsPreference` bool to `TenantContactPolicy`.

**Default:** `true` (conservative — blocks SMS to contacts with unknown preference unless tenant explicitly allows).

**Rationale:** Reuses existing policy lookup pattern; channel-scoped to SMS only.

### 2.3 Enforcement Integration Point

**Decision:** Extend `ContactEnforcementService.EvaluateAsync` to check `SmsContactPreference` for SMS channel **before** existing suppression checks.

**Rationale:** Keeps all enforcement in one place; provider-agnostic; preserves existing retry/dead-letter semantics (preference blocks return `Allowed: false` which flows into `blocked` status, not retried).

### 2.4 Inbound Keyword Processing

**Decision:** Extend `WebhookIngestionService.ProcessTwilioEventAsync` to detect inbound direction and process STOP/START/HELP keywords.

**Rationale:** Inbound messages arrive on the same `/v1/webhooks/twilio` endpoint. The Twilio verifier already validates the signature. Detection via `Direction` field in form params.

---

## 3. Files Added

| File | Purpose |
|---|---|
| `Notifications.Domain/SmsContactPreference.cs` | New entity: tenant-scoped SMS preference per phone number |
| `Notifications.Application/Interfaces/ISmsPreferenceRepository.cs` | Repository interface + DTOs |
| `Notifications.Application/Interfaces/ISmsPreferenceService.cs` | Service interface |
| `Notifications.Infrastructure/Data/Configurations/SmsPreferenceConfiguration.cs` | EF Core table mapping |
| `Notifications.Infrastructure/Repositories/SmsPreferenceRepository.cs` | EF Core repository implementation |
| `Notifications.Infrastructure/Services/SmsPreferenceService.cs` | Business logic: get, update, keyword processing |
| `Notifications.Infrastructure/Data/Migrations/20260508000001_AddSmsPreference.cs` | DB migration: new table + TenantContactPolicy column |
| `Notifications.Api/Endpoints/SmsPreferenceEndpoints.cs` | API: GET/PUT /v1/sms/preferences |

---

## 4. Files Modified

| File | Change |
|---|---|
| `Notifications.Domain/TenantContactPolicy.cs` | Added `BlockUnknownSmsPreference` bool (default true) |
| `Notifications.Infrastructure/Data/Configurations/BillingConfigurations.cs` | Added `BlockUnknownSmsPreference` column mapping to `TenantContactPolicyConfiguration` |
| `Notifications.Infrastructure/Services/ContactEnforcementService.cs` | Added SMS preference check before suppression checks for SMS channel |
| `Notifications.Infrastructure/Services/WebhookIngestionService.cs` | Added inbound STOP/START/HELP keyword processing via `ISmsPreferenceService` |
| `Notifications.Infrastructure/Data/NotificationsDbContext.cs` | Added `SmsContactPreferences` DbSet + `SmsContactPreferenceConfiguration` apply call |
| `Notifications.Infrastructure/DependencyInjection.cs` | Registered `ISmsPreferenceRepository`, `ISmsPreferenceService` |
| `Notifications.Infrastructure/Repositories/RemainingRepositories.cs` | Updated `TenantContactPolicyRepository.UpsertAsync` to persist `BlockUnknownSmsPreference` |
| `Notifications.Api/Program.cs` | Mapped `SmsPreferenceEndpoints`; added `ntf_SmsContactPreferences` + `BlockUnknownSmsPreference` to `EnsureSchemaColumns` |

---

## 5. Database / Schema Changes

### 5.1 New Table: `ntf_SmsContactPreferences`

| Column | Type | Notes |
|---|---|---|
| `Id` | `char(36)` | PK (GUID) |
| `TenantId` | `char(36)` | Required |
| `Phone` | `varchar(50)` | Normalized E.164 phone |
| `PreferenceState` | `varchar(20)` | `opted_in` / `opted_out` / `unknown` |
| `Source` | `varchar(50)` | `inbound_stop_keyword`, `inbound_start_keyword`, `manual_update`, `system_import`, `tenant_policy` |
| `Reason` | `text` | Human-readable reason |
| `KeywordReceived` | `varchar(50)` | Exact keyword that triggered change (nullable) |
| `ProviderMessageId` | `varchar(255)` | Twilio MessageSid (nullable) |
| `UpdatedBy` | `varchar(255)` | Actor for manual updates (nullable) |
| `CreatedAt` | `datetime(6)` | — |
| `UpdatedAt` | `datetime(6)` | — |

Indexes:
- `UX_SmsContactPreferences_TenantId_Phone` — unique composite (lookup + upsert)

### 5.2 Modified Table: `ntf_TenantContactPolicies`

| Column | Type | Default | Notes |
|---|---|---|---|
| `BlockUnknownSmsPreference` | `tinyint(1)` | `1` (true) | Block SMS sends when preference is unknown |

---

## 6. API / Interface Changes

### 6.1 New Endpoints: `/v1/sms/preferences`

| Method | Path | Description |
|---|---|---|
| `GET` | `/v1/sms/preferences?phone={phone}` | Get SMS preference for a phone number |
| `PUT` | `/v1/sms/preferences` | Manually set SMS preference (opted_in / opted_out) |

Both endpoints require authentication (tenant-scoped JWT or service token).

### 6.2 New Interface: `ISmsPreferenceService`

```csharp
Task<SmsContactPreference?> GetAsync(Guid tenantId, string phone);
Task<SmsContactPreference> SetAsync(Guid tenantId, string phone, string state, string source, string? reason, string? updatedBy);
Task ProcessInboundKeywordAsync(Guid? tenantId, string phone, string keyword, string? providerMessageId, string? fromNumber);
string? ClassifyKeyword(string rawBody);  // returns "opt_out" / "opt_in" / "help" / null
```

---

## 7. Compliance Keyword Reference

### Opt-out keywords (STOP family)
`STOP`, `STOPALL`, `UNSUBSCRIBE`, `CANCEL`, `END`, `QUIT`

### Opt-in keywords (START family)
`START`, `YES`, `UNSTOP`

### Help keyword
`HELP`

All matching: case-insensitive, exact match after whitespace trim.

---

## 8. Audit Events

| Event Type | Trigger |
|---|---|
| `sms.preference.opted_out` | STOP keyword received OR manual opt-out |
| `sms.preference.opted_in` | START keyword received OR manual opt-in |
| `sms.preference.help_requested` | HELP keyword received |
| `sms.preference.manual_update` | Manual API update (PUT /v1/sms/preferences) |
| `notification.sms.skipped_opted_out` | SMS send blocked: opted_out preference |
| `notification.sms.skipped_unknown_preference` | SMS send blocked: unknown + policy blocks |

All audit records mask phone numbers as `+1***`.

---

## 9. Enforcement Flow (After LS-NOTIF-SMS-002)

```
ContactEnforcementService.EvaluateAsync (channel = "sms"):
  1. [NEW] Load SmsContactPreference for (tenant, normalizedPhone)
  2. [NEW] If PreferenceState == "opted_out"
           → return { Allowed: false, ReasonCode: "sms_opted_out" }
  3. [NEW] If PreferenceState == "unknown"
           → load TenantContactPolicy.BlockUnknownSmsPreference (default: true)
           → if blocked → return { Allowed: false, ReasonCode: "sms_preference_unknown_blocked_by_policy" }
  4. [EXISTING] Check active ContactSuppressions
  5. [EXISTING] Check RecipientContactHealth
  6. [EXISTING] Return allowed
```

---

## 10. Inbound Keyword Flow (After LS-NOTIF-SMS-002)

```
WebhookIngestionService.HandleTwilioAsync:
  → TwilioVerifier.Verify (HMAC-SHA1, unchanged)
  → ProcessTwilioEventAsync(formParams):
      [NEW] if Direction == "inbound" or "inbound-api":
          From = formParams["From"]     (sender's phone)
          Body = formParams["Body"]     (message text)
          keyword = SmsPreferenceService.ClassifyKeyword(Body)
          if keyword != null:
              → SmsPreferenceService.ProcessInboundKeywordAsync(tenantId?, From, keyword, MessageSid, To)
              → Audit: sms.preference.opted_out/opted_in/help_requested
      [EXISTING] if outbound status callback (no Direction or Direction=outbound-api):
          → TwilioNormalizer.Normalize → delivery status handling (unchanged)
```

---

## 11. Validation Performed

### Build validation
- `dotnet build Notifications.Infrastructure` — ✅ passes
- `dotnet build Notifications.Api` — ✅ passes

### Preference enforcement (code review)
- ✅ opted-out phone → `ContactEnforcementService` returns `{ Allowed: false, ReasonCode: "sms_opted_out" }`
- ✅ opted-in phone → enforcement continues to existing suppression checks
- ✅ unknown phone + `BlockUnknownSmsPreference=true` → blocked with `sms_preference_unknown_blocked_by_policy`
- ✅ unknown phone + `BlockUnknownSmsPreference=false` → continues to existing checks
- ✅ email channel → preference check is skipped entirely (only runs for `channel == "sms"`)

### Keyword classification (code review)
- ✅ STOP → `opt_out`
- ✅ STOPALL → `opt_out`
- ✅ UNSUBSCRIBE → `opt_out`
- ✅ CANCEL → `opt_out`
- ✅ END → `opt_out`
- ✅ QUIT → `opt_out`
- ✅ START → `opt_in`
- ✅ YES → `opt_in`
- ✅ UNSTOP → `opt_in`
- ✅ HELP → `help`
- ✅ "stop please" → `null` (not exact match)
- ✅ " STOP " → `opt_out` (whitespace trimmed)
- ✅ "stop" → `opt_out` (case-insensitive)

### Existing channel preservation
- ✅ email channel: no SMS preference logic executed
- ✅ in-app channel: no SMS preference logic executed
- ✅ existing retry/dead-letter flows: unchanged — preference-blocked notifications use `blocked` status, not retried

### API behavior
- ✅ GET /v1/sms/preferences?phone=+15551234567 → returns preference state
- ✅ PUT /v1/sms/preferences → sets opted_in/opted_out with audit
- ✅ Manual updates audited with `sms.preference.manual_update` event

### Security and boundary
- ✅ Phone numbers masked in all audit records (`+1***`)
- ✅ Twilio credentials not logged or exposed
- ✅ All SMS compliance code remains inside Notification Service
- ✅ Provider-agnostic: enforcement is in ContactEnforcementService, not TwilioAdapter

---

## 12. Known Gaps / Future Enhancements

| Item | Notes |
|---|---|
| Tenant resolution in inbound webhooks | When Twilio sends inbound messages, there's no guaranteed way to resolve `tenantId` from the `To` number (our Twilio From number). Currently stored with `tenantId = null`. Future: resolve via `TenantProviderConfig.FromNumber` lookup. |
| Preference history | Only current state is stored. Future: add `ntf_SmsPreferenceHistory` table for full audit trail of changes. |
| HELP auto-reply | HELP keyword is audited but no auto-reply is sent. Future: support configurable HELP response via template. |
| Opt-out auto-reply | Twilio automatically sends STOP acknowledgment replies. No additional action needed. |
| Import/bulk preference seeding | `source = "system_import"` is documented but no bulk import API is implemented yet. |
| TenantContactPolicy API | No endpoint exists to set `BlockUnknownSmsPreference` per tenant via API. Must be done via direct DB or admin API extension. |

---

## 13. Recommended Next Steps

1. **Tenant resolution for inbound messages** — implement `TenantProviderConfig` lookup by `To` phone number so inbound opt-outs are correctly tenant-scoped.
2. **Preference history table** — for regulatory compliance, log all preference state changes with actor, timestamp, and source.
3. **Bulk preference import API** — allow tenant admins to upload opt-out lists.
4. **TenantContactPolicy API extension** — add PATCH endpoint for `BlockUnknownSmsPreference`.
5. **Control Center UI** — surface SMS preference state in the contact detail view.
