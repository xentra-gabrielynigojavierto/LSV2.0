# INVITE-FIX — Invitation Issues Fix Report

**Date:** 2026-04-20
**Status:** COMPLETE

---

## Issues Addressed

### Issue 1 — Invitation emails not being received

#### Root Cause
The invitation email flow was working correctly at the infrastructure level:
- Notifications service running on port 5008 (healthy)
- SendGrid provider seeded and health-checked as `email=healthy`
- `NotificationsService__BaseUrl=http://localhost:5008` and `NotificationsService__PortalBaseUrl=http://localhost:3050` injected by `scripts/run-dev.sh` as environment variables, correctly overriding the empty `"BaseUrl": ""` in `appsettings.Development.json`

However, two practical problems remained:
1. **The activation link in emails uses `http://localhost:3050`** (PortalBaseUrl) — a link that is only reachable by someone on the same machine. External invitees cannot click it.
2. **No fallback surface for the admin**: the backend already returned `inviteToken` in non-production responses (logged to stdout and included in the 201 Created body), but the BFF swallowed it and the UI never showed it.

#### Fixes Applied

| Layer | File | Change |
|---|---|---|
| Identity service | `AdminEndpoints.cs` `InviteUser` | Dev-mode response now includes both `inviteToken` and `activationLink` fields |
| Identity service | `AdminEndpoints.cs` `ResendInvite` | Same — `activationLink` added to dev-mode 200 OK body |
| BFF API | `controlCenterServerApi.users.invite()` | Return type changed from `void` to `Promise<{ activationLink?: string }>` |
| BFF route | `POST /api/identity/admin/users/invite/route.ts` | Forwards `activationLink` in the JSON response |
| UI | `invite-form.tsx` | Success screen shows an amber "Activation link" panel with the raw URL and a "Copy link" button when `activationLink` is present in the API response |

The amber panel is only visible when the backend returns an `activationLink` (non-production environments). In production the field is absent and the panel is hidden — no behaviour change for deployed environments.

---

### Issue 2 — Cancelled invites still showing "Invited" status

#### Root Cause
The Identity service backend has a fully implemented `POST /api/admin/users/{id}/cancel-invite` endpoint that:
1. Revokes all `Pending` `UserInvitation` records for the user
2. Leaves the `User` record intact (still inactive)
3. Emits an `identity.user.invite_cancelled` audit event

After cancellation, `hasPendingInvite` becomes `false`, so the user's computed status flips from `"Invited"` → `"Inactive"`.

However, **no part of the control-center was wired to this endpoint**:
- No BFF route existed
- No `controlCenterServerApi.users.cancelInvite()` method existed
- No "Cancel Invite" button existed in the user list or user detail views

#### Fixes Applied

| Layer | File | Change |
|---|---|---|
| API client | `control-center-api.ts` | Added `cancelInvite(id)` method with `safeRevalidateTag(CACHE_TAGS.users)` |
| BFF route | `POST /api/identity/admin/users/[id]/cancel-invite/route.ts` | New file — proxies to Identity service, requires admin auth |
| User list | `user-row-actions.tsx` | Added `cancel-invite` to `RowAction` type; "Cancel Invite" button (danger variant) appears for `isInvited` users with inline confirmation prompt |
| User detail | `user-actions.tsx` | Added `cancel-invite` to `UserAction` type and `ACTION_LABELS`; "Cancel Invite" button (danger variant) with `ConfirmDialog` appears next to "Resend Invite" for invited users |
| Analytics | `analytics.ts` | Added `user.invite.cancel` to `TrackEvent` union |

After cancel-invite:
- `safeRevalidateTag(CACHE_TAGS.users)` purges the 30-second server cache
- `router.refresh()` triggers a re-render of the server component
- The user row switches from `Invited` (blue badge) to `Inactive` (grey badge)

---

## Files Changed

```
apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs
  └─ InviteUser: dev response includes activationLink
  └─ ResendInvite: dev response includes activationLink

apps/control-center/src/lib/control-center-api.ts
  └─ users.invite(): returns { activationLink? }
  └─ users.cancelInvite(): new method

apps/control-center/src/app/api/identity/admin/users/invite/route.ts
  └─ forwards activationLink in success response

apps/control-center/src/app/api/identity/admin/users/[id]/cancel-invite/route.ts
  └─ NEW: BFF proxy for cancel-invite

apps/control-center/src/app/tenant-users/invite/invite-form.tsx
  └─ success screen shows activationLink panel + copy button

apps/control-center/src/components/users/user-row-actions.tsx
  └─ Cancel Invite button for Invited-status rows

apps/control-center/src/components/users/user-actions.tsx
  └─ Cancel Invite button for Invited-status user detail page

apps/control-center/src/lib/analytics.ts
  └─ user.invite.cancel TrackEvent added
```

## Validation

- `npx tsc --noEmit` → 0 errors
- `.NET` build → 0 errors, 8 pre-existing MSB3277 warnings (version conflicts in Liens/Notifications — unrelated)
- Services restarted cleanly
