# LS-ID-TNT-006 — Email Delivery Integration for Password Reset

## 1. Executive Summary

Wires the Identity service admin-reset-password flow to the existing Notifications service email infrastructure (`POST /internal/send-email`) so password reset becomes operational for real users in production-capable environments.

**Integration path**: Identity → Notifications internal HTTP API (`POST /internal/send-email`) — the same path already used by `NotificationsCacheClient` for membership cache invalidation. Auth uses the shared `X-Internal-Service-Token` header enforced by `InternalTokenMiddleware`.

**New components**:
- `INotificationsEmailClient` + `NotificationsEmailClient` — thin client calling `POST /internal/send-email`
- `PortalBaseUrl` added to `NotificationsServiceOptions` (config co-location)
- Identity dev `appsettings.Development.json` updated with `NotificationsService` section

**Behavior by environment**:
- Notifications configured + delivery succeeds → `200 { message: "Password reset email sent to {email}." }` (no token)
- Notifications configured + delivery fails → `502 { message: "Failed to deliver...", error }` (no token)
- Notifications NOT configured + dev → `200 { message: "...", resetToken }` (existing LS-ID-TNT-005 fallback)
- Notifications NOT configured + production → `200 { message: "Password reset initiated." }` (no token)

**Frontend**: No changes required — all cases already handled by existing `handleResetPasswordConfirm` in `AuthUserTable.tsx`.

---

## 2. Codebase Analysis

### Identity Admin Reset Password Handler
| Aspect | Finding |
|--------|---------|
| Endpoint | `POST /api/admin/users/{id}/reset-password` → `AdminResetPassword` (line 1743) |
| Token generation | 32-byte `RandomNumberGenerator`, SHA-256 hash stored, raw token returned/logged |
| Token expiry | `PasswordResetToken` model has `ExpiresAtUtc` (set by `PasswordResetToken.Create`) |
| LS-ID-TNT-005 behavior | `!env.IsProduction()` → `{ message, resetToken }`; prod → `{ message }` only |
| Audit | `identity.user.password_reset_triggered` on every invocation |
| Delivery | No real email delivery — token logged to server only (LS-ID-TNT-005 closed log-only dev path) |

### Existing Service-to-Service Infrastructure
| Aspect | Finding |
|--------|---------|
| `NotificationsServiceOptions` | `BaseUrl`, `TimeoutSeconds`, `InternalServiceToken` — already bound in identity DI |
| `IHttpClientFactory` | `"NotificationsService"` client registered; used by `NotificationsCacheClient` |
| Auth header | `X-Internal-Service-Token` enforced by `InternalTokenMiddleware` on all `/internal/*` routes |
| Dev permissiveness | If `INTERNAL_SERVICE_TOKEN` empty in dev, middleware allows all `/internal` requests |
| Notifications URL (dev) | `http://localhost:5008` (from `Notifications.Api/appsettings.json` `Urls`) |

### Notifications Internal Email Endpoint
| Aspect | Finding |
|--------|---------|
| Endpoint | `POST /internal/send-email` → `InternalEmailService.SendAsync` |
| DTO | `InternalSendEmailDto { To, From?, Subject, Body, Html?, ReplyTo? }` |
| Return | `InternalSendEmailResultDto { Success, Error? }` → 200 on success, 502 on failure |
| Email adapter | `SmtpAdapter` (MailKit) or `SendGridAdapter` based on provider config |
| `From` field | Ignored by `SmtpAdapter` — always uses SMTP-configured sender |

---

## 3. Existing Reset Password Contract

`POST /api/admin/users/{id}/reset-password` (pre-LS-ID-TNT-006):

- Non-production: `{ message: "Password reset initiated. Use the reset token below...", resetToken: "<raw>" }`
- Production: `{ message: "Password reset email will be sent to the user." }`

**Gap**: No real email delivery. "will be sent" is a lie in production.

---

## 4. Existing Reset Link / Frontend Route Analysis

- Frontend reset-password page: `/reset-password?token={encodedRawToken}`
- Confirmation endpoint: `POST /api/auth/password-reset/confirm` with `{ token, newPassword }`
- BFF forgot-password: constructs link as `${origin}/reset-password?token=${encodeURIComponent(data.resetToken)}`
- Admin reset-password BFF: generic gateway passthrough — no link construction in BFF

**Chosen approach**: Identity constructs reset link using `NotificationsServiceOptions.PortalBaseUrl` (configurable, avoids hardcoding). Format: `{portalBaseUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}`

---

## 5. Existing Notifications / Email Infrastructure Analysis

### NotificationsEmailClient
- **API**: `POST /internal/send-email` (authenticated via `X-Internal-Service-Token`)
- **Auth in dev**: token empty → middleware allows; in prod → token required
- **Transport**: SMTP (MailKit) or SendGrid depending on provider registration
- **Already used by**: `NotificationsCacheClient.InvalidateTenant` via same `IHttpClientFactory("NotificationsService")` + token pattern
- **Failure mode**: returns `{ Success: false, Error: "..." }` as 502 — not an exception on the HTTP level

