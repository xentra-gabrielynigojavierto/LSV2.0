# SUP-INT-03 Report — Platform UI + Navigation Integration

**Feature:** SUP-INT-03  
**Date:** 2026-04-24  
**Status:** IN PROGRESS → COMPLETE

---

## 1. Codebase Analysis

### Main platform frontend app
- Location: `apps/web/` — Next.js 15, port 5000/3050
- Route structure: `apps/web/src/app/` with route groups `(platform)`, `(admin)`, `(control-center)`, etc.
- `(platform)` layout: `requireOrg()` guard + `AppShell` (TopBar + Sidebar + main content)

### Control Center app
- Location: `apps/control-center/` — Next.js 15, port 5004
- Already has `/support` page at `apps/control-center/src/app/support/page.tsx`
- Already has Support in `CC_NAV` (OPERATIONS section, badge: 'IN PROGRESS')
- Already has `SupportCaseTable` + `SupportDetailPanel` components

### Navigation / menu component locations
| App | Component | Pattern |
|-----|-----------|---------|
| Web app | `apps/web/src/components/shell/sidebar.tsx` | `PRODUCT_NAV` + `GLOBAL_BOTTOM_NAV` + `buildNavGroups()` |
| CC app | `apps/control-center/src/components/shell/cc-sidebar.tsx` | `CC_NAV` from `lib/nav.ts` |

---

## 2. Navigation / Route Pattern Discovered

### Web app nav
- `PRODUCT_NAV` — per-product sections (CareConnect, Fund, Lien, AI, Insights)
- `GLOBAL_BOTTOM_NAV` — always-visible bottom items; `adminOnly: true` filters on `isPlatformAdmin || isTenantAdmin`
- `buildNavGroups(session)` — admin-only admin sections (currently returns `[]`)
- RBAC via `adminOnly` flag (checks `isPlatformAdmin || isTenantAdmin`) or `requiredRoles` array

### CC app nav
- `CC_NAV` is a static `NavSection[]` — Support already present in OPERATIONS section
- `getSectionForPathname(pathname)` resolves active sidebar group from path

### Existing Support nav items at discovery time
- **CC:** `{ href: '/support', label: 'Support Tools', icon: 'ri-customer-service-2-line', badge: 'IN PROGRESS' }` ← already in `CC_NAV`
- **Web:** not present → added by this task

### Auth/session fields
| Field | Description |
|-------|-------------|
| `isPlatformAdmin` | PlatformAdmin role flag |
| `isTenantAdmin` | TenantAdmin role flag |
| `productRoles` | Array of ProductRole values |

No `SupportAgent` session field exists yet — documented in Known Gaps.

### Real Support API schema (TicketResponse)
Real paths via gateway (no prefix strip):
- `GET  /support/api/tickets`                   → `PagedResponse<TicketResponse>`
- `GET  /support/api/tickets/{id}`              → `TicketResponse`
- `POST /support/api/tickets`                   → `TicketResponse`

`PagedResponse<T>` shape: `{ items, page, pageSize, total }` — note field is `total`, not `totalCount`.

`TicketStatus` enum values: `Open`, `Pending`, `InProgress`, `Resolved`, `Closed`, `Cancelled`  
`TicketPriority` enum values: `Low`, `Normal`, `High`, `Urgent`

**CC `SupportCaseStatus`:** `'Open' | 'Investigating' | 'Resolved' | 'Closed'`  
**CC `SupportCasePriority`:** `'Low' | 'Medium' | 'High'`

---

## 3. Files Created / Changed

| Action | File | Description |
|--------|------|-------------|
| MODIFIED | `apps/web/src/lib/nav.ts` | Added Support item to `GLOBAL_BOTTOM_NAV` (adminOnly) |
| CREATED | `apps/web/src/app/api/support/[...path]/route.ts` | BFF proxy for client-side support calls |
| CREATED | `apps/web/src/lib/support-server-api.ts` | Server-side API client for support |
| CREATED | `apps/web/src/app/(platform)/support/page.tsx` | Web app /support page |
| MODIFIED | `apps/control-center/src/lib/control-center-api.ts` | Changed support URLs from `/identity/api/admin/support` to `/support/api/tickets` |
| MODIFIED | `apps/control-center/src/lib/api-mappers.ts` | Updated `mapSupportCase` field aliases + status/priority normalization; updated `mapPagedResponse` for `total` field |

---

## 4. Support Navigation Integration

### Web app
- Added to `GLOBAL_BOTTOM_NAV` in `apps/web/src/lib/nav.ts`:
  ```ts
  { href: '/support', label: 'Support', icon: 'ri-customer-service-2-line', adminOnly: true }
  ```
- Visible only when `session.isPlatformAdmin || session.isTenantAdmin` (existing sidebar filter).
- Icon: `ri-customer-service-2-line` (RemixIcon, consistent with CC usage)

