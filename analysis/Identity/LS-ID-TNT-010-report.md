# LS-ID-TNT-010 — Enforcement Completion
## Ticket: Route-level access guards, token/session consistency, cross-service standardization
**Date:** 2026-04-18  
**Status:** COMPLETED  
**Depends on:** LS-ID-TNT-001 – LS-ID-TNT-009

---

## 1. Background and Scope

LS-ID-TNT-010 is the enforcement-completion ticket for the multi-tenant access control rollout begun in LS-ID-TNT-001.  While earlier tickets established the data model, JWT claim emission, middleware wire-up, product-code inventory, and AppSwitcher product filtering, three gaps remained:

| Gap | Category |
|---|---|
| Product routes without server-side layout guards | **Route-level access** |
| `access_version` only validated at login / `/auth/me` poll, not on tab-return | **Token/session consistency** |
| Reports/SynqInsights service missing `RequireProductAccess` | **Cross-service standardisation** |

---

## 2. Gap Analysis — Frontend Routes Without Product Guards

**Investigation:** examined all route groups under `apps/web/src/app/(platform)/`.

| Route group | Layout file pre-TNT-010 | Guard present? |
|---|---|---|
| `lien/` | `layout.tsx` (`'use client'`, wraps LienProviders) | No — client-only, no server guard |
| `careconnect/` | — | No layout at all |
| `fund/` | — | No layout at all |
| `insights/` | — | No layout at all |

Any authenticated user with a valid platform session could navigate directly to `/careconnect/…`, `/fund/…`, or `/insights/…` and render those pages even if the tenant had not provisioned that product for them.  The `lien/layout.tsx` carried client-side context providers but no access enforcement.

---

## 3. Gap Analysis — Access Version Visibility-Change Re-validation

**Finding:** `session-provider.tsx` called `/auth/me` once on mount and on an interval (if configured), but did NOT subscribe to `document.visibilitychange`.  This meant:

- TenantAdmin revokes User's CareConnect access while the user has the tab open but hidden.
- User returns to the tab and continues to use CareConnect pages.
- Next `/auth/me` poll (or next navigation) eventually catches it, but there is a window where the frontend permits access after the backend has removed it.

The Identity service already validates `access_version` on every `/auth/me` call and returns 401 if the version stored in the JWT no longer matches the DB value.  The gap was purely on the polling/event side in the frontend provider.

---

## 4. Gap Analysis — Reports / SynqInsights Missing Backend Filter

**Finding:** `apps/services/reports/src/Reports.Api/Endpoints/` had four endpoint groups:

- `ExecutionEndpoints` — runs reports
- `ExportEndpoints` — exports report data
- `ViewEndpoints` — view-only report access
- `OverrideEndpoints` — override/correction entries

None of these groups called `.RequireProductAccess(…)`.  The template endpoints were already guarded by `Policies.PlatformOrTenantAdmin`, but the core report-execution API was open to any authenticated user regardless of product entitlement.

---

## 5. Solution — Backend: ProductCodes.cs

Added two new product code constants to `BuildingBlocks/Authorization/ProductCodes.cs`:

```csharp
public const string SynqInsights = "SYNQ_INSIGHTS";
public const string SynqComms    = "SYNQ_COMMS";
```

`SynqInsights` is the Reports service product code.  `SynqComms` is added proactively for the future Comms product.  Both mirror the convention established in TNT-003 for `SynqLien`, `SynqFund`, and `CareConnect`.

---

## 6. Solution — Backend: Reports.Api RequireProductAccess

Applied `.RequireProductAccess(ProductCodes.SynqInsights)` to all four endpoint groups in the Reports service:

- `ExecutionEndpoints` — `/api/reports/execute/{…}`
- `ExportEndpoints`    — `/api/reports/export/{…}`
- `ViewEndpoints`      — `/api/reports/{…}` (read)
- `OverrideEndpoints`  — `/api/reports/overrides/{…}`

`RequireProductAccessFilter` (BuildingBlocks) already short-circuits for `IsTenantAdminOrAbove()`, so TenantAdmins and PlatformAdmins are unaffected.

---

## 7. Solution — Frontend: FrontendProductCode Constants

Added a `FrontendProductCode` constant dictionary to `apps/web/src/lib/auth-guards.ts`:

