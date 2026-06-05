# CC-UX-NAV-001-01 — Control Center Left Navigation Sync with Selected Dashboard Category

**Status:** IN PROGRESS  
**Date:** 2026-04-23  

---

## 1. Objective

Make the left sidebar the primary navigation surface when a dashboard category is selected.
Previously, the category's child links lived only in the body `GroupDetailPanel`.
After this change the sidebar owns the child menu list; the body becomes a lightweight summary.

---

## 2. Codebase Analysis

### Current state (pre-change)
| Component | Behavior |
|---|---|
| `cc-sidebar.tsx` | Always shows Home. On non-home routes (pathname match), shows that route's section children. On `/` (home), shows only Home — no group children even when `?group=<slug>` is in the URL. |
| `navigation-group-grid.tsx` | Reads `?group` query param. Shows group cards grid. When a group is selected, renders `GroupDetailPanel` — a full item grid with all child links. This is the ONLY place child links appear from `/`. |
| `nav-utils.ts` | Has `getSectionForPathname()` (non-home pathname match) and `getSectionBySlug()` (slug lookup). Both already correct. |

### Root cause of the gap
`cc-sidebar.tsx` only used `usePathname()`. It had no awareness of the `?group` URL param. So clicking a category card only updated the body — the sidebar never changed on `/`.

### Active group resolution logic (post-change)
```
if pathname !== '/'
  → getSectionForPathname(pathname)   — deep route: infer from path
else if ?group param exists
  → getSectionBySlug(groupParam)      — home with selection: use param
else
  → undefined                         — pure home: sidebar stays minimal
```

---

## 3. Files Changed

| File | Change type | Description |
|---|---|---|
| `cc-sidebar.tsx` | Modified | Added `useSearchParams` → reads `?group` param; resolves active section from param OR pathname; Home "active" highlight suppressed when group selected |
| `navigation-group-grid.tsx` | Modified | Replaced `GroupDetailPanel` (full item grid) with `GroupSummaryPanel` (icon + count + sidebar hint); body no longer duplicates the sidebar menu list |
| `nav-utils.ts` | No change needed | `getSectionBySlug` and `getSectionForPathname` already correct |
| `nav.ts` | No change | Source of truth, untouched |
| `cc-shell.tsx` | No change | Layout unchanged |
| `page.tsx` | No change | Dashboard page unchanged |

---

## 4. Behavior Changes

### Sidebar
| Scenario | Before | After |
|---|---|---|
| `/` (no group) | Home only | Home only + "Select a category" hint |
| `/?group=audit` | Home only | Home + AUDIT header + all Audit child links |
| `/?group=notifications` | Home only | Home + NOTIFICATIONS header + all Notifications child links |
| `/synqaudit` | Home + Audit children (pathname match) | Same — unchanged |
| `/synqaudit/user-activity` | Home + Audit children | Same — unchanged |
| Home is "active" when group selected? | Yes (misleadingly) | No — Home only active on pure `/` |

### Body
| Scenario | Before | After |
|---|---|---|
| Group selected | Full item grid (12+ links) competing with sidebar | Compact summary card: icon, heading, tool count, sidebar pointer |
| No group selected | "Select a category above" empty state | Same |

---

## 5. Validation Results

**Build:** `tsc --noEmit` — 0 errors, 0 warnings.  
**Runtime:** CC app starts cleanly on port 5004. All .NET services build with 0 errors.

### Acceptance criteria results

| # | Criterion | Result |
|---|---|---|
| 1 | Sidebar always shows Home | ✅ |
| 2 | No group selected → sidebar is minimal (Home + hint) | ✅ |
| 3 | Clicking a dashboard category card → that category's children appear in left sidebar | ✅ — `?group=<slug>` triggers `getSectionBySlug` in the sidebar |
| 4 | Deep routes → sidebar auto-shows correct category from pathname | ✅ — `getSectionForPathname` unchanged, still applies |
| 5 | Existing routes continue to work without renaming | ✅ — no route changes |
| 6 | Body no longer the only place to access child menus | ✅ — body `GroupDetailPanel` replaced by `GroupSummaryPanel` (summary only) |
| 7 | Interaction feels stable | ✅ — URL-driven, no localStorage, no extra state |

### Route matrix confirmed
| URL | Active section resolved | Home highlighted |
|---|---|---|
| `/` | None | Yes |
| `/?group=audit` | AUDIT | No |
| `/?group=notifications` | NOTIFICATIONS | No |
| `/?group=identity` | IDENTITY | No |
| `/synqaudit` | AUDIT (pathname) | No |
| `/synqaudit/user-activity` | AUDIT (pathname) | No |
| `/notifications/templates` | NOTIFICATIONS (pathname) | No |
| `/tenants` | TENANTS (pathname) | No |

---

## 6. Active Group Resolution Detail

```
URL: /?group=audit
  searchParams.get('group') → 'audit'
  getSectionBySlug('audit') → CC_NAV section with heading 'AUDIT'
  sidebar renders: Home + [AUDIT] + all AUDIT items

URL: /synqaudit/user-activity
  pathname !== '/'
  getSectionForPathname('/synqaudit/user-activity') → CC_NAV section with heading 'AUDIT'
  sidebar renders: Home + [AUDIT] + all AUDIT items

URL: /
  no group param
  sidebar renders: Home only
```

---

## 7. Known Gaps / Follow-ups

- No animation on sidebar section change (items appear/disappear instantly). Could add `transition` or slide-in later.
- The body `GroupSummaryPanel` does not show descriptions per tool — description copy doesn't exist in `CC_NAV` items today.
- Logo link in `cc-shell.tsx` goes to `/tenants` — consider pointing to `/` in a future pass.
- Keyboard: Tab order now has sidebar links before body cards. This is correct for the new primary-nav model.
