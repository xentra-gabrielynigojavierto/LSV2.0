# LS-NOTIF-CORE-020 Report

## Summary

**Status**: Complete  
**Scope**: Canonical producer contract definition and producer alignment for the Notifications microservice platform.

The Notifications service is now a standalone, product-agnostic platform service with a well-defined canonical contract. All in-scope producers (Liens, Comms, Reports, Flow) have been aligned to submit canonical fields. A shared `NotificationsProducerRequest` type has been added to the `BuildingBlocks` shared library — referenced by all producer services — to reduce future drift.

Identity and CareConnect are documented as remaining gaps with explicit migration paths.

---

## Canonical Contract Implemented

### `POST /v1/notifications` — Canonical Producer Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `channel` | string | Yes | `email`, `sms`, `in-app`, `push`, `event`, `internal` |
| `recipient` | object | Yes | Generic recipient — see Recipient Model below |
| `productKey` | string | Recommended | Stable lowercase kebab-case product identifier. Replaces `productType`. |
| `eventKey` | string | Recommended | Stable business event identifier. Not channel/template-specific. |
| `sourceSystem` | string | Recommended | Originating service name. E.g. `"liens-service"`. |
| `correlationId` | string | Optional | Cross-service trace identifier. |
| `requestedBy` | string | Optional | Actor identity (userId or service name). |
| `templateKey` | string | Contextual | Template identifier. Separate concern from `eventKey`. |
| `templateData` | dict | Contextual | Render data only. Concise and deliberate. |
| `message` | object | Optional | Raw message content when not using templates. |
| `metadata` | object | Optional | Operational/context data. Not for secrets. |
| `idempotencyKey` | string | Optional | Deduplication key. |
| `priority` | string | Optional | `low`, `normal`, `high`, `critical`. |
| `severity` | string | Optional | Notification severity hint. |
| `category` | string | Optional | Classification tag. |
| `brandedRendering` | bool | Optional | Enable branding token injection. |
| `overrideSuppression` | bool | Optional | Bypass suppression list. |
| `overrideReason` | string | Optional | Required when `overrideSuppression=true`. |

**Backward-compat alias**: `productType` (legacy) → mapped to `productKey` at ingest. Existing producers using `productType` continue to work unchanged.

### Recipient Model

```json
{
  "email": "user@example.com",
  "userId": "...",
  "tenantId": "...",
  "phone": "...",
  "roleKey": "...",
  "orgId": "...",
  "mode": "UserId | Email | Role | Org",
  "cc": "...",
  "bcc": "..."
}
```

Recipients are generic. No product-specific assumptions. Fan-out is triggered when `mode = "Role" | "Org"` or when `recipient` is an array.

### Canonical Fields in Server Metadata

When `eventKey`, `sourceSystem`, `correlationId`, or `requestedBy` are submitted by a producer, the service merges them into `MetadataJson` (additive, no overwrite of existing metadata keys). This preserves them for observability and audit without requiring new DB columns.

---

## Producer Alignment Review

### Liens — `NotificationPublisher` ✅ Updated

**Before:**
- `channel = "event"` (hard-coded)
- `productType = "synqliens"` (non-canonical field name, LegalSynq-specific value)
- No `eventKey`, `sourceSystem`, `correlationId`
- Recipient: `{ tenantId }` (tenant-as-recipient, not a real recipient model)

**Changes:**
- `productKey = "liens"` (canonical field, generic value)
- `eventKey` = passed-in `notificationType` (e.g. `"lien.offer.submitted"`)
- `sourceSystem = "liens-service"`
- Recipient now uses `NotificationsRecipient` with `tenantId`

**Remaining debt:**
- `notificationType` is still passed as `eventKey` — callers should standardise to `kebab.case` event keys (e.g. `"lien.offer.submitted"` not `"lienoffer.submitted"`)
- `INotificationPublisher.PublishAsync` signature is preserved; callers in `LienOfferService`, `LienSaleService`, `LienTaskService` require no changes

---

### Comms — `NotificationsServiceClient` ✅ Updated

**Before:**
- `productType = "commss"` (**typo**: extra `s`) — would cause template resolution misses
- No `eventKey`, `sourceSystem`
- `templateKey` placed in metadata rather than as a top-level field

**Changes (`SendEmailAsync`):**
- `productKey = "comms"` (fixes typo, canonical field name)
- `eventKey = "comms.outbound_email"`
- `sourceSystem = "comms-service"`

