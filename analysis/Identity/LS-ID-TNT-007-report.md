# LS-ID-TNT-007 — Invite-Based User Creation

## 1. Executive Summary

Replaces the "admin sets a temporary password" flow for new tenant users with a secure invite-based onboarding flow. An admin creates an inactive user account; the platform generates a 72-hour single-use invite token, sends a branded invite email via the Notifications service, and the invitee lands on `/accept-invite?token=…` to choose their own password and activate the account. Reuses the LS-ID-TNT-006 email-client infrastructure (`INotificationsEmailClient`, `NotificationsServiceOptions`). Preserves a dev-token modal fallback when email is not configured.

## 2. Codebase Analysis

The existing `POST /admin/tenants/{tenantId}/users` endpoint already created users but required the admin to supply a `temporaryPassword`. The `UserInvitation` domain entity and invite state machine (`PENDING → ACCEPTED | EXPIRED | REVOKED`) were already fully implemented in the Identity service. The `INotificationsEmailClient` interface introduced in LS-ID-TNT-006 was extended to add `SendInviteEmailAsync`. The frontend `AddUserModal` previously rendered a password field; the invite flow removes it.

## 3. Existing Add User Contract (pre-LS-ID-TNT-007)

```
POST /admin/tenants/{tenantId}/users
Body: { firstName, lastName, email, roleId, temporaryPassword }
Response: 201 { id, email, ... }
```

Admin was expected to communicate the temporary password out-of-band. No email was sent automatically.

## 4. Existing Auth / Activation / Reset Token Analysis

- `UserInvitation` entity: `RawToken` (64-hex, two Guid.N concat), SHA-256 hashed in DB, `ExpiresAt = UtcNow + 72h`, status enum `PENDING | ACCEPTED | EXPIRED | REVOKED`.
- `POST /auth/accept-invite` endpoint already existed in `AuthEndpoints.cs` and handled token validation, password setting, and user activation.
- Pattern mirrors `reset-password` exactly — same raw/hash separation, same 72h window.

## 5. Existing User State / Login Behavior Analysis

- Users created by admin flow start `IsActive = false`.
- `AcceptInvite` handler sets `IsActive = true` on success.
- Inactive users cannot log in (login endpoint checks `IsActive`).
- Backend `ListUsers` already returns `status = "Invited"` for users with a `PENDING` invitation.

## 6. Existing Email / Notifications Reuse Analysis

- `INotificationsEmailClient` and `NotificationsEmailClient` introduced in LS-ID-TNT-006.
- Config gate: `NotificationsService:BaseUrl` — when empty (dev default), email is skipped and raw token is returned in response.
- Same `IHttpClientFactory("NotificationsService")` + `X-Internal-Service-Token` auth pattern used for invite emails.

## 7. Invite Flow Design

```
Admin fills "Invite User" modal (name, email, role)
  → POST /admin/tenants/{tenantId}/users  (no password field)
  → Identity creates inactive user + PENDING invitation record
  → Identity calls NotificationsEmailClient.SendInviteEmailAsync
  → Notifications service sends branded invite email
  → If configured: 201 { id, email, status }
  → If not configured (dev): 201 { id, email, status, inviteToken }

Dev modal shows invite link with copy button.

Invitee clicks link → /accept-invite?token=…
  → AcceptInviteForm → POST /api/auth/accept-invite (BFF)
  → BFF → POST /identity/api/auth/accept-invite
  → Identity validates token hash, sets password, activates user
  → Redirect to /login
```

## 8. Invite State Model

| Transition | Trigger |
|------------|---------|
| `PENDING` → `ACCEPTED` | Invitee submits `/accept-invite` with valid token |
| `PENDING` → `EXPIRED` | Token age > 72h (checked at accept time) |
| `PENDING` → `REVOKED` | Admin calls `DELETE /admin/tenants/{id}/users/{userId}/invite` |

## 9. Files Changed

### Backend
| File | Change |
|------|--------|
| `Identity.Infrastructure/Services/NotificationsEmailClient.cs` | Added `SendInviteEmailAsync` to interface + implementation; branded HTML/text invite templates; activation link built from `PortalBaseUrl` |
| `Identity.Api/Endpoints/AdminEndpoints.cs` | `InviteUser` handler: removed `temporaryPassword` requirement, injects `IOptions<NotificationsServiceOptions>` + `INotificationsEmailClient` + `IWebHostEnvironment`; builds activation link; calls email client; env-gated raw-token fallback; `ResendInvite` handler updated identically |

### Frontend
| File | Change |
|------|--------|
| `apps/web/src/types/index.ts` | Added `status?: string` to `TenantUser` |
| `apps/web/src/lib/tenant-client-api.ts` | Added `inviteUser()` and `resendInvite()` client methods |
| `apps/web/src/app/(platform)/tenant/authorization/users/AddUserModal.tsx` | Rewritten: removed password field; "Send Invite" CTA; shows invite-link panel (copy button) when `inviteToken` in response; "Done" calls `onSuccess()` |
| `apps/web/src/app/(platform)/tenant/authorization/users/AuthUserTable.tsx` | Added amber "Invited" badge; "Invited" status filter; "Resend Invite" row action for pending-invite users; "Invite User" button label; dedicated invite-link modal |
| `apps/web/src/app/accept-invite/page.tsx` | New: public route — renders `AcceptInviteForm`; left-panel branding ("Welcome to LegalSynq") |
| `apps/web/src/app/accept-invite/accept-invite-form.tsx` | New: client form — new password + confirm; posts to `/api/auth/accept-invite`; redirects to `/login` on success; mirrors reset-password pattern |
| `apps/web/src/app/api/auth/accept-invite/route.ts` | New: BFF POST handler — proxies to `${GATEWAY_URL}/identity/api/auth/accept-invite` |
| `apps/web/src/middleware.ts` | Added `/accept-invite` and `/api/auth/accept-invite` to `PUBLIC_PATHS` |

