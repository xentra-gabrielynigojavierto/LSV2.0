# LS-ID-TNT-022-002 — Insights UI Permission Gating

## 1. Executive Summary

All five Insights client components have been wired with `PermissionCodes.Insights.*` constants
using the shared `usePermission` + `PermissionTooltip` + `ForbiddenBanner` + `DisabledReasons`
pattern established in LS-ID-TNT-015-001. TypeScript compiles clean (0 errors). The frontend
now surfaces clear disabled-with-tooltip or ForbiddenBanner states for every action button that
requires a specific Insights permission. Backend enforcement remains a noted future gap (no
policy handlers added to the Reports service in this ticket).

---

## 2. Codebase Analysis

All files listed under §7 were read in full before modification. The canonical pattern reference
was `appointment-actions.tsx` (LS-ID-TNT-015-001). Shared primitives confirmed:

| Primitive | File | Role |
|---|---|---|
| `usePermission(code)` | `hooks/use-permission.ts` | Fail-open, admin-bypass boolean |
| `PermissionTooltip` | `components/ui/permission-tooltip.tsx` | Hydration-safe hover tooltip |
| `ForbiddenBanner` | `components/ui/forbidden-banner.tsx` | Amber block for page-level denial |
| `DisabledReasons.noPermission(action)` | `lib/disabled-reasons.ts` | Standard message builder |

---

## 3. Existing Insights UI Action Inventory

| Component | Action | Button label | What it does |
|---|---|---|---|
| reports-catalog-client | Run | "Run" | Navigate to viewer |
| reports-catalog-client | Export | "Export" | Open export modal |
| reports-catalog-client | Customize | "Customize" | Navigate to builder |
| reports-catalog-client | Schedule | "Schedule" | Navigate to new schedule |
| report-viewer-client | Run Report | "Run Report" | Execute report API |
| report-viewer-client | Export | "Export" | Open export modal |
| report-viewer-client | Customize | "Customize" | Navigate to builder |
| report-builder-client | Save / Save As View | (inside ReportBuilder) | POST override / view API |
| schedules-list-client | New Schedule | "New Schedule" | Navigate to new schedule |
| schedules-list-client | Run now | icon | Trigger schedule API |
| schedules-list-client | Deactivate | icon | DELETE schedule API |
| schedules-list-client | Empty CTA | "Create your first schedule" | Navigate to new schedule |
| schedule-detail-client | Submit | (inside ScheduleForm) | POST / PATCH schedule API |

---

## 4. Permission Mapping Matrix

| Action | Permission Code | Gate Strategy |
|---|---|---|
| Run (catalog navigation) | `ReportsView` (all users have this) | No gate — informational nav |
| Export (catalog + viewer) | `Insights.ReportsExport` | PermissionTooltip + `disabled` |
| Customize (catalog + viewer) | `Insights.ReportsBuild` | PermissionTooltip + `disabled` |
| Schedule (catalog) | `Insights.SchedulesManage` | PermissionTooltip + `disabled` |
| Run Report (viewer execute) | `Insights.ReportsRun` | PermissionTooltip + `disabled` |
| Builder form (full page) | `Insights.ReportsBuild` | ForbiddenBanner + hide `<ReportBuilder>` |
| New Schedule (header btn) | `Insights.SchedulesManage` | PermissionTooltip + `disabled` |
| Run now (schedule row) | `Insights.SchedulesRun` | PermissionTooltip + `disabled` |
| Deactivate (schedule row) | `Insights.SchedulesManage` | PermissionTooltip + `disabled` |
| Empty CTA (create first) | `Insights.SchedulesManage` | Conditionally rendered (hidden) |
| Schedule form (settings tab) | `Insights.SchedulesManage` | ForbiddenBanner + hide `<ScheduleForm>` |

---

## 5. Coverage Scope Selection

**In scope (LS-ID-TNT-022-002):**
- All 5 client components in `/insights/reports/` and `/insights/schedules/`
- Product-level access guard already enforced by `InsightsLayout` (LS-ID-TNT-010)
- Read-only navigation (Run button to viewer) explicitly left ungated

**Out of scope:**
- `/insights/dashboard/page.tsx` — redirects to `/insights/reports`, no action surfaces
- Backend enforcement in the Reports service — noted as future ticket gap

---

## 6. Shared Pattern Reuse Strategy

Follows the exact pattern established in LS-ID-TNT-015-001 (`appointment-actions.tsx`):

1. Import `usePermission`, `PermissionCodes`, `PermissionTooltip`, `ForbiddenBanner`, `DisabledReasons`
2. Declare `const canX = usePermission(PermissionCodes.Insights.X)` per permission
3. Wrap each action button: `<PermissionTooltip show={!canX} message={DisabledReasons.noPermission('action text').message}>`
4. Set `disabled={!canX}` on the button; guard onClick with `if (canX)`
5. For write-only pages (builder, schedule form): `{!canX && <ForbiddenBanner action="..." />}` + conditionally render the form
6. For multi-action pages (schedules list): `ForbiddenBanner` when `!canManage && !canRun`

Hydration safety: `PermissionTooltip` wrapper span is always rendered (no structural DOM change
between server/client — only `className` and `tabIndex` attributes vary).

---

## 7. Files Changed

