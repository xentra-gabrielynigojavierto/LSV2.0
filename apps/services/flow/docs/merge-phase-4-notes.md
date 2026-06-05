# LS-FLOW-MERGE — Phase 4 notes

**Status:** delivered
**Date:** 2026-04-17

Phase 4 hardens the Flow ↔ product integration introduced in Phase 3 by giving Flow a real `WorkflowInstance` grain, centralizing capability permission codes, and standing up a shared product-side client (`IFlowClient`) that SynqLien, CareConnect, and SynqFund all consume from minimal `/workflows` endpoints.

## Highlights

1. **Dedicated `WorkflowInstance` entity.** New entity `Flow.Domain/Entities/WorkflowInstance.cs` (table `flow_workflow_instances`) replaces the Phase-3 stop-gap of using a `TaskItem` as the workflow-instance grain. Indexed on `(TenantId, ProductKey)` and `(TenantId, WorkflowDefinitionId)` and tenant-filtered like every other Flow entity. Migration: `20260417034039_AddWorkflowInstancesP4`.

2. **Mapping carries both ids.** `ProductWorkflowMapping` now exposes a new canonical FK `WorkflowInstanceId` alongside the legacy `WorkflowInstanceTaskId` (kept for back-compat). `ProductWorkflowService.CreateAsync` writes `WorkflowInstance` + `TaskItem` + mapping atomically in one transaction; the mapping carries both ids.

3. **Centralized permission codes.** New constants in `BuildingBlocks/Authorization/PermissionCodes.cs`:

   | Code (string)                  | Constant                                |
   |--------------------------------|-----------------------------------------|
   | `SYNQ_LIEN.lien:sell`          | `PermissionCodes.LienSell`              |
   | `CARE_CONNECT.referral:create` | `PermissionCodes.ReferralCreate`        |
   | `SYNQ_FUND.application:refer`  | `PermissionCodes.ApplicationRefer`      |

   `Flow.Api/Program.cs` capability policies now reference these constants instead of inline strings.

4. **Shared `IFlowClient` (BuildingBlocks).** New folder `shared/building-blocks/BuildingBlocks/FlowClient/`:
   - `IFlowClient.cs` — typed contract: `StartWorkflowAsync`, `ListWorkflowsAsync`.
   - `FlowClient.cs` — typed `HttpClient` with retry, timeout, structured logging, and bearer pass-through via `IHttpContextAccessor`.
   - `FlowClientOptions.cs` — `Flow:BaseUrl` binding.
   - `FlowClientUnavailableException.cs` — raised on Flow downtime / non-success.
   - `ServiceCollectionExtensions.AddFlowClient(IConfiguration)` — single-call registration.

5. **Product-side `/workflows` endpoints.** Each product API now exposes a small endpoint pair that calls Flow on behalf of the user:

   | Service           | Endpoint                                                      |
   |-------------------|---------------------------------------------------------------|
   | `Liens.Api`       | `POST/GET /api/liens/cases/{id}/workflows`                    |
   | `CareConnect.Api` | `POST/GET /api/referrals/{id}/workflows`                      |
   | `Fund.Api`        | `POST/GET /api/applications/{id}/workflows`                   |

   Implemented as Minimal API endpoint groups (`*/Endpoints/WorkflowEndpoints.cs`) and registered from each `Program.cs` next to the existing endpoint maps.

6. **Operational hardening.** Adapter boundaries log structured request/response (no PII). On Flow downtime the product endpoints return **HTTP 503** with `{ error, code: "flow_unavailable" }` rather than bubbling exceptions. Dev fallback for missing capability claims remains gated behind `IsDevelopment()`.

7. **Minimal frontend UX.** `/product-workflows/page.tsx` now ships a collapsible **Start workflow** form per product (productKey, sourceEntityType, sourceEntityId, workflowDefinitionId, title) that calls the existing `POST /api/v1/product-workflows/{product}` endpoint and refreshes the table inline. Mapping rows now show `workflowInstanceId`. Client helper: `startProductWorkflow` in `lib/api/product-workflows.ts`.

