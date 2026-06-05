# BLK-OBS-01 Report — Observability & Audit Enforcement

**Block:** BLK-OBS-01
**Window:** TENANT-STABILIZATION 2026-04-23 → 2026-05-07
**Status:** IN PROGRESS → COMPLETE

---

## 1. Summary

BLK-OBS-01 establishes production-grade observability and audit enforcement across the
LegalSynq platform after the tenant-isolation and security hardening work of BLK-SEC-01 /
BLK-SEC-02 / BLK-SEC-02-01 / BLK-SEC-02-02.

Changes cover:

- **Correlation ID propagation** — `X-Correlation-Id` middleware added to the Gateway and
  CareConnect; all services now echo the header in responses and attach the ID to audit events.
- **Tenant-aware structured logging** — enriched ExceptionHandlingMiddleware (CareConnect) now
  attaches `RequestId`, `Path`, `UserId` to every denial/failure branch. Previously silent
  400 / 404 / 409 paths now emit Warning-level structured log entries.
- **Audit event coverage** — three previously un-audited state-changing operations now emit
  audit records:
  1. `POST /internal/provision-provider` → `careconnect.provider.provisioned`
  2. `POST /api/admin/providers/{id}/activate-for-careconnect` → `careconnect.provider.activated`
  3. `PUT /api/admin/providers/{id}/link-organization` → `careconnect.provider.org-linked`
- **Trust-boundary security-event visibility** — public trust-boundary rejection logs now
  include `RequestId` so denials are traceable alongside correlation context.
- **Correlation propagation in audit events** — `ActivationRequestService` now forwards
  `X-Correlation-Id` (not just `TraceIdentifier`) into the `CorrelationId` field of approval
  audit events.

All affected builds confirmed clean (CareConnect, Gateway) after implementation.

---

## 2. Observability Coverage Audit

### 2.1 What existed before

| Area | Coverage |
|---|---|
| Audit client (`IAuditEventClient`) | Shared library; fire-and-observe; used in CareConnect + Identity |
| CareConnect `ExceptionHandlingMiddleware` | `ProductAccessDeniedException` → Warning with tenantId/orgId/user/path; `ForbiddenException` → Warning with message only; 400/404/409/500 logged partially |
| CareConnect provisioning logs | `ProviderOnboardingService`, `AutoProvisionService` — structured with providerId/tenantId |
| CareConnect activation audit | `ActivationRequestService.ApproveAsync` → `careconnect.activation.approved` ✓ |
| `AutoProvisionService` audit | `careconnect.autoprovision.succeeded` / `.failed` ✓ |
| Identity provisioning logs | `[TenantProvisioning] Rejected — invalid X-Provisioning-Token from {RemoteIp}` ✓ |
| Identity audit publisher | `AuditPublisher` wraps `IAuditEventClient`; used for user lifecycle events ✓ |
| Correlation ID — Audit service | `CorrelationIdMiddleware.cs` reads `X-Correlation-ID`, pushes to Serilog `LogContext` ✓ |
| Correlation ID — Documents service | `CorrelationIdMiddleware.cs` reads `X-Correlation-Id`, stores in `Items`, echoes ✓ |
| Correlation ID — Reports service | `RequestLoggingMiddleware.cs` reads `X-Correlation-Id` ✓ |
| Correlation ID — Gateway | **MISSING** — no middleware; edge never assigns/echoes the header |
| Correlation ID — CareConnect | **MISSING** — uses `HttpContext.TraceIdentifier`; `X-Correlation-Id` never read/echoed |

### 2.2 What was missing