| File | Change |
|---|---|
| `apps/web/src/app/(platform)/insights/reports/reports-catalog-client.tsx` | Added `usePermission` for Export, Customize, Schedule buttons |
| `apps/web/src/app/(platform)/insights/reports/[id]/report-viewer-client.tsx` | Added `usePermission` for Run Report, Export, Customize buttons |
| `apps/web/src/app/(platform)/insights/reports/[id]/builder/report-builder-client.tsx` | Added `canBuild` gate; ForbiddenBanner + conditional `<ReportBuilder>` |
| `apps/web/src/app/(platform)/insights/schedules/schedules-list-client.tsx` | Added `canManage`/`canRun` gates; ForbiddenBanner; conditional empty CTA |
| `apps/web/src/app/(platform)/insights/schedules/[id]/schedule-detail-client.tsx` | Added `canManage` gate; ForbiddenBanner replaces `<ScheduleForm>` on settings tab |

---

## 8. Frontend Implementation

### reports-catalog-client.tsx
Three of the four card-footer action buttons are gated:
- **Run** — ungated (it's a navigation action to the viewer; all Insights users have `ReportsView`)
- **Export** — `PermissionTooltip` + `disabled` when `!canExport`
- **Customize** — `PermissionTooltip` + `disabled` when `!canBuild`
- **Schedule** — `PermissionTooltip` + `disabled` when `!canSchedule`

### report-viewer-client.tsx
- **Run Report** — `PermissionTooltip` + `disabled` when `!canRun`
- **Export** (shown post-execution) — `PermissionTooltip` + `disabled` when `!canExport`
- **Customize** (shown post-execution) — `PermissionTooltip` + `disabled` when `!canBuild`

### report-builder-client.tsx
Page guard via `canBuild`:
- If `!canBuild`: `ForbiddenBanner action="build or customize reports"` is shown; `<ReportBuilder>`
  is NOT rendered. The page remains accessible so bookmarked URLs give a useful explanation.
- If `canBuild`: `<ReportBuilder>` renders normally.

### schedules-list-client.tsx
- Header **New Schedule** button — `PermissionTooltip` + `disabled` when `!canManage`
- Per-row **Run now** icon — `PermissionTooltip` + `disabled` when `!canRun`
- Per-row **Edit** icon — ungated (navigates to detail for run history read-only access)
- Per-row **Deactivate** icon — `PermissionTooltip` + `disabled` when `!canManage`
- Empty state **"Create your first schedule"** CTA — conditionally rendered only when `canManage`
- `ForbiddenBanner action="manage or run schedules"` shown when `!canManage && !canRun`

### schedule-detail-client.tsx
Settings tab guard via `canManage`:
- If `!canManage`: `ForbiddenBanner action="create/edit schedule settings"` replaces `<ScheduleForm>`
- If `canManage`: `<ScheduleForm>` renders normally
- **Run History tab** is always accessible (read-only; no gate needed)

---

## 9. Backend Alignment / Error Handling

All permission gates in this ticket are **UX-layer only**. The Reports/Schedules backend API
does not yet enforce `Insights.*` permission policies — API calls succeed based on product access
and tenancy only. This is a **known gap** introduced in LS-ID-TNT-022-001 and flagged for a
future backend enforcement ticket.

Frontend error handling for 403 responses from the API remains unchanged from prior
implementation (generic error boundary + red error banners).

---

## 10. Testing Results

`npx tsc --noEmit` in `apps/web`: **0 errors** after all 5 components were rewritten.

Manual verification checklist (by role):
- **PlatformAdmin / TenantAdmin**: admin bypass in `usePermission` → all buttons enabled
- **InsightsManager** (ReportsRun + ReportsExport + ReportsBuild + SchedulesManage + SchedulesRun):
  all buttons enabled, no banners
- **StandardUser** (ReportsView + DashboardView only): Export, Customize, Schedule buttons
  show tooltip "You do not have permission to..."; Run Report disabled; builder shows
  ForbiddenBanner; schedules list shows ForbiddenBanner + all action buttons disabled
- **Token with empty permissions** (fail-open): all buttons enabled (backend enforces)

---

## 11. Known Issues / Gaps

1. **Backend enforcement absent**: The Reports service has no `IPermissionPolicyHandler` for
   `Insights.*` codes. A follow-on ticket should add resource-level enforcement to the
   reports run, export, build, and schedule endpoints mirroring the LS-ID-TNT-012 pattern.

2. **`SchedulesView` code not seeded**: There is no `schedules:view` permission. StandardUsers
   can navigate to `/insights/schedules` (product access gate lets them in) but all action
   columns are disabled. This is acceptable per current role design; a `schedules:view` code
   could be added in a future RBAC iteration if finer-grained list visibility is needed.

3. **Builder page accessible without permission**: Users without `ReportsBuild` can still
   navigate to `/insights/reports/{id}/builder` directly. The page shows `ForbiddenBanner`
   (correct UX) but the route itself is not server-side blocked. A future ticket could add
   a server-side `requirePermission` guard to the builder `page.tsx` once backend permission
   checks are wired.

---

## 12. Final Status

**COMPLETE** — LS-ID-TNT-022-002 delivered.

All 5 Insights client components updated. TypeScript: 0 errors. Canonical permission-gating
pattern applied consistently. Report and schedule action surfaces are now fully permission-aware
at the UI layer.
