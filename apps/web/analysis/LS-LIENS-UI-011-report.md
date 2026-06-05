# LS-LIENS-UI-011: Provider Mode (Sell vs Manage Internally)

## Objective
Implement provider mode derivation from session product roles. Users with marketplace roles (`SYNQLIEN_SELLER`, `SYNQLIEN_BUYER`, `SYNQLIEN_HOLDER`) operate in **sell mode** with full offer/marketplace UI. Users without these roles operate in **manage mode** — a simplified view focused on internal lien tracking.

## Architecture

### Mode Derivation
- **Source**: `productRoles` array from session (via `/identity/api/auth/me`)
- **Logic**: Presence of any `SYNQLIEN_SELLER | SYNQLIEN_BUYER | SYNQLIEN_HOLDER` = `sell`; absence = `manage`
- **No new Zustand state**: Mode is derived reactively from session — no store pollution

### Files Created
| File | Purpose |
|------|---------|
| `lib/provider-mode/provider-mode.types.ts` | `ProviderMode` union type, `ProviderModeInfo` interface |
| `lib/provider-mode/provider-mode.service.ts` | `deriveProviderMode()`, `getMode()`, `isSellMode()`, `isManageMode()` |
| `lib/provider-mode/index.ts` | Barrel export |
| `hooks/use-provider-mode.ts` | React hook wrapping `useSession()` → `ProviderModeInfo & { isReady }` |

### Files Modified
| File | Changes |
|------|---------|
| `lib/nav.ts` | Added `requiredRoles` to Bill of Sales nav item (seller/buyer/holder) |
| `app/(platform)/lien/liens/[id]/page.tsx` | Gated: Submit Offer button, Offers panel, pending-offers banner, Offer Price/Purchase Price KPI cards, offer modal, offer fetch |
| `app/(platform)/lien/liens/page.tsx` | Gated: Offer column in table + drawer, Offered/Sold status filter options |
| `app/(platform)/lien/dashboard/page.tsx` | Added mode badge (Sell Mode / Manage Mode), gated Bill of Sale quick action |

## UI Gating Summary

### Sell Mode (has marketplace roles)
- Full offer/marketplace UI visible
- Submit Offer button on lien detail
- Offers panel with accept/reject actions
- Pending offers action-required banner
- Offer Price + Purchase Price KPI cards
- Bill of Sales nav item + quick action
- Offered/Sold status filters in liens list
- Offer column in liens table + preview drawer

### Manage Mode (no marketplace roles)
- Simplified lien tracking view
- No offer-related UI anywhere
- No Bill of Sales navigation
- Only Original Amount KPI card on lien detail
- Only Draft/Active/Withdrawn status filters
- No offer column in liens table
- Dashboard shows "Manage Mode" badge

## Sidebar Navigation
The existing `filterNavByRoles()` in `lib/nav.ts` already handles nav gating by checking `requiredRoles` against session `productRoles`. Adding `requiredRoles` to the Bill of Sales nav item is sufficient — the MARKETPLACE section was already gated this way.

## Default Behavior
- While session is loading (`isReady: false`), mode defaults to `manage` — the most restrictive view
- This prevents flash of marketplace UI for manage-mode users

## TypeScript
All changes pass `tsc --noEmit` with zero errors.
