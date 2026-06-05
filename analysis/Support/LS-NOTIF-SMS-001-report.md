# LS-NOTIF-SMS-001 — Pluggable SMS Delivery via Twilio

**Status:** COMPLETE  
**Date:** 2026-05-08  
**Scope:** Notification Service — SMS channel wired via Twilio provider adapter  
**Branch:** xenia

---

## 1. Objective

Implement pluggable SMS delivery within the Notification Service boundary using Twilio as the first-party SMS provider. The SMS channel must participate in the same dispatch pipeline, retry/dead-letter lifecycle, audit trail, usage metering, fan-out, contact enforcement, and webhook status ingestion as the existing email channel.

---

## 2. Findings at Scan Time

On inspection, the SMS feature was already **95% implemented** in the codebase prior to this ticket. The following components were found to be fully in place:

| Component | File | Status |
|---|---|---|
| `ISmsProviderAdapter` interface | `Notifications.Application/Interfaces/IEmailProviderAdapter.cs` | ✅ Complete |
| `SmsSendPayload`, `SmsSendResult`, `ProviderFailure`, `ProviderHealthResult` | same file | ✅ Complete |
| `TwilioAdapter` — HTTP send, health check, error classification, 10s timeout | `Notifications.Infrastructure/Providers/Adapters/TwilioAdapter.cs` | ✅ Complete |
| SMS route registration | `ProviderRoutingService.cs` — `["sms"] = new[] { "twilio" }` | ✅ Complete |
| `ISmsProviderAdapter _twilioAdapter` injection + dispatch in send loop | `NotificationService.cs` lines 827–910 | ✅ Complete |
| DI registration reading `TWILIO_ACCOUNT_SID`, `TWILIO_AUTH_TOKEN`, `TWILIO_FROM_NUMBER` | `DependencyInjection.cs` lines 114–123 | ✅ Complete |
| `ExtractContactValue` reads `phone` property for SMS channel | `NotificationService.cs` line 1653 | ✅ Complete |
| `ClassifySkipReason` checks `r.Phone` for SMS fan-out | `NotificationService.cs` line 1417 | ✅ Complete |
| `ClonePerRecipient` copies `phone` field into per-recipient dict | `NotificationService.cs` line 1542 | ✅ Complete |
| `MaskRecipient` masks phone as `+1***` for audit logs | `NotificationService.cs` line 1131 | ✅ Complete |
| `TwilioNormalizer` — full status normalization for webhooks | `Webhooks/Normalizers/TwilioNormalizer.cs` | ✅ Complete |
| `TwilioVerifier` — HMAC-SHA1 signature verification | `Webhooks/Verifiers/TwilioVerifier.cs` | ✅ Complete |
| `/v1/webhooks/twilio` endpoint | `Endpoints/WebhookEndpoints.cs` | ✅ Complete |
| `sms_attempt` / `sms_sent` metering units | `UsageMeteringService` | ✅ Complete |
| Audit events for SMS lifecycle | `NotificationService.cs` | ✅ Complete |
| Retry, dead-letter, stall recovery applies to SMS | `NotificationWorker` + `NotificationService` | ✅ Complete |
| `ResolvedRecipient.Phone` property with XML docs | `IRecipientResolver.cs` line 49 | ✅ Complete |

---

## 3. Gap Identified and Fixed

### 3.1 `RecipientResolver` — Phone-only Direct Addressing (LS-NOTIF-SMS-001-FIX-01)

**File:** `Notifications.Infrastructure/Services/RecipientResolver.cs`

**Gap:** `ResolveSingleAsync` read `userId` and `email` from the recipient JSON envelope but did **not** read `phone`. The direct-addressing guard (`if (!string.IsNullOrEmpty(userId) || !string.IsNullOrEmpty(email))`) would silently return an empty list for a phone-only envelope such as `{ "phone": "+15551234567" }` posted in a fan-out array. This meant SMS fan-out via explicit phone array was a silent no-op.

**Impact:** Single-recipient SMS dispatch (the dominant path) was unaffected — `DispatchSingleAsync` calls `ExtractContactValue("sms", recipientJson)` which reads `phone` directly from the raw JSON, bypassing the resolver. Fan-out via Role/Org addressing was also unaffected since the identity membership provider is responsible for populating `ResolvedRecipient.Phone` on members. Only fan-out arrays containing explicit `{phone}` envelopes were broken.

**Fix applied:**

