# LS-NOTIF-CORE-008 — Notifications Access Model & Authorization Hardening

> **Status: COMPLETE** — build verified: `Build succeeded. 3 Warning(s), 0 Error(s)` (all warnings pre-existing).

---

## Summary

Adds JWT-based authentication and authorization enforcement to the Notifications microservice. Extends the existing header-based tenant isolation with identity-claim-based tenant binding for all user-facing endpoints; adds a new `/v1/admin/notifications/*` route group for platform-admin cross-tenant operations; enhances retry/resend audit trails with actor userId; preserves full backward compatibility with internal service callers (Comms, Liens, Reports).

---

## Authorization Model Implemented

### JWT Claim Structure (as emitted by `Identity.Infrastructure.Services.JwtTokenService`)

| Claim | Type | Description |
|---|---|---|
| `sub` | string | User ID (UUID) |
| `tenant_id` | string | Tenant ID (UUID) |
| `tenant_code` | string | Tenant short code |
| `ClaimTypes.Role` | string (multi) | `PlatformAdmin`, `TenantAdmin`, `StandardUser` |
| `email` | string | User email |
| `session_version` | string | For session invalidation (not read here) |

**`isPlatformAdmin`** is NOT a separate boolean claim — it is derived from the presence of `role == "PlatformAdmin"`.

### Policy Configuration

Policies are registered in `Program.cs` matching the platform-standard `BuildingBlocks.Authorization.Policies` constants:

| Policy | Requirement |
|---|---|
| `AuthenticatedUser` | Valid JWT; any role |
| `AdminOnly` | Valid JWT; role == `PlatformAdmin` |

### Tenant Binding Rules

| Endpoint group | Auth required | Tenant source |
|---|---|---|
| POST `/v1/notifications` | None (backward compat) | `X-Tenant-Id` header |
| GET/operational `/v1/notifications/*` | `AuthenticatedUser` | `tenant_id` JWT claim |
| GET/operational `/v1/admin/notifications/*` | `AdminOnly` | optional `tenantId` query param (null = all tenants) |

---

## Endpoints Secured

### Tenant endpoints (now require `AuthenticatedUser` JWT policy)
- `GET /v1/notifications` (paged list + legacy list)
- `GET /v1/notifications/stats`
- `GET /v1/notifications/{id}`
- `GET /v1/notifications/{id}/events`
- `GET /v1/notifications/{id}/issues`
- `POST /v1/notifications/{id}/retry`
- `POST /v1/notifications/{id}/resend`

### Exempted (backward-compat)
- `POST /v1/notifications` — `.AllowAnonymous()` — used by internal services (Comms, Liens, Reports) which call with `X-Tenant-Id` header + no JWT

---

## Admin Endpoints Added

Route group: `/v1/admin/notifications`, policy: `AdminOnly` (PlatformAdmin role required)

| Method | Path | Description |
|---|---|---|
| GET | `/v1/admin/notifications` | Cross-tenant paged list; optional `tenantId` filter |
| GET | `/v1/admin/notifications/stats` | Cross-tenant stats; optional `tenantId` filter |
| GET | `/v1/admin/notifications/{id}/events` | Event timeline by notification ID (no tenant filter) |
| GET | `/v1/admin/notifications/{id}/issues` | Delivery issues by notification ID (no tenant filter) |
| POST | `/v1/admin/notifications/{id}/retry` | Retry any notification (cross-tenant) |
| POST | `/v1/admin/notifications/{id}/resend` | Resend any notification (cross-tenant) |

All admin endpoints:
- Reject non-PlatformAdmin roles with 403
- Accept optional `tenantId` query param for scoped filtering
- Omit `tenantId` to query across all tenants
- Log all queries and actions via structured logger + AuditClient

---

## Files Changed

### New files
- `Notifications.Api/Authorization/UserContext.cs` — `UserContext` record + `HttpContextAuthExtensions` for claim extraction
- `Notifications.Api/Endpoints/AdminNotificationEndpoints.cs` — 6 admin endpoints

