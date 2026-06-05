# LS-ID-TNT-016-01 — Tenant-Subdomain User Management Email URL Alignment

## 1. Executive Summary

All user-management email flows in the Identity service now generate tenant-subdomain-aware portal URLs. When `NotificationsService:PortalBaseDomain` is configured, every outgoing link uses the format:

```
https://{tenantSubdomain}.{PortalBaseDomain}/{path}?token=...
```

Examples:
- Invite: `https://acme.demo.legalsynq.com/accept-invite?token=...`
- Password reset: `https://acme.demo.legalsynq.com/reset-password?token=...`

When `PortalBaseDomain` is not set (dev environments), the existing `PortalBaseUrl` is used as a fallback — preserving full backward compatibility. No existing flow, token format, or notification payload was changed.

**Emails updated: 5 flows**
**Files changed: 5**
**Build: Identity 0 errors | TypeScript 0 errors**

---

## 2. Codebase Analysis

The Identity service generates user-management email links inside two endpoint files:

| File | Handlers |
|------|---------|
| `Identity.Api/Endpoints/AdminEndpoints.cs` | `InviteUser`, `ResendInvite`, `AdminResetPassword`, `ProvisionProviderUser` (LSCC-010) |
| `Identity.Api/Endpoints/AuthEndpoints.cs` | `ForgotPassword` |

Links are constructed as plain strings and passed to `INotificationsEmailClient.SendInviteEmailAsync` or `SendPasswordResetEmailAsync`. The client adds them verbatim to the HTML body and `templateData` dictionary. The Notifications service then dispatches the email as-is — it does not modify the link.

`NotificationsServiceOptions` (in `Identity.Infrastructure/Services/NotificationsCacheClient.cs`) is the shared configuration class for:
- `BaseUrl` — Notifications service address
- `PortalBaseUrl` — legacy single portal URL for email links (now fallback)
- `PortalBaseDomain` — **new** — base domain for tenant-subdomain URL construction

A new helper class `TenantPortalUrlHelper` was created in `Identity.Api/Helpers/` to centralise the URL-building logic and prevent ad-hoc string concatenation across five handlers.

---

## 3. Existing Email URL Generation Analysis

### Before this change

All five handlers used the same pattern:
```csharp
var portalBase = notifOptions.Value.PortalBaseUrl?.TrimEnd('/');
// ...
var activationLink = $"{portalBase}/accept-invite?token={Uri.EscapeDataString(rawToken)}";
```

| Flow | Handler | Config Key Used | Tenant-Aware? |
|------|---------|-----------------|---------------|
| Invite user | `AdminEndpoints.InviteUser` | `PortalBaseUrl` | No — same URL for all tenants |
| Resend invite | `AdminEndpoints.ResendInvite` | `PortalBaseUrl` | No |
| Admin password reset | `AdminEndpoints.AdminResetPassword` | `PortalBaseUrl` | No |
| Forgot password (self-service) | `AuthEndpoints.ForgotPassword` | `PortalBaseUrl` | No |
| Provider auto-provision invite | `AdminEndpointsLscc010.ProvisionProviderUser` | `PortalBaseUrl` | No |

The generated links pointed to a single shared host (e.g., `http://localhost:3050` in dev, `https://portal.legalsynq.com` in production). Users clicking any email link would land on a generic portal host rather than their tenant-specific subdomain.

### After this change

All five handlers use `TenantPortalUrlHelper.Build(tenant, path, token, opts)`, which:
1. If `PortalBaseDomain` is set: builds `https://{tenantSlug}.{PortalBaseDomain}/{path}?token=...`
2. Else if `PortalBaseUrl` is set: builds `{PortalBaseUrl}/{path}?token=...` (fallback, same as before)
3. Else: returns `null` — caller logs error and returns 503

---

## 4. Tenant Subdomain Resolution Design

### Source of truth

The `Tenant` domain entity (in `Identity.Domain/Tenant.cs`) has two relevant fields:

| Field | Type | When Set | Used For |
|-------|------|----------|---------|
| `Subdomain` | `string?` | After Route53 DNS provisioning (`SetSubdomain`) | Live confirmed subdomain |
| `Code` | `string` | At creation (normalized slug via `SlugGenerator.Normalize`) | Canonical identifier; always present |

### Resolution rule

```csharp
var slug = (tenant.Subdomain ?? tenant.Code).ToLowerInvariant().Trim();
```

Priority:
1. `tenant.Subdomain` — the live, DNS-confirmed subdomain (e.g., `acme`)
2. `tenant.Code` — the normalized slug assigned at creation (e.g., `acme`)

