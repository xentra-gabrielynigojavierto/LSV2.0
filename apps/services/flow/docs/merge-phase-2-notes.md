# Flow — Merge Phase 2 Notes

Phase 2 transforms Flow from a structurally-merged but standalone service
into a first-class LegalSynq platform service. The service boundary, DB
isolation, and bounded responsibility are preserved.

## What Changed

### Identity / Authentication
- Flow.Api now wires `Microsoft.AspNetCore.Authentication.JwtBearer` using the
  shared `Jwt:Issuer`, `Jwt:Audience`, `Jwt:SigningKey` configuration block —
  identical to Reports/Liens/Fund/Comms.
- Authorization policies are sourced from `BuildingBlocks.Authorization`:
  - `Policies.AuthenticatedUser` — applied to all V1 controllers
    (`TasksController`, `WorkflowsController`, `NotificationsController`).
  - `Policies.AdminOnly` and `Policies.PlatformOrTenantAdmin` are registered
    for use in Phase 3 product-consumption work.
- The current request principal is exposed through
  `BuildingBlocks.Context.ICurrentRequestContext` via DI.
- `[AllowAnonymous]` is applied to `HealthController` (`/health`, `/info`) and
  `StatusController` (`/api/v1/status`).

### Tenant Resolution
- The legacy `HttpTenantProvider`, which silently substituted `"default"` when
  no `X-Tenant-Id` header was present, has been **removed**.
- The new `ClaimsTenantProvider` resolves the tenant strictly from the JWT
  `tenant_id` claim (via `ICurrentRequestContext`). Authenticated requests
  always have a tenant; missing-claim cases throw, ensuring tenant isolation
  cannot be bypassed by anonymous or malformed traffic.
- Public/anonymous endpoints (health, info, status) bypass tenant lookup by
  path allowlist.
- `FlowDbContext.SaveChangesAsync` no longer falls back to `"default"`; it
  throws `InvalidOperationException` when an entity is added without a
  resolvable tenant context.
- A new `TenantValidationMiddleware` (ported from Reports) blocks requests
  where a query-string or JSON-body `tenantId` disagrees with the JWT claim,
  returning HTTP 403.

### CORS / Security
- The previous policy used `SetIsOriginAllowed(_ => true) + AllowCredentials`,
  effectively a wildcard with credentials. That has been replaced with an
  explicit `WithOrigins(...).AllowCredentials()` driven by
  `Cors:AllowedOrigins`. Local dev defaults preserve `:3000`, `:3001`,
  `:5000` (proxy), `:5004` (Control Center), and `:5010` (Gateway).
- Middleware order is now: `Exception → Routing → CORS → Authentication →
  Authorization → TenantValidation → Endpoints`.

### Internal Events
- A lightweight in-process event abstraction lives at
  `Flow.Application/Events/FlowEvents.cs`:
  - `WorkflowCreatedEvent`, `WorkflowStateChangedEvent`,
    `WorkflowCompletedEvent`, `TaskAssignedEvent`, `TaskCompletedEvent`.
- `IFlowEventDispatcher` (impl `Flow.Infrastructure/Events/FlowEventDispatcher`)
  fans out to the audit + notification adapter seams. Dispatch failures are
  swallowed so adapter outages cannot break workflow/task operations.
- This is **not** a platform event bus. It exists only to decouple Flow
  application code from the audit/notification adapter wiring.

### Audit Adapter
- `IAuditAdapter` (interface in `Flow.Application/Adapters/AuditAdapter`) now
  exposes `WriteEventAsync(AuditEvent, CancellationToken)`.
- `LoggingAuditAdapter` is the safe baseline — always registered, never fails.
- `HttpAuditAdapter` is registered automatically when `Audit:BaseUrl` is
  configured. Failures fall back to the logging adapter.
- See `PlatformAdapterRegistration.AddFlowPlatformAdapters`.

### Notifications Adapter
- `INotificationAdapter` exposes `SendAsync(NotificationMessage, CancellationToken)`.
- `LoggingNotificationAdapter` is the safe baseline.
- `HttpNotificationAdapter` activates only when `Notifications:BaseUrl` is
  configured. Failures fall back to the logging adapter.

### Gateway Routing
Routes added to `apps/gateway/Gateway.Api/appsettings.json`:
- `flow-service-health`  → `/flow/health`            (Anonymous, Order 90)
- `flow-service-info`    → `/flow/info`              (Anonymous, Order 91)
- `flow-service-status`  → `/flow/api/v1/status`     (Anonymous, Order 92)
- `flow-protected`       → `/flow/{**catch-all}`     (Order 190)
Cluster `flow-cluster` → `http://localhost:5012`. The `/flow` prefix is
stripped before forwarding.

### Frontend
- `lib/config.ts` now defaults `apiBaseUrl` to `/flow` (the gateway prefix)
  rather than an empty string. Set `NEXT_PUBLIC_FLOW_API_URL` to override
  for standalone hosting.
- `lib/api/client.ts` no longer sends `X-Tenant-Id`. It now attaches
  `Authorization: Bearer <token>` from `localStorage`/`sessionStorage`
  (`ls_access_token`) when present, and sends `credentials: "include"`.
- `getTenantId` / `setTenantId` are kept as deprecated shims so legacy UI
  components (`TenantSwitcher`) keep compiling; they no longer affect
  outgoing requests.
- `app/tasks/page.tsx` and `app/workflows/page.tsx` are wrapped in
  `<Suspense>` to satisfy Next.js 16's `useSearchParams()` prerender
  contract — fixing the known Phase 1 build instability.

## What Was NOT Changed (Explicit Deferrals)

1. **Product-specific workflow templates / mappings** — SynqLien, CareConnect,
   SynqFund integrations are Phase 3.
2. **DB consolidation** — Flow keeps its own `flow_db`. No merge into other
   service databases.
3. **Direct DB access from other services into Flow** — disallowed.
4. **Full event bus** — only the in-process dispatcher exists today.
5. **Broad UI redesign / workflow-engine redesign** — out of scope.
6. **Adding Flow.sln to LegalSynq.sln** — kept separate to preserve service
   boundary. Built/run via its own `Flow.sln` (see Run section).
7. **Capability-based authorization (`CanReceiveCareConnect`, etc.)** — those
   policies will be wired when Phase 3 product consumption begins. Today only
   `AuthenticatedUser` is enforced on Flow controllers.
8. **Application-service wiring of `IFlowEventDispatcher.PublishAsync`** —
   the dispatcher and event types are in place but the call sites inside
   `TaskService` / `WorkflowService` are intentionally left for the next
   slice; injecting events into every CRUD path was deemed risky in this
   integration phase. Seams are ready; emission is the next step.

## Local Run

Flow is started by `scripts/run-dev.sh` alongside the other LegalSynq
services. It listens on port **5012**. The gateway forwards `/flow/**` to
that port.

To run Flow standalone (without the platform):
```bash
cd apps/services/flow/backend
dotnet build Flow.sln
dotnet run --project src/Flow.Api/Flow.Api.csproj
```

Required configuration (typically supplied via secrets):
- `Jwt:SigningKey` — must match the LegalSynq Identity signing key for
  authenticated calls to succeed.
- `ConnectionStrings:FlowDb` (or `FLOW_DB_CONNECTION_STRING` env var) —
  MySQL connection string.
- `Audit:BaseUrl` — optional. When set, audit events go HTTP; otherwise
  they are logged.
- `Notifications:BaseUrl` — optional. Same pattern as audit.
- `Cors:AllowedOrigins` — explicit list for non-local environments.
