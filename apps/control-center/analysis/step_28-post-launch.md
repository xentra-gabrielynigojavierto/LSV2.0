# Step 28 — Post-Launch Enhancements

**Date completed:** 2026-03-29  
**Prior step:** Step 27 — Security hardening, env config (`env.ts`), health endpoint, Dockerfile, CI/CD

---

## Summary

Step 28 delivers five categories of post-launch improvements to the Control Center admin app. All changes are purely additive — no existing routes, data models, or API stubs were altered.

---

## 1. Global Error Boundary (`src/app/error.tsx`)

**File:** `src/app/error.tsx`  
**Type:** Client Component (required by Next.js App Router)

### What it does
- Renders whenever an uncaught error propagates from within a route segment
- Shows a user-friendly panel with an icon, title, error digest (for log correlation), and two CTAs: "Try again" (calls `reset()`) and "Back to dashboard"
- Logs the error to `console.error` in all environments; has a clear `TODO` block for wiring Sentry / Datadog / Rollbar in production

### Accessibility
- `role="alert"` on the error panel — screen readers announce it immediately
- `autoFocus` on the heading — keyboard users land on the message first
- All CTAs have visible `focus-visible` rings

---

## 2. Route Loading Skeletons

**Files created:**
- `src/components/ui/loading-shell.tsx` — shared skeleton primitives
- `src/app/tenants/loading.tsx`
- `src/app/support/loading.tsx`
- `src/app/audit-logs/loading.tsx`
- `src/app/roles/loading.tsx`
- `src/app/tenant-users/loading.tsx`

### What it does
`loading-shell.tsx` exports:
- `LoadingShell` — full-screen shell skeleton (top bar + sidebar + main), used by all route loading files. Matches `CCShell` dimensions so there is no layout shift when the page mounts.
- `HeaderSkeleton` — pulsing title + optional subtitle bar
- `FilterBarSkeleton` — pulsing search / filter inputs + submit button
- `TableSkeleton` — configurable rows × cols animated skeleton table

Each route's `loading.tsx` composes these primitives to match the visual shape of the real page. Next.js App Router automatically shows the loading file as a Suspense fallback while the page's server-side data fetches are in flight.

### Accessibility
- `aria-busy="true"` and `aria-label="Loading, please wait"` on the root container
- `aria-live="polite"` on the main content region

---

## 3. Analytics Module

**Files:**
- `src/lib/analytics.ts` — core tracking functions
- `src/components/analytics/analytics-provider.tsx` — page view provider
- `src/app/layout.tsx` — updated to mount `AnalyticsProvider`

### What it does

#### `analytics.ts`
Pure TypeScript module (no React imports) that can be imported from any Server Component, Client Component, Server Action, or Route Handler.

Exports:
- `track(event, properties?)` — emit a named analytics event
- `identifyUser(userId, email, traits?)` — associate events with a user
- `resetUser()` — clear user identity on logout

Contains a typed `TrackEvent` union covering all named CC events (page views, tenant/user actions, impersonation, audit, settings).

Behaviour:
- **Dev**: `console.debug('[CC Analytics] event.name {...}')`
- **Prod**: no-op stub with clear `TODO` block — supports Segment, Mixpanel, Amplitude, PostHog

Server-side guard: `if (typeof window === 'undefined') return;` prevents Node.js errors.

#### `analytics-provider.tsx`
Thin Client Component wrapping `usePathname()`. Fires `track('page.view', { path })` on every Next.js route change. Mounted once in `layout.tsx` — zero visible output.

---

## 4. Confirm Dialog (`src/components/ui/confirm-dialog.tsx`)

**File:** `src/components/ui/confirm-dialog.tsx`

### What it does
Reusable modal confirmation dialog for destructive or state-changing actions.

Props:
- `title` — short imperative heading
- `description?` — optional expanded explanation
- `confirmLabel` / `cancelLabel` — button text (defaults: "Confirm" / "Cancel")
- `variant: 'danger' | 'warning' | 'neutral'` — controls confirm button colour
- `isPending` — shows spinner, disables both buttons while action is in flight
- `onConfirm` / `onCancel` — event handlers