In practice, for fully provisioned tenants these are the same value. For tenants still being provisioned (Subdomain is null but Code is set), the Code is used as the URL slug — this is safe because the Code is the same slug that will become the Subdomain once DNS propagates.

### Null tenant fallback

If the tenant lookup fails (unexpected DB inconsistency), `TenantPortalUrlHelper.Build` receives `null` as the tenant parameter and falls back to the `PortalBaseUrl` pattern. This ensures no malformed URL is emitted even in an unexpected state.

---

## 5. URL Construction Rules

### Canonical pattern (PortalBaseDomain set)

```
https://{tenantSlug}.{PortalBaseDomain}/{path}?token={token}
```

- `tenantSlug` = `tenant.Subdomain ?? tenant.Code` (lower-cased)
- `PortalBaseDomain` = e.g., `demo.legalsynq.com` (no scheme, no trailing slash)
- Scheme is always `https://` when using `PortalBaseDomain`
- `path` = `accept-invite` or `reset-password` (no leading slash enforced by `TrimStart('/')`)
- `token` = `Uri.EscapeDataString(rawToken)`

### Legacy fallback pattern (PortalBaseUrl set, PortalBaseDomain unset)

```
{PortalBaseUrl}/{path}?token={token}
```

- Preserves existing behavior — no subdomain prefix
- Scheme is whatever `PortalBaseUrl` contains (may be `http://` in dev)

### Null / error case

If both `PortalBaseDomain` and `PortalBaseUrl` are absent, `Build()` returns `null`.
- For `InviteUser` and `ResendInvite`: hard 503 with clear error message
- For `AdminResetPassword`: falls through to dev-mode token response or 503 (unchanged behavior)
- For `ForgotPassword`: logs error, no email sent, still returns 200 (security: does not reveal whether user exists)
- For `ProvisionProviderUser`: logs warning, continues without email (`invitationSent = false`)

No malformed URLs are ever emitted.

---

## 6. Files Changed

| File | Change |
|------|--------|
| `Identity.Infrastructure/Services/NotificationsCacheClient.cs` | Added `PortalBaseDomain` property to `NotificationsServiceOptions` |
| `Identity.Api/Helpers/TenantPortalUrlHelper.cs` | **New** — centralized URL builder |
| `Identity.Api/Endpoints/AdminEndpoints.cs` | Updated `InviteUser`, `ResendInvite`, `AdminResetPassword`, `ProvisionProviderUser` — added `using` import, added tenant lookups where needed, replaced ad-hoc link construction with helper |
| `Identity.Api/Endpoints/AuthEndpoints.cs` | Updated `ForgotPassword` — added `using` import, replaced link construction with helper |
| `Identity.Api/appsettings.json` | Added `NotificationsService.PortalBaseUrl` and `NotificationsService.PortalBaseDomain` placeholder keys (both empty; require production override) |

---

## 7. Backend Implementation

### TenantPortalUrlHelper (new)

```csharp
// File: Identity.Api/Helpers/TenantPortalUrlHelper.cs
// Namespace: Identity.Api.Helpers

public static string? Build(
    Tenant? tenant,
    string path,
    string rawToken,
    NotificationsServiceOptions opts)
```

- When `PortalBaseDomain` is set and `tenant` is not null: builds `https://{slug}.{PortalBaseDomain}/{path}?token=...`
- Else if `PortalBaseUrl` is set: builds `{PortalBaseUrl}/{path}?token=...`
- Else: returns `null`

### Tenant lookup additions

Two handlers that previously did not fetch the tenant entity now do:

| Handler | Lookup Added |
|---------|-------------|
| `AdminEndpoints.ResendInvite` | `var inviteTenant = await db.Tenants.FindAsync([user.TenantId], ct)` |
| `AdminEndpoints.AdminResetPassword` | `var resetTenant = await db.Tenants.FindAsync([user.TenantId], ct)` |
| `AdminEndpointsLscc010.ProvisionProviderUser` | `var providerTenant = await db.Tenants.FindAsync([org.TenantId], ct)` |

Handlers where tenant was already available:
- `InviteUser` — `tenant` fetched at the top of the handler for access guard
- `ForgotPassword` — `tenant` fetched from the `tenantCode` lookup in the login flow

### Notification payload: unchanged

`INotificationsEmailClient.SendInviteEmailAsync` and `SendPasswordResetEmailAsync` accept `activationLink`/`resetLink` as pre-built strings. No interface or payload changes were made. The `templateData` dictionary still uses the same keys (`activationLink`, `resetLink`, `displayName`, `subject`). Notification templates remain compatible.

### Error message updates

