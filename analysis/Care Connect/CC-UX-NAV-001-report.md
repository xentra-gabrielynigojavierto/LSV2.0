# CC-UX-NAV-001 — Control Center Navigation Hub Refactor

**Status:** COMPLETE  
**Date:** 2026-04-23  

---

## 1. Objective

Replace the long scrollable left sidebar in the Control Center with:
1. A compact left rail (Home + contextual section links only)
2. A navigation hub landing page with category cards derived from `CC_NAV`
3. URL-driven group selection (`/?group=<slug>`) for refresh persistence

---

## 2. Codebase Analysis

### nav.ts (unchanged — single source of truth)
- `CC_NAV: NavSection[]` — 14 sections, ~52 nav items total
- Sections: OVERVIEW, PLATFORM, IDENTITY, RELATIONSHIPS, PRODUCT RULES, CARECONNECT, TENANTS, NOTIFICATIONS, AUDIT, TRACEABILITY, OPERATIONS, CATALOG, SYSTEM
- OVERVIEW contains only Dashboard (`/`) — this becomes the persistent Home link
- Items carry `badge?: 'LIVE' | 'IN PROGRESS' | 'MOCKUP' | 'NEW'`

### cc-sidebar.tsx (before)
- Rendered all 14 `CC_NAV` sections with every item visible — ~52 items
- Section headings were collapsible but sidebar still required vertical scroll
- Sidebar collapse (52px icon-only mode) preserved but items still exhaustive

### cc-shell.tsx
- Server component; renders top nav bar + `<CCSidebar />` + `<main>`
- No structural changes required

### app/page.tsx (before)
- Server component; `SystemStatusCard`, stat grid, breakdown cards, audit table, 3 quick `LinkCard` items
- No navigation hub; quick links were hand-coded, not derived from `CC_NAV`

---

## 3. Architecture Decisions

| Decision | Choice |
|---|---|
| URL state | `/?group=<slug>` query param — `router.push` on card click |
| Group slug format | lowercase + hyphens: `PRODUCT RULES` → `product-rules` |
| NavHub state | Client component using `useSearchParams()` (Suspense-wrapped) |
| Sidebar contextual lookup | `getSectionForPathname()` utility — O(n) scan of `CC_NAV` |
| Source of truth | `CC_NAV` only — no duplicate nav definitions anywhere |
| Collapse toggle | Preserved (220px ↔ 52px, Ctrl+[) |

---

## 4. Files Changed

| File | Change Type | Description |
|---|---|---|
| `apps/control-center/src/lib/nav-utils.ts` | **NEW** | Slug/lookup/group-model utilities |
| `apps/control-center/src/components/dashboard/navigation-group-grid.tsx` | **NEW** | Client NavHub component (group cards + detail panel) |
| `apps/control-center/src/components/shell/cc-sidebar.tsx` | **REFACTORED** | Compact contextual sidebar |
| `apps/control-center/src/app/page.tsx` | **UPDATED** | NavHub section added above metrics |

---

## 5. Component Summary

### `nav-utils.ts`
- `slugify(heading)` — heading string → URL slug
- `getSectionBySlug(slug)` — slug → `NavSection | undefined`
- `getSectionForPathname(pathname)` — pathname → owning `NavSection` (excludes OVERVIEW)
- `getNavGroupModels()` — returns `NavGroupModel[]` for dashboard cards (excludes OVERVIEW)
- `GROUP_ICON_MAP` — explicit icon mapping per group heading; falls back to first item's icon

### `NavigationGroupGrid` (client component)
- Suspense-wrapped `NavHubInner` for streaming compatibility
- Reads `?group` via `useSearchParams()` — no server round-trip on selection change
- Renders one `GroupCard` per group (12 groups — all CC_NAV except OVERVIEW)
- Each card: icon, heading, item count, live count
- Selected card: orange highlight (`border-orange-300 bg-orange-50`)
- Clicking selected card toggles deselection (routes back to `/`)
- "Clear" button in section header for deselecting
- `GroupDetailPanel`: grid of nav item cards (3 columns) showing icon, label, badge, route preview
- Skeleton loading state during Suspense resolution

### `CCSidebar` (refactored — compact)
- Home link always rendered at top (routes to `/`)
- `getSectionForPathname(pathname)` determines contextual section
- On `/` (dashboard): sidebar shows **only** Home — navigation happens in body
- On any other page: sidebar shows Home **+** the matched section's heading + items
- No more full multi-section list — never requires vertical scrolling
- Collapse toggle and icon-only mode fully preserved
- Active state treatment (accent bar, right pip, color) identical to original

### `DashboardPage` (updated)
- Navigation Hub card inserted between page intro and `SystemStatusCard`
- All existing stat cards, breakdown cards, audit table, monitoring card preserved
- Three hard-coded quick `LinkCard` items removed (superseded by NavHub)

---

## 6. Route Behavior

| Route | Sidebar | Page body |
|---|---|---|
| `/` (no group) | Home only | NavHub cards + empty-state prompt + metric cards |
| `/?group=audit` | Home only | NavHub cards (audit highlighted) + Audit detail panel + metrics |
| `/?group=tenants` | Home only | NavHub cards (tenants highlighted) + Tenants detail panel + metrics |
| `/synqaudit/*` | Home + AUDIT section items | Normal page content |
| `/tenants` | Home + TENANTS section items | Normal page content |
| `/notifications/*` | Home + NOTIFICATIONS section items | Normal page content |
| `/workflows/*` | Home + OPERATIONS section items | Normal page content |
| Any unmatched route | Home only | Normal page content |

---

## 7. Validation Results

| Check | Result |
|---|---|
| `tsc --noEmit` (CC app) | **0 errors** |
| CC app startup (`✓ Ready in ~5s`) | **Clean — no compilation errors** |
| CC HTTP probe (`GET /login`) | **HTTP 200** |
| `CC_NAV` groups rendered via `getNavGroupModels()` | 12 groups (OVERVIEW excluded — it's Home) |
| Sidebar renders only Home on `/` | Confirmed by code path: `isHome=true` → `contextSection=undefined` |
| Sidebar renders contextual links on sub-pages | Confirmed via `getSectionForPathname` |
| URL-driven group state (`/?group=<slug>`) | Via `useSearchParams` + `router.push` — survives refresh |
| Existing routes preserved | No route files modified |
| TypeScript: new files type-clean | `tsc --noEmit` → 0 errors |

Note: The preview screenshot tool is locked to port 5000 (the web app proxy). The CC app runs independently on port 5004. Manual login to the CC dashboard is required to visually validate the NavHub rendering.

---

## 8. Deviations from Spec

| Spec item | Treatment |
|---|---|
| 3 quick `LinkCard`s on old dashboard | Removed — superseded by the NavigationGroupGrid which covers all groups |
| Optional recently-used / pinned shortcuts | Deferred — not implemented as noted in spec as optional |
| Group-level health/status on cards | Deferred — non-blocking, cards show item count + live count instead |

---

## 9. Known Gaps / Follow-ups

- Recently used links / pinned shortcuts: explicitly deferred per spec
- Group-level health summary on nav cards: could be added later if desired
- The three removed `LinkCard` items (Platform Readiness, SynqAudit, CareConnect Integrity) are now discoverable through the NavHub under their respective groups
