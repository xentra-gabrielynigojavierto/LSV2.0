# LS-NOTIF-SMS-012 — Control Center SMS Operational Alert & Escalation Management UI

## Status: IMPLEMENTED

## Scope

Implements the Control Center administration UI for SMS operational alert and escalation management, consuming the Notification Service APIs from LS-NOTIF-SMS-010 (alert monitoring endpoints) and LS-NOTIF-SMS-011 (escalation policy & history endpoints).

No backend logic lives in Control Center. All data is owned by the Notification Service; Control Center is a read-mostly admin surface with targeted write operations (resolve, suppress, retry, policy CRUD).

---

## Routes Added

| Path | Purpose |
|---|---|
| `/notifications/sms-incidents` | Overview hub — alert + escalation summary at a glance |
| `/notifications/sms-incidents/alerts` | Alert list with resolve / suppress / evaluate actions |
| `/notifications/sms-incidents/escalations` | Escalation attempt history with retry action |
| `/notifications/sms-incidents/policies` | Escalation policy CRUD (create, update, disable) |

All routes are guarded by `requirePlatformAdmin()`.

---

## Files Created

### Library

| File | Purpose |
|---|---|
| `src/lib/sms-incidents-api.ts` | Server-side API client using `notifClient`. TypeScript DTOs matching LS-NOTIF-SMS-010/011 backend exactly. Covers alert list/summary, escalation list/summary, policy list/get. |
| `src/lib/sms-incidents-client-api.ts` | Browser-safe mutation client via `/api/notifications/v1/...` BFF proxy. Covers resolve, suppress, evaluate, retry escalation, disable policy, create/update policy. |

### Pages (Server Components)

| File | Purpose |
|---|---|
| `src/app/notifications/sms-incidents/page.tsx` | Overview with KPI cards (active/critical alerts, escalation sent/failed counts, enabled policies), quick-action links to sub-pages. |
| `src/app/notifications/sms-incidents/alerts/page.tsx` | Alert list + summary cards; mounts `AlertsPanel` client component for interactive actions. |
| `src/app/notifications/sms-incidents/escalations/page.tsx` | Escalation history + summary breakdown; mounts `EscalationsPanel` for retry actions. |
| `src/app/notifications/sms-incidents/policies/page.tsx` | Policy list; mounts `PoliciesPanel` for CRUD operations. |

### Client Components

| File | Purpose |
|---|---|
| `src/components/sms-incidents/alerts-panel.tsx` | Interactive alert table: resolve (with optional note), suppress (configurable duration), evaluate cycle trigger. Uses `ConfirmDialog`. |
| `src/components/sms-incidents/escalations-panel.tsx` | Escalation history table with channel/status badges, retry button for failed attempts. Uses `ConfirmDialog`. |
| `src/components/sms-incidents/policies-panel.tsx` | Policy management: list with masked target, enable/disable toggle, create/update forms, disable confirm. Uses `ConfirmDialog`. |

---

## Security Guarantees

- **No raw targets exposed**: `TargetMasked` is the only target field used throughout; raw webhook URLs and email addresses are never returned by the backend.
- **No credentials**: SettingsJson, CredentialsJson, RecipientJson are never referenced.
- **No phone numbers**: No recipient PII rendered anywhere.
- **No side effects**: No SMS sends, provider calls, reconciliation, or retries triggered by read operations.
- **PlatformAdmin only**: All 4 routes call `requirePlatformAdmin()` before any data fetch.
- **Write-only target on create**: The Create Policy form accepts a `target` field (webhook URL / email) that is write-only — it is never pre-filled, never displayed after submission, and the backend never returns it.
- **Evaluate action**: The manual evaluation trigger is guarded by a `ConfirmDialog` — it runs one evaluation cycle server-side but does not itself send any notifications.

---

## API Paths Consumed

### From LS-NOTIF-SMS-010 (Alerts)

```
GET  /notifications/v1/admin/sms/alerts/         — list (status, severity, alertType, limit, offset)
GET  /notifications/v1/admin/sms/alerts/summary  — aggregate counts
GET  /notifications/v1/admin/sms/alerts/{id}     — single (not consumed directly by CC)
POST /notifications/v1/admin/sms/alerts/{id}/resolve   — resolve alert
POST /notifications/v1/admin/sms/alerts/{id}/suppress  — suppress alert (duration configurable)
POST /notifications/v1/admin/sms/alerts/evaluate       — trigger one evaluation cycle
```

### From LS-NOTIF-SMS-011 (Escalations + Policies)

```
GET  /notifications/v1/admin/sms/alerts/escalations            — list escalation history
GET  /notifications/v1/admin/sms/alerts/escalations/summary    — aggregate counts
GET  /notifications/v1/admin/sms/alerts/escalations/{id}       — single (not consumed directly)
POST /notifications/v1/admin/sms/alerts/escalations/{id}/retry — retry failed escalation

GET  /notifications/v1/admin/sms/alerts/policies              — list policies (masked)
GET  /notifications/v1/admin/sms/alerts/policies/{id}         — get policy by ID
POST /notifications/v1/admin/sms/alerts/policies              — create policy
PUT  /notifications/v1/admin/sms/alerts/policies/{id}         — update policy
POST /notifications/v1/admin/sms/alerts/policies/{id}/disable — soft-disable policy
```

---

## Nav Changes

Four items added to the NOTIFICATIONS section of `CC_NAV` in `lib/nav.ts`:

```
SMS Incidents     /notifications/sms-incidents             LIVE
Alert List        /notifications/sms-incidents/alerts      LIVE
Escalations       /notifications/sms-incidents/escalations LIVE
Escalation Policies /notifications/sms-incidents/policies  LIVE
```

---

## Architecture Decisions

1. **Server Components for data** — initial data is fetched server-side (`requirePlatformAdmin()` + `notifClient`) with `Promise.allSettled` for graceful degradation per section.
2. **Client Components for mutations** — interactive panels (`AlertsPanel`, `EscalationsPanel`, `PoliciesPanel`) are Client Components that re-fetch after successful mutations using `router.refresh()`.
3. **No local state caching** — all reads go through Next.js `no-store` cache (mutations) or page refresh.
4. **Error isolation** — per-section error banners prevent one failing API call from blocking the entire page.
5. **Pagination** — all list endpoints support limit/offset; the UI surfaces page size 25 with prev/next controls.
6. **Filter URL params** — status, severity, channelType, etc. are read from `searchParams` in server pages and forwarded as query strings to the API.
