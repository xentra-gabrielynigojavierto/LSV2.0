# LS-NOTIF-CORE-021 — Service-to-Service Authentication & Submission Security Closure

**Status**: Complete  
**Date**: 2026-04-19  
**Engineer**: Agent

---

## Summary

Replaces `AllowAnonymous + X-Tenant-Id` on `POST /v1/notifications` with a proper
JWT-based service-identity authentication model.  A transitional backward-compatibility
layer is preserved so existing producers (Liens, Comms, Flow) continue working while
they complete migration to service-token issuance.

---

## Authentication Model Implemented

### Two-layer scheme on Notifications.Api

| Scheme | Name | Validates |
|--------|------|-----------|
| Primary user JWT | `Bearer` (JwtBearer default) | Operator / user tokens from Identity |
| Service JWT | `ServiceToken` (JwtBearer) | HS256 tokens minted by `ServiceTokenIssuer` using `FLOW_SERVICE_TOKEN_SECRET` |

Both schemes are registered on `POST /v1/notifications` via the `ServiceSubmission`
policy's `AddAuthenticationSchemes` call.  Either scheme succeeding marks the caller
as authenticated.

### Claims extracted for service callers

| Claim | JWT field | Purpose |
|-------|-----------|---------|
| Subject / identity | `sub` | Must start with `service:` for service tokens |
| Service name | `svc` | Identifies the originating microservice |
| Tenant | `tenant_id` | Used by `TenantMiddleware` for tenant scoping |
| Role | `ClaimTypes.Role` | Must include `"service"` for service tokens |

`UserContext` now exposes a `ServiceName` property populated from the `svc` claim.

### Token signing

