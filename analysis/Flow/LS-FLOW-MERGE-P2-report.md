# LS-FLOW-MERGE-P2 Report

**Status:** COMPLETE
**Date:** 2026-04-17

## Scope Executed

Phase 2 — Platform integration of the Flow service:
- Identity v2 (JWT bearer + claims-based context) on Flow.Api
- Strict tenant resolution (no silent default fallback)
- Environment-driven CORS
- Internal in-process event abstraction
- Audit adapter seam + safe baseline (logging) + optional HTTP adapter
- Notifications adapter seam + safe baseline (logging) + optional HTTP adapter
- Gateway routing for `/flow/health`, `/flow/info`, `/flow/api/v1/status`, `/flow/{**catch-all}`
- Controller `[Authorize]` enforcement
- Frontend `/tasks` and `/workflows` Suspense fix; bearer-token auth on outgoing requests; gateway-prefix default
- `scripts/run-dev.sh` updated to start Flow.Api alongside the platform
- Documentation updates (README, architecture, merge-phase-2-notes)

## Assumptions

1. Flow continues as a bounded service under `/apps/services/flow` with its own DB (`flow_db`) and its own `Flow.sln`. No DB consolidation, no merge into `LegalSynq.sln`.
2. JWT scheme matches the platform-wide convention used by Reports/Liens/Fund/Comms: shared `Jwt:Issuer = legalsynq-identity`, `Jwt:Audience = legalsynq-platform`, signing key delivered via secret/config (`Jwt:SigningKey`).
3. Tenant identity is resolved from the JWT `tenant_id` claim through `BuildingBlocks.Context.ICurrentRequestContext`, the same interface every other service uses. No more `X-Tenant-Id` header trust.
4. Existing schema column `TenantId` is `string`. The JWT claim is a `Guid`. `ClaimsTenantProvider` formats the claim as a stable lowercase 36-char string ("D"). No data migration is needed: pre-existing `"default"` rows are unreachable to authenticated callers, and unauthenticated callers can no longer reach the data layer.
5. CORS origins are read from `Cors:AllowedOrigins`. Local dev defaults preserve `:3000`, `:3001`, `:5000` (proxy), `:5004` (Control Center), `:5010` (Gateway). Higher environments must supply explicit origins via config.
6. Audit and Notifications integration is wired as **adapter seams + safe-baseline (logging) implementations**, with HTTP-backed implementations that activate only when `Audit:BaseUrl` / `Notifications:BaseUrl` are configured. The seams are ready; emission from inside `TaskService`/`WorkflowService` CRUD paths is a deliberate Phase 3 follow-up.
7. Flow listens on port **5012** (next free port: 5011 = Comms, 5010 = Gateway).
8. Flow is **not** added to `LegalSynq.sln`. It is started by `scripts/run-dev.sh` via its own `Flow.sln` build + `Flow.Api.csproj` run, preserving the service boundary.

## Repository / Architecture Notes

Files **created**:
- `apps/services/flow/backend/src/Flow.Api/Services/ClaimsTenantProvider.cs`
- `apps/services/flow/backend/src/Flow.Api/Middleware/TenantValidationMiddleware.cs`
- `apps/services/flow/backend/src/Flow.Application/Events/FlowEvents.cs`
- `apps/services/flow/backend/src/Flow.Infrastructure/Adapters/LoggingAuditAdapter.cs`
- `apps/services/flow/backend/src/Flow.Infrastructure/Adapters/HttpAuditAdapter.cs`
- `apps/services/flow/backend/src/Flow.Infrastructure/Adapters/LoggingNotificationAdapter.cs`
- `apps/services/flow/backend/src/Flow.Infrastructure/Adapters/HttpNotificationAdapter.cs`
- `apps/services/flow/backend/src/Flow.Infrastructure/Adapters/PlatformAdapterRegistration.cs`
- `apps/services/flow/backend/src/Flow.Infrastructure/Events/FlowEventDispatcher.cs`
- `apps/services/flow/docs/merge-phase-2-notes.md`
- `analysis/LS-FLOW-MERGE-P2-report.md`