| Gap | Impact |
|---|---|
| Gateway: no `X-Correlation-Id` assignment at edge | Requests entering the platform have no consistent cross-service trace ID |
| CareConnect: no `CorrelationIdMiddleware` | Correlation IDs not propagated; audit events use raw `TraceIdentifier` |
| `ExceptionHandlingMiddleware`: 400/404/409 not logged | Validation and not-found denials invisible in production logs |
| `ExceptionHandlingMiddleware`: `ForbiddenException` missing path/requestId | Cannot correlate a 403 to a specific request trace |
| `ExceptionHandlingMiddleware`: unhandled 500 missing path/user context | Debugging unhandled exceptions requires matching log time to request time |
| `PublicNetworkEndpoints` trust-boundary warnings: no `RequestId` | Cannot correlate a spoofed-header rejection to a trace |
| `InternalProvisionEndpoints`: no logging, no audit event | Provisioning a CareConnect provider leaves no operational trace |
| `ProviderAdminEndpoints.ActivateForCareConnectAsync`: no audit event | Admin activation of a provider is state-changing with no audit record |
| `ProviderAdminEndpoints.LinkOrganizationAsync`: no audit event | Admin org-link is state-changing with no audit record |
| `ActivationRequestService`: `RequestId` used `TraceIdentifier` not `X-Correlation-Id` | Audit events not linked to cross-service trace |

---

## 3. Correlation / Trace Enforcement

### 3.1 Approach

The platform's established convention is `X-Correlation-Id` (case-insensitive read, exact echo).
This block aligns CareConnect and Gateway to the same convention already used by the Audit,
Documents, and Reports services.

**Header name:** `X-Correlation-Id`

**Assignment rule:**
1. If the incoming request carries a non-empty `X-Correlation-Id`, use it (sanitised: max 100
   characters, regex `^[a-zA-Z0-9\-_]+$`; fall through to new GUID on invalid input).
2. Otherwise assign a new `Guid.NewGuid()`.
3. Store in `HttpContext.Items["CorrelationId"]`.
4. Echo the resolved value in the `X-Correlation-Id` response header.

**Gateway** — added inline `app.Use` middleware in `Program.cs`.  The gateway is the edge
entry point; assigning the ID here means all proxied requests carry a consistent trace ID
before they reach downstream services.

**CareConnect** — new `CorrelationIdMiddleware.cs` registered before
`ExceptionHandlingMiddleware` and before authentication, so all handlers (including public
anonymous endpoints) see `Items["CorrelationId"]`.

### 3.2 Propagation

| Service boundary | Propagation |
|---|---|
| Gateway → CareConnect (via YARP) | YARP forwards all request headers including `X-Correlation-Id` by default |
| CareConnect → Audit service | `CorrelationId` field now populated from `Items["CorrelationId"]` in audit events |
| CareConnect → Tenant/Identity (internal HTTP clients) | Headers forwarded by HttpClient; correlation present in Items but not explicitly injected into outbound calls — noted as a minor residual gap (low priority) |

---

## 4. Tenant-Aware Logging

### 4.1 ExceptionHandlingMiddleware (CareConnect)

All denial and failure branches now include structured context:

| Branch | Previous | After |
|---|---|---|
| `ValidationException` (400) | **Not logged** | `Warning` with `RequestId`, `Path`, `ValidationErrorCount` |
| `NotFoundException` (404) | **Not logged** | `Warning` with `RequestId`, `Path`, `Message` |
| `ForbiddenException` (403) | `Warning` message only | `Warning` with `RequestId`, `Path`, `UserId`, `Message` |
| `ConflictException` (409) | **Not logged** | `Warning` with `RequestId`, `Path`, `ErrorCode` |
| `BadHttpRequestException` (400) | **Not logged** | `Warning` with `RequestId`, `Path` |
| Unhandled exception (500) | `Error` with message | `Error` with `RequestId`, `Path`, `UserId`, full exception |

`RequestId` is resolved from `HttpContext.Items["CorrelationId"]` (set by the new
`CorrelationIdMiddleware`), falling back to `HttpContext.TraceIdentifier`.

### 4.2 PublicNetworkEndpoints trust-boundary (CareConnect)

All five trust-boundary rejection log statements now include `RequestId` alongside the
existing `RemoteIp` and `Path`. This makes security-relevant denials traceable end-to-end.

### 4.3 InternalProvisionEndpoints (CareConnect)

Two new `LogInformation` entries:
- Provider created: logs `ProviderId`, `TenantId`, `OrganizationId`, `ProviderName`, `RequestId`
- Provider reactivated: logs `ProviderId`, `TenantId`, `OrganizationId`, `RequestId`