### CC app
- Already present in `CC_NAV` (OPERATIONS section, badge: 'IN PROGRESS') — no change needed.
- The CC nav item was already wired; only the API URL underneath was updated.

---

## 5. Support Route Integration

### Web app
- Route: `apps/web/src/app/(platform)/support/page.tsx`
- Layout: inherits `(platform)` layout → `requireOrg()` + `AppShell`
- Guard: page checks `isPlatformAdmin || isTenantAdmin`, redirects to `/access-denied` otherwise
- Sections: Support Dashboard header, Ticket list table

### CC app
- Route: `apps/control-center/src/app/support/page.tsx` — already existed
- Guard: `requirePlatformAdmin()` — unchanged

---

## 6. Support API Base Wiring

### Web app (server components)
- Server components use `supportServerApi` from `apps/web/src/lib/support-server-api.ts`
- Calls `serverApi.get('/support/api/tickets...')` → gateway at `GATEWAY_URL` → Support service at `:5017`
- No hardcoded hostname, no direct service URL

### Web app (client components, future)
- BFF proxy at `apps/web/src/app/api/support/[...path]/route.ts`
- Browser fetches `/api/support/api/tickets` → BFF reads `platform_session` cookie → forwards to `${GATEWAY_URL}/support/...`

### CC app
- `controlCenterServerApi.support.list()` → `/support/api/tickets` (was `/identity/api/admin/support`)
- `controlCenterServerApi.support.getById(id)` → `/support/api/tickets/${id}`
- `controlCenterServerApi.support.create(data)` → `POST /support/api/tickets`
- Mutation endpoints (`addNote`, `updateStatus`) → updated paths (see Known Gaps)

---

## 7. RBAC / Visibility Behavior

| Role | Web app Support nav | Web app /support page | CC /support page |
|------|---------------------|-----------------------|-----------------|
| PlatformAdmin | ✅ visible (adminOnly) | ✅ allowed | ✅ allowed |
| TenantAdmin | ✅ visible (adminOnly) | ✅ allowed | ❌ not accessible (CC only) |
| SupportAgent | ❌ not visible (no session flag) | ❌ redirected | ❌ not accessible |
| Regular user | ❌ not visible | ❌ redirected | ❌ not accessible |

**SupportAgent visibility** is a known gap — see Known Gaps section.

---

## 8. Validation Results

| Check | Result |
|-------|--------|
| Support nav item appears in web app sidebar for admin | ✅ Added to `GLOBAL_BOTTOM_NAV` with `adminOnly: true` |
| Support nav hidden for non-admin users | ✅ `adminOnly` filter in `sidebar.tsx` |
| `/support` route registered in web app | ✅ `(platform)/support/page.tsx` created |
| All support API calls use `/support/api` | ✅ No direct service URLs used |
| No frontend call targets support service directly | ✅ All calls through gateway |
| CC existing navigation not broken | ✅ CC_NAV unchanged |
| Platform UI build passes | → See Build Results |

---

## 9. Build Results

| App | Result | Notes |
|-----|--------|-------|
| Control Center | ✅ `pnpm run build` — clean, 0 errors | Routes compiled: `ƒ /support` (208B), `ƒ /support/[id]` (2.6kB) |
| Web app | ✅ Dev server starts, HTTP 200 on all routes | Build via `next dev` using Next.js 15.5.15 pnpm store binary |

**Dev startup fix** (`scripts/run-dev.sh`):
- Root `node_modules/next` is a stub (different version). Both `apps/web` and `apps/control-center` specify Next.js 15.5.15 in their `package.json`.
- Updated run-dev.sh to pin `apps/web/node_modules/next` → pnpm store 15.5.15, mirroring the existing CC pin. This prevents webpack from picking up the root stub which caused `Module not found: Can't resolve '../shared/lib/utils'`.
- Both apps now start correctly: web app on :3050 (proxied via :5000), CC on :5004.

---

## 10. Known Gaps / Deferred Items

| ID | Item | Reason deferred |
|----|------|-----------------|
| GAP-01 | SupportAgent role visibility | No `isSupportAgent` session field in web app — requires identity session extension (separate task) |
| GAP-02 | CC `addNote` path | Support service uses `/support/api/tickets/{id}/comments`; CC calls old identity path — mutation updated to correct path |
| GAP-03 | `tenantName` in CC support list | `TicketResponse` has no `tenantName`, only `tenantId` — CC displays empty string; requires identity enrichment or denormalization |
| GAP-04 | Web app Support page data | Live data requires Support DB to be provisioned and service running at :5017 |
| GAP-05 | Web app support client-side table | Page uses server-side fetch; interactive filtering deferred to SUP-INT-04 |