Files **modified**:
- `apps/services/flow/backend/src/Flow.Api/Program.cs` (full rewrite — JWT, authz, CORS, middleware order)
- `apps/services/flow/backend/src/Flow.Api/Flow.Api.csproj` (BuildingBlocks ref + JwtBearer pkg)
- `apps/services/flow/backend/src/Flow.Api/appsettings.json` (Urls, Jwt, Audit, Notifications, Cors)
- `apps/services/flow/backend/src/Flow.Api/Controllers/HealthController.cs` (`[AllowAnonymous]`, added `/info`)
- `apps/services/flow/backend/src/Flow.Api/Controllers/V1/StatusController.cs` (`[AllowAnonymous]`)
- `apps/services/flow/backend/src/Flow.Api/Controllers/V1/TasksController.cs` (`[Authorize]`)
- `apps/services/flow/backend/src/Flow.Api/Controllers/V1/WorkflowsController.cs` (`[Authorize]`)
- `apps/services/flow/backend/src/Flow.Api/Controllers/V1/NotificationsController.cs` (`[Authorize]`)
- `apps/services/flow/backend/src/Flow.Application/Adapters/AuditAdapter/IAuditAdapter.cs` (interface widened + `AuditEvent` record)
- `apps/services/flow/backend/src/Flow.Application/Adapters/NotificationAdapter/INotificationAdapter.cs` (interface widened + `NotificationMessage` record)
- `apps/services/flow/backend/src/Flow.Infrastructure/Flow.Infrastructure.csproj` (added Microsoft.Extensions.Http + Logging.Abstractions)
- `apps/services/flow/backend/src/Flow.Infrastructure/Persistence/FlowDbContext.cs` (removed `?? "default"` fallback in `SaveChangesAsync`)
- `apps/services/flow/frontend/src/lib/config.ts` (default `/flow` gateway prefix)
- `apps/services/flow/frontend/src/lib/api/client.ts` (Bearer auth header; deprecated tenant shims; `credentials: include`)
- `apps/services/flow/frontend/src/app/tasks/page.tsx` (Suspense wrapper around `useSearchParams` consumer)
- `apps/services/flow/frontend/src/app/workflows/page.tsx` (Suspense wrapper)
- `apps/gateway/Gateway.Api/appsettings.json` (4 routes + 1 cluster)
- `scripts/run-dev.sh` (Flow.sln build + Flow.Api launch)
- `apps/services/flow/docs/README.md` (Phase 2 section)
- `apps/services/flow/docs/architecture.md` (Phase 2 section)

Files **removed**:
- `apps/services/flow/backend/src/Flow.Api/Services/HttpTenantProvider.cs`
- `apps/services/flow/backend/src/Flow.Api/Middleware/TenantMiddleware.cs`

Service boundary: Flow remains under `apps/services/flow/{backend,frontend,docs}` with its own `Flow.sln`. No code in any other service was modified except the gateway routing table.

## Identity Integration Notes

- Flow.Api wires `Microsoft.AspNetCore.Authentication.JwtBearer` against `Jwt:Issuer`, `Jwt:Audience`, `Jwt:SigningKey`. With no signing key configured, the bearer scheme is still registered but rejects all tokens — protected endpoints respond 401, anonymous endpoints (health/info/status) remain reachable.
- `BuildingBlocks.Authorization.Policies` registered: `AuthenticatedUser` (used by all V1 controllers), `AdminOnly`, `PlatformOrTenantAdmin` (registered for Phase 3 use).
- `BuildingBlocks.Context.ICurrentRequestContext` and `CurrentRequestContext` are registered scoped, exposing `UserId`, `TenantId`, `OrgId`, `Roles`, `ProductRoles`, `Permissions`, `IsPlatformAdmin`.
- Identity itself is not redesigned: Flow consumes the existing platform JWT format unchanged.

## Tenant Resolution Notes

- `HttpTenantProvider` (which silently substituted `"default"` from a header) is **removed**.
- `ClaimsTenantProvider` reads `tenant_id` strictly from the JWT (via `ICurrentRequestContext`). For excluded paths (`/health`, `/healthz`, `/ready`, `/api/v1/status`) it returns empty string so anonymous health probes don't crash. For all other paths with no authenticated tenant, it throws `InvalidOperationException`.
- `FlowDbContext.SaveChangesAsync` now throws if a new entity is added without a resolvable tenant. Pre-existing tenant-scoped query filters continue to work.
- New `TenantValidationMiddleware` (ported from Reports) inspects query string `tenantId` and JSON body `tenantId`/`TenantId` on POST/PUT/PATCH and returns HTTP 403 when they disagree with the JWT. It excludes the same paths.

