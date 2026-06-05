# Flow Service

A generic, reusable workflow and task orchestration service. Flow provides primitives for defining workflows, managing task lifecycles, processing events, and (in later phases) participating in audit and notification flows.

## Service Boundary

Flow is a **detachable, standalone platform service**:

- Owns its own database (`flow_db`); never shares schema or connections with consuming applications.
- Exposes a REST API for external integration; consumers must not access `flow_db` directly.
- Uses generic `ContextReference` linkage instead of product-specific entities (no `caseId`/`lienId`/`referralId` in core models).
- Adapter interfaces let consuming products plug in product-specific behavior without polluting Flow's core.

## Layout

```
apps/services/flow/
  backend/      .NET 8 Web API (Flow.Api / Flow.Application / Flow.Domain / Flow.Infrastructure)
  frontend/    Next.js 16 / React 19 / Tailwind v4 admin UI
  docs/        architecture + merge notes
```

## Running locally (Phase 1)

Backend (separate from `LegalSynq.sln`):

```bash
dotnet build apps/services/flow/backend/Flow.sln
dotnet run --project apps/services/flow/backend/src/Flow.Api/Flow.Api.csproj
```

Frontend (uses npm, isolated from the main pnpm workspace):

```bash
cd apps/services/flow/frontend
npm install
npm run dev
```

## Database

- Connection-string key: `ConnectionStrings:FlowDb`
- Default DB name: `flow_db`
- Provider: MySQL (Pomelo.EntityFrameworkCore.MySql)

## Planned Integrations (deferred to later phases)

- Identity v2 (auth, tenant context)
- Notifications service (event delivery)
- Audit service (event capture)
- Cross-service event bus
- DB / sln consolidation decisions

See `merge-phase-1-notes.md` for the full deferral list.

---

## Phase 2 — Platform Integration (2026-04-17)

Flow is now a first-class LegalSynq platform service. Key changes:

- **Auth:** JWT bearer auth using shared `Jwt:*` config; `[Authorize(Policy = AuthenticatedUser)]` on all V1 controllers.
- **Tenant:** strict claims-based resolution (`tenant_id` JWT claim). No silent default fallback.
- **CORS:** environment-driven via `Cors:AllowedOrigins`.
- **Gateway:** `/flow/health`, `/flow/info`, `/flow/api/v1/status` (anonymous) and `/flow/{**catch-all}` (protected) routed to `localhost:5012`.
- **Adapters:** `IAuditAdapter` and `INotificationAdapter` seams + safe logging baselines + optional HTTP impls (activate via `Audit:BaseUrl` / `Notifications:BaseUrl`).
- **Internal events:** `IFlowEventDispatcher` + `Flow.Application/Events/*` (in-process only).

See `merge-phase-2-notes.md` for the full Phase 2 changelog and deferrals.

## Phase 3 — product consumption (LS-FLOW-MERGE-P3, 2026-04-17)

Flow is now product-consumable for **SynqLien**, **CareConnect**, and **SynqFund**:

- **Real event emission.** Workflow create / state-change / complete and task assign / complete now publish via `IFlowEventDispatcher`.
- **Product↔workflow correlation.** New `ProductWorkflowMapping` entity + table `flow_product_workflow_mappings` (migration `20260417030704_AddProductWorkflowMappings`).
- **Product-facing API.** `/api/v1/product-workflows/{synqlien|careconnect|synqfund}` gated by `CanSellLien` / `CanReferCareConnect` / `CanReferFund` policies.
- **Legacy cleanup.** `backend/sql/cleanup-default-tenant.sql` (manual review).
- **UI.** `/product-workflows` page lists mappings grouped by product.

See `merge-phase-3-notes.md` for the full Phase 3 changelog.

## Phase 4 — operational hardening (LS-FLOW-MERGE-P4, 2026-04-17)

Phase 4 hardens the Flow ↔ product integration:

- **Dedicated `WorkflowInstance` entity.** New `flow_workflow_instances` table replaces the Phase-3 stop-gap of using a `TaskItem` as the workflow-instance grain. Mapping rows now carry both `WorkflowInstanceId` (canonical) and `WorkflowInstanceTaskId` (deprecated, kept for back-compat). Migration `20260417034039_AddWorkflowInstancesP4`.
- **Centralized capability codes.** `BuildingBlocks/Authorization/PermissionCodes.cs` now defines `LienSell` / `ReferralCreate` / `ApplicationRefer`; Flow.Api policies reference the constants.
- **Shared `IFlowClient` (BuildingBlocks).** Typed `HttpClient` with retry, timeout, structured logging, and bearer pass-through. Single-call registration via `services.AddFlowClient(configuration)`.
- **Product-side `/workflows` endpoints.** `Liens.Api`, `CareConnect.Api`, `Fund.Api` each expose `POST/GET /api/{liens/cases|referrals|applications}/{id}/workflows`, returning **HTTP 503** on Flow downtime.
- **Frontend.** `/product-workflows` page now ships a per-product **Start workflow** form and shows `workflowInstanceId` in the mapping table.

See `merge-phase-4-notes.md` for the full Phase 4 changelog.