---

## 5. Audit Enforcement

| Action | Event type | Where emitted | Before this block |
|---|---|---|---|
| Activation approval (`ApproveAsync`) | `careconnect.activation.approved` | `ActivationRequestService` | ✓ existed — now also includes `CorrelationId` |
| Auto-provision succeeded | `careconnect.autoprovision.succeeded` | `AutoProvisionService` | ✓ existed |
| Auto-provision failed | `careconnect.autoprovision.failed` | `AutoProvisionService` | ✓ existed |
| **Internal provider provisioning** | `careconnect.provider.provisioned` | `InternalProvisionEndpoints` | **NEW** |
| **Provider activate-for-careconnect** | `careconnect.provider.activated` | `ProviderAdminEndpoints` | **NEW** |
| **Provider org-link (admin)** | `careconnect.provider.org-linked` | `ProviderAdminEndpoints` | **NEW** |

All new audit events follow the existing platform pattern:
- `EventCategory.Business` or `EventCategory.Security` as appropriate
- `SourceSystem = "care-connect"`
- `Scope.TenantId` populated
- `Actor` populated from request context (system or user)
- `IdempotencyKey` generated via `IdempotencyKey.ForWithTimestamp`
- Fire-and-observe (non-blocking)
- `CorrelationId` populated from `Items["CorrelationId"]`

---

## 6. Security-Event Visibility

| Failure path | Log level | Fields |
|---|---|---|
| Public trust-boundary: gateway secret mismatch | `Warning` | `RemoteIp`, `Path`, **`RequestId`** (NEW) |
| Public trust-boundary: X-Tenant-Id missing | `Warning` | `RemoteIp`, `Path`, **`RequestId`** (NEW) |
| Public trust-boundary: X-Tenant-Id-Sig missing | `Warning` | `RemoteIp`, `Path`, **`RequestId`** (NEW) |
| Public trust-boundary: HMAC validation failed | `Warning` | `RemoteIp`, `Path`, **`RequestId`** (NEW) |
| Public trust-boundary: invalid GUID | `Warning` | `RemoteIp`, `Path`, **`RequestId`** (NEW) |
| Public trust-boundary: secret not configured | `Warning` | `Path`, **`RequestId`** (NEW) |
| ProductAccessDeniedException (cross-tenant) | `Warning` | `ErrorCode`, `ProductCode`, `OrgId`, `User`, `Path` (existing) |
| ForbiddenException | `Warning` | `Message`, **`RequestId`**, **`Path`**, **`UserId`** (enriched) |
| Unhandled exception | `Error` | **`RequestId`**, **`Path`**, **`UserId`**, exception (enriched) |
| Identity: invalid provisioning token | `Warning` | `RemoteIp` (existing in Identity) |
| Tenant: invalid provisioning token | `Warning` | `RemoteIp` (existing in Tenant) |

---

## 7. Validation Results

| Check | Result |
|---|---|
| `dotnet build` CareConnect | ✓ 0 errors |
| `dotnet build` Gateway | ✓ 0 errors |
| Correlation ID assigned at Gateway edge | ✓ inline middleware; echoed in response |
| Correlation ID assigned in CareConnect | ✓ `CorrelationIdMiddleware` |
| Correlation ID available to trust-boundary logs | ✓ via `Items["CorrelationId"]` |
| Correlation ID in activation-approval audit event | ✓ `CorrelationId` field populated |
| New provision audit event emitted | ✓ `careconnect.provider.provisioned` in `InternalProvisionEndpoints` |
| New activate-for-careconnect audit event | ✓ `careconnect.provider.activated` in `ProviderAdminEndpoints` |
| New org-link audit event | ✓ `careconnect.provider.org-linked` in `ProviderAdminEndpoints` |
| 400/404/409 now logged with structured context | ✓ `ExceptionHandlingMiddleware` enriched |
| No secrets or tokens in logs | ✓ verified — only IDs, paths, error codes logged |

---

## 8. Changed Files

