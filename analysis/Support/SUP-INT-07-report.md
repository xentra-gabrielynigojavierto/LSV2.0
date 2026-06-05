# SUP-INT-07 Report — Customer Support Portal UI

_Status: COMPLETE_

---

## 1. Codebase Analysis

### Web app location
- `apps/web` — Next.js 15 App Router, route-group based
- Route groups under `apps/web/src/app/`:
  - `(platform)/` — main tenant/user portal (uses `AppShell`)
  - `(admin)/` — admin area
  - `api/` — BFF/proxy routes

### Existing Support UI
| Route | File | Access |
|---|---|---|
| `/support` | `(platform)/support/page.tsx` | PlatformAdmin or TenantAdmin |
| `/support/[id]` | `(platform)/support/[id]/page.tsx` | PlatformAdmin or TenantAdmin |

Both pages are Server Components (`force-dynamic`) that call `getServerSession()` and the internal `/support/api/tickets` endpoint via `supportServerApi`.

### BFF Proxy
`apps/web/src/app/api/support/[...path]/route.ts` — catch-all that reads `platform_session` cookie and forwards as `Authorization: Bearer` to the gateway. Already handles all paths including `/support/api/customer/...` paths.

### Server-side API client
`apps/web/src/lib/server-api-client.ts` — `serverApi.get/post/patch/delete`; reads `platform_session` cookie; throws `ServerApiError(status, message)` on non-2xx.

### Session model
`getServerSession()` → `PlatformSession`. Populated from `/identity/api/auth/me`.
`SystemRole` values: `PlatformAdmin`, `TenantAdmin`, `StandardUser`.
**ExternalCustomer is not a platform SystemRole** — it is a Support-service-specific JWT role.

### UI conventions
- Tailwind CSS, Remix Icons (`ri-*`)
- Server Components for data fetching; Client Components (`'use client'`) for interactivity
- `force-dynamic` on all data pages

---

## 2. Existing Portal / Auth Pattern

### Platform session
The `platform_session` HttpOnly cookie stores the internal user's JWT. This JWT has roles like `PlatformAdmin`, `TenantAdmin`, `StandardUser`. The Identity service's `/auth/me` endpoint validates it.

### Customer session — MISSING
There is no customer login flow, customer JWT issuance, or separate customer session cookie. The customer JWT (with `role: ExternalCustomer`, `tenant_id`, `external_customer_id` claims) required by the SUP-TNT-03 backend endpoints **does not exist in the current platform**.

**Consequence:** All calls to `/support/api/customer/*` from the platform web app will return `403 Forbidden`, because the platform_session token has internal roles, not `ExternalCustomer`.

**Handling strategy (fail-closed):**
- Pages check for platform login (redirect to `/login` if absent)
- Pages call only customer endpoints (`/support/api/customer/tickets`)
- `403` response → display "Customer portal sign-in not yet available" message
- `401` response → redirect to `/login`
- `404` response → show not-found message

This is intentional: the UI is wired up correctly and will work as soon as customer token issuance is implemented. Until then, users see an honest, safe message.

---

## 3. Customer Route Design

New routes added under the existing `(platform)` layout:

| Route | Purpose |
|---|---|
| `/support/customer` | Redirect → `/support/customer/tickets` |
| `/support/customer/tickets` | Customer ticket list |
| `/support/customer/tickets/[id]` | Customer ticket detail + comment form |

**Why under `(platform)`?**
- Consistent shell and navigation
- Inherits AppShell layout
- Reuses session cookie infrastructure

**Why distinct from `/support`?**
- `/support` is the admin/agent view (TenantAdmin, PlatformAdmin)
- `/support/customer` is the customer-facing view
- Different access guard logic

---

## 4. API Client / BFF Wiring

### Server-side (Server Components)
Added `customerSupportServerApi` to `apps/web/src/lib/support-server-api.ts`:
- `customerTickets.list(params)` → `GET /support/api/customer/tickets`
- `customerTickets.getById(id)` → `GET /support/api/customer/tickets/{id}`

Uses the same `serverApi.get()` pattern → reads `platform_session` cookie → sends as `Authorization: Bearer` → gateway → Support service customer endpoint.

Backend enforces: `tenantId + externalCustomerId + CustomerVisible`. The platform_session token will be rejected with 403 (no ExternalCustomer role) until customer login is implemented.

### Client-side (comment submission)
`CustomerCommentForm` (Client Component) POSTs to `/api/support/api/customer/tickets/{id}/comments`.
The BFF proxy at `/api/support/[...path]/route.ts` forwards it with the `platform_session` token.
Backend returns 403 (no ExternalCustomer role). The form shows an access-denied message safely.

**Never called:**
- `/support/api/tickets` (internal admin endpoint)
- `/support/api/tickets/{id}/comments` (internal comment endpoint)
- Any internal/admin endpoint

**Never accepted from UI as auth:**
- `externalCustomerId`
- `tenantId` (not injected)

---

## 5. Customer Ticket List UI

File: `apps/web/src/app/(platform)/support/customer/tickets/page.tsx`