**Changes (`SendOperationalAlertAsync`):**
- `productKey = "comms"`
- `eventKey = "comms.sla_alert.{triggerType}"`
- `sourceSystem = "comms-service"`

---

### Reports — `HttpEmailReportDeliveryAdapter` ✅ Updated

**Before:**
- `productType = "reports"` (non-canonical field name)
- No `eventKey`, `sourceSystem`, `idempotencyKey`
- Client-side retry loop (1s × attempt, linear backoff) duplicates server-side retry from LS-NOTIF-CORE-011

**Changes:**
- `productKey = "reports"`
- `eventKey = "report.delivery"`
- `sourceSystem = "reports-service"`
- `idempotencyKey = notificationId` (prevents duplicate delivery on retry)

**Remaining debt:**
- Client-side retry loop retained (removing it is out of scope for this task: "Do NOT redesign notification sending logic"). The server-side retry (LS-NOTIF-CORE-011) now handles failures after the initial attempt. The client-side loop provides fast-path retry before the async retry worker picks up the job.

---

### Flow — `HttpNotificationAdapter` ✅ Updated

**Before:**
- `POST /notifications` (missing `/v1/` prefix — 404 at runtime!)
- Posted raw `NotificationMessage` object (not the canonical contract shape)
- No `productKey`, `sourceSystem`, `correlationId`

**Changes:**
- `POST /v1/notifications` (fixed endpoint path)
- Maps `NotificationMessage` → `NotificationsProducerRequest` with all canonical fields
- `productKey = "flow"`, `sourceSystem = "flow-service"`
- `eventKey`, `correlationId` forwarded from `NotificationMessage.EventKey` and `NotificationMessage.Data["correlationId"]`

---

### Identity — `NotificationsEmailClient` ⚠️ NOT MIGRATED (Documented Gap)

**Current state:**
- Uses `POST /internal/send-email` with `{ to, subject, body, html }` payload
- Authenticates with `X-Internal-Service-Token` header
- No tenantId in password-reset or invite email calls

**Why not migrated:**
- `SendPasswordResetEmailAsync` and `SendInviteEmailAsync` are called from `AuthEndpoints` where no authenticated tenantId is available (pre-auth flow)
- Migrating to `/v1/notifications` requires surfacing tenantId at the call site, which touches the auth endpoint layer — out of scope for this feature
- The `/internal/send-email` route is a valid internal seam for pre-auth transactional email

**Recommended follow-up (LS-NOTIF-CORE-025 or similar):**
- Add an optional `tenantId` parameter to `SendPasswordResetEmailAsync` / `SendInviteEmailAsync`
- When tenantId is available, post to `/v1/notifications` with `productKey = "identity"`, `eventKey = "identity.password_reset"` / `"identity.user_invite"`, `sourceSystem = "identity-service"`
- Keep `/internal/send-email` as fallback when tenantId is absent

---

### CareConnect — `ReferralEmailService` / `NotificationService` ⚠️ NOT MIGRATED (Documented Gap)

**Current state:**
- CareConnect maintains its own internal `INotificationService`, `INotificationRepository`, and `CareConnectNotification` domain entity
- Sends email directly via `ISmtpEmailSender` (SMTP) with no dependency on the platform Notifications service
- `ReferralEmailService` builds its own referral token, HMAC-signs it, and dispatches via SMTP
- CareConnect's notification model tracks `AttemptCount`, `FailureReason`, and `ReferralEmailRetryWorker`

**Why not migrated:**
- CareConnect has a deeply integrated notification model specific to the referral workflow (token signing, retry policy, source tracking)
- Migrating SMTP delivery to the platform Notifications service requires mapping the `CareConnectNotification` domain model to the canonical contract — a larger refactor involving `ReferralEmailService`, `ReferralRetryPolicy`, and `ReferralEmailRetryWorker`
- Referral token generation is explicitly in-process and should remain so

**Recommended follow-up:**
- Introduce `INotificationsProducerClient` from BuildingBlocks into CareConnect
- Route referral email dispatch through the platform service, preserving token-in-template-data pattern
- Retain `CareConnectNotification` for status tracking, with the platform service as the delivery engine

---

## Shared Client / SDK Changes

### `BuildingBlocks.Notifications.NotificationsProducerRequest` (NEW)

**File:** `shared/building-blocks/BuildingBlocks/Notifications/NotificationsProducerRequest.cs`

