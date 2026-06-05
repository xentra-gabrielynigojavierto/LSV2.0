# LS-FLOW-MERGE-P4 Report

> Phase 4 — Operational Hardening & Real Product Integration. Builds on Phase 3 (`LS-FLOW-MERGE-P3-report.md`).

## Scope Executed

1. ✅ Dedicated `WorkflowInstance` entity replacing the `TaskItem`-as-instance surrogate.
2. ✅ `ProductWorkflowMapping` carries `WorkflowInstanceId` (canonical) alongside legacy `WorkflowInstanceTaskId` (back-compat).
3. ✅ EF migration `20260417034039_AddWorkflowInstancesP4` applied to RDS via Flow.Api dev migrate path.
4. ✅ Centralized capability codes in `BuildingBlocks.Authorization.PermissionCodes` (`LienSell`, `ApplicationRefer`, existing `ReferralCreate`); Flow.Api policies reference constants.
5. ✅ Shared `IFlowClient` HTTP adapter (`BuildingBlocks.FlowClient`) with bearer pass-through, structured logging, timeout, and bounded retry.
6. ✅ Real product-side integrations: minimal `WorkflowEndpoints` in `Liens.Api`, `CareConnect.Api`, `Fund.Api` calling Flow through the shared adapter.
7. ✅ Operational hardening: 503 on Flow downtime, upstream 4xx propagated intact, 502 on upstream 5xx; bounded retries on transient failures; structured logs.
8. ✅ Minimal UI: `/product-workflows` page extended with a per-product "Start workflow" form; mapping table now shows `workflowInstanceId`.
9. ✅ Docs: `merge-phase-4-notes.md` + Phase-4 sections appended to `docs/README.md` and `docs/architecture.md`.

## Assumptions

- Flow continues to live under `/apps/services/flow` and is consumed by product services exclusively over HTTP (no shared DB).
- Product services pass through the caller's `Authorization` header to Flow; service-to-service tokens are deferred to a later phase.
- Capability policies on Flow continue to enforce per-product gating; products themselves additionally gate their own integration endpoints with `Policies.AuthenticatedUser`.
- Phase-4 `WorkflowInstance` is a thin entity (id + tenant + product + definition + status + correlation). Stage progression and richer state remain on `TaskItem` for now.

## Repository / Architecture Notes

- BuildingBlocks reaches Fund.Api / Liens.Api / CareConnect.Api transitively via `*.Domain.csproj` ProjectReferences — no csproj changes needed in product Api projects.
- Flow.Api uses controllers (`ProductWorkflowsController` route `/api/v1/product-workflows/{slug}`); product services use Minimal API endpoints (`*/Endpoints/WorkflowEndpoints.cs`).

## Workflow Instance Model Notes

- Entity: `Flow.Domain/Entities/WorkflowInstance.cs` (table `flow_workflow_instances`).
- Indexes: `(TenantId, ProductKey)` and `(TenantId, WorkflowDefinitionId)`. Tenant query filter applied like every other Flow entity.
- `ProductWorkflowMapping.WorkflowInstanceId` is the new canonical FK; `WorkflowInstanceTaskId` remains for back-compat.
- `ProductWorkflowService.CreateAsync` writes `WorkflowInstance` + `TaskItem` + mapping atomically inside an EF execution strategy + transaction.

## Migration / Data Transition Notes

- Migration: `20260417034039_AddWorkflowInstancesP4` (Flow.Infrastructure).
- Auto-applied at Flow.Api startup in Development; manual `dotnet ef database update` documented for higher environments.
- Two empty stale migration files (`20260417033717_*`, `20260417033803_*`) generated during regen attempts were removed. The Flow.Api workflow had to be stopped before `dotnet ef migrations add` could refresh the assembly.

## Authorization / Capability Hardening Notes

- Constants added: `PermissionCodes.LienSell` (`SYNQ_LIEN.lien:sell`), `PermissionCodes.ApplicationRefer` (`SYNQ_FUND.application:refer`); existing `PermissionCodes.ReferralCreate` kept.
- `Flow.Api/Program.cs` capability policies (`CanSellLien`, `CanReferCareConnect`, `CanReferFund`) now reference these constants.
- Dev fallback (`allowMissingPermissions`) remains gated behind `IsDevelopment()`.

## Product Integration Notes

- Shared client lives at `shared/building-blocks/BuildingBlocks/FlowClient/`:
  - `IFlowClient` — typed contract (`StartWorkflowAsync`, `ListBySourceEntityAsync`).
  - `FlowClient` — typed `HttpClient`; surfaces transport failures as `FlowClientUnavailableException`; surfaces upstream non-2xx as `HttpRequestException` carrying `StatusCode`.
  - `FlowClientOptions` — binds `Flow:BaseUrl` + `Flow:TimeoutSeconds`.
  - `FlowRetryHandler` — bounded exponential-backoff + jitter retry on transient transport / 408 / 429 / 5xx; non-idempotent verbs are only retried on transport failure.
  - `FlowEndpointResults.MapFailure` — shared mapper used by all three product endpoints (503 on unavailability, upstream 4xx propagated, 502 on upstream 5xx).
  - `AddFlowClient` — single-call DI registration including the retry handler and `IHttpContextAccessor`.
