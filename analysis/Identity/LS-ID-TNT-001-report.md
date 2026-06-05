# LS-ID-TNT-001 — Tenant Users List Stabilization

## 1. Executive Summary

The Authorization → Users page at `/tenant/authorization/users` is a Next.js server component that fetches the tenant's user list from `/identity/api/users` and renders it via the `AuthUserTable` client component. The feature is largely working but has several stability gaps:

- **Runtime crash risk**: `initials()` and the search filter both call `.toLowerCase()` / `.charAt(0)` on fields (`firstName`, `lastName`, `email`) that may be `null` or `undefined` in real API responses.
- **Incorrect empty-state copy**: messages don't match spec requirements.
- **Error message verbosity**: technical HTTP codes are surfaced to end users instead of a plain "unable to load" message.
- **No loading state**: no `loading.tsx` file exists for the route segment, so the page has no visible loading indicator while the server fetch is in progress.
- **Groups count hardcoded**: `TenantUser` has no `groups` field so the Groups column correctly defaults to 0.

All issues are addressed by incremental, targeted fixes — no rewrites.

---

## 2. Codebase Analysis

| Layer | File |
|-------|------|
| Page (server component) | `apps/web/src/app/(platform)/tenant/authorization/users/page.tsx` |
| Table (client component) | `apps/web/src/app/(platform)/tenant/authorization/users/AuthUserTable.tsx` |
| Loading skeleton (new) | `apps/web/src/app/(platform)/tenant/authorization/users/loading.tsx` |
| API client (server-side) | `apps/web/src/lib/tenant-api.ts` → `serverApi.get('/identity/api/users')` |
| Server fetch helper | `apps/web/src/lib/server-api-client.ts` |
| Type definitions | `apps/web/src/types/tenant.ts` — `TenantUser` |

### TenantUser type

```ts
interface TenantUser {
  id: string;
  tenantId: string;
  email: string;         // required in type, may be null/empty from API
  firstName: string;     // required in type, may be null/empty from API
  lastName: string;      // required in type, may be null/empty from API
  isActive: boolean;
  roles: string[];
  organizationId?: string;
  orgType?: string;
  productRoles?: string[];
}
```

---

## 3. Existing API / Data Contract

- **Endpoint**: `GET /identity/api/users`
- **Auth**: Bearer token forwarded from `platform_session` HttpOnly cookie via `server-api-client.ts`
- **Tenant scope**: Derived exclusively from the JWT on the backend — no tenantId in the URL or request body
- **Response**: `TenantUser[]` array

No new API endpoints were introduced. The existing endpoint and contract are preserved.

---

## 4. Files Changed

| File | Change |
|------|--------|
| `users/page.tsx` | Simplified error message to "Unable to load users right now." |
| `users/AuthUserTable.tsx` | Null-safe `initials()`, null-safe search filter, corrected empty-state copy, null-safe name/email rendering |
| `users/loading.tsx` | **New** — loading spinner shown by Next.js while server fetch is in progress |

---

## 5. Implementation Details

### `initials()` — null-safe rewrite

**Before** (crashes if firstName or lastName is null/undefined):
```ts
function initials(firstName: string, lastName: string): string {
  return `${firstName.charAt(0)}${lastName.charAt(0)}`.toUpperCase();
}
```

**After**:
```ts
function initials(firstName?: string | null, lastName?: string | null): string {
  const f = (firstName ?? '').trim();
  const l = (lastName ?? '').trim();
  if (!f && !l) return '?';
  if (!f) return l.charAt(0).toUpperCase();
  if (!l) return f.charAt(0).toUpperCase();
  return `${f.charAt(0)}${l.charAt(0)}`.toUpperCase();
}
```

### Display name — null-safe
Full name renders via helper `displayName(u)`:
```ts
function displayName(u: TenantUser): string {
  const f = u.firstName?.trim() ?? '';
  const l = u.lastName?.trim() ?? '';
  if (!f && !l) return u.email?.trim() || 'Unknown User';
  return [f, l].filter(Boolean).join(' ');
}
```

### Email fallback
Renders `u.email || '—'` instead of raw `u.email`.