| File | Change type |
|---|---|
| `apps/gateway/Gateway.Api/Program.cs` | Modified — inline correlation ID middleware |
| `apps/services/careconnect/CareConnect.Api/Middleware/CorrelationIdMiddleware.cs` | **New** |
| `apps/services/careconnect/CareConnect.Api/Program.cs` | Modified — register correlation middleware |
| `apps/services/careconnect/CareConnect.Api/Middleware/ExceptionHandlingMiddleware.cs` | Modified — enriched log context |
| `apps/services/careconnect/CareConnect.Api/Endpoints/PublicNetworkEndpoints.cs` | Modified — RequestId in trust-boundary logs |
| `apps/services/careconnect/CareConnect.Api/Endpoints/InternalProvisionEndpoints.cs` | Modified — logging + audit event |
| `apps/services/careconnect/CareConnect.Api/Endpoints/ProviderAdminEndpoints.cs` | Modified — audit events for activate + link-org |
| `apps/services/careconnect/CareConnect.Application/Services/ActivationRequestService.cs` | Modified — CorrelationId in audit event |

---

## 9. Methods / Endpoints Updated

| Endpoint / Method | Change |
|---|---|
| Gateway inline middleware | Correlation ID assignment at edge |
| `CareConnect.Api.Middleware.CorrelationIdMiddleware` | New — reads/assigns/echoes `X-Correlation-Id` |
| `CareConnect.Api.Middleware.ExceptionHandlingMiddleware.InvokeAsync` | Enriched log context (all branches) |
| `CareConnect.Api.Endpoints.PublicNetworkEndpoints.ValidateTrustBoundaryAndResolveTenantId` | RequestId added to all warning branches |
| `POST /internal/provision-provider` (`InternalProvisionEndpoints.ProvisionProvider`) | ILogger + audit event |
| `POST /api/admin/providers/{id}/activate-for-careconnect` (`ProviderAdminEndpoints.ActivateForCareConnectAsync`) | Audit event |
| `PUT /api/admin/providers/{id}/link-organization` (`ProviderAdminEndpoints.LinkOrganizationAsync`) | Audit event |
| `ActivationRequestService.EmitApprovalAuditAsync` | CorrelationId populated from `X-Correlation-Id` |

---

## 10. GitHub Commits

- `e269ca2a32f5e1ad3e36ac0a0d8f0b7095f84219` — BLK-OBS-01: Observability & Audit Enforcement — correlation ID, structured logging, audit events

---

## 11. Issues / Gaps

### Residual gaps (out of scope or low priority)

1. **CareConnect → Tenant/Identity outbound headers**: The internal `HttpClient` calls from
   CareConnect to Tenant and Identity services do not explicitly inject `X-Correlation-Id`
   into outbound request headers. The ID is available in `Items["CorrelationId"]` but an
   `HttpClientHandler` or `DelegatingHandler` would be needed to propagate it automatically.
   This is a low-priority gap — the ID is present at the CareConnect boundary and in all
   audit events. Fixing it requires touching the HttpClient registration in `AddInfrastructure`
   and is scoped as a follow-on improvement.

2. **Gateway auth/authz failure logging**: When YARP rejects a request due to JWT validation
   failure (401), ASP.NET's built-in `JwtBearer` handler returns the response without emitting
   a structured log entry at the gateway level. The downstream services independently log these
   failures. A `JwtBearerEvents.OnAuthenticationFailed` / `OnForbidden` hook could add
   gateway-level structured logging. Deferred as a follow-on.

3. **Tenant/Identity services**: Both services already have solid provisioning-level logging
   and use `IAuditEventClient` / `AuditPublisher` for security events. No gaps requiring
   changes were identified in this block.

---

## 12. GitHub Diff Reference

- **Commit ID:** `e269ca2a32f5e1ad3e36ac0a0d8f0b7095f84219`
- **Diff file:** `analysis/BLK-OBS-01-commit.diff.txt`
- **Summary file:** `analysis/BLK-OBS-01-commit-summary.md`
- **Files changed:** 8 source files + 2 analysis artifacts
