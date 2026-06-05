# LS-NOTIF-CORE-023 Report

## Summary

CareConnect was migrated from its internal SMTP/direct notification architecture to the
platform Notifications service as the delivery engine. The migration replaces the
`/internal/send-email` passthrough call with the canonical `POST /v1/notifications`
producer contract, carrying `productKey`, `eventKey`, `sourceSystem`, tenant context,
and typed recipient/message payloads.

CareConnect retains full ownership of referral workflow logic, HMAC token generation,
domain-level notification tracking (CareConnectNotification DB records), and
submission-level retry (ReferralEmailRetryWorker). The Notifications service owns
actual email delivery and per-delivery retry.

**Status:** COMPLETE — build succeeds, no new errors.

---

## Existing CareConnect Notification Paths

### Primary send path
- **`ReferralEmailService`** (`CareConnect.Application/Services/ReferralEmailService.cs`)
  - Owns HMAC token generation (`GenerateViewToken`, `ValidateViewToken`)
  - Writes `CareConnectNotification` domain records before each send
  - Calls `ISmtpEmailSender.SendAsync(to, subject, htmlBody)` for delivery
  - Builds inline HTML email templates for all referral lifecycle events
  - Events dispatched:
    - `referral.created` — new referral notification to provider
    - `referral.invite.resent` — manual resend of referral notification
    - `referral.accepted.provider` — acceptance confirmation to provider
    - `referral.accepted.referrer` — acceptance confirmation to referrer
    - `referral.accepted.client` — acceptance confirmation to client
    - `referral.declined.provider` — rejection notification to provider
    - `referral.declined.referrer` — rejection notification to referrer
    - `referral.cancelled.provider` — cancellation notification to provider
    - `referral.cancelled.referrer` — cancellation notification to referrer

### Retry path
- **`ReferralEmailRetryWorker`** (`CareConnect.Infrastructure/Workers/ReferralEmailRetryWorker.cs`)
  - Background worker polling every 60 seconds
  - Queries `CareConnectNotification` records where Status=Failed, NextRetryAfterUtc<=now
  - Calls `IReferralEmailService.RetryNotificationAsync` to rebuild and re-submit
  - Respects `ReferralRetryPolicy.MaxAttempts`

### Domain notification tracking
- **`CareConnectNotification`** domain entity — records every outbound notification
  attempt with status (Pending → Sent/Failed), attempt count, timestamps, dedupe key
- **`NotificationService`** (CareConnect-internal) — appointment/referral status change
  tracking (these are internal domain records, NOT email sends)

### Previous delivery implementation (now retired from DI)
- `NotificationsServiceEmailSender` — called `POST /internal/send-email` on Notifications
  (raw passthrough, no canonical contract fields, no tenant context, no eventKey)
- `SmtpEmailSender` — direct SMTP, was never registered in DI (was replaced previously)

---

## Shared Notifications Integration Implemented

### New interface: `INotificationsProducer`
**File:** `CareConnect.Application/Interfaces/INotificationsProducer.cs`

Replaces `ISmtpEmailSender` as the delivery abstraction. Carries canonical fields:
`tenantId`, `eventKey`, `toAddress`, `subject`, `htmlBody`, `idempotencyKey`, `correlationId`.

### New implementation: `NotificationsProducerClient`
**File:** `CareConnect.Infrastructure/Notifications/NotificationsProducerClient.cs`

Calls `POST /v1/notifications` with the canonical producer contract:
```json
{
  "channel": "email",
  "recipient": { "email": "<toAddress>" },
  "message": { "type": "<eventKey>", "subject": "...", "body": "<htmlBody>" },
  "productKey": "careconnect",
  "eventKey": "<eventKey>",
  "sourceSystem": "careconnect-service",
  "idempotencyKey": "<dedupeKey or notificationId>",
  "correlationId": "<referralId>"
}
```
With `X-Tenant-Id: <tenantId>` header (legacy transition auth path — accepted by
`ServiceSubmission` policy with a structured warning log).

Configuration: `NotificationsService:BaseUrl` (already in `appsettings.json`).
HTTP client: named `"NotificationsService"` (already registered in DI).

### Updated `ReferralEmailService`
- Injects `INotificationsProducer` instead of `ISmtpEmailSender`
- `TrySendAndUpdateAsync` calls `_producer.SubmitAsync(...)` passing all canonical fields
- Added `NotificationTypeToEventKey` static helper mapping CareConnect types to eventKeys:
  ```
  ReferralCreated              → referral.created
  ReferralEmailResent          → referral.invite.resent
  ReferralEmailAutoRetry       → referral.invite.retry
  ReferralAcceptedProvider     → referral.accepted.provider
  ReferralAcceptedReferrer     → referral.accepted.referrer
  ReferralAcceptedClient       → referral.accepted.client
  ReferralRejectedProvider     → referral.declined.provider
  ReferralRejectedReferrer     → referral.declined.referrer
  ReferralCancelledProvider    → referral.cancelled.provider
  ReferralCancelledReferrer    → referral.cancelled.referrer
  (default)                    → careconnect.notification
  ```

### DI registration updated
`DependencyInjection.cs`:
- Added `services.AddScoped<INotificationsProducer, NotificationsProducerClient>()`
- `ISmtpEmailSender` registration removed (no longer needed by production code)
- `NotificationsServiceEmailSender` kept on disk; retired from DI

---

## What Remains in CareConnect