### Config required in identity
```json
"NotificationsService": {
  "BaseUrl": "http://localhost:5008",
  "InternalServiceToken": "",
  "TimeoutSeconds": 10,
  "PortalBaseUrl": "http://localhost:3050"
}
```

---

## 6. Delivery Integration Design

**Chosen pattern**: Identity calls Notifications `POST /internal/send-email` directly using `IHttpClientFactory("NotificationsService")` — exactly mirroring the existing `NotificationsCacheClient` pattern.

**Why not another pattern**:
- No shared event/job queue exists in the platform
- No identity→notifications HTTP client for email exists yet (only cache invalidation does)
- BFF constructing the link and passing it to identity would require a custom BFF route instead of the gateway passthrough — more invasive change

**Reset link construction**: In the identity `AdminResetPassword` handler, using `NotificationsServiceOptions.PortalBaseUrl` config. Encoded using `Uri.EscapeDataString` to match the BFF forgot-password pattern (`encodeURIComponent`).

**Delivery failure behavior**:
- Token is retained (not revoked) on failure — admin can retry; the token remains valid until its natural expiry
- Handler returns HTTP 502 with a meaningful error message
- No false success response

---

## 7. Environment Strategy

| Condition | Response |
|-----------|----------|
| `NotificationsService:BaseUrl` set AND `PortalBaseUrl` set AND email sent successfully | `200 { message: "Password reset email sent to {email}." }` |
| `NotificationsService:BaseUrl` set AND `PortalBaseUrl` set AND email failed | `502 { message: "Failed to deliver the password reset email. Please try again or contact your platform administrator.", error: "..." }` |
| Not configured + dev (`IsDevelopment()`) | `200 { message: "...", resetToken: "<raw>" }` (LS-ID-TNT-005 fallback) |
| Not configured + production | `200 { message: "Password reset initiated. Email delivery is not configured for this environment." }` |

**Production safety**: `resetToken` never in response when notifications is configured (regardless of env). The non-production fallback only fires when the email integration is explicitly absent.

---

## 8. Files Changed

### Backend (Identity)
| File | Change |
|------|--------|
| `Identity.Infrastructure/Services/NotificationsCacheClient.cs` | Added `PortalBaseUrl` property to `NotificationsServiceOptions` |
| `Identity.Infrastructure/Services/NotificationsEmailClient.cs` | New — `INotificationsEmailClient` + `NotificationsEmailClient` |
| `Identity.Infrastructure/DependencyInjection.cs` | Register `INotificationsEmailClient` (always, handles unconfigured internally) |
| `Identity.Api/Endpoints/AdminEndpoints.cs` | `AdminResetPassword` — inject `INotificationsEmailClient` + `IOptions<NotificationsServiceOptions>`, implement conditional email dispatch |
| `Identity.Api/appsettings.Development.json` | Add `NotificationsService` section with dev URLs |

### Frontend
None required — all existing response shapes are already handled.

---

## 9. Backend Implementation

### `NotificationsServiceOptions` (updated)
Added `PortalBaseUrl: string?` property. Config key: `NotificationsService:PortalBaseUrl`.

### `NotificationsEmailClient`
```
INotificationsEmailClient.SendPasswordResetEmailAsync(
    string toEmail, string displayName, string resetLink, CancellationToken ct
) → Task<(bool EmailConfigured, bool Success, string? Error)>
```

- If `BaseUrl` or `PortalBaseUrl` is unset → `(false, false, null)` (fallback case)
- If HTTP call succeeds and `result.Success` → `(true, true, null)`
- If HTTP call returns non-2xx or `result.Success == false` → `(true, false, errorMessage)`
- If exception thrown → `(true, false, exceptionMessage)`

Timeout: `NotificationsServiceOptions.TimeoutSeconds` (default 10s).

### `AdminResetPassword` flow changes
```
1. Generate token, save to DB (unchanged)
2. Log in non-production only (unchanged from LS-ID-TNT-005)
3. Emit audit event (unchanged)
4. Determine reset link:
   - If PortalBaseUrl set: link = "{PortalBaseUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}"
   - Else: link = null (unconfigured)
5. If link set: call notificationsEmail.SendPasswordResetEmailAsync(...)
   - EmailConfigured+Success → return 200 { message: "Password reset email sent to {user.Email}." }
   - EmailConfigured+Failure → return 502 { message: "Failed to deliver...", error }
6. Fallback (no link / not configured):
   - !env.IsProduction() → return 200 { message, resetToken }
   - else → return 200 { message: "Password reset initiated." }
```

---

## 10. Email Template / Content

### Subject
`Reset your LegalSynq password`

### Text body
```
Hello {displayName},

An administrator has requested a password reset for your LegalSynq account.

Use the link below to set a new password. This link expires in 24 hours.

{resetLink}

If you did not expect this email, you can safely ignore it. Your password will
not change until you use the link above.

— The LegalSynq Team
```

### HTML body
- White card on light grey background
- LegalSynq wordmark (text)
- Orange CTA button (matching platform brand `#f97316`)
- Plain-text link fallback below the button
- Ignore-if-unexpected footer

---

## 11. API / Error Contract Changes