Shows:
- Ticket number (font-mono)
- Title (link to detail)
- Status badge
- Priority badge
- Created date

Does NOT show:
- Queue info, assignment, agent names
- Internal metadata, source, audit fields
- Tenant-wide filters

Auth handling:
- No session → `/login` redirect
- 403 → "Customer portal access not yet available"
- 401 → `/login` redirect
- 500/service error → error card

---

## 6. Customer Ticket Detail UI

File: `apps/web/src/app/(platform)/support/customer/tickets/[id]/page.tsx`

Shows:
- Ticket number, title, status, priority
- Description
- Created / updated dates
- Comment submission form (via `CustomerCommentForm` client component)

Does NOT show:
- Product references (internal deep links, not customer-safe)
- Internal timeline or audit log
- Queue/assignment/status controls
- Internal notes

---

## 7. Customer Comment UI

Component: `apps/web/src/components/support/CustomerCommentForm.tsx`

- Required body field, min 1 char, max 4000 chars
- Submits to: `POST /api/support/api/customer/tickets/{id}/comments` (via BFF)
- On 403: shows "Customer portal sign-in is not yet available" message
- On 201: shows success state; clears form
- Author identity: comes from JWT only (not UI)
- No visibility selector, no author override

---

## 8. Auth / Access Behavior

| Scenario | Behavior |
|---|---|
| No platform_session cookie | Redirect to `/login` |
| Session expired | Redirect to `/login` (403 from `/auth/me`) |
| Logged in, no customer token | 403 from backend → "Customer portal access not yet available" |
| Backend 401 | Redirect to `/login` |
| Backend 403 | Show customer-login-unavailable message (fail-closed) |
| Backend 404 | Show "ticket not found" (not found message, no leakage) |
| Backend 500/network error | Show generic error card |

---

## 9. Security Validation

| Rule | Status |
|---|---|
| Customer UI calls only `/support/api/customer/*` | PASS |
| No `/support/api/tickets` (internal) calls | PASS |
| No externalCustomerId from UI as auth override | PASS |
| No tenantId injected from UI | PASS |
| 403 shown as access-denied, not bypassed | PASS |
| 404 shown without leaking reason | PASS |
| Existing `/support` admin UI unchanged | PASS |
| No customer login/token issuance added | PASS — deferred |
| No Comms/SLA/Task/Queue logic | PASS |

---

## 10. Files Created / Changed

| File | Change |
|---|---|
| `apps/web/src/lib/support-server-api.ts` | Added customer types + `customerSupportServerApi` |
| `apps/web/src/app/(platform)/support/customer/page.tsx` | New — redirect to /tickets |
| `apps/web/src/app/(platform)/support/customer/tickets/page.tsx` | New — customer ticket list |
| `apps/web/src/app/(platform)/support/customer/tickets/[id]/page.tsx` | New — customer ticket detail |
| `apps/web/src/components/support/CustomerCommentForm.tsx` | New — comment submission client component |
| `analysis/SUP-INT-07-report.md` | This file |

---

## 11. Build / Test Results

**TypeScript type check** (`tsc --noEmit --skipLibCheck`) → **EXIT 0**, no errors.

New files type-checked successfully:
- `apps/web/src/lib/support-server-api.ts` (new types + `customerSupportServerApi`)
- `apps/web/src/app/(platform)/support/customer/page.tsx`
- `apps/web/src/app/(platform)/support/customer/tickets/page.tsx`
- `apps/web/src/app/(platform)/support/customer/tickets/[id]/page.tsx`
- `apps/web/src/components/support/CustomerCommentForm.tsx`

No backend changes — `dotnet build` not required for this block.

---

## 12. Known Gaps / Deferred Items

1. **Customer token issuance** — No customer login/JWT issuance is implemented. All calls to `/support/api/customer/*` will return 403 (platform_session has internal roles, not ExternalCustomer). The UI handles this gracefully with an access-unavailable message. This is the primary blocker for end-to-end functionality.

2. **Customer comment list** — SUP-TNT-03 did not expose a customer-safe comment list endpoint. After submitting a comment, the detail page shows a success message explaining that the conversation view requires a future customer comment endpoint.

3. **Customer navigation** — No nav item is added to AppShell sidebar for the customer portal. Customer users would need to be directed here via direct URL or a future nav item once customer login is available.

4. **Customer session cookie** — When customer login is implemented, a separate customer session pattern (separate cookie or same cookie with ExternalCustomer role) will be needed. The BFF proxy is already compatible.

---

## 13. Final Readiness Assessment

**READY (with deferred customer login)**

| Criterion | Status |
|---|---|
| Customer routes exist | PASS |
| Only `/support/api/customer/*` called | PASS |
| No internal/admin endpoints | PASS |
| No externalCustomerId/tenantId from UI | PASS |
| 403/401/404 handled safely | PASS |
| No customer login faked | PASS — shown as unavailable |
| Existing admin/agent Support UI unbroken | PASS |
| Comment list gap documented | PASS |
| Build passes | PASS — tsc EXIT 0 |
| Report complete | PASS |