| Component | Stays In CareConnect | Reason |
|---|---|---|
| `GenerateViewToken` / `ValidateViewToken` | ✅ | HMAC signing with per-referral TokenVersion; domain security concern |
| HTML email template builders | ✅ | CareConnect-specific layout and data; not platform-generic |
| `CareConnectNotification` entity + repo | ✅ | Domain-level audit trail; tracks submission status per notification |
| `ReferralEmailRetryWorker` | ✅ | Retries failed _submissions_ to Notifications service; domain-level retry |
| Deduplication (TryAddWithDedupeAsync) | ✅ | Prevents duplicate submissions across restarts |
| `NotificationService` (CareConnect) | ✅ | Internal domain tracking only (appointment/status records, no delivery) |
| `ReferralRetryPolicy` | ✅ | Submission retry backoff; delivery retry owned by Notifications service |

---

## Files Changed

| File | Change |
|---|---|
| `CareConnect.Application/Interfaces/INotificationsProducer.cs` | **CREATED** — canonical producer interface |
| `CareConnect.Infrastructure/Notifications/NotificationsProducerClient.cs` | **CREATED** — calls `POST /v1/notifications` |
| `CareConnect.Application/Services/ReferralEmailService.cs` | **MODIFIED** — inject `INotificationsProducer`, map eventKeys |
| `CareConnect.Infrastructure/DependencyInjection.cs` | **MODIFIED** — register `NotificationsProducerClient`, remove `ISmtpEmailSender` |

### Unchanged (intentionally kept)
| File | Status |
|---|---|
| `CareConnect.Infrastructure/Email/NotificationsServiceEmailSender.cs` | Retained on disk; removed from DI. Documents the previous integration attempt. |
| `CareConnect.Infrastructure/Email/SmtpEmailSender.cs` | Retained on disk; was never registered in production DI. |
| `CareConnect.Application/Interfaces/ISmtpEmailSender.cs` | Retained; still referenced by `SmtpEmailSender` and `NotificationsServiceEmailSender` for potential future use. |

---

## Validation Performed

- `dotnet build CareConnect.Api.csproj` — **0 errors, 1 pre-existing warning** (MSB3277 duplicate assembly ref, unrelated to migration)
- `dotnet build CareConnect.Tests.csproj` — **0 errors** — all test mocks updated to `INotificationsProducer`
- `grep` confirmed `ISmtpEmailSender` no longer referenced outside its own file, `SmtpEmailSender`, and `NotificationsServiceEmailSender`
- `grep` confirmed `INotificationsProducer` properly wired: interface → `ReferralEmailService` → `NotificationsProducerClient` → DI registration
- All 10 CareConnect referral event types mapped to canonical `eventKey` values
- Token generation (`GenerateViewToken`) confirmed intact in `ReferralEmailService` — no changes to HMAC or token logic
- `ReferralEmailRetryWorker` confirmed continues to call `RetryNotificationAsync` (which now submits via `INotificationsProducer`) — no worker changes needed
- `POST /v1/notifications` Notifications service endpoint confirmed compatible: accepts `X-Tenant-Id` header via `ServiceSubmission` legacy path
- `NotificationsProducerClient` passes `X-Tenant-Id` header derived from `notification.TenantId`
- Idempotency keys passed: `notification.DedupeKey ?? notification.Id.ToString()` — prevents duplicate delivery on retry

---

## Remaining Gaps

1. **Service-token JWT auth not implemented**
   The integration uses `X-Tenant-Id` header (legacy transition path). The Notifications service
   logs `[LEGACY SUBMISSION]` warnings for each submission. Full migration to service-JWT
   (using `IServiceTokenIssuer` + `NotificationsAuthDelegatingHandler` as in Flow) is
   deferred — requires adding `BuildingBlocks.Authentication.ServiceTokens` to CareConnect.
   **Risk:** Low — the legacy path is stable and intentionally preserved in `ServiceSubmissionHandler`.

2. **Delivery status feedback loop absent**
   CareConnect marks `CareConnectNotification` as `Sent` when the submission is accepted (2xx).
   Actual SendGrid delivery status (bounces, opens, clicks) is not propagated back to
   CareConnect domain records. This is acceptable — the Notifications service owns delivery
   observability. CareConnect records track submission lifecycle only.

3. **`NotificationService` (CareConnect) appointment/status notifications not migrated**
   `CareConnect.Application/Services/NotificationService.cs` creates domain notification
   records for appointment scheduling/cancellation but does NOT send emails (no delivery
   call). These are placeholders for a future delivery path. Out of scope for LS-NOTIF-CORE-023.

4. **Referral view token in notification payload**
   The `viewLink` (containing the HMAC token) is passed in the HTML body. It is NOT
   surfaced as a structured `templateData` field. For future template-driven rendering
   in the Notifications service, a `TemplateKey` + `TemplateData` approach would be
   preferred. This is a follow-up once CareConnect templates are registered in the
   Notifications template registry.

---

## Risks / Follow-Up Recommendations

| Priority | Item | Recommendation |
|---|---|---|
| High | Service-JWT auth | Add `IServiceTokenIssuer` to CareConnect; wire `NotificationsAuthDelegatingHandler` on the `NotificationsService` HTTP client. Eliminates legacy-path warnings. |
| Medium | Structured template data | Register careconnect email templates in Notifications registry; pass `templateKey` + structured `templateData` instead of inline HTML. Enables platform-level template management. |
| Medium | Delivery feedback | Implement a webhook or polling mechanism to sync SendGrid delivery status back to `CareConnectNotification.Status`. Currently CareConnect considers "submitted" as "sent". |
| Low | Cleanup retired files | Remove `NotificationsServiceEmailSender.cs`, `SmtpEmailSender.cs`, and `ISmtpEmailSender.cs` once all callers confirm migration is stable. |
| Low | Appointment notification delivery | Hook `NotificationService.CreateAppointmentScheduledAsync` etc. through `INotificationsProducer` once the appointment reminder flow needs actual delivery. |