Both schemes share the same HMAC-SHA256 secret via `FLOW_SERVICE_TOKEN_SECRET`
(env var) or `ServiceTokens:SigningKey` (config).  Notifications accepts tokens
with audience `notifications-service` or `flow-service` (for Flow's existing issuer).

---

## Endpoint Security Changes

### `POST /v1/notifications`

**Before**: `.AllowAnonymous()` — any caller, no identity enforcement.

**After**: `.RequireAuthorization(Policies.ServiceSubmission)`

The `ServiceSubmission` policy uses a custom `ServiceSubmissionRequirement` /
`ServiceSubmissionHandler`:

1. **Authenticated** (user or service JWT present and valid) → **Succeed**.  
   Service identity logged at Debug level.

2. **Legacy unauthenticated** (no JWT, but valid `X-Tenant-Id` header) → **Succeed**
   with a `[LEGACY SUBMISSION]` WARNING log.  Tenant derived from header as before.

3. **Rejected** (no JWT, no valid header) → not succeeded → framework returns **401**.

### Tenant derivation (unchanged behaviour)

`TenantMiddleware` continues to:
- Authenticated request → `tenant_id` JWT claim (ignores header, prevents spoofing).
- Unauthenticated legacy request → `X-Tenant-Id` header (now logs LEGACY WARNING).

---

## Producer Migration

| Producer | Auth status | Notes |
|----------|-------------|-------|
| Liens | `NotificationsAuthDelegatingHandler` added | Mints service token when `FLOW_SERVICE_TOKEN_SECRET` configured; `X-Tenant-Id` still sent as fallback |
| Comms | Same | Same |
| Flow | Same + `X-Tenant-Id` header added to `HttpNotificationAdapter` | Flow already has `IServiceTokenIssuer` registered; reuses it |
| Reports | No change | `HttpEmailReportDeliveryAdapter` is a stub; `INotificationAdapter` is `MockNotificationAdapter` — no real HTTP calls |

### `NotificationsAuthDelegatingHandler` (BuildingBlocks)

Registered on the `"NotificationsService"` named HTTP client in Liens and Comms,
and on the typed `HttpNotificationAdapter` client in Flow.

Behaviour:
1. Reads `X-Tenant-Id` from the outgoing request header.
2. If `IServiceTokenIssuer.IsConfigured` → mints a service JWT and injects
   `Authorization: Bearer <token>`.  The Notifications server then uses the JWT
   `tenant_id` claim for tenant resolution.
3. If issuer not configured (dev without secret) → passes through; Notifications
   falls back to the `X-Tenant-Id` header and logs LEGACY WARNING.

---

## Files Changed

### New files
- `shared/building-blocks/BuildingBlocks/Notifications/NotificationsAuthDelegatingHandler.cs`
- `apps/services/notifications/Notifications.Api/Authorization/ServiceSubmissionRequirement.cs`

### Modified files
- `shared/building-blocks/BuildingBlocks/Authorization/Policies.cs` — added `ServiceSubmission`
- `apps/services/notifications/Notifications.Api/Authorization/UserContext.cs` — added `ServiceName` + `svc` claim extraction
- `apps/services/notifications/Notifications.Api/Middleware/TenantMiddleware.cs` — LEGACY WARNING log added
- `apps/services/notifications/Notifications.Api/Endpoints/NotificationEndpoints.cs` — replaced `AllowAnonymous` with `RequireAuthorization(Policies.ServiceSubmission)`
- `apps/services/notifications/Notifications.Api/Program.cs` — ServiceToken JWT scheme + ServiceSubmission policy registered
- `apps/services/liens/Liens.Infrastructure/DependencyInjection.cs` — `AddServiceTokenIssuer` + handler
- `apps/services/comms/Comms.Infrastructure/DependencyInjection.cs` — same
- `apps/services/flow/backend/src/Flow.Infrastructure/Adapters/HttpNotificationAdapter.cs` — `X-Tenant-Id` header added to outgoing request
- `apps/services/flow/backend/src/Flow.Infrastructure/Adapters/PlatformAdapterRegistration.cs` — handler added to notification client

---

## Validation Performed

| Check | Result |
|-------|--------|
| `dotnet build Notifications.Api` | ✅ 0 errors |
| `dotnet build BuildingBlocks` | ✅ 0 errors |
| `dotnet build Liens.Infrastructure` | ✅ 0 errors |
| `dotnet build Comms.Infrastructure` | ✅ 0 errors |
| `dotnet build Flow.Infrastructure` | ✅ 0 errors |
| Endpoint no longer carries `AllowAnonymous` | ✅ confirmed |
| Missing token + missing header → 401 | ✅ (ServiceSubmissionHandler does not call Succeed) |
| Valid service JWT → Succeed, tenant from claim | ✅ (TenantMiddleware JWT path) |
| Legacy header only → Succeed + LEGACY log | ✅ (ServiceSubmissionHandler legacy branch) |

---

## Remaining Gaps

1. **Identity service** — uses `POST /internal/send-email` with `X-Internal-Service-Token`;
   out of scope for this task.  Migration path: issue a service JWT in Identity and
   switch to the `POST /v1/notifications` endpoint.

2. **CareConnect** — maintains its own SMTP stack; does not call Notifications.

3. **Reports `INotificationAdapter`** — currently `MockNotificationAdapter`; real HTTP
   integration not yet wired.  When activated it should use `NotificationsAuthDelegatingHandler`.

4. **Service token audience per-producer** — Liens and Comms default to `flow-service`
   audience unless `ServiceTokens:Audience = notifications-service` is set in config.
   Notifications accepts both audiences.  Recommend tightening to `notifications-service`
   only once all producers are confirmed migrated.

5. **Token rotation / secret management** — `FLOW_SERVICE_TOKEN_SECRET` is a single
   shared HMAC key.  Long-term: rotate to per-service asymmetric keys.

---

## Risks / Follow-Up Recommendations

| Risk | Severity | Recommendation |
|------|----------|----------------|
| LEGACY SUBMISSION warnings reveal missing producers | Low | Monitor logs after deploy; treat each warning as a migration ticket |
| Shared HMAC secret means any service can impersonate any other | Medium | Rotate to per-service RS256 keys in a follow-up hardening sprint |
| `AllowAnonymous` removal breaks undiscovered internal callers | Medium | Run with LEGACY path active for one sprint, then flip to hard-reject |
| `FLOW_SERVICE_TOKEN_SECRET` not set in new envs → issuer unconfigured | Low | Health probe / startup check on issuer configuration |
