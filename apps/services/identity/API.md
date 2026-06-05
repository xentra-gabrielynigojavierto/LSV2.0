# Identity Service API Documentation

## Table of Contents

- [Overview](#overview)
- [Authentication & Authorization](#authentication--authorization)
- [Common Models](#common-models)
- [Error Responses](#error-responses)
- [Auth Endpoints](#auth-endpoints)
- [User Endpoints](#user-endpoints)
- [Tenant Endpoints](#tenant-endpoints)
- [Product Endpoints](#product-endpoints)
- [Group Endpoints](#group-endpoints)
- [Access Source Endpoints](#access-source-endpoints)
- [Tenant Branding Endpoints](#tenant-branding-endpoints)
- [Admin Endpoints](#admin-endpoints)
  - [Tenant Management](#admin-tenant-management)
  - [DNS Provisioning](#admin-dns-provisioning)
  - [User Management](#admin-user-management)
  - [User Lifecycle](#admin-user-lifecycle)
  - [User Security & Sessions](#admin-user-security--sessions)
  - [Role Management](#admin-role-management)
  - [Organization Management](#admin-organization-management)
  - [Organization Types](#admin-organization-types)
  - [Relationship Types](#admin-relationship-types)
  - [Organization Relationships](#admin-organization-relationships)
  - [Product Org-Type Rules](#admin-product-org-type-rules)
  - [Product Relationship-Type Rules](#admin-product-relationship-type-rules)
  - [Memberships](#admin-memberships)
  - [Permissions Catalog](#admin-permissions-catalog)
  - [Role Permissions](#admin-role-permissions)
  - [User Effective Permissions](#admin-user-effective-permissions)
  - [Access Debug](#admin-access-debug)
  - [ABAC Policies](#admin-abac-policies)
  - [Policy Rules](#admin-policy-rules)
  - [Permission–Policy Mappings](#admin-permissionpolicy-mappings)
  - [Authorization Simulation](#admin-authorization-simulation)
  - [Audit Logs](#admin-audit-logs)
  - [Platform Settings](#admin-platform-settings)
  - [Support Cases](#admin-support-cases)
  - [Legacy Coverage](#admin-legacy-coverage)
  - [Platform Readiness](#admin-platform-readiness)
  - [CareConnect Readiness & Provisioning](#admin-careconnect-readiness--provisioning)

---

## Overview

The Identity service handles authentication, user management, tenant management, group-based access control, role assignment, product entitlements, ABAC policy management, and a comprehensive admin API surface consumed by the LegalSynq Control Center.

All endpoints are JSON-based. Request and response bodies use `application/json` content type unless otherwise noted.

---

## Authentication & Authorization

The Identity service uses **JWT Bearer tokens** for authentication. Endpoints fall into three categories:

1. **Anonymous** — no authentication required (e.g., login, accept-invite, password-reset/confirm, tenant branding). These are explicitly marked with `.AllowAnonymous()`.
2. **Authenticated** — requires a valid JWT Bearer token via `Authorization: Bearer <token>`. The JWT contains claims: `sub` (user ID), `tenant_id`, `email`, `name`, roles, and `session_version`.
3. **Admin** — accessed via the YARP gateway under `/identity/api/admin/...`. Auth is enforced at the gateway layer (JWT cookie validation). The Identity service trusts all forwarded requests unconditionally.

### Tenant Isolation

- Non-admin endpoints enforce tenant isolation: callers may only access resources within their own tenant (derived from the `tenant_id` JWT claim).
- Admin endpoints enforce tenant boundaries for **TenantAdmin** callers (scoped to own tenant). **PlatformAdmin** callers have cross-tenant access.

### Role-Based Access

| Role | Scope |
|---|---|
| `PlatformAdmin` | Full platform access, cross-tenant |
| `TenantAdmin` | Full tenant access, restricted to own tenant |
| `User` | Standard authenticated user |

---

## Common Models

### PaginatedResult\<T\>

Many list endpoints return results wrapped in this paginated envelope.

| Field | Type | Description |
|---|---|---|
| `items` | `T[]` | Array of result items for the current page |
| `page` | `integer` | Current page number |
| `pageSize` | `integer` | Number of items per page |
| `totalCount` | `integer` | Total number of matching items across all pages |

---

## Error Responses

### 400 Bad Request

Returned when request validation fails.

```json
{
  "error": "Description of the validation failure."
}
```

### 401 Unauthorized

Returned when the request lacks valid authentication credentials or the JWT is invalid/expired.

### 403 Forbidden

Returned when the user is authenticated but does not have the required role or is attempting cross-tenant access.

### 404 Not Found

Returned when a requested resource does not exist.

### 409 Conflict

Returned when an operation conflicts with existing state (e.g., duplicate email, already-assigned role).

```json
{
  "error": "Description of the conflict."
}
```

### Common Status Codes

| Endpoint Type | Success | Possible Errors |
|---|---|---|
| List / Search (`GET` returning paginated results) | `200 OK` | `401`, `403` |
| Get by ID (`GET` returning single item) | `200 OK` | `401`, `403`, `404` |
| Create (`POST`) | `201 Created` | `400`, `401`, `403`, `409` |
| Update (`PUT` / `PATCH`) | `200 OK` | `400`, `401`, `403`, `404` |
| Delete / Deactivate (`DELETE`) | `204 No Content` | `401`, `403`, `404` |
| Action (`POST` on resource) | `200 OK` or `204 No Content` | `400`, `401`, `403`, `404` |

---

## Auth Endpoints

### POST `/api/auth/login`

Authenticate a user with credentials and receive a JWT access token.

**Auth:** Anonymous

**Request Body: `LoginRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `tenantCode` | `string` | Yes | Tenant code (e.g., `ACME`) |
| `email` | `string` | Yes | User email address |
| `password` | `string` | Yes | User password |

**Response:** `200 OK` — `LoginResponse`

| Field | Type | Description |
|---|---|---|
| `accessToken` | `string` | JWT access token |
| `expiresAtUtc` | `datetime` | Token expiration timestamp |
| `user` | `UserResponse` | Authenticated user details |

**Errors:**
- `401` — Invalid credentials
- `400` — Account locked or inactive

---

### GET `/api/auth/me`

Get the current authenticated user's session envelope from the validated JWT claims.

**Auth:** Authenticated (Bearer JWT required)

**Response:** `200 OK` — `AuthMeResponse`

| Field | Type | Nullable | Description |
|---|---|---|---|
| `userId` | `string` | No | User unique identifier |
| `email` | `string` | No | User email address |
| `tenantId` | `string` | No | Tenant unique identifier |
| `tenantCode` | `string` | No | Tenant code |
| `orgId` | `string` | Yes | Primary organization ID |
| `orgType` | `string` | Yes | Primary organization type |
| `orgName` | `string` | Yes | Primary organization name |
| `productRoles` | `string[]` | No | Product-scoped role codes |
| `systemRoles` | `string[]` | No | System role names (e.g., `PlatformAdmin`) |
| `expiresAtUtc` | `datetime` | No | Session expiration |
| `sessionTimeoutMinutes` | `integer` | No | Idle session timeout (default: 30) |
| `avatarDocumentId` | `guid` | Yes | User's avatar document ID |
| `enabledProducts` | `string[]` | Yes | Frontend product codes enabled for the tenant |

---

### POST `/api/auth/logout`

Log out the current user. Backend is stateless; real logout is cookie deletion on the Next.js BFF. Emits an audit event for HIPAA compliance.

**Auth:** Anonymous (JWT may already be expired)

**Response:** `204 No Content`

---

### GET `/api/organizations/my/config`

Get organization-level configuration for the caller's organization. The `org_id` claim from the JWT is used to resolve the organization. If the claim is missing or the organization is not found, a fallback response is still returned with `200 OK` (never errors).

**Auth:** Authenticated

**Response:** `200 OK`

| Field | Type | Description |
|---|---|---|
| `organizationId` | `string` | Organization ID (`null` if `org_id` claim is missing or org not found) |
| `productCode` | `string` | Product code (currently always `LIENS`) |
| `settings` | `object` | Settings object with `providerMode` (currently always `"sell"`; `"manage"` mode planned for a future DB-backed setting) |

---

### POST `/api/auth/accept-invite`

Accept an invitation token, set a new password, and activate the invited user account.

**Auth:** Anonymous

**Request Body: `AcceptInviteRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `token` | `string` | Yes | Raw invitation token |
| `newPassword` | `string` | Yes | New password (minimum 8 characters) |

**Response:** `200 OK`

```json
{
  "message": "Invitation accepted. Your account is now active."
}
```

**Errors:**
- `400` — Invalid/expired token, already accepted, or password too short

---

### POST `/api/auth/change-password`

Change the authenticated user's password.

**Auth:** Authenticated

**Request Body: `ChangePasswordRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `currentPassword` | `string` | Yes | Current password |
| `newPassword` | `string` | Yes | New password (minimum 8 characters, must differ from current) |

**Response:** `200 OK`

```json
{
  "message": "Password changed successfully."
}
```

**Errors:**
- `400` — Current password incorrect, new password too short, or same as current
- `404` — User record not found

---

### PATCH `/api/profile/avatar`

Set the authenticated user's avatar document reference.

**Auth:** Authenticated

**Request Body: `SetAvatarRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `documentId` | `string` | Yes | UUID of the uploaded avatar document |

**Response:** `200 OK`

```json
{
  "avatarDocumentId": "guid"
}
```

---

### DELETE `/api/profile/avatar`

Remove the authenticated user's avatar.

**Auth:** Authenticated

**Response:** `204 No Content`

---

### POST `/api/auth/password-reset/confirm`

Confirm a password reset using an admin-triggered reset token. Sets a new password and invalidates all existing sessions.

**Auth:** Anonymous

**Request Body: `PasswordResetConfirmRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `token` | `string` | Yes | Raw reset token |
| `newPassword` | `string` | Yes | New password (minimum 8 characters) |

**Response:** `200 OK`

```json
{
  "message": "Password updated successfully."
}
```

**Errors:**
- `400` — Invalid/expired/used token, or password too short

---

### POST `/api/auth/forgot-password`

Self-service password reset request. Generates a reset token for the user.

**Auth:** Anonymous

**Request Body: `ForgotPasswordRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `tenantCode` | `string` | Yes | Tenant code |
| `email` | `string` | Yes | User email address |

**Response:** `200 OK`

```json
{
  "message": "If an account exists with that email, a password reset link has been generated.",
  "resetToken": "base64-encoded-token"
}
```

---

## User Endpoints

Base path: `/api/users`

### POST `/api/users`

Create a new user within the caller's tenant.

**Auth:** Authenticated (caller must belong to the same tenant as `tenantId`)

**Request Body: `CreateUserRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `tenantId` | `guid` | Yes | Tenant ID (must match caller's tenant) |
| `email` | `string` | Yes | User email address |
| `password` | `string` | Yes | Initial password |
| `firstName` | `string` | Yes | First name |
| `lastName` | `string` | Yes | Last name |
| `roleIds` | `guid[]` | No | List of role IDs to assign |

**Response:** `201 Created` — `UserResponse`

Returns the created user with a `Location` header pointing to `/api/users/{id}`.

**Errors:**
- `400` — Validation failure (e.g., duplicate email)
- `401` — Missing tenant claim
- `403` — Cross-tenant creation attempt

---

### GET `/api/users`

List all users in the caller's tenant.

**Auth:** Authenticated

**Response:** `200 OK` — `UserResponse[]`

---

### GET `/api/users/{id}`

Get a user by ID (must be in the caller's tenant).

**Auth:** Authenticated

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | User unique identifier |

**Response:** `200 OK` — `UserResponse`

**Errors:**
- `403` — User belongs to a different tenant
- `404` — User not found

---

### UserResponse

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | `guid` | No | User unique identifier |
| `tenantId` | `guid` | No | Tenant ID |
| `email` | `string` | No | Email address |
| `firstName` | `string` | No | First name |
| `lastName` | `string` | No | Last name |
| `isActive` | `boolean` | No | Active status |
| `roles` | `string[]` | No | Assigned role names |
| `organizationId` | `guid` | Yes | Primary organization ID |
| `orgType` | `string` | Yes | Organization type code |
| `productRoles` | `string[]` | Yes | Product-scoped role codes |

---

## Tenant Endpoints

### GET `/api/tenants`

List all tenants.

**Auth:** None (no explicit auth policy on this endpoint)

**Response:** `200 OK` — `TenantDto[]`

| Field | Type | Description |
|---|---|---|
| `id` | `guid` | Tenant unique identifier |
| `name` | `string` | Tenant name |
| `code` | `string` | Tenant code |
| `isActive` | `boolean` | Active status |

---

## Product Endpoints

### GET `/api/products`

List all active products.

**Auth:** None (no explicit auth policy on this endpoint)

**Response:** `200 OK` — `ProductDto[]`

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | `guid` | No | Product unique identifier |
| `name` | `string` | No | Product name |
| `code` | `string` | No | Product code (e.g., `SYNQ_FUND`) |
| `description` | `string` | Yes | Product description |
| `isActive` | `boolean` | No | Active status |

---

## Group Endpoints

Base path: `/api/tenants/{tenantId}/groups`

All group endpoints require authentication. Read operations require the caller to belong to the target tenant (or be PlatformAdmin). Mutation operations additionally require the `TenantAdmin` role.

### GET `/api/tenants/{tenantId}/groups`

List all groups for a tenant.

**Auth:** Authenticated (tenant member or PlatformAdmin)

**Response:** `200 OK` — `GroupResponse[]`

---

### POST `/api/tenants/{tenantId}/groups`

Create a new group.

**Auth:** Authenticated (TenantAdmin or PlatformAdmin)

**Request Body: `CreateGroupRequest`**

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `name` | `string` | Yes | | Group name |
| `description` | `string` | No | `null` | Group description |
| `scopeType` | `string` | No | `Tenant` | Scope type (`Tenant`, `Product`, `Organization`) |
| `productCode` | `string` | No | `null` | Product code for product-scoped groups |
| `organizationId` | `guid` | No | `null` | Organization ID for org-scoped groups |

**Response:** `201 Created` — `GroupResponse`

---

### GET `/api/tenants/{tenantId}/groups/{groupId}`

Get a group by ID.

**Auth:** Authenticated (tenant member or PlatformAdmin)

**Response:** `200 OK` — `GroupResponse`

**Error:** `404` — Group not found

---

### PATCH `/api/tenants/{tenantId}/groups/{groupId}`

Update a group.

**Auth:** Authenticated (TenantAdmin or PlatformAdmin)

**Request Body: `UpdateGroupRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `name` | `string` | Yes | Group name |
| `description` | `string` | No | Group description |

**Response:** `200 OK` — `GroupResponse`

---

### DELETE `/api/tenants/{tenantId}/groups/{groupId}`

Archive (soft-delete) a group.

**Auth:** Authenticated (TenantAdmin or PlatformAdmin)

**Response:** `204 No Content`

**Error:** `404` — Group not found

---

### GroupResponse

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | `guid` | No | Group unique identifier |
| `tenantId` | `guid` | No | Tenant ID |
| `name` | `string` | No | Group name |
| `description` | `string` | Yes | Description |
| `status` | `string` | No | Status (`Active`, `Archived`) |
| `scopeType` | `string` | No | Scope type (`Tenant`, `Product`, `Organization`) |
| `productCode` | `string` | Yes | Product code (if product-scoped) |
| `organizationId` | `guid` | Yes | Organization ID (if org-scoped) |
| `createdAtUtc` | `datetime` | No | Creation timestamp |
| `updatedAtUtc` | `datetime` | No | Last update timestamp |

---

### Group Members

#### GET `/api/tenants/{tenantId}/groups/{groupId}/members`

List members of a group.

**Auth:** Authenticated (tenant member or PlatformAdmin)

**Response:** `200 OK` — Array of membership objects

| Field | Type | Description |
|---|---|---|
| `id` | `guid` | Membership ID |
| `tenantId` | `guid` | Tenant ID |
| `groupId` | `guid` | Group ID |
| `userId` | `guid` | User ID |
| `membershipStatus` | `string` | Status (`Active`, `Removed`) |
| `addedAtUtc` | `datetime` | When the member was added |
| `removedAtUtc` | `datetime` | When the member was removed (null if active) |

---

#### POST `/api/tenants/{tenantId}/groups/{groupId}/members`

Add a member to a group.

**Auth:** Authenticated (TenantAdmin or PlatformAdmin)

**Request Body: `AddMemberRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `userId` | `guid` | Yes | User ID to add |

**Response:** `201 Created` — Membership object

---

#### DELETE `/api/tenants/{tenantId}/groups/{groupId}/members/{userId}`

Remove a member from a group.

**Auth:** Authenticated (TenantAdmin or PlatformAdmin)

**Response:** `204 No Content`

**Error:** `404` — Member not found

---

#### GET `/api/tenants/{tenantId}/users/{userId}/groups`

List groups a user belongs to.

**Auth:** Authenticated (tenant member or PlatformAdmin)

**Response:** `200 OK` — Array of membership objects

---

### Group Products

#### GET `/api/tenants/{tenantId}/groups/{groupId}/products`

List products granted to a group.

**Auth:** Authenticated (tenant member or PlatformAdmin)

**Response:** `200 OK` — Array of group product access objects

| Field | Type | Description |
|---|---|---|
| `id` | `guid` | Access record ID |
| `tenantId` | `guid` | Tenant ID |
| `groupId` | `guid` | Group ID |
| `productCode` | `string` | Product code |
| `accessStatus` | `string` | Status (`Active`, `Revoked`) |
| `grantedAtUtc` | `datetime` | When access was granted |
| `revokedAtUtc` | `datetime` | When access was revoked (null if active) |

---

#### PUT `/api/tenants/{tenantId}/groups/{groupId}/products/{productCode}`

Grant a product to a group.

**Auth:** Authenticated (TenantAdmin or PlatformAdmin)

**Response:** `200 OK` — Group product access object

---

#### DELETE `/api/tenants/{tenantId}/groups/{groupId}/products/{productCode}`

Revoke a product from a group.

**Auth:** Authenticated (TenantAdmin or PlatformAdmin)

**Response:** `204 No Content`

---

### Group Roles

#### GET `/api/tenants/{tenantId}/groups/{groupId}/roles`

List roles assigned to a group.

**Auth:** Authenticated (tenant member or PlatformAdmin)

**Response:** `200 OK` — Array of group role assignment objects

| Field | Type | Description |
|---|---|---|
| `id` | `guid` | Assignment ID |
| `tenantId` | `guid` | Tenant ID |
| `groupId` | `guid` | Group ID |
| `roleCode` | `string` | Role code |
| `productCode` | `string` | Product code (null if global) |
| `organizationId` | `guid` | Organization scope (null if unscoped) |
| `assignmentStatus` | `string` | Status (`Active`, `Removed`) |
| `assignedAtUtc` | `datetime` | When the role was assigned |
| `removedAtUtc` | `datetime` | When the role was removed (null if active) |

---

#### POST `/api/tenants/{tenantId}/groups/{groupId}/roles`

Assign a role to a group.

**Auth:** Authenticated (TenantAdmin or PlatformAdmin)

**Request Body: `AssignGroupRoleRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `roleCode` | `string` | Yes | Role code to assign |
| `productCode` | `string` | No | Product scope |
| `organizationId` | `guid` | No | Organization scope |

**Response:** `201 Created` — Role assignment object

---

#### DELETE `/api/tenants/{tenantId}/groups/{groupId}/roles/{assignmentId}`

Remove a role from a group.

**Auth:** Authenticated (TenantAdmin or PlatformAdmin)

**Response:** `204 No Content`

---

## Access Source Endpoints

Base path: `/api/tenants/{tenantId}`

All endpoints require authentication. Read operations require tenant membership or PlatformAdmin. Mutation operations require TenantAdmin or PlatformAdmin.

### Tenant Products

#### GET `/api/tenants/{tenantId}/products`

List product entitlements for a tenant.

**Auth:** Authenticated (tenant member or PlatformAdmin)

**Response:** `200 OK` — Array of tenant product entitlements

| Field | Type | Description |
|---|---|---|
| `id` | `guid` | Entitlement ID |
| `tenantId` | `guid` | Tenant ID |
| `productCode` | `string` | Product code |
| `status` | `string` | Status (`Active`, `Disabled`) |
| `enabledAtUtc` | `datetime` | When enabled |
| `disabledAtUtc` | `datetime` | When disabled (null if active) |
| `createdAtUtc` | `datetime` | Creation timestamp |
| `updatedAtUtc` | `datetime` | Last update timestamp |

---

#### PUT `/api/tenants/{tenantId}/products/{productCode}`

Enable (upsert) a product entitlement for a tenant.

**Auth:** Authenticated (TenantAdmin or PlatformAdmin)

**Response:** `200 OK` — Tenant product entitlement object

---

#### DELETE `/api/tenants/{tenantId}/products/{productCode}`

Disable a product entitlement for a tenant.

**Auth:** Authenticated (TenantAdmin or PlatformAdmin)

**Response:** `204 No Content`

**Error:** `404` — Product entitlement not found

---

### User Products

#### GET `/api/tenants/{tenantId}/users/{userId}/products`

List product access records for a user.

**Auth:** Authenticated (tenant member or PlatformAdmin)

**Response:** `200 OK` — Array of user product access objects

| Field | Type | Description |
|---|---|---|
| `id` | `guid` | Access record ID |
| `tenantId` | `guid` | Tenant ID |
| `userId` | `guid` | User ID |
| `productCode` | `string` | Product code |
| `accessStatus` | `string` | Status (`Active`, `Revoked`) |
| `sourceType` | `string` | How access was granted |
| `grantedAtUtc` | `datetime` | When granted |
| `revokedAtUtc` | `datetime` | When revoked (null if active) |
| `createdAtUtc` | `datetime` | Creation timestamp |
| `updatedAtUtc` | `datetime` | Last update timestamp |

---

#### PUT `/api/tenants/{tenantId}/users/{userId}/products/{productCode}`

Grant product access to a user.

**Auth:** Authenticated (TenantAdmin or PlatformAdmin)

**Response:** `200 OK` — User product access object

---

#### DELETE `/api/tenants/{tenantId}/users/{userId}/products/{productCode}`

Revoke product access from a user.

**Auth:** Authenticated (TenantAdmin or PlatformAdmin)

**Response:** `204 No Content`

---

### User Roles

#### GET `/api/tenants/{tenantId}/users/{userId}/roles`

List role assignments for a user.

**Auth:** Authenticated (tenant member or PlatformAdmin)

**Response:** `200 OK` — Array of user role assignment objects

| Field | Type | Description |
|---|---|---|
| `id` | `guid` | Assignment ID |
| `tenantId` | `guid` | Tenant ID |
| `userId` | `guid` | User ID |
| `roleCode` | `string` | Role code |
| `productCode` | `string` | Product scope (null if unscoped) |
| `organizationId` | `guid` | Organization scope (null if unscoped) |
| `assignmentStatus` | `string` | Status (`Active`, `Removed`) |
| `sourceType` | `string` | How the role was assigned |
| `assignedAtUtc` | `datetime` | When assigned |
| `removedAtUtc` | `datetime` | When removed (null if active) |
| `createdAtUtc` | `datetime` | Creation timestamp |
| `updatedAtUtc` | `datetime` | Last update timestamp |

---

#### POST `/api/tenants/{tenantId}/users/{userId}/roles`

Assign a role to a user.

**Auth:** Authenticated (TenantAdmin or PlatformAdmin)

**Request Body: `AssignRoleRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `roleCode` | `string` | Yes | Role code to assign |
| `productCode` | `string` | No | Product scope |
| `organizationId` | `guid` | No | Organization scope |

**Response:** `201 Created` — Role assignment object

---

#### DELETE `/api/tenants/{tenantId}/users/{userId}/roles/{assignmentId}`

Remove a role from a user.

**Auth:** Authenticated (TenantAdmin or PlatformAdmin)

**Response:** `204 No Content`

---

### Access Snapshot

#### GET `/api/tenants/{tenantId}/users/{userId}/access-snapshot`

Get a complete access snapshot for a user showing tenant products, user products, and user roles.

**Auth:** Authenticated (tenant member or PlatformAdmin)

**Response:** `200 OK`

| Field | Type | Description |
|---|---|---|
| `tenantProducts` | `array` | Tenant-level product entitlements |
| `userProducts` | `array` | User-level product access records |
| `userRoles` | `array` | User role assignments |

---

## Tenant Branding Endpoints

### GET `/api/tenants/current/branding`

Resolve tenant branding for the current request context. Used by the login page before authentication is available.

**Auth:** Anonymous

**Tenant Resolution Priority:**
1. `X-Tenant-Code` request header (dev override / Next.js BFF)
2. `Host` / `X-Forwarded-Host` header — subdomain-based (production)

**Response:** `200 OK` — `TenantBrandingResponse`

| Field | Type | Nullable | Description |
|---|---|---|---|
| `tenantId` | `string` | No | Tenant ID (empty string if unresolved) |
| `tenantCode` | `string` | No | Tenant code (empty string if unresolved) |
| `displayName` | `string` | No | Display name (defaults to `LegalSynq`) |
| `logoUrl` | `string` | Yes | Logo URL (reserved for future use) |
| `logoDocumentId` | `string` | Yes | Logo document ID for authenticated proxy |
| `logoWhiteDocumentId` | `string` | Yes | White/reversed logo document ID |
| `primaryColor` | `string` | Yes | Primary brand color (Phase 2) |
| `faviconUrl` | `string` | Yes | Favicon URL (Phase 2) |

---

## Admin Endpoints

Base path: `/api/admin`

All admin endpoints are consumed by the LegalSynq Control Center. Auth is enforced at the YARP gateway layer. Within endpoints, **TenantAdmin** callers are scoped to their own tenant; **PlatformAdmin** callers have unrestricted access.

---

### Admin: Tenant Management

#### GET `/api/admin/tenants`

List tenants with pagination and search.

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `page` | `integer` | No | `1` | Page number |
| `pageSize` | `integer` | No | `20` | Items per page |
| `search` | `string` | No | `""` | Search by name or code |

**Response:** `200 OK` — `PaginatedResult` with tenant summary objects

---

#### GET `/api/admin/tenants/{id}`

Get detailed tenant information including users, organizations, product entitlements, and provisioning status.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Tenant unique identifier |

**Response:** `200 OK` — Detailed tenant object

**Error:** `404` — Tenant not found

---

#### POST `/api/admin/tenants`

Create a new tenant with a default admin user in a single atomic transaction.

**Request Body: `CreateTenantRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `name` | `string` | Yes | Tenant display name |
| `code` | `string` | Yes | Unique tenant code (2–12 alphanumeric, uppercased) |
| `adminEmail` | `string` | Yes | Admin user email |
| `adminFirstName` | `string` | Yes | Admin user first name |
| `adminLastName` | `string` | Yes | Admin user last name |
| `orgType` | `string` | No | Organization type (`PROVIDER`, `FUNDER`, `LIEN_OWNER`, or default `LAW_FIRM`) |
| `preferredSubdomain` | `string` | No | Preferred subdomain for DNS provisioning |
| `products` | `string[]` | No | Product codes to enable |

**Response:** `201 Created`

| Field | Type | Description |
|---|---|---|
| `tenantId` | `guid` | New tenant ID |
| `displayName` | `string` | Tenant name |
| `code` | `string` | Tenant code |
| `status` | `string` | `Active` |
| `adminUserId` | `guid` | Admin user ID |
| `adminEmail` | `string` | Admin email |
| `temporaryPassword` | `string` | One-time temporary password |
| `subdomain` | `string` | Assigned subdomain |
| `provisioningStatus` | `string` | DNS provisioning status |
| `hostname` | `string` | Full hostname |
| `productsProvisioned` | `array` | Products provisioned |

**Errors:**
- `400` — Validation failure
- `409` — Duplicate tenant code or admin email

---

#### POST `/api/admin/tenants/{id}/entitlements/{productCode}`

Update a product entitlement for a tenant (enable or disable).

**Request Body: `EntitlementRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `enabled` | `boolean` | Yes | Whether the product is enabled |

**Response:** `200 OK` — Entitlement status with provisioning result

---

#### PATCH `/api/admin/tenants/{id}/session-settings`

Update tenant session timeout settings.

**Request Body: `SessionSettingsRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `sessionTimeoutMinutes` | `integer` | No | Session timeout in minutes |

**Response:** `200 OK`

---

#### PATCH `/api/admin/tenants/{id}/logo`

Set the tenant's logo document ID.

**Request Body:**

| Field | Type | Required | Description |
|---|---|---|---|
| `documentId` | `string` | Yes | UUID of the uploaded logo document |

**Response:** `200 OK`

---

#### DELETE `/api/admin/tenants/{id}/logo`

Clear the tenant's logo.

**Response:** `204 No Content`

---

#### PATCH `/api/admin/tenants/{id}/logo-white`

Set the tenant's white/reversed logo document ID.

**Request Body:**

| Field | Type | Required | Description |
|---|---|---|---|
| `documentId` | `string` | Yes | UUID of the uploaded logo document |

**Response:** `200 OK`

---

#### DELETE `/api/admin/tenants/{id}/logo-white`

Clear the tenant's white/reversed logo.

**Response:** `204 No Content`

---

#### POST `/api/admin/tenants/{id}/provisioning/retry`

Retry DNS provisioning for a tenant whose provisioning previously failed.

**Response:** `200 OK`

| Field | Type | Description |
|---|---|---|
| `success` | `boolean` | Whether provisioning succeeded |
| `provisioningStatus` | `string` | Current provisioning status |
| `hostname` | `string` | Assigned hostname (null if failed) |
| `error` | `string` | Error message (null if succeeded) |

---

#### POST `/api/admin/tenants/{id}/verification/retry`

Retry DNS verification for a tenant whose verification previously failed.

**Response:** `200 OK`

| Field | Type | Description |
|---|---|---|
| `success` | `boolean` | Whether verification succeeded |
| `provisioningStatus` | `string` | Current provisioning status |
| `hostname` | `string` | Hostname |
| `error` | `string` | Error message (null if succeeded) |
| `failureStage` | `string` | Stage where failure occurred |
| `attemptNumber` | `integer` | Attempt count |
| `stillRetrying` | `boolean` | Whether retries remain |
| `exhausted` | `boolean` | Whether all retries are exhausted |
| `nextRetryAtUtc` | `datetime` | Next scheduled retry (null if exhausted) |

---

### Admin: DNS Provisioning

#### POST `/api/admin/dns/provision`

Provision an infrastructure subdomain for a service.

**Request Body: `InfraSubdomainRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `subdomain` | `string` | Yes | Subdomain to provision |

**Response:** `200 OK`

| Field | Type | Description |
|---|---|---|
| `success` | `boolean` | Whether provisioning succeeded |
| `hostname` | `string` | Full hostname |
| `subdomain` | `string` | Subdomain slug |

**Error:** `502` — DNS provisioning failed

---

### Admin: User Management

#### GET `/api/admin/users`

List users with pagination, search, and filters.

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `page` | `integer` | No | `1` | Page number |
| `pageSize` | `integer` | No | `20` | Items per page |
| `search` | `string` | No | `""` | Search by email, first name, or last name |
| `tenantId` | `guid` | No | | Filter by tenant (PlatformAdmin only) |
| `status` | `string` | No | | Filter by status: `active`, `inactive`, `invited` |

**Response:** `200 OK` — `PaginatedResult` with user summary objects

---

#### GET `/api/admin/users/{id}`

Get detailed user information including roles, memberships, and groups.

**Response:** `200 OK` — Detailed user object with `memberships`, `groups`, `roles`

**Error:** `404` — User not found

---

#### GET `/api/admin/users/{id}/activity`

Get audit trail entries for a user.

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `page` | `integer` | No | `1` | Page number |
| `pageSize` | `integer` | No | `20` | Items per page (max 100) |
| `category` | `string` | No | `""` | Filter by entity type |

**Response:** `200 OK` — `PaginatedResult` with audit log entries

---

### Admin: User Lifecycle

#### PATCH `/api/admin/users/{id}/deactivate`

Deactivate a user account. Idempotent.

**Response:** `204 No Content`

---

#### POST `/api/admin/users/{id}/activate`

Activate a user account. Idempotent.

**Response:** `204 No Content`

---

#### POST `/api/admin/users/invite`

Create a new user and send an invitation.

**Request Body: `InviteUserRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `tenantId` | `guid` | Yes | Tenant to invite user into |
| `email` | `string` | Yes | User email |
| `firstName` | `string` | Yes | First name |
| `lastName` | `string` | Yes | Last name |
| `roleId` | `guid` | No | Initial role to assign |
| `invitedByUserId` | `guid` | No | Admin who sent the invite |

**Response:** `201 Created`

| Field | Type | Description |
|---|---|---|
| `userId` | `guid` | New user ID |
| `invitationId` | `guid` | Invitation record ID |
| `email` | `string` | Normalized email |

**Errors:**
- `400` — Missing required fields
- `404` — Tenant not found
- `409` — Email already exists in tenant

---

#### POST `/api/admin/users/{id}/resend-invite`

Revoke existing invitations and create a new one for the user.

**Response:** `200 OK`

| Field | Type | Description |
|---|---|---|
| `invitationId` | `guid` | New invitation ID |

---

### Admin: User Security & Sessions

#### POST `/api/admin/users/{id}/lock`

Lock a user account. Locked users cannot authenticate. Invalidates all active JWTs. Idempotent.

**Response:** `204 No Content`

---

#### POST `/api/admin/users/{id}/unlock`

Unlock a locked user account. Idempotent.

**Response:** `204 No Content`

---

#### POST `/api/admin/users/{id}/reset-password`

Trigger a password reset workflow. Creates a 24-hour expiry reset token.

**Response:** `200 OK`

```json
{
  "message": "Password reset email will be sent to the user."
}
```

---

#### POST `/api/admin/users/{id}/set-password`

Directly set a new password for a user. Invalidates all existing sessions.

**Request Body: `SetPasswordRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `newPassword` | `string` | Yes | New password (minimum 8 characters) |

**Response:** `200 OK`

```json
{
  "message": "Password updated successfully."
}
```

---

#### POST `/api/admin/users/{id}/force-logout`

Revoke all active sessions by incrementing the user's session version.

**Response:** `204 No Content`

---

#### GET `/api/admin/users/{id}/security`

Get security summary for a user.

**Response:** `200 OK`

| Field | Type | Description |
|---|---|---|
| `userId` | `guid` | User ID |
| `email` | `string` | User email |
| `isLocked` | `boolean` | Lock status |
| `lockedAtUtc` | `datetime` | When locked (null if not locked) |
| `lastLoginAtUtc` | `datetime` | Last login timestamp |
| `sessionVersion` | `integer` | Current session version |
| `isActive` | `boolean` | Active status |
| `hasPendingInvite` | `boolean` | Whether a pending invite exists |
| `recentPasswordResets` | `array` | Last 5 password reset tokens |

---

### Admin: Role Management

#### GET `/api/admin/roles`

List all roles with user counts and permission counts.

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `page` | `integer` | No | `1` | Page number |
| `pageSize` | `integer` | No | `20` | Items per page |

**Response:** `200 OK` — `PaginatedResult` with role objects

---

#### GET `/api/admin/roles/{id}`

Get a role by ID with assigned permissions.

**Response:** `200 OK` — Detailed role object

**Error:** `404` — Role not found

---

#### POST `/api/admin/users/{id}/roles`

Assign a role to a user. Supports GLOBAL scope (default). Enforces product enablement and org-type eligibility guardrails for product roles.

**Request Body: `AssignRoleRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `roleId` | `guid` | Yes | Role to assign |
| `assignedByUserId` | `guid` | No | Admin who assigned the role |
| `scopeType` | `string` | No | Scope type (default: `GLOBAL`) |
| `organizationId` | `guid` | No | Organization scope |
| `productId` | `guid` | No | Product scope |
| `organizationRelationshipId` | `guid` | No | Relationship scope |

**Response:** `201 Created` — Assignment details

**Errors:**
- `400` — Scope validation error, product not enabled, or org type mismatch
- `404` — User or role not found
- `409` — Duplicate assignment

---

#### DELETE `/api/admin/users/{id}/roles/{roleId}`

Revoke a role from a user.

**Response:** `204 No Content`

---

#### GET `/api/admin/users/{id}/assignable-roles`

Get all roles with eligibility metadata for a specific user. Includes org-type and product enablement checks.

**Response:** `200 OK`

| Field | Type | Description |
|---|---|---|
| `items` | `array` | Roles with `assignable`, `disabledReason`, `isAssigned` flags |
| `userOrgType` | `string` | User's primary organization type |
| `tenantEnabledProducts` | `integer` | Count of enabled products |

---

#### GET `/api/admin/users/{id}/scoped-roles`

Get all active scoped role assignments for a user grouped by scope type.

**Response:** `200 OK`

| Field | Type | Description |
|---|---|---|
| `userId` | `guid` | User ID |
| `totalActive` | `integer` | Total active assignments |
| `assignments` | `array` | Assignment details with scope information |
| `byScope` | `object` | Count of assignments per scope type |

---

### Admin: Organization Management

#### GET `/api/admin/organizations`

List active organizations, optionally filtered by tenant.

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `tenantId` | `guid` | No | | Filter by tenant ID |

**Response:** `200 OK`

---

#### POST `/api/admin/organizations`

Create a minimal PROVIDER organization for a CareConnect provider. Idempotent.

**Request Body: `CreateProviderOrgRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `tenantId` | `guid` | Yes | Tenant ID |
| `providerCcId` | `guid` | Yes | CareConnect provider ID |
| `providerName` | `string` | Yes | Provider display name |

**Response:** `201 Created` (new) or `200 OK` (existing)

| Field | Type | Description |
|---|---|---|
| `id` | `guid` | Organization ID |
| `name` | `string` | Organization name |
| `isNew` | `boolean` | Whether a new org was created |

---

#### GET `/api/admin/organizations/{id}`

Get an organization by ID.

**Response:** `200 OK` — Organization details

**Error:** `404` — Organization not found

---

#### PUT `/api/admin/organizations/{id}`

Update an organization's name, display name, or org type.

**Auth:** PlatformAdmin only

**Request Body: `UpdateOrganizationRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `name` | `string` | No | Organization name |
| `displayName` | `string` | No | Display name |
| `orgType` | `string` | No | Organization type code |

**Response:** `200 OK` — Updated organization

---

### Admin: Organization Types

#### GET `/api/admin/organization-types`

List all organization types.

**Response:** `200 OK`

| Field | Type | Description |
|---|---|---|
| `items` | `array` | Organization types with `id`, `code`, `displayName`, `description`, `isSystem`, `isActive` |
| `totalCount` | `integer` | Total count |

---

#### GET `/api/admin/organization-types/{id}`

Get an organization type by ID.

**Response:** `200 OK` — Organization type details

**Error:** `404` — Not found

---

### Admin: Relationship Types

#### GET `/api/admin/relationship-types`

List all relationship types.

**Response:** `200 OK`

| Field | Type | Description |
|---|---|---|
| `items` | `array` | Relationship types with `id`, `code`, `displayName`, `description`, `isDirectional`, `isSystem`, `isActive` |
| `totalCount` | `integer` | Total count |

---

#### GET `/api/admin/relationship-types/{id}`

Get a relationship type by ID.

**Response:** `200 OK` — Relationship type details

**Error:** `404` — Not found

---

### Admin: Organization Relationships

#### GET `/api/admin/organization-relationships`

List organization relationships with filters.

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `page` | `integer` | No | `1` | Page number |
| `pageSize` | `integer` | No | `20` | Items per page |
| `tenantId` | `guid` | No | | Filter by tenant |
| `sourceOrgId` | `guid` | No | | Filter by source organization |
| `activeOnly` | `boolean` | No | `true` | Show only active relationships |

**Response:** `200 OK` — `PaginatedResult` with relationship objects

---

#### GET `/api/admin/organization-relationships/{id}`

Get an organization relationship by ID.

**Response:** `200 OK` — Relationship details

**Error:** `404` — Not found

---

#### POST `/api/admin/organization-relationships`

Create an organization relationship.

**Request Body: `CreateOrgRelationshipRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `sourceOrganizationId` | `guid` | Yes | Source organization ID |
| `targetOrganizationId` | `guid` | Yes | Target organization ID |
| `relationshipTypeId` | `guid` | Yes | Relationship type ID |
| `productId` | `guid` | No | Associated product ID |

**Response:** `201 Created` — Relationship details

**Errors:**
- `404` — Source org, target org, or relationship type not found
- `409` — Relationship already exists

---

#### DELETE `/api/admin/organization-relationships/{id}`

Deactivate an organization relationship.

**Response:** `200 OK`

| Field | Type | Description |
|---|---|---|
| `id` | `guid` | Relationship ID |
| `isActive` | `boolean` | `false` |

---

### Admin: Product Org-Type Rules

#### GET `/api/admin/product-org-type-rules`

List active product org-type rules (plain array, not paginated).

**Response:** `200 OK` — Array of rule objects

| Field | Type | Description |
|---|---|---|
| `id` | `guid` | Rule ID |
| `productId` | `guid` | Product ID |
| `productCode` | `string` | Product code |
| `productRoleId` | `guid` | Product role ID |
| `productRoleCode` | `string` | Product role code |
| `productRoleName` | `string` | Product role name |
| `organizationTypeId` | `guid` | Organization type ID |
| `organizationTypeCode` | `string` | Organization type code |
| `organizationTypeName` | `string` | Organization type display name |
| `isActive` | `boolean` | Active status |
| `createdAtUtc` | `datetime` | Creation timestamp |

---

### Admin: Product Relationship-Type Rules

#### GET `/api/admin/product-relationship-type-rules`

List active product relationship-type rules (plain array, not paginated).

**Alias:** `GET /api/admin/product-rel-type-rules`

**Response:** `200 OK` — Array of rule objects

| Field | Type | Description |
|---|---|---|
| `id` | `guid` | Rule ID |
| `productId` | `guid` | Product ID |
| `productCode` | `string` | Product code |
| `relationshipTypeId` | `guid` | Relationship type ID |
| `relationshipTypeCode` | `string` | Relationship type code |
| `relationshipTypeName` | `string` | Relationship type display name |
| `isActive` | `boolean` | Active status |
| `createdAtUtc` | `datetime` | Creation timestamp |

---

### Admin: Memberships

#### POST `/api/admin/users/{id}/memberships`

Assign a user to an organization.

**Request Body: `AssignMembershipRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `organizationId` | `guid` | Yes | Organization to join |
| `memberRole` | `string` | No | Member role (default: `Member`) |
| `grantedByUserId` | `guid` | No | Admin who granted the membership |

**Response:** `201 Created`

| Field | Type | Description |
|---|---|---|
| `membershipId` | `guid` | Membership ID |
| `userId` | `guid` | User ID |
| `organizationId` | `guid` | Organization ID |
| `memberRole` | `string` | Assigned member role |
| `isPrimary` | `boolean` | Whether this is the primary membership |

**Errors:**
- `404` — User or organization not found
- `400` — Organization not in user's tenant
- `409` — Already a member

---

#### POST `/api/admin/users/{id}/memberships/{membershipId}/set-primary`

Set a membership as the user's primary organization.

**Response:** `204 No Content`

---

#### DELETE `/api/admin/users/{id}/memberships/{membershipId}`

Remove a membership. Safety rules prevent removing the last membership or the primary membership.

**Response:** `204 No Content`

**Errors:**
- `404` — Membership not found
- `409` — Cannot remove last membership or primary membership

---

### Admin: Permissions Catalog

#### GET `/api/admin/permissions`

List all active permissions (capabilities) in the platform.

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `productId` | `guid` | No | | Filter by product |
| `search` | `string` | No | `""` | Search by code, name, or description |

**Response:** `200 OK`

| Field | Type | Description |
|---|---|---|
| `items` | `array` | Permissions with `id`, `code`, `name`, `description`, `category`, `productId`, `productCode`, `productName`, `isActive` |
| `totalCount` | `integer` | Total count |

---

#### GET `/api/admin/permissions/by-product/{productCode}`

List permissions for a specific product.

**Auth:** PlatformAdmin or TenantAdmin

**Response:** `200 OK` — Same format as `GET /api/admin/permissions`

---

#### POST `/api/admin/permissions`

Create a new permission.

**Auth:** PlatformAdmin only

**Request Body: `CreatePermissionRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `code` | `string` | Yes | Permission code (format: `PRODUCT.resource:action`) |
| `name` | `string` | Yes | Display name |
| `description` | `string` | No | Description |
| `category` | `string` | No | Category for grouping |
| `productCode` | `string` | No | Product code (provide this or `productId`) |
| `productId` | `guid` | No | Product ID (provide this or `productCode`) |

**Response:** `201 Created` — Permission details

---

#### PATCH `/api/admin/permissions/{id}`

Update a permission's name, description, or category.

**Auth:** PlatformAdmin only

**Request Body: `UpdatePermissionRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `name` | `string` | Yes | Display name |
| `description` | `string` | No | Description |
| `category` | `string` | No | Category |

**Response:** `200 OK` — Updated permission

---

#### DELETE `/api/admin/permissions/{id}`

Deactivate a permission.

**Auth:** PlatformAdmin only

**Response:** `204 No Content`

---

### Admin: Role Permissions

#### GET `/api/admin/roles/{id}/permissions`

List all permissions assigned to a role.

**Auth:** PlatformAdmin or TenantAdmin (with tenant boundary check for non-system roles)

**Response:** `200 OK`

| Field | Type | Description |
|---|---|---|
| `items` | `array` | Permissions with assignment metadata (`assignedAtUtc`, `assignedByUserId`) |
| `totalCount` | `integer` | Total count |

---

#### POST `/api/admin/roles/{id}/permissions`

Assign a permission to a role. Idempotent (returns 200 if already assigned).

**Auth:** PlatformAdmin only (system roles require PlatformAdmin)

**Request Body: `AssignRolePermissionRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `permissionId` | `guid` | Yes | Permission to assign |

**Response:** `201 Created` or `200 OK` (if already assigned)

---

#### DELETE `/api/admin/roles/{id}/permissions/{permissionId}`

Revoke a permission from a role.

**Auth:** PlatformAdmin only

**Response:** `204 No Content`

---

### Admin: User Effective Permissions

#### GET `/api/admin/users/{id}/permissions`

Get the effective (union) permissions for a user, derived from all active role assignments. Each permission includes which role(s) grant it.

**Auth:** PlatformAdmin or TenantAdmin (with tenant boundary check)

**Response:** `200 OK`

| Field | Type | Description |
|---|---|---|
| `items` | `array` | Permissions with `sources` array showing granting roles |
| `totalCount` | `integer` | Total distinct permissions |
| `roleCount` | `integer` | Number of active role assignments |

---

### Admin: Access Debug

#### GET `/api/admin/users/{id}/access-debug`

Comprehensive authorization debug view for a user showing all access sources.

**Auth:** PlatformAdmin or TenantAdmin

**Response:** `200 OK`

| Field | Type | Description |
|---|---|---|
| `userId` | `guid` | User ID |
| `tenantId` | `guid` | Tenant ID |
| `accessVersion` | `integer` | Access version |
| `products` | `array` | Product access sources (direct, group) |
| `roles` | `array` | Role sources (direct, group) |
| `systemRoles` | `array` | System-level scoped roles |
| `groups` | `array` | Group memberships |
| `entitlements` | `array` | Tenant-level entitlements |
| `productRolesFlat` | `string[]` | Flattened product role codes |
| `tenantRoles` | `string[]` | Tenant-level role codes |
| `permissions` | `string[]` | Effective permission codes |
| `permissionSources` | `array` | Permission source details |
| `policies` | `array` | ABAC policies linked to effective permissions |

---

### Admin: ABAC Policies

#### GET `/api/admin/policies`

List ABAC policies.

**Auth:** PlatformAdmin only

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `productCode` | `string` | No | `""` | Filter by product |
| `search` | `string` | No | `""` | Search by policy code or name |
| `activeOnly` | `boolean` | No | `true` | Show only active policies |

**Response:** `200 OK`

---

#### GET `/api/admin/policies/{id}`

Get a policy with its rules and permission mappings.

**Auth:** PlatformAdmin only

**Response:** `200 OK` — Detailed policy with rules and permission mappings

---

#### POST `/api/admin/policies`

Create a new ABAC policy.

**Auth:** PlatformAdmin only

**Request Body: `CreatePolicyRequest`**

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `policyCode` | `string` | Yes | | Unique policy code |
| `name` | `string` | Yes | | Display name |
| `productCode` | `string` | Yes | | Associated product code |
| `description` | `string` | No | `null` | Description |
| `priority` | `integer` | No | `0` | Evaluation priority |
| `effect` | `string` | No | `Allow` | Policy effect (`Allow` or `Deny`) |

**Response:** `201 Created` — Policy details

---

#### PATCH `/api/admin/policies/{id}`

Update a policy.

**Auth:** PlatformAdmin only

**Request Body: `UpdatePolicyRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `name` | `string` | Yes | Display name |
| `description` | `string` | No | Description |
| `priority` | `integer` | Yes | Evaluation priority |
| `effect` | `string` | No | Policy effect (`Allow` or `Deny`) |

**Response:** `200 OK` — Updated policy

---

#### DELETE `/api/admin/policies/{id}`

Deactivate a policy.

**Auth:** PlatformAdmin only

**Response:** `204 No Content`

---

### Admin: Policy Rules

#### GET `/api/admin/policies/{policyId}/rules`

List rules for a policy.

**Auth:** PlatformAdmin only

**Response:** `200 OK`

| Field | Type | Description |
|---|---|---|
| `policyId` | `guid` | Policy ID |
| `policyCode` | `string` | Policy code |
| `rules` | `array` | Array of rule objects |

Rule object fields:

| Field | Type | Description |
|---|---|---|
| `id` | `guid` | Rule ID |
| `conditionType` | `string` | Condition type (enum) |
| `field` | `string` | Field being evaluated |
| `op` | `string` | Operator (enum) |
| `value` | `string` | Comparison value |
| `logicalGroup` | `string` | Logical group (`And` or `Or`) |
| `createdAtUtc` | `datetime` | Creation timestamp |

---

#### POST `/api/admin/policies/{policyId}/rules`

Create a rule for a policy.

**Auth:** PlatformAdmin only

**Request Body: `CreatePolicyRuleRequest`**

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `conditionType` | `string` | Yes | | Condition type |
| `field` | `string` | Yes | | Field to evaluate (must be a supported field) |
| `operator` | `string` | Yes | | Comparison operator |
| `value` | `string` | Yes | | Comparison value |
| `logicalGroup` | `string` | No | `And` | Logical group (`And` or `Or`) |

**Response:** `201 Created` — Rule details

---

#### PATCH `/api/admin/policies/{policyId}/rules/{ruleId}`

Update a policy rule.

**Auth:** PlatformAdmin only

**Request Body: `UpdatePolicyRuleRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `conditionType` | `string` | Yes | Condition type |
| `field` | `string` | Yes | Field to evaluate |
| `operator` | `string` | Yes | Comparison operator |
| `value` | `string` | Yes | Comparison value |
| `logicalGroup` | `string` | Yes | Logical group |

**Response:** `200 OK` — Updated rule

---

#### DELETE `/api/admin/policies/{policyId}/rules/{ruleId}`

Delete a policy rule.

**Auth:** PlatformAdmin only

**Response:** `204 No Content`

---

#### GET `/api/admin/policies/supported-fields`

Get the supported fields, operators, condition types, logical groups, and effects for the ABAC condition builder.

**Auth:** PlatformAdmin only

**Response:** `200 OK`

| Field | Type | Description |
|---|---|---|
| `fields` | `string[]` | Supported field names |
| `operators` | `string[]` | Supported operators |
| `conditionTypes` | `string[]` | Supported condition types |
| `logicalGroups` | `string[]` | Supported logical groups |
| `effects` | `string[]` | Supported policy effects |

---

### Admin: Permission–Policy Mappings

#### GET `/api/admin/permission-policies`

List permission-to-policy mappings.

**Auth:** PlatformAdmin only

**Query Parameters:**

| Parameter | Type | Required | Description |
|---|---|---|---|
| `permissionCode` | `string` | No | Filter by permission code |
| `policyId` | `guid` | No | Filter by policy ID |

**Response:** `200 OK`

---

#### POST `/api/admin/permission-policies`

Create a permission-to-policy mapping. Reactivates if a deactivated mapping exists.

**Auth:** PlatformAdmin only

**Request Body: `CreatePermissionPolicyRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `permissionCode` | `string` | Yes | Permission code |
| `policyId` | `guid` | Yes | Policy ID |

**Response:** `201 Created` — Mapping details

**Errors:**
- `400` — Policy or permission not found
- `409` — Mapping already exists and is active

---

#### DELETE `/api/admin/permission-policies/{id}`

Deactivate a permission-policy mapping.

**Auth:** PlatformAdmin only

**Response:** `204 No Content`

---

### Admin: Authorization Simulation

#### POST `/api/admin/authorization/simulate`

Simulate an authorization check for a user with optional draft policies.

**Auth:** PlatformAdmin or TenantAdmin

**Request Body: `SimulateAuthorizationRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `tenantId` | `guid` | Yes | Tenant ID |
| `userId` | `guid` | Yes | User to simulate for |
| `permissionCode` | `string` | Yes | Permission to check (format: `PRODUCT.resource:action`) |
| `resourceContext` | `object` | No | Resource attributes for ABAC evaluation |
| `requestContext` | `object` | No | Request attributes for ABAC evaluation |
| `draftPolicy` | `object` | No | Draft policy to include in simulation |
| `excludePolicyIds` | `guid[]` | No | Policies to exclude from simulation |

**Draft Policy object:**

| Field | Type | Required | Description |
|---|---|---|---|
| `policyCode` | `string` | Yes | Policy code |
| `name` | `string` | Yes | Policy name |
| `description` | `string` | No | Description |
| `priority` | `integer` | No | Priority |
| `effect` | `string` | No | Effect (`Allow` or `Deny`) |
| `rules` | `array` | No | Draft rules with `field`, `operator`, `value`, `logicalGroup` |

**Response:** `200 OK` — Simulation result with `allowed` boolean and evaluation details

---

### Admin: Audit Logs

#### GET `/api/admin/audit`

List audit log entries.

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `page` | `integer` | No | `1` | Page number |
| `pageSize` | `integer` | No | `20` | Items per page |
| `search` | `string` | No | `""` | Search in action, entity ID, or actor name |
| `entityType` | `string` | No | `""` | Filter by entity type |
| `actorType` | `string` | No | `""` | Filter by actor type |

**Response:** `200 OK` — `PaginatedResult` with audit log entries

---

### Admin: Platform Settings

#### GET `/api/admin/settings`

List all platform settings (static seed, no DB table).

**Response:** `200 OK` — `PaginatedResult` with setting objects

| Field | Type | Description |
|---|---|---|
| `key` | `string` | Setting key |
| `label` | `string` | Display label |
| `value` | `any` | Current value |
| `type` | `string` | Value type (`boolean`, `number`, `string`) |
| `description` | `string` | Setting description |
| `editable` | `boolean` | Whether the setting can be modified |

---

#### PUT `/api/admin/settings/{key}`

Update a platform setting.

**Request Body: `SettingUpdateRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `value` | `any` | Yes | New setting value |

**Response:** `200 OK` — Updated setting

---

### Admin: Support Cases

Support case endpoints are stubs (not yet persisted).

#### GET `/api/admin/support`

List support cases (returns empty list).

**Response:** `200 OK` — Empty paginated result

---

#### GET `/api/admin/support/{id}`

Get a support case (always returns 404).

**Response:** `404 Not Found`

---

#### POST `/api/admin/support`

Create a support case (stub).

**Request Body: `CreateSupportRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `title` | `string` | Yes | Case title |
| `priority` | `string` | No | Priority (default: `Medium`) |
| `category` | `string` | No | Category (default: `General`) |

**Response:** `201 Created` — Stub case object

---

#### POST `/api/admin/support/{id}/notes`

Add a note to a support case (stub).

**Request Body: `SupportNoteRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `message` | `string` | Yes | Note message |

**Response:** `200 OK` — Stub note object

---

#### PATCH `/api/admin/support/{id}/status`

Update support case status (stub).

**Request Body: `SupportStatusRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `status` | `string` | Yes | New status |

**Response:** `200 OK` — Stub status update

---

### Admin: Legacy Coverage

#### GET `/api/admin/legacy-coverage`

Get a point-in-time snapshot of Phase F/G migration coverage including eligibility rule coverage and scoped role assignment adoption.

**Response:** `200 OK`

| Field | Type | Description |
|---|---|---|
| `generatedAtUtc` | `datetime` | Snapshot timestamp |
| `eligibilityRules` | `object` | OrgTypeRule coverage statistics |
| `roleAssignments` | `object` | ScopedRoleAssignment adoption statistics |

---

### Admin: Platform Readiness

#### GET `/api/admin/platform-readiness`

Get a cross-domain readiness summary covering Phase G completion, org-type consistency, product-role eligibility coverage, and scoped assignment breakdown.

**Response:** `200 OK`

| Field | Type | Description |
|---|---|---|
| `generatedAtUtc` | `datetime` | Snapshot timestamp |
| `phaseGCompletion` | `object` | Phase G migration status |
| `orgTypeCoverage` | `object` | Organization type consistency |
| `productRoleEligibility` | `object` | Product role OrgTypeRule coverage |
| `orgRelationships` | `object` | Organization relationship counts |
| `scopedAssignmentsByScope` | `object` | Active SRAs by scope type (global, organization, product, relationship, tenant) |

---

### Admin: CareConnect Readiness & Provisioning

#### GET `/api/admin/users/{id}/careconnect-readiness`

Check whether a user meets the four conditions required for CareConnect referral reception.

**Response:** `200 OK`

| Field | Type | Description |
|---|---|---|
| `userId` | `guid` | User ID |
| `hasPrimaryOrg` | `boolean` | Has active primary org membership |
| `primaryOrgId` | `guid` | Primary org ID (null if none) |
| `primaryOrgType` | `string` | Primary org type (null if none) |
| `tenantHasCareConnect` | `boolean` | Tenant has SYNQ_CARECONNECT enabled |
| `orgHasCareConnect` | `boolean` | Org has SYNQ_CARECONNECT enabled |
| `hasCareConnectRole` | `boolean` | User has RECEIVER or REFERRER role |
| `isFullyProvisioned` | `boolean` | All four conditions met |

---

#### POST `/api/admin/users/{id}/provision-careconnect`

Idempotently provision a user for CareConnect. Ensures tenant product, org product, and receiver role are in place.

**Response:** `200 OK`

| Field | Type | Description |
|---|---|---|
| `userId` | `guid` | User ID |
| `organizationId` | `guid` | Organization ID |
| `organizationName` | `string` | Organization name |
| `tenantProductAdded` | `boolean` | Whether tenant product was newly created |
| `orgProductAdded` | `boolean` | Whether org product was newly created |
| `roleAdded` | `boolean` | Whether role was newly assigned |
| `isFullyProvisioned` | `boolean` | `true` |

**Errors:**
- `422` — No primary org membership or org not found