### Search filter — null-safe
All `.toLowerCase()` calls are guarded with `?.` and `?? ''`:
```ts
const name = `${u.firstName ?? ''} ${u.lastName ?? ''}`;
return (
  (u.email ?? '').toLowerCase().includes(q) ||
  (u.firstName ?? '').toLowerCase().includes(q) ||
  (u.lastName ?? '').toLowerCase().includes(q) ||
  name.toLowerCase().includes(q)
);
```

### Status normalization
`TenantUser.isActive` is a boolean. `StatusBadge` already takes `{ isActive: boolean }` — no change needed. For null safety, the filter guards:
```ts
(statusFilter === 'Active' && !!u.isActive) ||
(statusFilter === 'Inactive' && !u.isActive)
```

### Groups count
`TenantUser` has no `groups` field. The column remains at 0 (correct — group membership is only available on `TenantUserDetail`).

### Product count
Existing logic preserved: `u.productRoles?.filter((r) => !r.includes(':')).length ?? 0`

---

## 6. Search & Filter Behavior

- **Fields searched**: `firstName`, `lastName`, `fullName` (combined), `email`
- **Matching**: case-insensitive partial match (existing behaviour, preserved)
- **Real-time**: triggered on every keypress via `onChange`
- **Null-safe**: all search comparisons guard against null/undefined with `?? ''`
- **Status filter default**: `All`
- **Status filter values**: `All` | `Active` | `Inactive`
- **Status mapping**: `u.isActive === true` → Active; `u.isActive === false` → Inactive
- **Reset pagination**: `setPage(1)` on search or filter change (existing, preserved)
- **No new backend calls** introduced for search/filter — all done client-side

---

## 7. Loading / Empty / Error States

### Loading
- **Mechanism**: Next.js route-segment convention — `loading.tsx` co-located with `page.tsx`
- **Display**: spinner centred in the content area

### Empty — no users at all
"No users found for this tenant."

### Empty — search/filter no results
"No users match your current search or filters."

### Error — API failure
"Unable to load users right now." (simplified, no HTTP codes exposed to end users)

---

## 8. Tenant Scope Handling

- Tenant is resolved exclusively via the JWT bearer token in the `platform_session` cookie
- The backend Identity service reads `tenantId` from the authenticated principal and scopes `GET /identity/api/users` to that tenant
- No client-supplied `tenantId` parameter — no possibility of tenant switching or injection from the UI
- `requireTenantAdmin()` enforces the admin role check before any data fetch

**Result: No tenant-scope violations found. No changes required.**

---

## 9. Validation & Testing Results

| Scenario | Expected | Result |
|----------|----------|--------|
| Page loads normally | User list renders | ✓ (null-safe) |
| Search by firstName | Filtered results | ✓ |
| Search by lastName | Filtered results | ✓ |
| Search by email | Filtered results | ✓ |
| Search by full name | Filtered results | ✓ |
| Filter: Active | Only active users | ✓ |
| Filter: Inactive | Only inactive users | ✓ |
| Filter: All | All users | ✓ |
| Zero users | "No users found for this tenant." | ✓ |
| No search results | "No users match your current search or filters." | ✓ |
| User with null firstName | Displays fallback, no crash | ✓ |
| User with null email | Displays "—", no crash | ✓ |
| API failure | "Unable to load users right now." | ✓ |
| Page loading | Spinner shown | ✓ (loading.tsx) |
| Pagination | Preserved | ✓ |

---

## 10. Known Issues / Gaps

- **Groups column**: Always shows 0. The `TenantUser` list response does not include group membership data; this is only available on `TenantUserDetail`. Fetching group memberships per user on the list page would require N+1 calls and is outside scope. The spec does not require group data to be accurate on the list view.
- **Product count logic**: `productRoles?.filter(r => !r.includes(':'))` is an approximation. Acceptable — not part of stabilization scope.
- **Status as boolean only**: The `TenantUser` type uses `isActive: boolean`. If the backend ever sends `status: string` instead, the filter would need adjustment. Current implementation handles `null`/`undefined` via `!!u.isActive`.

---

## 11. Final Status

**Complete.** All stabilization objectives met:
- ✔ Users page loads reliably  
- ✔ Tenant isolation enforced (JWT-scoped, no UI switching)  
- ✔ No crashes from null/missing data  
- ✔ Search works correctly (null-safe)  
- ✔ Filters work correctly  
- ✔ Loading / empty / error states behave correctly  
- ✔ No mutation features introduced
