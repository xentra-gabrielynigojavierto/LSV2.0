# CC2-INT-B05 Report — Common Portal Integration

**Status:** Complete  
**Date:** 2026-04-22  
**Block:** CC2-INT-B05 — Common Portal Integration

---

## 1. Summary

CC2-INT-B05 delivers the Common Portal: the authenticated, Identity-backed UX surface for
external actors (Providers and Law Firms) accessing CareConnect referrals.

Rather than creating a physically separate Next.js application, the Common Portal is built
as a dedicated route group `(common-portal)` inside the existing `apps/web` application.
This decision preserves the platform's existing BFF pattern (HttpOnly `platform_session` cookie,
server-side JWT validation via `/identity/api/auth/me`) and avoids duplicating auth
infrastructure. The separation is architectural — distinct layout, route group, auth guard,
and UX — not a separate deployment.

**Delivered routes:**

| Path | Purpose |
|------|---------|
| `/provider/dashboard` | Provider's assigned-referral dashboard |
| `/provider/referrals/[id]` | Provider referral detail with accept/reject actions |

**Key decisions:**

- Common Portal auth guard (`requireExternalPortal`) accepts `orgType = PROVIDER` or `LAW_FIRM`.
- Referral list scoping is enforced server-side by the CareConnect API based on JWT `orgId`/`orgType`.
- Activation redirect targets updated from `/careconnect/referrals/{id}` → `/provider/referrals/{id}`.
- AttachmentPanel rendered with `canUpload={false}` — providers cannot upload documents.
- Tenant Portal users (lien company, platform admin) are not affected; their routes are unchanged.

---

## 2. App Setup

**Location:** `apps/web/src/app/(common-portal)/`  
**Framework:** Next.js 15 (App Router), TypeScript, Tailwind CSS — shared with existing `apps/web`  
**BFF layer:** Same as Tenant Portal — Next.js Server Components read `platform_session` cookie;
`serverApi` forwards it to the Identity `/auth/me` endpoint for validation.

**Route group structure:**

```
apps/web/src/app/(common-portal)/
  layout.tsx                        ← provider/external layout (top-nav only, no sidebar)
  provider/
    dashboard/page.tsx              ← provider referral list dashboard
    referrals/[id]/page.tsx         ← provider referral detail
```

---

## 3. Authentication Flow

The Common Portal shares the platform's Identity-backed session model:

1. User authenticates via `/login` (unchanged).
2. Identity issues a JWT; the BFF stores it as an HttpOnly `platform_session` cookie.
3. `getServerSession()` calls `GET /identity/api/auth/me` server-side to validate and hydrate session.
4. `requireExternalPortal()` (new guard) ensures `orgType` is `PROVIDER` or `LAW_FIRM`;
   redirects unauthenticated users to `/login?returnTo={path}`.
5. JWT is never exposed to the browser; all API calls go through Next.js Server Components or
   the `/api/careconnect/[...path]` BFF route handler.

**Session shape exposed to the Common Portal UI:**
- `userId`, `email`, `orgId`, `orgType`, `productRoles`
- No `tenantId` for providers (they are not tenant-bound)

**Logout:** existing `/api/auth/logout` route — clears `platform_session` cookie.

---

## 4. Token → Identity UX

The activation UX was already built in B04 and prior blocks:

```
/referrals/accept/{id}?token=...
  → /referrals/activate?referralId=...&token=...
    → ActivationForm.handleSubmit → POST /api/careconnect/api/referrals/{id}/auto-provision
      → success → /login?returnTo=/provider/referrals/{id}   ← updated in B05
```

**B05 change:** The `returnTo` target in both `referrals/activate/page.tsx` and
`activation-form.tsx` was updated from `/careconnect/referrals/{id}` to
`/provider/referrals/{id}`, routing activated providers into the Common Portal.

**Existing user path:** `referrals/view/page.tsx` similarly updated so active providers
are redirected to `/provider/referrals/{id}` after login.

**Already-active outcome:** Redirects to `/login?returnTo=/provider/referrals/{id}`.

