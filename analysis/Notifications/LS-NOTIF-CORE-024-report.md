# LS-NOTIF-CORE-024 Report — Identity Migration to Canonical Notifications

**Status:** COMPLETE
**Date:** 2026-04-19
**Author:** Platform Engineering

---

## Summary

The Identity service was sending transactional emails (password reset, user invitation) by calling
`POST /internal/send-email` on the Notifications service — a raw passthrough endpoint with no event
taxonomy, no idempotency, no tenant routing, and no producer identity.

This migration replaces that path with `POST /v1/notifications` using the canonical producer contract:
- `productKey = "identity"`, `sourceSystem = "identity-service"`
- `eventKey = identity.user.password.reset` / `identity.user.invite.sent`
- Service-JWT auth via `NotificationsAuthDelegatingHandler` + `IServiceTokenIssuer`
- `X-Tenant-Id` header passed on every request (required for service token minting and tenant routing)
- Idempotency key per email (prevents duplicates on retry)

Token generation, link construction, and inline HTML rendering remain in Identity — those are
domain concerns, not Notifications service concerns.

---

## Identity Email Flows

### Flow 1 — Password Reset (`identity.user.password.reset`)

**Trigger:** Admin calls `POST /api/admin/users/{id}/reset-password`

**Business logic (stays in Identity):**
1. Revoke any existing pending reset tokens for the user
2. Generate a 32-byte cryptographically random token → SHA-256 hash stored in DB
3. Construct reset link: `{PortalBaseUrl}/reset-password?token={base64url(rawToken)}`
4. Call `INotificationsEmailClient.SendPasswordResetEmailAsync(...)` → deliver email

**Before:** `POST /internal/send-email` with inline `{ to, subject, body, html }` payload
**After:** `POST /v1/notifications` with canonical producer contract, eventKey = `identity.user.password.reset`

### Flow 2 — User Invitation (`identity.user.invite.sent`)

**Trigger:** Admin calls `POST /api/admin/users/invite` (new invite) or `POST /api/admin/users/{id}/resend-invite` (resend)

**Business logic (stays in Identity):**
1. Create / revoke existing invitation record; generate invitation token
2. Construct activation link: `{PortalBaseUrl}/accept-invite?token={rawToken}`
3. Call `INotificationsEmailClient.SendInviteEmailAsync(...)` → deliver email

**Before:** `POST /internal/send-email` with inline `{ to, subject, body, html }` payload
**After:** `POST /v1/notifications` with canonical producer contract, eventKey = `identity.user.invite.sent`

---

## Notifications Integration

### Auth Migration

| Before                              | After                                    |
|-------------------------------------|------------------------------------------|
| `X-Internal-Service-Token: <secret>` | `Authorization: Bearer <service-JWT>`   |
| `/internal/send-email` (no auth gate) | `/v1/notifications` (ServiceSubmission policy) |
| `NotificationsAuthDelegatingHandler` NOT wired | `NotificationsAuthDelegatingHandler` wired to `"NotificationsService"` HTTP client |

The `NotificationsAuthDelegatingHandler` reads `X-Tenant-Id` from the outgoing request,
mints a short-lived HS256 service JWT via `IServiceTokenIssuer`, and injects
`Authorization: Bearer <token>`. When `FLOW_SERVICE_TOKEN_SECRET` is not configured (dev),
the handler is a no-op and the Notifications service accepts the request in legacy X-Tenant-Id mode.

### Request Payload (Password Reset)

```json
{
  "channel": "email",
  "productKey": "identity",
  "eventKey": "identity.user.password.reset",
  "sourceSystem": "identity-service",
  "idempotencyKey": "<guid>",
  "recipient": { "email": "user@example.com", "tenantId": "<tenantId>" },
  "message": {
    "type": "identity.user.password.reset",
    "subject": "Reset your LegalSynq password",
    "body": "<html>...</html>"
  },
  "templateData": {
    "displayName": "Jane Doe",
    "resetLink": "https://portal.legalsynq.com/reset-password?token=...",
    "subject": "Reset your LegalSynq password"
  },
  "metadata": { "tenantId": "<tenantId>" }
}
```

### Request Payload (Invitation)