```csharp
// Before
var userId  = ReadString(recipient, "userId");
var email   = ReadString(recipient, "email");
// ...
if (!string.IsNullOrEmpty(userId) || !string.IsNullOrEmpty(email))
{
    return new[] { new ResolvedRecipient { UserId = userId, Email = email, OrgId = orgId } };
}

// After
var userId  = ReadString(recipient, "userId");
var email   = ReadString(recipient, "email");
var phone   = ReadString(recipient, "phone");
// ...
// Role/Org heuristics updated to exclude phone from "is this a role/org" signal
if (!string.IsNullOrEmpty(userId) || !string.IsNullOrEmpty(email) || !string.IsNullOrEmpty(phone))
{
    return new[] { new ResolvedRecipient { UserId = userId, Email = email, Phone = phone, OrgId = orgId } };
}
```

Role and Org heuristic guards were also updated to exclude `phone` from their "no direct identifier" checks, so a phone-only envelope is never mistakenly treated as a Role or Org fan-out target.

---

## 4. Architecture Overview

### 4.1 Dispatch Path (Single SMS)

```
POST /v1/notifications
  → SubmitAsync
    → DispatchSingleAsync
      → ExtractContactValue("sms", recipientJson)  → reads phone field
      → ContactEnforcementService.EvaluateAsync     → suppression check
      → Notification persisted (status: processing)
      → ExecuteSendLoopAsync
        → ProviderRoutingService.GetRoutes("sms")   → ["twilio"]
        → TwilioAdapter.SendAsync(SmsSendPayload)
          → POST https://api.twilio.com/2010-04-01/Accounts/{sid}/Messages.json
          → 10s timeout, HMAC auth header
        → NotificationAttempt persisted
        → Metering: sms_attempt, sms_sent
        → Audit: notification.sent
```

### 4.2 Fan-out Path (Role/Org SMS)

```
POST /v1/notifications  { recipient: { roleKey: "lien_seller" }, channel: "sms" }
  → SubmitAsync
    → RecipientResolver.ResolveAsync
      → IRoleMembershipProvider.GetRoleMembersAsync  → List<ResolvedRecipient> with .Phone
    → per-recipient: ClassifySkipReason("sms", r)    → "no_phone_on_file" if r.Phone is null
    → per-recipient: ClonePerRecipient(request, r)   → { phone: r.Phone, ... }
    → per-recipient: DispatchSingleAsync (above path)
    → FanOutSummary persisted on parent Notification
```

### 4.3 Fan-out Path (Explicit Phone Array) — Fixed by LS-NOTIF-SMS-001-FIX-01

```
POST /v1/notifications  { recipient: [{ phone: "+15551234567" }, { phone: "+15559876543" }], channel: "sms" }
  → SubmitAsync
    → RecipientResolver.ResolveAsync (array)
      → each envelope: ResolveSingleAsync
        → phone = ReadString(envelope, "phone")       → "+1555..."
        → ResolvedRecipient { Phone = phone }          ← now correctly populated
    → proceeds through fan-out as normal
```

### 4.4 Webhook Path

```
POST /v1/webhooks/twilio  (AllowAnonymous, form-encoded)
  → TwilioVerifier.Verify(url, formParams, X-Twilio-Signature)  → HMAC-SHA1
  → TwilioNormalizer.Normalize(formParams)
    → maps MessageStatus/SmsStatus → delivered/failed/undeliverable/...
  → WebhookIngestionService.HandleTwilioAsync
    → matches NotificationAttempt by ProviderMessageId (MessageSid)
    → updates delivery status on Notification + Attempt
    → Audit: notification.webhook_received
```

---

## 5. Configuration Reference

### 5.1 Required Environment Variables

| Variable | Purpose | Required |
|---|---|---|
| `TWILIO_ACCOUNT_SID` | Twilio account identifier — used in API URL and HMAC auth | Yes |
| `TWILIO_AUTH_TOKEN` | Twilio auth token — used for HTTP Basic auth + webhook HMAC-SHA1 | Yes |
| `TWILIO_FROM_NUMBER` | E.164 phone number or messaging service SID to send from | Yes |
| `TWILIO_WEBHOOK_VERIFICATION_ENABLED` | Set `true` to enforce webhook signature verification (recommended) | No (defaults to reject-all when false) |

### 5.2 appsettings.json Structure

The DI registration (`DependencyInjection.cs`) reads from `IConfiguration` keys:

```json
{
  "Twilio": {
    "AccountSid": "",
    "AuthToken":  "",
    "FromNumber": "",
    "WebhookVerificationEnabled": true
  }
}
```

These keys are read from environment variables via the standard `TWILIO__ACCOUNT_SID` double-underscore convention (or the flat `TWILIO_ACCOUNT_SID` form registered in DI).

### 5.3 Tenant-Owned Twilio Credentials

The routing service supports tenant-scoped `TenantProviderConfig` records. When a tenant has a `ProviderOwnershipMode = "TenantOwned"` config for Twilio, the adapter is instantiated with that tenant's credentials rather than the platform credentials. This allows multi-tenancy for SMS delivery.