---

## 5. User Experience

### Provider Dashboard (`/provider/dashboard`)

- Lists all referrals where the provider's org is the receiving organisation.
- Scoping is enforced by the CareConnect API based on the JWT `orgId` claim — no frontend filter needed.
- Columns: Client name, Requested service, Urgency, Status, Date received.
- Status badges colour-coded (New=blue, Accepted=green, Declined=red, etc.).
- Links to referral detail page.
- Empty-state handling when no referrals are assigned.

### Provider Referral Detail (`/provider/referrals/[id]`)

- Fetches referral via `careConnectServerApi.referrals.getById(id)`.
- Access is enforced by the CareConnect API — 403 returned if provider is not the receiver.
- Renders: referral info panel, status actions (accept/decline), timeline, documents.
- `AttachmentPanel` rendered with `canUpload={false}` — providers cannot upload.
- Back navigation returns to `/provider/dashboard`.

---

## 6. Role Handling

| Actor | orgType | Guard result | Access |
|-------|---------|--------------|--------|
| Provider | PROVIDER | ✅ permitted | Dashboard + referral detail |
| Law Firm | LAW_FIRM | ✅ permitted | Dashboard + referral detail |
| Lien Company user | LIEN_COMPANY | ❌ redirect `/dashboard` | Tenant Portal only |
| Platform Admin | (none / platform) | ❌ redirect `/dashboard` | Tenant Portal only |
| Unauthenticated | — | ❌ redirect `/login?returnTo=...` | Login |

Law Firm support is included in the guard and layout. The referral list scope and detail
pages will work for Law Firms once the backend model supports law firm receiving flows.

---

## 7. Configuration

No new environment variables required. The Common Portal reuses:

- `GATEWAY_URL` — existing gateway base URL
- `platform_session` cookie — existing Identity-backed session

**Dev:** `GATEWAY_URL=http://127.0.0.1:5010` (default fallback in code)  
**Production:** `GATEWAY_URL` must be set to the production gateway URL (already done for Tenant Portal).

---

## 8. Test Results

| # | Test | Result | Notes |
|---|------|--------|-------|
| 1 | User can log in via Identity | ✅ Pass | Shared login flow, unchanged |
| 2 | Session persists via HttpOnly cookie | ✅ Pass | `platform_session`, unchanged |
| 3 | Logout clears session | ✅ Pass | Existing `/api/auth/logout` |
| 4 | Activation link provisions user | ✅ Pass | B04 flow, unchanged |
| 5 | Post-provision redirect → `/provider/referrals/{id}` | ✅ Pass | Updated in B05 |
| 6 | Provider dashboard loads referrals | ✅ Pass | Server component, API scoped |
| 7 | Referral detail loads correctly | ✅ Pass | Reuses `careConnectServerApi` |
| 8 | No JWT exposed to browser | ✅ Pass | HttpOnly cookie, server components |
| 9 | Tenant user redirected away from portal | ✅ Pass | `requireExternalPortal` guard |
| 10 | Unauthenticated access redirects to login | ✅ Pass | `requireExternalPortal` guard |
| 11 | build succeeds | ✅ Pass | No new packages |

---

## 9. Issues / Gaps

### 9.1 Law Firm referral list scoping
Law Firm users are admitted by the auth guard but the CareConnect backend does not yet
scope the referral list by `LAW_FIRM` orgType for the receiving side.  
**Status:** Deferred — data model does not yet support law firms as referral receivers.
Guard and layout are Law-Firm-ready for when the backend model is extended.

### 9.2 Provider profile / settings page
No profile management page is included in this block. Providers manage identity via Identity service.  
**Status:** Deferred to a future block.

### 9.3 Common Portal login page  
The portal uses the shared `/login` page. A co-branded provider login page is not included.  
**Status:** Out of scope for this block.

### 9.4 `portal/login` and `portal/my-application` (Injured Party Portal)
These existing placeholder routes are for a separate Injured Party Portal (phase 2).
They are NOT the Common Portal and are NOT modified by this block.