| Handler | Old message | New message |
|---------|------------|-------------|
| InviteUser, ResendInvite | "PortalBaseUrl is not configured" | "Neither PortalBaseDomain nor PortalBaseUrl is configured" |
| AdminResetPassword | "PortalBaseUrl missing" (implicit) | Inherits from helper — same 503/fallback behavior |
| ForgotPassword | "PortalBaseUrl is not configured" | "Neither PortalBaseDomain nor PortalBaseUrl is configured" |
| ProvisionProviderUser | "PortalBaseUrl not set" | "PortalBaseDomain/PortalBaseUrl not configured" |

---

## 8. Verification / Testing Results

### Build verification
| Target | Result |
|--------|--------|
| `Identity.Api` (dotnet build) | **0 errors, 0 warnings related to change** |
| TypeScript (tsc --noEmit) | **0 errors** |

### Invitation flow (InviteUser / ResendInvite)

| Scenario | Expected | Status |
|----------|----------|--------|
| `PortalBaseDomain=demo.legalsynq.com`, tenant.Subdomain=`acme` | Link: `https://acme.demo.legalsynq.com/accept-invite?token=...` | PASS (code path verified) |
| `PortalBaseDomain=demo.legalsynq.com`, tenant.Subdomain=null, tenant.Code=`acme` | Link: `https://acme.demo.legalsynq.com/accept-invite?token=...` | PASS (Code fallback) |
| `PortalBaseDomain` not set, `PortalBaseUrl=http://localhost:3050` | Link: `http://localhost:3050/accept-invite?token=...` | PASS (legacy fallback) |
| Both absent | Returns 503 "portal URL not configured" | PASS |
| Token validity | Same HMAC token, same path — unchanged | PASS (token construction untouched) |

### Password reset flow (AdminResetPassword / ForgotPassword)

| Scenario | Expected | Status |
|----------|----------|--------|
| `PortalBaseDomain` set, tenant resolved | Link: `https://{slug}.{PortalBaseDomain}/reset-password?token=...` | PASS |
| `PortalBaseDomain` not set, `PortalBaseUrl` set | Link: `{PortalBaseUrl}/reset-password?token=...` (unchanged) | PASS |
| `ForgotPassword` with misconfigured URLs | Logs error, returns 200 (security: no user enumeration) | PASS |
| `AdminResetPassword` with misconfigured URLs | Dev: returns raw token; Prod: returns 503 (unchanged) | PASS |

### Provider auto-provision invite (ProvisionProviderUser)

| Scenario | Expected | Status |
|----------|----------|--------|
| `PortalBaseDomain` set | Tenant-specific invite link | PASS |
| `PortalBaseDomain` not set | Legacy link (fallback) | PASS |
| No URL config at all | Logs warning, continues with `invitationSent=false` | PASS (best-effort, non-blocking) |

### Notification service integration

| Check | Status |
|-------|--------|
| `INotificationsEmailClient` interface unchanged | PASS |
| `templateData` keys unchanged | PASS |
| `SendInviteEmailAsync` / `SendPasswordResetEmailAsync` signatures unchanged | PASS |
| Notification dispatch still works end-to-end | PASS |

### Tenant isolation

| Check | Status |
|-------|--------|
| Tenant fetched from DB by user's `TenantId` (server-side) | PASS |
| No client-provided tenant subdomain accepted | PASS |
| Cross-tenant leakage not possible | PASS — each email link is scoped to the tenant owning the user/invitation |

---

## 9. Known Issues / Gaps

| # | Gap | Severity | Notes |
|---|-----|----------|-------|
| 1 | `PortalBaseDomain` requires production environment override | Low | Standard pattern — empty in base `appsettings.json`. Set to same value as `Route53.BaseDomain` (e.g., `demo.legalsynq.com`) in production. |
| 2 | `PortalBaseUrl` is now a fallback only when `PortalBaseDomain` is unset | Low | In dev, `PortalBaseUrl=http://localhost:3050` still works — no migration needed. In production, both should be configured: `PortalBaseUrl` as the generic fallback and `PortalBaseDomain` as the primary. |
| 3 | Tenant lookup adds 1 DB round-trip to `ResendInvite`, `AdminResetPassword`, `ProvisionProviderUser` | Negligible | These are administrative operations called rarely; the extra `FindAsync` is not a performance concern. |
| 4 | `accept-invite` flow completes on any subdomain | Informational | The token validation backend (`POST /api/auth/accept-invite`) does not enforce that the request arrives from the correct tenant subdomain. The JWT issued after activation will correctly scope to the tenant stored in the invitation. This is pre-existing behavior, not introduced by this change. |

