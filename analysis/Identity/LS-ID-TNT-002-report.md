# LS-ID-TNT-002 — Add User Flow

## 1. Executive Summary

Implements the first mutation feature on the stabilized Authorization → Users page: a modal-based Add User flow. A primary "Add User" button is added to the top-right of the users table. Clicking it opens a modal form with fields for First Name, Last Name, Email, Role (dropdown), and Temporary Password. On submit the form is validated client-side, then a `POST /identity/api/users` request is issued via the BFF proxy. On success the modal closes, a success toast is shown, and `router.refresh()` re-fetches the updated list from the server. Errors are shown inline without clearing form data. Tenant isolation is enforced at both the frontend (tenantId sourced exclusively from the server-side session) and the backend (JWT tenant_id must match request.TenantId).

All changes are incremental — no rewrites, no modifications to Groups / Access / Simulator tabs.

---

## 2. Codebase Analysis

| Layer | File | Role |
|-------|------|------|
| Page (server component) | `tenant/authorization/users/page.tsx` | Auth guard, SSR fetch, passes `tenantId` prop to table |
| Table (client component) | `tenant/authorization/users/AuthUserTable.tsx` | Add User button, modal state, user list + filters |
| Modal (client component) | `tenant/authorization/users/AddUserModal.tsx` | **New** — form, validation, API submit, toast |
| Client API | `lib/tenant-client-api.ts` | `createUser`, `getRoles` added |
| BFF proxy | `app/api/identity/[...path]/route.ts` | Existing — forwards `POST /identity/api/users` + `GET /identity/api/admin/roles` |
| Toast | `lib/toast-context.tsx` + `components/toast-container.tsx` | Existing — `useToast().show()` |
| Modal base | `components/lien/modal.tsx` — `Modal` | Reused directly |

**TenantId flow**: `requireTenantAdmin()` → `PlatformSession.tenantId` (string, from JWT) → passed as `tenantId: string` prop to `AuthUserTable` → threaded into `AddUserModal` → sent in request body.

---

## 3. Existing API / Create User Contract

**Endpoint**: `POST /identity/api/users`  
**Auth**: Bearer token from `platform_session` cookie, forwarded by BFF proxy  
**Backend file**: `Identity.Api/Endpoints/UserEndpoints.cs`

### Request body
```json
{
  "tenantId":  "<uuid>",
  "email":     "user@example.com",
  "password":  "TemporaryPass123",
  "firstName": "Jane",
  "lastName":  "Doe",
  "roleIds":   ["<role-uuid>"]
}
```

### Backend validation
- `tenantId` in body **must match** the `tenant_id` claim in the caller's JWT — returns 403 otherwise
- `InvalidOperationException` → HTTP 400 (duplicate email, invalid data)

### Response
- **201 Created**: `UserResponse` — shape identical to `TenantUser` (id, tenantId, email, firstName, lastName, isActive, roles, organizationId?, orgType?, productRoles?)
- **400 Bad Request**: `{ title: "...", detail: "..." }` or `{ error: "..." }` — duplicate email, invalid format
- **403 Forbidden**: tenantId mismatch

### Phone
The `Phone` column was added via migration `20260417100001_AddUserPhone` but is **not** in `CreateUserRequest.cs`. Phone is excluded from the form.

---

## 4. Role Source / Contract

**Endpoint**: `GET /identity/api/admin/roles`  
**BFF path**: `/api/identity/api/admin/roles`  
**Returns**: `{ id: string; name: string }[]`

Fetched inside `AddUserModal` on mount (when `open` becomes true). Loading state and error state are handled in the dropdown. Role selection is required (single select mapped to `roleIds: [selectedRoleId]`).

---

## 5. Files Changed

| File | Change |
|------|--------|
| `lib/tenant-client-api.ts` | Added `createUser(body)` and `getRoles()` |
| `tenant/authorization/users/page.tsx` | Passes `tenantId={session.tenantId}` prop to `AuthUserTable` |
| `tenant/authorization/users/AuthUserTable.tsx` | Accepts `tenantId` prop; Add User button; modal open/close state |
| `tenant/authorization/users/AddUserModal.tsx` | **New** — modal form, validation, API call, toast, refresh |

---

## 6. UI Implementation

### Add User button
- Location: top-right of the filter bar (same row as search + status filter)
- Label: "Add User" with `ri-user-add-line` icon
- Only rendered when `tenantId` is available (always true — server enforces auth)
- Clicking sets `showAddUser = true` → modal opens

### Modal
- Title: "Add User"
- Subtitle: "Create a new user in this tenant."
- Size: `md` (max-w-lg)
- Uses existing `Modal` component from `components/lien/modal.tsx`
- Footer: Cancel + "Create User" primary button
- Closes on: Cancel click, Escape key, backdrop click, successful creation

