# LS-NOTIF-SMS-009 — Control Center SMS Dashboard UI Integration

## 1. Initial Codebase Analysis

LS-NOTIF-SMS-009 adds the Control Center UI that consumes the five read-only Notification Service SMS Dashboard APIs from LS-NOTIF-SMS-008. No aggregation logic is added to Control Center.

## 2. Existing Control Center Routing/Layout Findings

- App router at `apps/control-center/src/app/`. All pages are Server Components under this directory.
- `<CCShell userEmail={session.email}>` wraps every page.
- `export const dynamic = 'force-dynamic'` is used on all data pages.
- Route for SMS Dashboard: `/notifications/sms-dashboard` — consistent with the existing NOTIFICATIONS nav section.
- Filter pattern: `?window=30d`, `?bucket=day`, `?ownership=all` URL params interpreted as searchParams in the Server Component (identical to `/analytics` page `?window=` pattern).

## 3. Existing Admin Authorization/Role Guard Findings

- `requirePlatformAdmin()` from `@/lib/auth-guards` redirects non-admins to `/login?reason=unauthorized`.
- All dashboard endpoints require PlatformAdmin. This guard is called at the top of the page, before any API calls.

## 4. Existing API Client/Service Pattern Findings

- `notifFetch<T>` / `notifClient` in `@/lib/notifications-api` — server-side only, reads `platform_session` cookie, sends `Authorization: Bearer`, prefix `/notifications/v1`.
- Error handling: throws `ApiError` on non-2xx; redirects to `/login` on 401.
- Graceful degradation: `Promise.allSettled` used for independent sections (exact pattern from existing pages).
- New types and client methods added to a dedicated `@/lib/sms-dashboard-api.ts` to keep `notifications-api.ts` focused on existing notification operations.

## 5. Existing Chart/Table/Card Component Findings

- **StatCard**: `@/components/dashboard/stat-card.tsx` — `label`, `value`, `icon`, `href`, `trend`.
- **Inline KPI cards**: `<div className="rounded-lg border border-gray-200 bg-white px-4 py-3">` + `<p className="text-2xl font-bold ...">` pattern (notifications/page.tsx).
- **Tables**: Inline `<table className="min-w-full divide-y divide-gray-100 text-sm">` + `<thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">`.
- **SVG chart**: `LatencySparkline` at `@/components/monitoring/latency-sparkline.tsx` — pure SVG, no library. Multi-line trend chart (`SmsTrendChart`) follows the same pattern.
- **Error banner**: `<div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">`.
- **Empty state**: `<div className="rounded-lg border border-gray-200 bg-white px-6 py-10 text-center">`.
- **Section header**: `<h2 className="text-base font-semibold text-gray-800 flex items-center gap-2">`.
- **Filter tabs**: `<a href="?param=value">` pill links (analytics/page.tsx pattern).

## 6. Files Added

| File | Purpose |
|------|---------|
| `apps/control-center/src/lib/sms-dashboard-api.ts` | TypeScript DTOs + API client methods for 5 dashboard endpoints |
| `apps/control-center/src/components/notifications/sms-trend-chart.tsx` | Pure-SVG multi-line trend chart (Client Component) |
| `apps/control-center/src/app/notifications/sms-dashboard/page.tsx` | Main dashboard Server Component page |

## 7. Files Modified

| File | Change |
|------|--------|
| `apps/control-center/src/lib/nav.ts` | Add "SMS Dashboard" nav entry under NOTIFICATIONS section |

## 8. API/Client Changes

New file `sms-dashboard-api.ts` adds:
- TypeScript interfaces: `SmsDashboardSummary`, `SmsDashboardTrendPoint`, `SmsDashboardTrendResult`, `SmsDashboardFailureItem`, `SmsDashboardFailureResult`, `SmsDashboardTenantItem`, `SmsDashboardTenantResult`, `SmsDashboardProviderItem`, `SmsDashboardProviderResult`, `SmsDashboardQuery`
- `smsDashboardApi.getSummary()`, `getTrends()`, `getFailures()`, `getTenants()`, `getProviders()` — all delegate to `notifClient.get<T>`

## 9. UI/Route Changes

- New route: `/notifications/sms-dashboard` — PlatformAdmin only.
- Nav entry added to NOTIFICATIONS section: "SMS Dashboard" with `badge: 'LIVE'`.
- Filter bar: `?window=7d|30d|90d` (default 30d), `?bucket=hour|day|week` (default day), `?ownership=all|tenant|platform` (default all).
- All sections load independently. A failure in one section shows a section-level error banner; other sections render normally.
- No client-side state needed — all filtering via URL params and full-page Server Component re-render.

## 10. Validation/Testing Performed

- TypeScript compilation: `pnpm --filter control-center build` — clean.
- Auth guard: `requirePlatformAdmin()` at page top — non-admins redirected before any API call.
- Security: No credentials, SettingsJson, CredentialsJson, full phone numbers, or raw provider payloads in any rendered output.
- All 5 API calls use `Promise.allSettled` — individual failures are isolated per section.
- Empty states tested via conditional rendering.
- Trend chart renders gracefully with 0 points.

## 11. Known Gaps / Issues

- **Tenant name enrichment**: Tenant breakdown table shows `tenantId` (GUID) only. Control Center has no local tenant name store for Notification Service-scoped tenants. Future enhancement: enrich tenant IDs from Identity service (existing `tenant-fetch.ts` pattern could be extended).
- **No provider credential filter**: Provider name filter is applied via URL `?provider=twilio` as a manual query param. No provider autocomplete UI — users type the provider name. Future enhancement: fetch provider list from `/notifications/v1/providers/configs` and render a dropdown.
- **No free-text tenantId filter UI**: The API supports tenantId filter but the filter bar doesn't expose it as a text input. Can be added in a follow-up.
- **Trend chart date labels**: X-axis bucket labels are omitted from SVG to avoid distortion with `preserveAspectRatio="none"`. A separate label row is rendered below the chart as HTML.

## 12. Recommended Next Steps

- Tenant name enrichment: call Identity service from Control Center to resolve tenant names in the tenant breakdown table.
- Provider autocomplete: fetch provider config list and render a dropdown filter.
- Export: add CSV download for failure and tenant breakdown tables.
- Date range picker: replace preset buttons with a date range input for custom windows.
- Trend chart interaction: add hover tooltips with exact counts per bucket.