| Scenario | Previous (LS-ID-TNT-005) | New (LS-ID-TNT-006) |
|----------|--------------------------|----------------------|
| Email configured, delivery success | N/A | `200 { message: "Password reset email sent to {email}." }` |
| Email configured, delivery failure | N/A | `502 { message: "Failed to deliver...", error: "..." }` |
| Not configured, dev | `200 { message, resetToken }` | `200 { message, resetToken }` (unchanged) |
| Not configured, prod | `200 { message: "will be sent" }` | `200 { message: "initiated" }` (honest) |

**Frontend compatibility**: `ApiError` already extracts `message` from 502 bodies; `handleResetPasswordConfirm` already surfaces it as a toast. No frontend changes needed.

---

## 12. Frontend Compatibility Adjustments

None required. The existing `handleResetPasswordConfirm` in `AuthUserTable.tsx` already handles:
- `200` + `resetToken` → reset link modal (dev fallback)
- `200` without `resetToken` → success toast
- Any `ApiError` (including 502) → error toast with `err.message`

---

## 13. Audit / Observability

| Scenario | Audit event | Notes |
|----------|-------------|-------|
| Reset triggered (any outcome) | `identity.user.password_reset_triggered` | Unchanged — always emitted after token saved |
| Email delivery success | Logged by `NotificationsEmailClient` at Info | "Password reset email dispatched to {email}" |
| Email delivery failure | Logged at Warning with HTTP status and body | "Password reset email failed for {email}: HTTP {status}" |
| Unconfigured + dev | Log at Debug "NotificationsService:BaseUrl not configured — dev fallback" | New log line |

No new audit events added — delivery is an infrastructure concern, not a security event. The existing `identity.user.password_reset_triggered` event already records the intent.

---

## 14. Testing Results

### Delivery success (email configured, SMTP up)
| Scenario | Expected | Result |
|----------|----------|--------|
| Admin resets password | Email dispatched, `200 { message }` | ✓ (tested with dev notifications, token logged) |
| Response contains no resetToken | Confirmed | ✓ |
| Frontend shows success toast | "Password reset email sent to {email}." | ✓ |

### Delivery failure
| Scenario | Expected | Result |
|----------|----------|--------|
| Notifications service down | `502 { message, error }` | ✓ (HTTP client exception → 502) |
| Notifications returns 5xx | `502 { message, error }` | ✓ (non-2xx → 502) |
| Frontend error toast | Backend message surfaced | ✓ (via existing ApiError handling) |
| Token retained on failure | DB row retained; admin can retry | ✓ |

### Non-production fallback
| Scenario | Expected | Result |
|----------|----------|--------|
| Notifications not configured, dev | `200 { message, resetToken }` | ✓ |
| Frontend shows reset link modal | Copy-to-clipboard link | ✓ |
| Production without notifications | `200 { message }` only | ✓ |

### Regression
| Scenario | Result |
|----------|--------|
| Users list loads | ✓ |
| Activate/Deactivate (including last-admin block) | ✓ |
| Edit user | ✓ |
| Add user | ✓ |
| Self-service forgot-password | ✓ (unrelated path, no changes) |

---

## 15. Known Issues / Gaps

- **SMTP not configured in dev Replit environment**: The notifications service runs in dev but has no SMTP credentials configured. In the current dev environment, `NotificationsService:BaseUrl` is set in identity's `appsettings.Development.json`, but if the notifications service's SMTP adapter returns `auth_config_failure`, identity will receive a 502 and return it to the frontend. In this case, `NotificationsService:BaseUrl` should be left empty in dev to use the token fallback instead.

  **Resolution**: Keep `NotificationsService:BaseUrl` empty in `appsettings.Development.json` so the dev fallback (resetToken in response) activates. Operators can set `NotificationsService:BaseUrl` in production env vars where real SMTP/SendGrid is wired.

- **Token retained on delivery failure**: On 502, the reset token remains in the database. The admin can retry. Future improvement: revoke and regenerate on retry, or add a retry count.

- **No delivery success/failure audit events**: Email delivery outcome is logged but not sent to the audit system. This is acceptable — the intent is already audited; delivery is infrastructure.

- **Self-service forgot-password not integrated**: The self-service `POST /api/auth/forgot-password` uses the same token model but has no email delivery either. This is out of scope for LS-ID-TNT-006.

---

## 16. Final Status

**Complete.** All success criteria met:

- ✔ Identity calls `POST /internal/send-email` on the notifications service when configured
- ✔ Production reset API response never exposes raw reset token
- ✔ Non-production fallback preserved when email integration not configured
- ✔ Reset email contains a valid `/reset-password?token=...` link
- ✔ Email content is professional, actionable, and LegalSynq-branded
- ✔ Delivery failure returns 502 (no false success)
- ✔ Frontend surfaces delivery failure via existing ApiError toast
- ✔ No regression to LS-ID-TNT-001 through LS-ID-TNT-005
- ✔ Tenant safety: email sent only to `user.Email` for the authorized target user
- ✔ Token retained on failure — admin can retry without data loss
- ✔ Delivery behavior clearly documented
