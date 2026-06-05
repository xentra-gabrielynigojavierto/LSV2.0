# LS-FLOW-MERGE — Phase 3 notes

**Status:** delivered
**Date:** 2026-04-17

Phase 3 makes Flow product-consumable for **SynqLien**, **CareConnect**, and **SynqFund**, building on the auth/tenant work of Phase 2.

## Highlights

1. **Real event emission.** `IFlowEventDispatcher` is now wired into the application layer. `WorkflowService` emits `WorkflowCreated`, `WorkflowStateChanged`, and `WorkflowCompleted` (when status transitions to `Completed`). `TaskService` emits `TaskAssigned` (on creation-with-assignment and on `Assign`) and `TaskCompleted` (on terminal `Done` status). The dispatcher fans out to the existing audit + notification adapter seams.

2. **Tenant + user context for events.** `IFlowUserContext` is a small abstraction in `Flow.Domain.Interfaces` (mirrors the `ITenantProvider` pattern) and is implemented in `Flow.Api/Services/FlowUserContext.cs` on top of `BuildingBlocks.Context.ICurrentRequestContext`. This keeps `Flow.Application` independent of the BuildingBlocks shared library.

3. **Product↔workflow correlation.** New entity `ProductWorkflowMapping` (table `flow_product_workflow_mappings`) stores the explicit link between a product-side entity (e.g. a SynqLien lien_case) and a Flow workflow instance. Indexed on `(TenantId, ProductKey)` and `(TenantId, ProductKey, SourceEntityType, SourceEntityId)`. Migration: `20260417030704_AddProductWorkflowMappings`.

4. **Product-facing API.** New controller `ProductWorkflowsController` at `/api/v1/product-workflows/{product}` with three product segments: `synqlien`, `careconnect`, `synqfund`. Each segment is gated by the matching BuildingBlocks capability policy:

   | Segment       | Policy              |
   |---------------|---------------------|
   | `synqlien`    | `CanSellLien`       |
   | `careconnect` | `CanReferCareConnect` |
   | `synqfund`    | `CanReferFund`      |

   `POST` creates a Flow task (the workflow-instance grain today) and a mapping row, validating that the workflow's `ProductKey` matches the route. `GET` lists by product (optionally filtered by `sourceEntityType` + `sourceEntityId`) or fetches by mapping id.

5. **Legacy tenant cleanup.** `apps/services/flow/backend/sql/cleanup-default-tenant.sql` reports legacy `default`-tenant rows per table and contains a commented `DELETE` block for manual review. Not auto-executed.

6. **Minimal UI.** New `/product-workflows` page lists mappings grouped by product. Each section calls its product endpoint independently, so missing capability claims affect only that section.

## File map

- `Flow.Domain/Interfaces/IFlowUserContext.cs` — new abstraction.
- `Flow.Domain/Entities/ProductWorkflowMapping.cs` — new entity.
- `Flow.Application/DTOs/ProductWorkflowDtos.cs` — request/response.
- `Flow.Application/Services/IProductWorkflowService.cs` + `ProductWorkflowService.cs`.
- `Flow.Application/Services/WorkflowService.cs` + `TaskService.cs` — event emission.
- `Flow.Application/Interfaces/IFlowDbContext.cs` — `ProductWorkflowMappings` DbSet.
- `Flow.Infrastructure/Persistence/FlowDbContext.cs` — entity mapping + indexes.
- `Flow.Infrastructure/Persistence/Migrations/20260417031705_AddProductWorkflowMappingsP3.{cs,Designer.cs}`.
- `Flow.Infrastructure/DependencyInjection.cs` — registers `IProductWorkflowService`.
- `Flow.Api/Services/FlowUserContext.cs` — adapter.
- `Flow.Api/Controllers/V1/ProductWorkflowsController.cs` — product-scoped routes.
- `Flow.Api/Program.cs` — registers `IFlowUserContext`.
- `apps/services/flow/backend/sql/cleanup-default-tenant.sql`.
- `apps/services/flow/frontend/src/lib/api/product-workflows.ts` + `app/product-workflows/page.tsx`.

## Compatibility

- All existing Flow APIs (`/api/v1/workflows`, `/api/v1/tasks`, `/api/v1/notifications`) keep their contracts. Phase 3 is additive.
- Event payloads follow the Phase-2 `IFlowEvent` shape; no changes to adapter wiring were required.
- The `ProductWorkflowMapping` query filter follows the same tenant-isolation rule as every other Flow entity.

## Migration / runbook

```bash
# Apply database migration (Flow service)
cd apps/services/flow/backend
dotnet ef database update --project src/Flow.Infrastructure --startup-project src/Flow.Api

# Optional, manual: report + clean legacy "default"-tenant rows
mysql -h <host> -u <user> -p flow_db < sql/cleanup-default-tenant.sql
```

## Known gaps / future work

- `WorkflowInstanceTaskId` references a `TaskItem` because Flow does not yet have a dedicated workflow-instance entity. When that lands (Phase 4 candidate), this column should be re-pointed without breaking the public API.
- The product↔workflow create flow does not yet write back to product services; correlation is one-directional (product → Flow) until product adapters are added.
- `ProductWorkflowsController` exposes only `synqlien`, `careconnect`, `synqfund`. New products require a new pair of policies and a new route group.