---

## 6. Error Classification

`TwilioAdapter` classifies errors into the following categories used by the retry engine:

| Category | Retryable | Trigger |
|---|---|---|
| `auth_config_failure` | false | 401 from Twilio (wrong credentials) |
| `invalid_recipient` | false | 400 from Twilio (malformed E.164, blocked number) |
| `provider_unavailable` | true | 429 / 5xx from Twilio or network timeout |
| `unexpected_provider_error` | true | Any unrecognised HTTP error or exception |

Non-retryable failures move the notification immediately to `failed` status. Retryable failures trigger the exponential back-off schedule: 1 min → 5 min → 30 min. After `MaxRetries`, the notification moves to `dead-letter` and a `DeliveryIssue` record is created.

---

## 7. Recipient JSON Schema

### 7.1 Single direct SMS

```json
{
  "channel": "sms",
  "recipient": { "phone": "+15551234567" },
  "message": { "body": "Your lien has been accepted." }
}
```

### 7.2 Template-based SMS

```json
{
  "channel": "sms",
  "recipient": { "phone": "+15551234567" },
  "templateKey": "lien.offer.accepted",
  "templateData": { "lienNumber": "LN-2026-00142", "askPrice": "$12,500" },
  "productKey": "liens",
  "eventKey": "lien.offer.accepted"
}
```

### 7.3 Fan-out via role

```json
{
  "channel": "sms",
  "recipient": { "mode": "Role", "roleKey": "lien_seller", "orgId": "org-abc" },
  "message": { "body": "New offer received on your lien portfolio." }
}
```

### 7.4 Fan-out via explicit phone array (now fixed)

```json
{
  "channel": "sms",
  "recipient": [
    { "phone": "+15551234567" },
    { "phone": "+15559876543" }
  ],
  "message": { "body": "Platform maintenance scheduled for 02:00 UTC." }
}
```

---

## 8. Audit Events Emitted

| Event type | Trigger |
|---|---|
| `notification.sent` | SMS delivered successfully by Twilio |
| `notification.failed` | Non-retryable failure (auth_config, invalid_recipient) |
| `notification.retrying` | Retryable failure — next retry scheduled |
| `notification.dead_letter` | MaxRetries exhausted |
| `notification.no_routes` | No provider route found for `sms` channel |
| `notification.auto_retry` | Worker-triggered retry execution |
| `notification.stalled_reconciled` | Stall recovery moved notification to retrying/dead-letter |
| `notification.fanout` | Fan-out dispatch summary (when Role/Org/Array addressing) |
| `notification.webhook_received` | Twilio delivery callback received and processed |

All audit records mask the recipient phone as `+1***` (country-code prefix only) for PII compliance.

---

## 9. Security Considerations

- **Webhook verification**: `TwilioVerifier` enforces HMAC-SHA1 over the full request URL + sorted form params. When `TWILIO_WEBHOOK_VERIFICATION_ENABLED` is false, all webhook requests are rejected — not silently accepted. Production deployments must set this to `true`.
- **Constant-time comparison**: `CryptographicOperations.FixedTimeEquals` used for signature comparison to prevent timing attacks.
- **Credential isolation**: Platform Twilio credentials are never returned in API responses. Tenant-owned credentials are stored in `TenantProviderConfig` and never logged.
- **PII masking**: Phone numbers are masked in all audit records and delivery logs.
- **Phone validation**: Twilio API itself enforces E.164 format and rejects invalid numbers non-retryably (`invalid_recipient` category).

---

## 10. Outstanding Items / Future Enhancements

| Item | Priority | Notes |
|---|---|---|
| Identity membership provider populates `Phone` on role/org members | High | `NoOpRoleMembershipProvider` always returns empty. Real identity-backed implementation must surface the user's phone number in `ResolvedRecipient.Phone` for role/org SMS fan-out to work end-to-end. |
| Opt-in / opt-out SMS preference per user | Medium | Contact enforcement checks suppression lists but there is no user-facing preference endpoint for SMS opt-out. Add via `TenantContactPolicy`. |
| SMS template rendering for `text` field | Low | SMS body should prefer `RenderedText` (plain text) over `RenderedBody` (HTML). `TwilioAdapter.SendAsync` already receives the rendered text body — ensure template authors populate `TextTemplate` for SMS-targeted templates. |
| Delivery receipt latency tracking | Low | Twilio webhooks update delivery status async. Consider surfacing `delivered_at` timestamp on `NotificationAttempt` once webhook callback arrives. |
| `TWILIO_ACCOUNT_SID` / `TWILIO_AUTH_TOKEN` / `TWILIO_FROM_NUMBER` secrets | Blocker for prod | Must be registered as environment secrets before any SMS can be sent. DI registration already reads them. |