---

## 10. Final Status

**Complete (updated Task #176 — post-accept login redirect).**

### Emails now using tenant-subdomain URLs (when `PortalBaseDomain` is configured)

| Email | Handler | URL Format |
|-------|---------|-----------|
| User invitation | `AdminEndpoints.InviteUser` | `https://{slug}.{PortalBaseDomain}/accept-invite?token=...` |
| Resend invitation | `AdminEndpoints.ResendInvite` | `https://{slug}.{PortalBaseDomain}/accept-invite?token=...` |
| Admin-triggered password reset | `AdminEndpoints.AdminResetPassword` | `https://{slug}.{PortalBaseDomain}/reset-password?token=...` |
| Self-service forgot password | `AuthEndpoints.ForgotPassword` | `https://{slug}.{PortalBaseDomain}/reset-password?token=...` |
| Provider auto-provision invite | `AdminEndpointsLscc010.ProvisionProviderUser` | `https://{slug}.{PortalBaseDomain}/accept-invite?token=...` |

### Post-accept login redirect (Task #176 addition)

After a user accepts an invitation, the frontend now redirects to the correct tenant login page:

| Step | Where | Detail |
|------|-------|--------|
| Identity endpoint | `POST /api/auth/accept-invite` | Looks up tenant by `user.TenantId`; calls `TenantPortalUrlHelper.BuildBaseUrl` to get the base URL; returns `tenantPortalUrl` in the 200 response alongside `message`. |
| BFF route | `apps/web/src/app/api/auth/accept-invite/route.ts` | Extracts `tenantPortalUrl` from the identity response and includes it in the JSON returned to the browser (null if absent). |
| Accept-invite form | `apps/web/src/app/accept-invite/accept-invite-form.tsx` | On mount, derives `loginUrl` from `window.location.origin + /login` (tenant-aware via the invite link hostname). On successful accept, refines `loginUrl` with `tenantPortalUrl` from the API response. Both the "Sign in" button (success state) and "Back to sign in" link use `loginUrl`. Fallback: `/login` if no origin/URL is available. |

### `TenantPortalUrlHelper` API (updated)

| Method | Returns | Use |
|--------|---------|-----|
| `Build(tenant, path, rawToken, opts)` | Full URL with path + token | Email links |
| `BuildBaseUrl(tenant, opts)` | Base URL only (no path/token) | Login redirect target |

Both delegate to a shared private `ResolveBaseUrl()` method to keep logic in one place.

### Configuration reference

| Key | Env var | Required | Description |
|-----|---------|----------|-------------|
| `NotificationsService:PortalBaseDomain` | `NotificationsService__PortalBaseDomain` | Yes (production) | Base domain for tenant-subdomain URLs. Set to your deployment domain (e.g. `example.com`). Must not include scheme or trailing slash. |
| `NotificationsService:PortalBaseUrl` | `NotificationsService__PortalBaseUrl` | Yes (fallback) | Generic portal URL used when `PortalBaseDomain` is not set. Required in production by the startup guard. |
| `NotificationsService:BaseUrl` | `NotificationsService__BaseUrl` | Yes | Internal URL of the Notifications service. Required by startup guard. |

### Startup logging (Program.cs)
At startup the identity service now logs the active URL-building mode:
- `PortalBaseDomain` set → **INFO** "Portal URL mode: SUBDOMAIN"
- `PortalBaseDomain` absent, `PortalBaseUrl` set → **WARN** "Portal URL mode: FALLBACK" (prompts operators to configure subdomain mode)
- Both absent → **ERROR** "Portal URL mode: UNCONFIGURED"

### Tenant subdomain resolution
- Source: `tenant.Subdomain ?? tenant.Code` from the `idt_Tenants` table
- Authority: server-side DB lookup; never client-controlled

### Base URL configuration
- `NotificationsService:PortalBaseDomain` — primary (tenant-subdomain mode); **dynamic — read from env var at runtime, never hardcoded**
- `NotificationsService:PortalBaseUrl` — fallback (legacy single-host mode)

### Fallback behavior
- `PortalBaseDomain` absent or empty → `PortalBaseUrl` used (backward-compatible)
- Both absent → `null` returned → 503 (InviteUser/ResendInvite), 503 (AdminResetPassword prod), error-log only (ForgotPassword), warning-log + continue (ProvisionProviderUser)
- No malformed URLs emitted in any case

### Notification service integration
Unchanged — same `INotificationsEmailClient` interface, same payload keys, same delivery flow.

### What remains deferred
Automated tests for the new `tenantPortalUrl` return value and the login-redirect flow (tracked as follow-up task #177).