## Security / CORS Notes

- Removed `SetIsOriginAllowed(_ => true)` + `AllowCredentials` (effectively a wildcard with credentials, which is unsafe and Chrome blocks anyway).
- Replaced with `WithOrigins(<Cors:AllowedOrigins>)` + `AllowCredentials`. When no origins are configured, `DisallowCredentials()` is set.
- Pipeline order: `Exception → Routing → CORS → Authentication → Authorization → TenantValidation → Endpoints`. Routing is placed before CORS so YARP-style downstream attribute routing is available; auth/authorization run before tenant validation so unauthenticated requests short-circuit.

## Gateway Integration Notes

- New cluster `flow-cluster` → `http://localhost:5012`.
- New routes (orders chosen between Comms (80/180) and Documents (50/150) ranges, leaving room for future siblings):
  - `flow-service-health` — `/flow/health` (Anonymous, Order 90)
  - `flow-service-info` — `/flow/info` (Anonymous, Order 91)
  - `flow-service-status` — `/flow/api/v1/status` (Anonymous, Order 92)
  - `flow-protected` — `/flow/{**catch-all}` (Order 190)
- Each route applies `PathRemovePrefix: /flow`. Bearer tokens are forwarded by YARP's default header behaviour; no token rewriting is required.
- No route collisions with existing services — the `/flow` prefix is unique.

## Audit Integration Notes

- `IAuditAdapter` widened from a marker interface to `WriteEventAsync(AuditEvent, CancellationToken)`.
- `AuditEvent` is a deliberately narrow envelope: `Action`, `EntityType`, `EntityId`, `TenantId`, `UserId`, `Description`, optional `Metadata`, `OccurredAtUtc`.
- `LoggingAuditAdapter` (singleton) is the always-registered safe baseline.
- `HttpAuditAdapter` (scoped) is registered only when `Audit:BaseUrl` is set; it POSTs to `audit/events` with a 5s timeout and decorates the logging adapter as fallback so audit failures cannot block the originating request.
- Wired through `IFlowEventDispatcher.PublishAsync(IFlowEvent)` from `Flow.Infrastructure/Events/FlowEventDispatcher.cs`, which maps event records into `AuditEvent` instances.

**Gap (intentional)**: emission of events from inside `TaskService` / `WorkflowService` CRUD paths is left for Phase 3. The seam, dispatcher, event types, and adapter mapping are complete; only the call-site injection inside the existing application services is deferred to avoid risky behaviour changes in this integration phase.

## Notifications Integration Notes

- `INotificationAdapter.SendAsync(NotificationMessage, CancellationToken)`.
- `NotificationMessage` carries `Channel`, `EventKey`, `TenantId`, `RecipientUserId`, `RecipientRoleKey`, `Subject`, `Body`, optional `Data`.
- `LoggingNotificationAdapter` is the always-on baseline.
- `HttpNotificationAdapter` activates when `Notifications:BaseUrl` is set; same fallback-on-failure pattern as audit.
- Dispatcher mapping currently sends notifications for `TaskAssignedEvent` and `WorkflowCompletedEvent`. Other events (workflow created, state changed, task completed) are audit-only by default — adjust in `FlowEventDispatcher.MapToNotification` when more triggers are needed.
- Strictly system notifications. No threading/two-way comms.

## Frontend Integration Notes

- `lib/config.ts` defaults `apiBaseUrl` to `/flow` (gateway prefix). Override with `NEXT_PUBLIC_FLOW_API_URL` for standalone hosting.
- `lib/api/client.ts`:
  - removed `X-Tenant-Id` header (tenant now derived from JWT server-side);
  - added `Authorization: Bearer <token>` from `localStorage`/`sessionStorage` (`ls_access_token`);
  - added `credentials: "include"`;
  - kept deprecated `getTenantId`/`setTenantId` as no-op shims so legacy `TenantSwitcher` UI keeps compiling.