### Accessibility
- `role="dialog"`, `aria-modal="true"`, `aria-labelledby`, `aria-describedby`
- Cancel button auto-focused on mount — forces deliberate action for destructive ops
- Escape key closes the dialog (guarded: ignored when `isPending`)
- Backdrop click also cancels (guarded: ignored when `isPending`)
- All buttons have `focus-visible` rings
- Spinner is `aria-hidden`; confirm button shows "Processing…" text during flight

---

## 5. Improved Action Components

### `src/components/tenants/tenant-actions.tsx` (updated)

**Changes over previous version:**
- Removed `alert()` calls — replaced with `ConfirmDialog` for Deactivate and Suspend
- Added `pending` state: each button shows an inline spinner while its action runs
- Added 3.5 s inline success / error feedback (`role="status"`, `aria-live="polite"`)
- Added `aria-label` and `aria-busy` to all buttons
- Added `focus-visible:ring-2` styles for keyboard accessibility
- Wired `track('tenant.status.change')` on each successful action
- `simulateAction()` stub (800 ms delay) replaces `alert()` until BFF proxy routes are wired

**Confirm gates:**
- **Deactivate** → `variant="warning"` dialog
- **Suspend** → `variant="danger"` dialog (more severe: warns about session termination)
- **Activate** → no confirm (non-destructive; reversal is easy)

### `src/components/users/user-actions.tsx` (updated)

Same improvements as `tenant-actions.tsx`, plus:
- Full action set: Activate, Deactivate, Lock, Unlock, Reset Password, Resend Invite
- Each action mapped to the correct `TrackEvent` constant
- **Deactivate** → `variant="warning"` dialog
- **Lock** → `variant="danger"` dialog (warns about session termination + active sessions)
- Unlock, Reset Password, Resend Invite → no confirm (reversible / informational)

---

## TypeScript Status

```
$ tsc --noEmit
(no output — 0 errors)
```

All 13 new and modified files pass type checking without errors.

---

## Runtime Status

```
[control-center] ✓ Ready in 2.2s
[web]            ✓ Ready in 2.4s
```

No compilation errors, no runtime warnings.

---

## Files Changed in Step 28

### Created
| File | Purpose |
|------|---------|
| `src/app/error.tsx` | Global error boundary UI |
| `src/lib/analytics.ts` | Analytics tracking module |
| `src/components/analytics/analytics-provider.tsx` | Page view tracking provider |
| `src/components/ui/confirm-dialog.tsx` | Reusable accessible confirm dialog |
| `src/components/ui/loading-shell.tsx` | Shared skeleton primitives |
| `src/app/tenants/loading.tsx` | Tenants page loading skeleton |
| `src/app/support/loading.tsx` | Support page loading skeleton |
| `src/app/audit-logs/loading.tsx` | Audit logs page loading skeleton |
| `src/app/roles/loading.tsx` | Roles page loading skeleton |
| `src/app/tenant-users/loading.tsx` | Tenant users page loading skeleton |
| `analysis/deployment-notes.md` | Full deployment + troubleshooting guide |
| `analysis/step_28-post-launch.md` | This report |

### Updated
| File | Change |
|------|--------|
| `src/app/layout.tsx` | Added `AnalyticsProvider` wrapper |
| `src/components/tenants/tenant-actions.tsx` | Confirm dialog + loading + analytics + a11y |
| `src/components/users/user-actions.tsx` | Confirm dialog + loading + analytics + a11y |

---

## Wiring Checklist (for when backend endpoints go live)

- [ ] Wire `tenant-actions.tsx` to `POST /identity/api/admin/tenants/{id}/{activate|deactivate|suspend}`
- [ ] Wire `user-actions.tsx` to `POST /identity/api/admin/users/{id}/{activate|deactivate|lock|unlock|reset-password|resend-invite}`
- [ ] Replace `simulateAction()` stubs in both action components with real `fetch` calls
- [ ] Wire analytics SDK — add provider key to `NEXT_PUBLIC_ANALYTICS_KEY` and implement `TODO` blocks in `analytics.ts`
- [ ] Call `identifyUser()` in `AnalyticsProvider` after session is available client-side
- [ ] Call `resetUser()` in `sign-out-button.tsx` on logout

---

_End of Step 28 report._