All producer services reference `BuildingBlocks` already. The shared type provides:
- `NotificationsProducerRequest` — strongly-typed canonical DTO for `POST /v1/notifications`
- `NotificationsRecipient` — generic recipient model replacing ad-hoc anonymous types
- XML documentation on all fields to guide future producers
- `[JsonPropertyName]` attributes for consistent camelCase serialisation

**Usage by producers:** Instead of constructing anonymous `new { channel = ..., productKey = ... }` objects, producers can `JsonContent.Create(new NotificationsProducerRequest { ... })` and benefit from compile-time checking.

---

## Files Changed

| File | Change |
|---|---|
| `apps/services/notifications/Notifications.Application/DTOs/NotificationDtos.cs` | Added `ProductKey`, `EventKey`, `SourceSystem`, `CorrelationId`, `RequestedBy`, `Priority` to `SubmitNotificationDto` |
| `apps/services/notifications/Notifications.Infrastructure/Services/NotificationService.cs` | Use `ProductKey ?? ProductType` for template/branding resolution; merge canonical context into `MetadataJson`; propagate new fields in `ClonePerRecipient` and `PersistFanOutParentAsync` |
| `shared/building-blocks/BuildingBlocks/Notifications/NotificationsProducerRequest.cs` | NEW — canonical producer request type + generic recipient model |
| `apps/services/liens/Liens.Infrastructure/Notifications/NotificationPublisher.cs` | Updated to use canonical fields (`productKey`, `eventKey`, `sourceSystem`) |
| `apps/services/comms/Comms.Infrastructure/Notifications/NotificationsServiceClient.cs` | Fixed `productType = "commss"` typo → `productKey = "comms"`; added `eventKey`, `sourceSystem` |
| `apps/services/reports/src/Reports.Infrastructure/Adapters/HttpEmailReportDeliveryAdapter.cs` | Added `productKey`, `eventKey`, `sourceSystem`, `idempotencyKey` |
| `apps/services/flow/backend/src/Flow.Infrastructure/Adapters/HttpNotificationAdapter.cs` | Fixed endpoint URL `/notifications` → `/v1/notifications`; maps `NotificationMessage` to canonical request |

---

## Validation Performed

| Project | Result |
|---|---|
| `Notifications.Api` | ✅ 0 errors |
| `Liens.Infrastructure` | ✅ 0 errors |
| `Comms.Infrastructure` | ✅ 0 errors |
| `Reports.Infrastructure` | ✅ 0 errors |
| `Flow.Infrastructure` | ✅ 0 errors |
| `BuildingBlocks` | ✅ 0 errors |

---

## Remaining Gaps

1. **Identity** — `NotificationsEmailClient` still uses `/internal/send-email`. Requires surfacing tenantId at the pre-auth call site.
2. **CareConnect** — Has its own internal notification system with direct SMTP. Referral email flow needs a dedicated migration task.
3. **Liens eventKey casing** — Callers pass `"lienoffer.submitted"` style; should be `"lien.offer.submitted"` (kebab.dot convention). Requires updating `LienOfferService`, `LienSaleService`, `LienTaskService` call sites.
4. **Reports client-side retry** — Retained; redundant with server-side retry from LS-NOTIF-CORE-011. Should be removed in a future cleanup once confidence in server-side retry is established.
5. **CareConnect `productType` in DB** — Currently `Category` field is reused as the `productKey` filter in list/stats queries. A dedicated `ProductKey` column on the `Notification` domain entity would make this explicit. Out of scope (requires migration).

---

## Risks / Follow-Up Recommendations

| Risk | Severity | Mitigation |
|---|---|---|
| `productType = "commss"` typo was sending wrong product key to template resolution for months | **High** | Fixed in this task. If templates were named with `"commss"`, they must be renamed to `"comms"`. Verify template keys in Notifications DB. |
| Flow `POST /notifications` was hitting a 404 silently (fallback to logging adapter) | **High** | Fixed in this task. Flow notifications were never delivered via the platform service. Verify after deployment. |
| Identity `X-Internal-Service-Token` is a pre-auth bypass — not aligned with canonical auth | Medium | Document and track. Fine for internal transactional email. |
| CareConnect SMTP sends bypass all platform policies (suppression, rate limits, branding) | Medium | Track as follow-up. |
| `BuildingBlocks` `NotificationsProducerRequest` is a DTO, not an HTTP client — producers still wire their own `IHttpClientFactory` | Low | Acceptable for now. A full `INotificationsProducerClient` implementation can be added when three or more services adopt it uniformly. |