### Form fields
| Field | Type | Required | Validation |
|-------|------|----------|------------|
| First Name | text | yes | non-empty after trim |
| Last Name | text | yes | non-empty after trim |
| Email | email | yes | valid email regex |
| Role | select | yes | must select a role |
| Temporary Password | password | yes | min 8 characters |

Inline field errors shown below each input when touched + invalid.

---

## 7. Validation Logic

Client-side validation runs on submit:
- `firstName.trim()` empty → "First name is required."
- `lastName.trim()` empty → "Last name is required."
- `email.trim()` empty → "Email is required." / fails regex → "Enter a valid email address."
- `roleId === ''` → "Please select a role."
- `password.length < 8` → "Password must be at least 8 characters."

All errors shown at once. Submit button disabled while loading. Form state preserved on API error.

---

## 8. API Integration

```typescript
// tenant-client-api.ts — new methods
createUser: (body: { tenantId: string; email: string; password: string;
                     firstName: string; lastName: string; roleIds?: string[] }) =>
  apiClient.post<TenantUser>('/identity/api/users', body),

getRoles: () =>
  apiClient.get<{ id: string; name: string }[]>('/identity/api/admin/roles'),
```

The `apiClient` prefixes `/api` → `/api/identity/api/users` → BFF proxy at `app/api/identity/[...path]/route.ts` → gateway → identity service.

On success:
1. `useToast().show('User created successfully.', 'success')`
2. `onSuccess()` callback → `router.refresh()` in parent + modal close

On API error:
- Modal stays open, form data preserved
- `ApiError.message` shown in a red banner inside the modal
- Specific handling: 409/duplicate-email → "A user with this email already exists."

---

## 9. Tenant Scope Handling

- `tenantId` is sourced exclusively from `requireTenantAdmin()` → `PlatformSession.tenantId` (derived from the JWT `tenant_id` claim server-side)
- It is passed as a React prop from the server component to the client table/modal
- The UI never allows the user to change or supply tenantId
- The backend enforces that `request.TenantId === caller JWT tenant_id` — so even if a malicious actor crafted a request, the backend rejects it with 403
- No cross-tenant creation is possible

---

## 10. Testing Results

| Scenario | Expected | Result |
|----------|----------|--------|
| Add User button visible | Renders top-right | ✓ |
| Modal opens on button click | Modal shown | ✓ |
| Modal closes on Cancel | Modal hidden, form reset | ✓ |
| Modal closes on Escape | Modal hidden | ✓ (modal.tsx Escape handler) |
| Empty form submit | All 5 field errors shown | ✓ |
| Invalid email | Email error shown | ✓ |
| Password < 8 chars | Password error shown | ✓ |
| No role selected | Role error shown | ✓ |
| Valid submit | Creates user, toast, list refreshes | ✓ |
| Duplicate email | Error shown in modal, form preserved | ✓ |
| API error | Error banner in modal, form preserved | ✓ |
| Roles load | Dropdown populated | ✓ |
| Roles fail to load | "Unable to load roles" message | ✓ |
| Existing list unchanged | Search/filter/pagination work | ✓ (regression) |
| Null-safe list | New user renders correctly | ✓ (LS-ID-TNT-001 guards active) |

---

## 11. Known Issues / Gaps

- **Password field required**: The API (`CreateUserRequest`) requires a `Password` field. An admin must set a temporary password; there is no invite/magic-link flow in the current identity service. The user must be informed of their initial password out-of-band.
- **Role is single-select**: The form maps one selected role to `roleIds: [id]`. The API supports multiple roles, but the UX starts with single for simplicity.
- **Roles endpoint scope**: `GET /identity/api/admin/roles` returns all platform-level roles. In practice, tenant admins should only assign tenant-appropriate roles. This is enforced at the backend level via the existing `assignable-roles` filtering per user; for creation the roles list may include system roles. A future task could use a tenant-scoped roles endpoint if one is introduced.

---

## 12. Final Status

**Complete.** All success criteria met:
- ✔ Add User button visible
- ✔ Modal opens correctly
- ✔ Form validates properly (5 fields, inline errors)
- ✔ Role selection works (fetched from API, dropdown)
- ✔ User created successfully via `POST /identity/api/users`
- ✔ New user appears in list (router.refresh() re-fetches server data)
- ✔ Errors handled gracefully (modal stays open, form preserved)
- ✔ Tenant isolation preserved (tenantId from server session, backend double-validates)
- ✔ No regression from LS-ID-TNT-001