## File map

- `shared/building-blocks/BuildingBlocks/Authorization/PermissionCodes.cs` — added `LienSell`, `ApplicationRefer`.
- `shared/building-blocks/BuildingBlocks/FlowClient/{IFlowClient,FlowClient,FlowClientOptions,FlowClientUnavailableException,ServiceCollectionExtensions}.cs`.
- `apps/services/flow/backend/src/Flow.Domain/Entities/WorkflowInstance.cs`.
- `apps/services/flow/backend/src/Flow.Domain/Entities/ProductWorkflowMapping.cs` — added `WorkflowInstanceId`.
- `apps/services/flow/backend/src/Flow.Application/Interfaces/IFlowDbContext.cs` — `WorkflowInstances` DbSet.
- `apps/services/flow/backend/src/Flow.Infrastructure/Persistence/FlowDbContext.cs` — entity mapping + indexes + tenant filter.
- `apps/services/flow/backend/src/Flow.Infrastructure/Persistence/Migrations/20260417034039_AddWorkflowInstancesP4.{cs,Designer.cs}`.
- `apps/services/flow/backend/src/Flow.Application/Services/ProductWorkflowService.cs` — atomic instance+task+mapping creation.
- `apps/services/flow/backend/src/Flow.Application/DTOs/ProductWorkflowDtos.cs` — `WorkflowInstanceId` in response.
- `apps/services/flow/backend/src/Flow.Api/Program.cs` — capability policies via `PermissionCodes.*`.
- `apps/services/{liens,careconnect,fund}/*.Api/Endpoints/WorkflowEndpoints.cs` — new minimal endpoints.
- `apps/services/{liens,careconnect,fund}/*.Api/Program.cs` — `AddFlowClient` + `MapWorkflowEndpoints`.
- `apps/services/{liens,careconnect,fund}/*.Api/appsettings.json` — `Flow:BaseUrl=http://localhost:5012`.
- `apps/services/flow/frontend/src/app/product-workflows/page.tsx` + `src/lib/api/product-workflows.ts`.

## Compatibility

- All Phase-3 routes (`/api/v1/product-workflows/{product}`) keep their contracts; response now also carries `workflowInstanceId`.
- `WorkflowInstanceTaskId` is preserved on `ProductWorkflowMapping` for back-compat; consumers should migrate to `WorkflowInstanceId`.
- The new product `/workflows` endpoints are additive — no existing product routes change.
- `IFlowClient` requires `Flow:BaseUrl` configuration; absent config falls back to `http://localhost:5012` (dev default).

## Migration / runbook

```bash
# Apply database migration (Flow service)
cd apps/services/flow/backend
dotnet ef database update \
  --project src/Flow.Infrastructure \
  --startup-project src/Flow.Api
```

In dev, the migration is auto-applied at Flow.Api startup.

## Smoke tests (post-deploy)

```bash
# Health
for p in 5012 5009 5002 5003; do curl -s -o /dev/null -w "%{http_code}\n" http://localhost:$p/health; done   # → 200,200,200,200

# Auth required (no bearer)
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5012/api/v1/product-workflows/synqlien                                  # → 401
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5009/api/liens/cases/<guid>/workflows                                   # → 401
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5002/api/applications/<guid>/workflows                                  # → 401
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5003/api/referrals/<guid>/workflows                                     # → 401
```

## Known gaps / future work

- Service-to-service auth uses bearer pass-through from the originating user request. A dedicated machine token (or OBO) is deferred.
- Product `/workflows` endpoints proxy to Flow synchronously; an outbox/queue path could be added if Flow latency becomes user-visible.
- `WorkflowInstance` currently mirrors only the minimum fields needed for Phase 4; richer state (current step, assignees, SLAs) will follow when the workflow engine itself moves off the `TaskItem` grain.