```typescript
export const FrontendProductCode = {
  SynqLien:    'SYNQ_LIENS',
  SynqFund:    'SYNQ_FUND',
  CareConnect: 'SYNQ_CARECONNECT',
  SynqInsights:'SYNQ_INSIGHTS',
  SynqComms:   'SYNQ_COMMS',
} as const;
```

These strings match the backend `ProductCodes` values and the `enabledProducts` / `userProducts` arrays emitted by `/auth/me`.

---

## 8. Solution — Frontend: requireProductAccess() Guard

Added `requireProductAccess(productCode)` to `auth-guards.ts`.  This is a server-async function (runs in Next.js Server Components / Route Handlers):

```typescript
export async function requireProductAccess(productCode: string): Promise<PlatformSession> {
  const session = await requireOrg();
  if (session.isPlatformAdmin || session.isTenantAdmin) return session;
  const products: string[] =
    (session.userProducts?.length ?? 0) > 0
      ? session.userProducts!
      : (session.enabledProducts ?? []);
  if (!products.includes(productCode)) redirect('/access-denied');
  return session;
}
```

Behaviour:
- Calls `requireOrg()` first (enforces authentication + org resolution).
- PlatformAdmins and TenantAdmins bypass product check (mirrors backend filter).
- Prefers `userProducts` (user-level, from JWT `product_codes` claim, LS-ID-TNT-009).
- Falls back to `enabledProducts` (tenant-level) for pre-TNT-009 sessions.
- On failure: calls Next.js `redirect('/access-denied')` — results in a 307 on the server, never renders the product page.

---

## 9. Solution — Platform Access Denied Page

Created `apps/web/src/app/(platform)/access-denied/page.tsx`.

This page is within the `(platform)` route group, so it:
- Requires an authenticated session (platform middleware checks `platform_session` cookie).
- Renders the `(platform)` root layout (nav, sidebar).
- Does NOT call any product guard (it is the destination of the redirect, not a guarded page).

Provides: icon, title, explanation, contact hint, and a "Back to Dashboard" CTA.  Distinct from `/tenant/access-denied` which is scoped to the admin tenant-management section.

---

## 10. Solution — Product Route Layouts

Four server component layouts created/updated:

### careconnect/layout.tsx (created)
```tsx
export default async function CareConnectLayout({ children }) {
  await requireProductAccess(FrontendProductCode.CareConnect);
  return <>{children}</>;
}
```

### fund/layout.tsx (created)
```tsx
export default async function FundLayout({ children }) {
  await requireProductAccess(FrontendProductCode.SynqFund);
  return <>{children}</>;
}
```

### insights/layout.tsx (created)
```tsx
export default async function InsightsLayout({ children }) {
  await requireProductAccess(FrontendProductCode.SynqInsights);
  return <>{children}</>;
}
```

### lien/layout.tsx (updated — server/client split)
Previously `'use client'` wrapping `LienProviders` only.  Converted to a server component:
```tsx
export default async function LienLayout({ children }) {
  await requireProductAccess(FrontendProductCode.SynqLien);
  return <LienProviders>{children}</LienProviders>;
}
```
`LienProviders` remains a client component (unchanged).  The server layout renders it as a child after the access guard passes.

---

## 11. Solution — Session Provider: Visibility-Change Re-validation

Added a `visibilitychange` event listener to `session-provider.tsx`:

```typescript
useEffect(() => {
  const handleVisibility = () => {
    if (document.visibilityState === 'visible' && sessionRef.current) {
      void fetchSession();
    }
  };
  document.addEventListener('visibilitychange', handleVisibility);
  return () => document.removeEventListener('visibilitychange', handleVisibility);
}, [fetchSession]);
```

- Only fires when `visibilityState === 'visible'` (tab return, not tab-hide).
- Only fires when a session currently exists (`sessionRef.current` is non-null) — avoids spurious calls on unauthenticated pages.
- Calls the existing `fetchSession()` which already handles 401 → clear + redirect.

---

## 12. Solution — Session Provider: access_updated Redirect Reason

Updated the 401 branch in `fetchSession()` to distinguish between "user had no session" and "user had a session but it was revoked":

```typescript
const hadSession = !!sessionRef.current;
setSession(null);
sessionRef.current = null;
const reason = hadSession ? 'access_updated' : 'unauthenticated';
window.location.href = `/login?reason=${reason}`;
```