```json
{
  "channel": "email",
  "productKey": "identity",
  "eventKey": "identity.user.invite.sent",
  "sourceSystem": "identity-service",
  "idempotencyKey": "<guid>",
  "recipient": { "email": "user@example.com", "tenantId": "<tenantId>" },
  "message": {
    "type": "identity.user.invite.sent",
    "subject": "You've been invited to LegalSynq",
    "body": "<html>...</html>"
  },
  "templateData": {
    "displayName": "Jane Doe",
    "activationLink": "https://portal.legalsynq.com/accept-invite?token=...",
    "subject": "You've been invited to LegalSynq"
  },
  "metadata": { "tenantId": "<tenantId>" }
}
```

---

## EventKey Usage

| Flow             | EventKey                        | TemplateKey (catalog)            |
|------------------|---------------------------------|----------------------------------|
| Password Reset   | `identity.user.password.reset`  | `identity-password-reset-email`  |
| User Invitation  | `identity.user.invite.sent`     | `identity-invite-email`          |

Both keys are registered in `NotificationTaxonomy.Identity.Events` (LS-NOTIF-CORE-022).
No `templateKey` is sent in the request — Notifications falls through to inline HTML rendering
from the `message.body` field until tenant-specific templates are registered in the catalog.

---

## Files Changed

| File                                                                                              | Change                                                                |
|---------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------|
| `apps/services/identity/Identity.Infrastructure/Services/NotificationsEmailClient.cs`             | Replaced `/internal/send-email` with `POST /v1/notifications`; added `tenantId` param; service-JWT auth |
| `apps/services/identity/Identity.Infrastructure/DependencyInjection.cs`                           | Added `AddServiceTokenIssuer("identity")`; `NotificationsAuthDelegatingHandler`; configured HTTP client BaseAddress + handler |
| `apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs`                                 | 3 call sites: added `user.TenantId` / `body.TenantId` to `SendPasswordResetEmailAsync` / `SendInviteEmailAsync` |

---

## Validation Performed

- `dotnet build Identity.Api.csproj` — **0 errors**
- Interface change (`tenantId` param) — all 3 call sites updated and verified
- `grep` confirmed no remaining `internal/send-email` calls in `NotificationsEmailClient`
- `grep` confirmed `identity.user.password.reset` and `identity.user.invite.sent` are emitted
- `NotificationsAuthDelegatingHandler` confirmed wired to `"NotificationsService"` HTTP client
- `AddServiceTokenIssuer("identity")` added to DI — key sourced from `FLOW_SERVICE_TOKEN_SECRET` env var (shared with Notifications service)
- When `FLOW_SERVICE_TOKEN_SECRET` not set: handler is no-op, Notifications accepts in legacy X-Tenant-Id mode (no breaking change in dev)
- Existing `/internal/send-email` endpoint in Notifications service remains active (no breaking change for other potential consumers)

---

## Remaining Gaps

| Gap                                                                           | Severity | Action                        |
|-------------------------------------------------------------------------------|----------|-------------------------------|
| Self-service password reset (`POST /api/auth/forgot-password`) still not sending email | Medium | Separate endpoint; future work |
| Inline HTML template builders still in Identity (not in template catalog)     | Low      | Template catalog seeding — future |
| No idempotency key per reset-token ID (uses random UUID per call)             | Low      | Add `tokenId` param when available |
| `InternalServiceToken` config key in `NotificationsServiceOptions` still present | Low   | Remove in cleanup pass (cache client still uses it) |

---

## Risks / Follow-Up Recommendations

1. **Self-service forgot-password flow** (`POST /api/auth/forgot-password`, `AuthEndpoints.cs`): This endpoint currently generates a token and logs it (non-prod only). It has a full `SendPasswordResetEmailAsync` integration path but calls `_emailClient` through a different code path. When LS-ID-TNT-006 is fully complete, audit both flows for consistency.

2. **`InternalServiceToken` legacy config**: The `NotificationsCacheClient` still uses `X-Internal-Service-Token` for cache invalidation calls (`/internal/cache/...`). That endpoint is not in scope for this ticket (it's an internal endpoint, not a producer contract). Consider migrating in a later cleanup pass.

3. **Template catalog seeding**: Once `identity.user.password.reset` and `identity.user.invite.sent` are stable, register platform-level templates in the Notifications catalog. This will enable branded rendering, delivery analytics per event type, and tenant override support.

4. **Self-service password reset email** (`forgot-password` BFF route): This route generates the reset link and token but the email delivery path was noted as "future work" in the original implementation. This should be wired with the same `SendPasswordResetEmailAsync` call used by the admin path.