### Modified files
- `Notifications.Api/appsettings.json` — added `Jwt` section (issuer, audience, placeholder signing key)
- `Notifications.Api/appsettings.Development.json` — added `Jwt` section (dev signing key matches identity service)
- `Notifications.Api/Program.cs` — added JWT Bearer auth, authorization policies, `UseAuthentication`, `UseAuthorization`, admin endpoint registration
- `Notifications.Api/Middleware/TenantMiddleware.cs` — extended to derive `TenantId` from JWT `tenant_id` claim for authenticated requests; header fallback preserved for unauthenticated paths
- `Notifications.Api/Endpoints/NotificationEndpoints.cs` — added `.RequireAuthorization(Policies.AuthenticatedUser)` to all GET + operational POST endpoints; POST `/` marked `.AllowAnonymous()`; tenant from claims for secured endpoints
- `Notifications.Application/Interfaces/INotificationService.cs` — updated `RetryAsync`/`ResendAsync` signatures with optional `actorUserId`; added 6 admin method signatures
- `Notifications.Application/Interfaces/INotificationRepository.cs` — added `GetPagedAdminAsync` and `GetStatsAdminAsync` (nullable tenantId)
- `Notifications.Infrastructure/Repositories/NotificationRepository.cs` — implemented `GetPagedAdminAsync`, `GetStatsAdminAsync`
- `Notifications.Infrastructure/Services/NotificationService.cs` — updated `RetryAsync`/`ResendAsync` to embed `actorUserId` in audit; implemented 6 admin methods

---

## Audit Implementation

### Existing audit (enhanced)
- **`notification.retry`** — now includes `actorUserId` in audit description and `SubjectId` scope
- **`notification.resend`** — now includes `actorUserId` in audit description and `SubjectId` scope

### New admin audit events
| Action | Event type | Logged when |
|---|---|---|
| Admin list query | `admin.notification.list` | Admin calls GET /v1/admin/notifications |
| Admin stats query | `admin.notification.stats` | Admin calls GET /v1/admin/notifications/stats |
| Admin event lookup | `admin.notification.events` | Admin calls GET /{id}/events |
| Admin issue lookup | `admin.notification.issues` | Admin calls GET /{id}/issues |
| Admin retry | `admin.notification.retry` | Admin calls POST /{id}/retry |
| Admin resend | `admin.notification.resend` | Admin calls POST /{id}/resend |

All admin audit records include: `userId`, `tenantId filter used`, `target notificationId`, action, timestamp, outcome. Audit is best-effort (failures do not break the operation).

### Unauthorized access logging
Unauthorized requests (missing/invalid JWT, wrong role) are rejected by ASP.NET Core's `UseAuthentication`/`UseAuthorization` pipeline before reaching handlers. These produce 401/403 responses. The `TenantMiddleware` logs a structured `WARNING` for any request where tenantId cannot be resolved after authentication.

---

## Validation Performed

- Build: `Build succeeded. 3 Warning(s), 0 Error(s)` — all 3 warnings are pre-existing (MailKit advisory, JwtBearer version conflict from shared BuildingBlocks)
- Authorization behavior verified by code inspection:
  - `RequireAuthorization(Policies.AuthenticatedUser)` on all GET + operational endpoints
  - `RequireAuthorization(Policies.AdminOnly)` on all admin endpoints
  - POST `/v1/notifications` carries `.AllowAnonymous()` — passes through TenantMiddleware header path
  - Admin endpoints accept `null` tenantId → queries all tenants without WHERE clause on TenantId

---

## Remaining Gaps

1. **No session version validation** — `session_version` and `access_version` claims are not checked against the DB. Other services (identity) have session invalidation middleware; notifications does not inherit this. Low risk for read/query endpoints; higher risk for retry/resend.
2. **POST /v1/notifications bypasses JWT** — internal callers (Comms, Liens, Reports) use `X-Tenant-Id` header + no JWT. These should eventually migrate to a service-account token pattern but this is out of scope for this task.
3. **No rate limiting on admin endpoints** — cross-tenant queries (no tenantId filter) could be expensive. Page size is capped at 200 records but no deeper rate limiting is applied.
4. **Admin retry race condition** — same as tenant retry: no distributed lock; concurrent retry calls for the same notification are possible.
5. **Audit client availability** — audit events are best-effort; failure is silently swallowed. No dead-letter queue or fallback.

---

## Risks / Follow-Up Recommendations

1. **Migrate internal notification callers to service token auth** — Comms/Liens/Reports should move to a proper service-to-service credential so POST can eventually be secured.
2. **Add `session_version` invalidation check** — integrate with the same session-version middleware pattern used in Identity service.
3. **Add `X-Forwarded-User` or claim forwarding at gateway** — currently the notifications service validates the JWT itself. If the gateway pre-validates and strips the token, update to trust forwarded identity headers instead.
4. **Paging defaults for admin cross-tenant** — add stricter max page size or time-bound defaults for cross-tenant queries to avoid performance issues at scale.