- Each product API exposes a minimal endpoint pair via `WorkflowEndpoints.MapWorkflowEndpoints()`:
  - `Liens.Api` → `POST/GET /api/liens/cases/{id}/workflows` (`product=synqlien`, `sourceEntityType=lien_case`).
  - `CareConnect.Api` → `POST/GET /api/referrals/{id}/workflows` (`product=careconnect`, `sourceEntityType=referral`).
  - `Fund.Api` → `POST/GET /api/applications/{id}/workflows` (`product=synqfund`, `sourceEntityType=fund_application`).
- `Flow:BaseUrl=http://localhost:5012` added to all three `appsettings.json`.

## Operational Hardening Notes

- Adapter logs request/response at info/warning levels (no PII).
- Failure mapping (`FlowEndpointResults.MapFailure`):
  - `FlowClientUnavailableException` → `503` `{ error, code: "flow_unavailable", detail }`.
  - `HttpRequestException` with upstream 4xx → propagated `{ error, code: "flow_rejected", upstreamStatus, detail }`.
  - `HttpRequestException` with upstream 5xx (or no `StatusCode`) → `502` `{ error, code: "flow_upstream_error", upstreamStatus?, detail }`.
- `FlowRetryHandler` retries on `408/429/500/502/503/504` for idempotent verbs (and on transport failures for any verb), max 3 attempts, exponential backoff 200 ms × 2^(n−1) + 0–100 ms jitter. Non-idempotent writes are never replayed against the server.
- Flow.Api now exposes `/health` as an alias for `/healthz` so unified product smoke checks pass.

## Frontend / Product UX Validation Notes

- `apps/services/flow/frontend/src/app/product-workflows/page.tsx` ships a collapsible **Start workflow** form per product (productKey, sourceEntityType, sourceEntityId, workflowDefinitionId, title) calling `POST /api/v1/product-workflows/{product}` and refreshing the table inline.
- Mapping rows now show `workflowInstanceId` (with fallback to legacy `workflowInstanceTaskId`).
- Client helper: `startProductWorkflow` in `lib/api/product-workflows.ts`.

## Documentation Changes

- New: `apps/services/flow/docs/merge-phase-4-notes.md` (full Phase-4 changelog, migration runbook, smoke checklist).
- Appended Phase-4 sections to `apps/services/flow/docs/README.md` and `apps/services/flow/docs/architecture.md`.
- This report (`analysis/LS-FLOW-MERGE-P4-report.md`).

## Validation Results

- `dotnet build LegalSynq.sln`: **Build succeeded**, 4 unrelated test-only warnings, 0 errors.
- Workflow restart: all services boot cleanly; Flow, Liens, Fund, CareConnect listening on `5012/5009/5002/5003`.
- Smoke (final pass after architect feedback applied):

  | URL                                                                                      | Expected | Actual |
  |------------------------------------------------------------------------------------------|----------|--------|
  | `GET http://localhost:5012/health`                                                       | 200      | **200** |
  | `GET http://localhost:5012/healthz`                                                      | 200      | **200** |
  | `GET http://localhost:5009/health`                                                       | 200      | **200** |
  | `GET http://localhost:5002/health`                                                       | 200      | **200** |
  | `GET http://localhost:5003/health`                                                       | 200      | **200** |
  | `GET http://localhost:5012/api/v1/product-workflows/synqlien` (no bearer)                | 401      | **401** |
  | `GET http://localhost:5009/api/liens/cases/{guid}/workflows` (no bearer)                 | 401      | **401** |
  | `GET http://localhost:5002/api/applications/{guid}/workflows` (no bearer)                | 401      | **401** |
  | `GET http://localhost:5003/api/referrals/{guid}/workflows` (no bearer)                   | 401      | **401** |

- Architect review (`evaluate_task`, includeGitDiff=true) flagged three issues; all addressed in this phase:
  1. Endpoints only caught `FlowClientUnavailableException` → upstream 4xx became 500. **Fixed** by adding shared `FlowEndpointResults.MapFailure` and catching `HttpRequestException` in all three product endpoints.
  2. Retry policy missing on `AddFlowClient`. **Fixed** by adding `FlowRetryHandler` and wiring it into the typed-client pipeline.
  3. Flow exposed only `/healthz`, breaking the unified smoke. **Fixed** by aliasing `/health` to the same health-check pipeline.

## Known Issues / Gaps

- Service-to-service auth still uses bearer pass-through from the originating user request; a dedicated machine token (or OBO) is deferred.
- Product `/workflows` endpoints proxy to Flow synchronously; an outbox/queue path could be added if Flow latency becomes user-visible.
- `WorkflowInstance` mirrors only the minimum fields needed for Phase 4. Richer state (current step, assignees, SLAs) will follow when the workflow engine itself moves off the `TaskItem` grain.
- `FlowRetryHandler` is a custom minimal implementation; if Polly is later added to BuildingBlocks, replacing it with a Polly policy would be straightforward.

## Recommendation

Phase 4 is complete and validated. Proceed with Phase 5 candidates: (a) lift `WorkflowInstance` to drive execution (move `currentStep`, `assignees`, `dueDates` off `TaskItem`), (b) add machine-to-machine auth for Flow ↔ product calls, (c) introduce an outbox path for create-workflow if SLA observability requires it.