`hadSession` is captured before clearing so the ternary reflects the state at the moment the 401 arrived, not after clearing.

---

## 13. Solution — Login Form: Reason Banners

Added three reason messages to `login-form.tsx`:

| Reason | Icon | Message |
|---|---|---|
| `idle` | `ri-time-line` | "Your session expired due to inactivity. Please sign in again." |
| `unauthenticated` | `ri-shield-keyhole-line` | "Your session has ended. Please sign in to continue." |
| `access_updated` | `ri-lock-unlock-line` | "Your access permissions have changed. Please sign in again to continue." |

The banner is rendered above the form fields, styled with a blue info palette (border-blue-200 bg-blue-50 text-blue-700) to convey an informational rather than error tone.

`useMemo` derives `reasonBanner` from `searchParams` to avoid re-deriving on every render.

---

## 14. Regression Verification — LS-ID-TNT-001 through 009

| Ticket | Feature | Regression risk | Verdict |
|---|---|---|---|
| TNT-001 | Tenant resolution from subdomain / tenant code | No auth-guards change affects middleware; `requireOrg()` is called before product check | Safe |
| TNT-002 | Tenant-level product enable/disable | `enabledProducts` still used as fallback when `userProducts` absent | Safe |
| TNT-003 | ProductCodes constants added | New constants appended, no existing values changed | Safe |
| TNT-004 | RequireProductAccessFilter in BuildingBlocks | Filter unchanged; new endpoints added as consumers | Safe |
| TNT-005 | Lien endpoints filtered | Lien endpoints untouched | Safe |
| TNT-006 | CareConnect endpoints filtered | CareConnect endpoints untouched | Safe |
| TNT-007 | Fund endpoints filtered | Fund endpoints untouched | Safe |
| TNT-008 | access_version bump on permission change | Logic in Identity service unchanged; TNT-010 adds the consumer side (visibility re-poll) | Safe |
| TNT-009 | JWT product_codes claim; userProducts in AuthMeResponse; AppSwitcher | `userProducts` preferred in `requireProductAccess()`, falls back to `enabledProducts` | Safe |

---

## 15. Build Verification

| Check | Result |
|---|---|
| `tsc --noEmit` in `apps/web` | 0 errors (1 null-safety issue found and fixed before final check) |
| .NET solution build | Warnings only (pre-existing MSB3277 JwtBearer version conflict — not introduced by TNT-010) |
| Next.js `/login` route compile | 7.7 s, 767 modules |
| Workflow restart | Running, HTTP 200 |

---

## 16. Summary of Changes

| File | Change |
|---|---|
| `shared/building-blocks/BuildingBlocks/Authorization/ProductCodes.cs` | Added `SynqInsights`, `SynqComms` constants |
| `apps/services/reports/src/Reports.Api/Endpoints/ExecutionEndpoints.cs` | Added `.RequireProductAccess(ProductCodes.SynqInsights)` |
| `apps/services/reports/src/Reports.Api/Endpoints/ExportEndpoints.cs` | Added `.RequireProductAccess(ProductCodes.SynqInsights)` |
| `apps/services/reports/src/Reports.Api/Endpoints/ViewEndpoints.cs` | Added `.RequireProductAccess(ProductCodes.SynqInsights)` |
| `apps/services/reports/src/Reports.Api/Endpoints/OverrideEndpoints.cs` | Added `.RequireProductAccess(ProductCodes.SynqInsights)` |
| `apps/web/src/lib/auth-guards.ts` | Added `FrontendProductCode` dict + `requireProductAccess()` |
| `apps/web/src/app/(platform)/access-denied/page.tsx` | Created — platform-level product access denied page |
| `apps/web/src/app/(platform)/careconnect/layout.tsx` | Created — server guard CareConnect |
| `apps/web/src/app/(platform)/fund/layout.tsx` | Created — server guard SynqFund |
| `apps/web/src/app/(platform)/insights/layout.tsx` | Created — server guard SynqInsights |
| `apps/web/src/app/(platform)/lien/layout.tsx` | Updated — split from client-only to server guard + LienProviders child |
| `apps/web/src/providers/session-provider.tsx` | Added visibility-change re-fetch; `access_updated` redirect reason |
| `apps/web/src/app/login/login-form.tsx` | Added reason banner for `idle`, `unauthenticated`, `access_updated` |