- `app/tasks/page.tsx` and `app/workflows/page.tsx` wrapped in `<Suspense>` because they call `useSearchParams()` — fixes the Phase 1 prerender failure. Both pages now prerender as static (○).

## Documentation Changes

- `apps/services/flow/docs/merge-phase-2-notes.md` — full Phase 2 changelog, deferrals, run instructions.
- `apps/services/flow/docs/README.md` — Phase 2 summary section appended.
- `apps/services/flow/docs/architecture.md` — Phase 2 update section appended (auth, tenant, adapters, gateway).
- `replit.md` — to be updated alongside the next platform-wide doc pass.

## Validation Results

**Backend build (`Flow.sln`)**: succeeded — 0 errors, 1 pre-existing warning in `AutomationConditionEvaluator.cs` (CS0108, unrelated to Phase 2).

**Backend build (`LegalSynq.sln`)**: succeeded — 0 errors, gateway updated cleanly.

**Frontend build (Flow `next build`)**: succeeded — 0 errors, all routes generated; `/tasks` and `/workflows` prerender as static. Lockfile-root warning unchanged from Phase 1 and pre-existing.

**Workflow `Start application`**: restarted; Flow.Api came up on `:5012`.

**Runtime smoke (after workflow restart)**:
| Endpoint | Expected | Got |
|---|---|---|
| `GET http://localhost:5012/info` | 200 | **200** |
| `GET http://localhost:5012/api/v1/status` | 200 | **200** |
| `GET http://localhost:5012/api/v1/tasks` (no token) | 401 | **401** |
| `GET http://localhost:5010/flow/info` (gateway, anonymous) | 200 | **200** |
| `GET http://localhost:5010/flow/api/v1/tasks` (gateway, no token) | 401 | **401** |
| `GET /health` on identity, fund, careconnect, documents, audit, notifications, liens, comms | 200 each | **200 each** |
| `GET http://localhost:5010/identity/health` | 200 | **200** |

Existing platform services remain operational; gateway behaviour unchanged for non-Flow paths.

## Known Issues / Gaps

1. **Application-service event emission deferred**: `IFlowEventDispatcher.PublishAsync` is registered and ready, but is not yet called from inside `TaskService` / `WorkflowService` CRUD methods. The seam is complete; emission is a small Phase 3 follow-up.
2. **Capability-based authorization unused on Flow**: only `Policies.AuthenticatedUser` is enforced. Product-specific gates (`CanReceiveCareConnect`, `CanReferFund`, etc.) will be wired when product-consumption work begins in Phase 3.
3. **Pre-existing `"default"` tenant rows in flow_db**: created by the Phase 1 migration `20260416054716_AddTenantId`. They are unreachable to authenticated callers (which carry real tenant Guids) and the `SaveChangesAsync` fallback is removed — but the rows remain. Cleanup is a Phase 3 data-migration question, not a security risk today.
4. **`turbopack.root` warning** in `next build` is unchanged — cosmetic, caused by the dual lockfile under `apps/services/flow/frontend`. Resolving it requires deleting `package-lock.json` (npm artefact) or pinning `turbopack.root` in `next.config.ts`. Left untouched in this phase to avoid touching frontend build infra unrelated to the integration scope.
5. **`Audit:BaseUrl` / `Notifications:BaseUrl` are empty by default** — the safe-baseline logging adapters run. Switching to live HTTP delivery is a config change, not a code change.
6. **Tenant claim format**: Flow now stores tenants as lowercase `Guid.ToString("D")`. If any sibling service stores tenants in a different format, cross-service joins on tenant strings will need a normalisation pass — flagged for the Phase 3 product-consumption review.

## Recommendation

**Ready for next phase.** Flow is securely accessible as a LegalSynq platform service. Authenticated user and tenant context are enforced. Unsafe `"default"` tenant fallback is removed at every layer (provider, middleware, DbContext). Gateway exposure is controlled. Audit + Notification adapter seams are in place with safe baselines, and HTTP impls are one config flag away. Flow remains independently bounded with its own DB and solution, ready for Phase 3 product-consumption work (event emission inside application services, product-specific authorization policies, and cleanup of legacy `"default"` rows).