## Phase 5 (LS-FLOW-MERGE-P5) — execution engine maturity

- **WorkflowInstance is the execution row.** New columns `CurrentStageId`, `CurrentStepKey`, `StartedAt`, `AssignedToUserId`, `LastErrorMessage`. Migration `20260417042541_AddWorkflowInstanceExecutionStateP5`.
- **Step/state engine.** `Flow.Application/Engines/WorkflowEngine` with `Start/Advance/Complete/Cancel/Fail`. Optimistic concurrency via `expectedCurrentStepKey`; invalid transitions throw `InvalidWorkflowTransitionException` → **HTTP 409** with stable `code` (`expected_step_mismatch`, `terminal_state`, `no_outgoing_transition`, …). Reuses the existing `WorkflowStage`/`WorkflowTransition` graph.
- **Execution API.** `WorkflowInstancesController` exposes `GET /api/v1/workflow-instances/{id}`, `.../current-step`, and `POST .../advance|complete|cancel`. `ProductWorkflowService.CreateAsync` now calls `engine.StartAsync` after the row is saved.
- **Service-token auth.** `BuildingBlocks/Authentication/ServiceTokens` mints HS256 JWTs (`aud=flow-service`, `tid`, optional `actor`). Flow.Api uses a `MultiAuth` PolicyScheme that dispatches by audience to the user `Bearer` or the new `ServiceToken` scheme. Shared secret via env `FLOW_SERVICE_TOKEN_SECRET`.
- **Product passthrough.** `IFlowClient` adds `GetWorkflowInstanceAsync` / `AdvanceWorkflowAsync` / `CompleteWorkflowAsync` (prefers a service token + `actor` claim when configured). Each product calls `group.MapFlowExecutionPassthrough()` to expose `GET/POST .../workflows/{wfId}{,/advance,/complete}`.

See `merge-phase-5-notes.md` for the full Phase 5 changelog.

## Phase A1 — Hardening (LS-FLOW-HARDEN-A1)

- **Atomic ownership endpoints.** `ProductWorkflowExecutionController` exposes `GET / POST advance / POST complete` under `/api/v1/product-workflows/{product}/{sourceEntityType}/{sourceEntityId}/{workflowInstanceId:guid}`. A single EF join over `ProductWorkflowMappings × WorkflowInstances` (both tenant-filtered) closes the Phase-5 TOCTOU window between the product-side pre-check and the engine call. Mismatch on tenant / product / parent / instance → uniform `404 workflow_instance_not_owned`.
- **Product passthroughs.** `BuildingBlocks/FlowClient/FlowExecutionEndpoints.MapFlowExecutionPassthrough` now calls the new atomic endpoints directly via `IFlowClient.{Get,Advance,Complete}ProductWorkflowAsync`. The two-call (`ListBySourceEntity` then engine) pattern is gone.
- **Service-token validation.** `AddServiceTokenBearer` now sets `RequireSignedTokens` and `RequireExpirationTime`, runs `OnTokenValidated` to require a `service:*` subject and a tenant claim, and accepts `failFastIfMissingSecret` (Flow.Api wires `!IsDevelopment()`). `ICallerContextAccessor` distinguishes `User` / `Service` / `ServiceOnBehalfOfUser`.
- **Diagnostics.** `BuildingBlocks/FlowClient/FlowErrorCodes` is the single source of truth for machine-readable codes (`workflow_instance_not_owned`, `expected_step_mismatch`, `instance_not_active`, `concurrent_state_change`, `invalid_service_token`, `missing_tenant_context`, `flow_unavailable`, `flow_upstream_error`).
- **Tests.** `apps/services/flow/backend/tests/Flow.UnitTests` (xUnit). 9 cases, 9 pass — caller classification + service-token startup guard.

See `merge-phase-A1-notes.md` for the full A1 changelog and the deferred Phase A1.1 integration matrix.

## Phase A1.1 — Hardening Validation Track (LS-FLOW-HARDEN-A1.1)

- **Real Flow.Api integration host.** `apps/services/flow/backend/tests/Flow.IntegrationTests` boots the actual API via `WebApplicationFactory<Program>` against an in-memory SQLite database and a header-driven `TestAuth` scheme. Every controller, middleware, capability policy, and the real `WorkflowEngine` execute under test — only auth and DB are substituted.
- **Security/integrity matrix (28 tests, all green).** Ownership IDOR, tenant isolation, cross-product correlation, transition integrity, auth (anonymous / capability / product-role / service-token), and error-contract code stability — all asserted from the wire against the real pipeline.
- **Per-product happy-path proof.** `HappyPathExecutionTests` exercises GET → advance → complete for SynqLien, CareConnect, and SynqFund.
- **Live-stack probe.** `apps/services/flow/backend/scripts/harden-a1-e2e.sh` mirrors `p5-e2e.sh` against the new atomic A1 endpoints and supports two optional negative probes (other-tenant, wrong-parent).
- **Tests.** `dotnet test tests/Flow.IntegrationTests/Flow.IntegrationTests.csproj` (28/28 pass) alongside the existing `Flow.UnitTests` (9/9 pass).

See `merge-phase-A1.1-notes.md` for the full A1.1 changelog.