## 10. Backend Implementation

### `SendInviteEmailAsync` (NotificationsEmailClient)
- Checks `BaseUrl` and `PortalBaseUrl` configured → returns `(false, false, null)` if not.
- Builds activation URL: `{PortalBaseUrl}/accept-invite?token={Uri.EscapeDataString(rawToken)}`
- Posts to `POST /internal/send-email` with JSON body including branded HTML and plain-text invite templates.
- Returns `(true, true, null)` on success, `(true, false, errorMessage)` on failure.

### `InviteUser` handler changes
- No longer reads `temporaryPassword` from request body.
- After creating user + invitation record, resolves activation link.
- Calls `SendInviteEmailAsync`; on delivery failure returns `502` when configured.
- When not configured + `IsDevelopment()`: returns `inviteToken` in 201 response for dev modal.

### `ResendInvite` handler
- Same pattern as `InviteUser` post-creation: regenerates token, calls `SendInviteEmailAsync`.

## 11. API / Error Contract Changes

### `POST /admin/tenants/{tenantId}/users`
| Condition | Response |
|-----------|----------|
| Email configured + delivery success | `201 { id, email, status }` |
| Email configured + delivery failure | `502 { message, error }` |
| Not configured + dev | `201 { id, email, status, inviteToken }` |
| Not configured + non-dev | `201 { id, email, status }` |

### `POST /admin/tenants/{tenantId}/users/{userId}/resend-invite`
Same response shape as above.

### `POST /auth/accept-invite` (unchanged — existed pre-LS-ID-TNT-007)
```json
Body: { "token": "<raw_token>", "newPassword": "<chosen_password>" }
201: { "message": "Account activated successfully." }
400: { "message": "<validation error>" }
```

## 12. Frontend / UX Implementation

### AddUserModal
- Form fields: First Name, Last Name, Email, Role (no password field).
- Submit label: "Send Invite".
- On success with `inviteToken` (dev): shows invite link panel with copy-to-clipboard button; "Done" button calls `onSuccess()` → closes modal + refreshes table.
- On success without `inviteToken` (production): shows success toast and closes immediately.

### AuthUserTable
- Users with `status === "Invited"` show an amber "Invited" badge next to their name.
- Status filter dropdown includes "Invited" option.
- Row actions for invited users include "Resend Invite" which opens a dedicated modal showing the new invite link (dev) or a confirmation toast (production).
- "Add User" button renamed to "Invite User".

### `/accept-invite` page
- Public route (no auth required).
- Left panel: "Welcome to LegalSynq" + "Set your password to activate your account and get started."
- Right panel: "Accept your invitation" / "Choose a password to activate your account" + New password + Confirm password fields + "Activate account" button + "Back to sign in" link.
- Mirrors `/reset-password` layout exactly.

## 13. Email Template / Content

### Subject
`You've been invited to LegalSynq`

### HTML Template (key sections)
- LegalSynq branded header (navy background, white logo text)
- Greeting: "You're invited!"
- Body: "You've been invited to join {tenantId} on LegalSynq. Click the button below to accept your invitation and set your password."
- CTA button: "Accept Invitation" → `{activationUrl}`
- Expiry notice: "This invitation link will expire in 72 hours."
- Footer: "If you didn't expect this invitation, you can ignore this email."

### Text Template
Plain-text fallback with activation URL on its own line.

## 14. Audit / Observability

- `Console.WriteLine` logs the raw invite token in non-production environments only (dev-only code path, gated on `IsDevelopment()`).
- Email delivery failures are logged at `Warning` level via the Identity service logger.
- Existing `UserInvitation` entity tracks `CreatedAt`, `ExpiresAt`, `AcceptedAt` per invitation.

## 15. Testing Results

- Build: `dotnet build LegalSynq.sln` succeeded with only pre-existing warnings (NU1902 MailKit advisory, MSB3277 JwtBearer version conflicts, CS0219 unused variable in test file).
- Frontend: Next.js Fast Refresh completed cleanly with no TypeScript or module errors.
- Identity service started on `http://0.0.0.0:5001`.
- `/accept-invite?token=abc123` renders the "Accept your invitation" form correctly after adding the route to `PUBLIC_PATHS`.

## 16. Known Issues / Gaps

- The `inviteToken` in the dev modal uses `Console.WriteLine` for secondary logging — this should be replaced with structured logging in a follow-up.
- No dedicated email preview/test for the invite template (same gap as reset-password).
- Invite expiry is enforced at acceptance time only; no background job to sweep and mark tokens `EXPIRED` (pre-existing gap, not introduced by this ticket).

## 17. Final Status

**COMPLETE** — Invite-based user creation is fully implemented across backend (email delivery, handler updates) and frontend (modal, table, accept-invite page, BFF route, middleware). All services started cleanly.
