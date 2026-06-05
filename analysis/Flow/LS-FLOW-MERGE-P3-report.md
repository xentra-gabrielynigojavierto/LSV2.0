# LS-FLOW-MERGE-P3 Report

**Status:** DELIVERED
**Date:** 2026-04-17
**Phase:** Make Flow product-consumable for SynqLien, CareConnect, SynqFund.

## Scope Executed

1. **Real event emission** wired into `WorkflowService` (Created / StateChanged / Completed) and `TaskService` (TaskAssigned / TaskCompleted) using the Phase-2 `IFlowEventDispatcher`.
2. **Tenant + user context for events** via new `IFlowUserContext` (Flow.Domain) implemented in Flow.Api on top of `BuildingBlocks.Context.ICurrentRequestContext`.
3. **Product↔workflow correlation entity** `ProductWorkflowMapping` + EF migration `20260417030704_AddProductWorkflowMappings` (table `flow_product_workflow_mappings`).
4. **Product-facing controller** `ProductWorkflowsController` at `/api/v1/product-workflows/{synqlien|careconnect|synqfund}` with per-route capability policies.
5. **Capability policy registration** in `Flow.Api/Program.cs` for `CanSellLien`, `CanReferCareConnect`, `CanReferFund` — each requires authenticated user PLUS (permission claim OR product-role access on matching ProductCode) with dev fallback when no permission claims are present.
6. **Legacy "default"-tenant cleanup script** `apps/services/flow/backend/sql/cleanup-default-tenant.sql` (idempotent, manual review).
7. **Minimal frontend validation page** `/product-workflows` listing mappings grouped by product.
8. **Phase 3 docs**: `apps/services/flow/docs/merge-phase-3-notes.md` + appended sections in `README.md` and `architecture.md`.

## Assumptions

- Flow workflow status `Completed` (existing enum value) is the terminal state that triggers `WorkflowCompletedEvent`. `TaskItemStatus.Done` is the terminal state for `TaskCompletedEvent`. Both reflect the existing enum surface in `Flow.Domain/Enums`.
- The current Flow workflow-instance grain is `TaskItem`; `ProductWorkflowMapping.WorkflowInstanceTaskId` will be re-pointed when a dedicated workflow-instance entity lands (Phase 4 candidate).
- Capability policies fall back to AuthenticatedUser in dev tokens that lack permission claims entirely — matches the spec's "fall back to AuthenticatedUser if claim absent in dev" guidance.

## Repository / Architecture Notes

- `IFlowUserContext` lives in `Flow.Domain.Interfaces` to avoid pulling BuildingBlocks into `Flow.Application` (mirrors existing `ITenantProvider` pattern).
- `FlowUserContext` adapter in `Flow.Api` formats the tenant id as the lowercase-`D` Guid string, matching `ClaimsTenantProvider`.
- Application services receive event/user dependencies via constructor injection registered in `Flow.Infrastructure.DependencyInjection.AddApplicationServices`.

## Application Event Emission

| Trigger                                  | Event                       | Notes |
|------------------------------------------|-----------------------------|-------|
| `WorkflowService.CreateAsync` success    | `WorkflowCreatedEvent`      | After SaveChanges. |
| `WorkflowService.UpdateAsync` status delta | `WorkflowStateChangedEvent` | Captures `previousStatus → workflow.Status`. |
| `WorkflowService.UpdateAsync` → `Completed` | `WorkflowCompletedEvent`  | Emitted alongside StateChanged. |
| `TaskService.CreateAsync` with assignment | `TaskAssignedEvent`        | Only when user/role/org is set on creation. |
| `TaskService.AssignAsync`                | `TaskAssignedEvent`         | Replaces in-line audit/notification path; existing notification still fires. |
| `TaskService.UpdateStatusAsync` → `Done` | `TaskCompletedEvent`        | After SaveChanges. |

## Audit / Notification Trigger Map

The Phase-2 `FlowEventDispatcher` fans out to both `IAuditAdapter` and `INotificationAdapter`. No adapter changes required in Phase 3 — the new emissions automatically reach both seams.

## Product Capability Model

| Product slug   | Flow ProductKey | Capability policy        | BuildingBlocks ProductCode | Permission claim attempted |
|----------------|-----------------|--------------------------|----------------------------|----------------------------|
| `synqlien`     | `SYNQ_LIENS`    | `CanSellLien`            | `SynqLiens`                | `SYNQ_LIENS.lien:sell`     |
| `careconnect`  | `CARE_CONNECT`  | `CanReferCareConnect`    | `SynqCareConnect`          | `PermissionCodes.ReferralCreate` |
| `synqfund`     | `SYNQ_FUND`     | `CanReferFund`           | `SynqFund`                 | `SYNQ_FUND.application:refer` |

Note: Flow uses `CARE_CONNECT` as its product key, while BuildingBlocks `ProductCodes.SynqCareConnect` is `SYNQ_CARECONNECT`. This is handled by the route → product key mapping inside `ProductWorkflowsController`.

## Product-to-Flow Mapping Model

`flow_product_workflow_mappings` columns:

| Column                      | Type            | Notes |
|-----------------------------|-----------------|-------|
| Id                          | char(36) PK     | Guid. |
| TenantId                    | varchar(128)    | Tenant query filter applied. |
| ProductKey                  | varchar(64)     | One of `ProductKeys.*` (not `FLOW_GENERIC`). |
| SourceEntityType            | varchar(128)    | e.g. `lien_case`, `referral`. |
| SourceEntityId              | varchar(256)    | Product-side id (string). |
| WorkflowDefinitionId        | char(36) FK     | Restrict on delete. |
| WorkflowInstanceTaskId      | char(36) FK?    | Set null on task delete. |
| CorrelationKey              | varchar(256)?   | Optional external id. |
| Status                      | varchar(32)     | `Active` / `Completed` / `Cancelled` (default Active). |
| CreatedAt / UpdatedAt / *By | (AuditableEntity) | |

Indexes: `(TenantId, ProductKey)`, `ix_pwm_product_entity (TenantId, ProductKey, SourceEntityType, SourceEntityId)`, `WorkflowDefinitionId`, `WorkflowInstanceTaskId`.

## Initial Product Integrations

The Phase-3 wiring on the **Flow** side is complete. Product service adapters that call these endpoints from SynqLien / CareConnect / SynqFund are intentionally out of scope for this phase — they will land alongside their respective product flows (next phase).

## Product-Facing API Patterns

```
POST  /api/v1/product-workflows/{product}
GET   /api/v1/product-workflows/{product}?sourceEntityType=&sourceEntityId=
GET   /api/v1/product-workflows/{product}/{id}
```

`{product}` is one of `synqlien`, `careconnect`, `synqfund`. Each route segment carries its own `[Authorize(Policy=...)]`. The service rejects the call if the workflow's `ProductKey` does not match the route's product (defense in depth).

## Legacy Tenant Cleanup

`apps/services/flow/backend/sql/cleanup-default-tenant.sql`:

- Step 1 (uncommented): `SELECT` row counts of legacy `TenantId='default'` rows across every Flow table, including the new `flow_product_workflow_mappings`.
- Step 2 (commented): transactional `DELETE` ordered by FK constraints. Operator must uncomment after reviewing counts.

Not auto-executed by the platform.

## Frontend / UX Validation

- New page `apps/services/flow/frontend/src/app/product-workflows/page.tsx` — three sections, one per product, each calling its own API. A 403/401 on one product does not break the others.
- New API client `apps/services/flow/frontend/src/lib/api/product-workflows.ts` — typed fetcher per product slug.

## Documentation Changes

- New: `apps/services/flow/docs/merge-phase-3-notes.md` (full Phase 3 changelog).
- Appended: `apps/services/flow/docs/README.md` (Phase 3 summary section).
- Appended: `apps/services/flow/docs/architecture.md` (product consumption section).

## Validation Results

- **Build**: `dotnet build Flow.sln` → 0 warnings, 0 errors.
- **Frontend**: page compiles into the existing Next.js app; no new dependencies.
- **Workflow restart**: green; Flow.Api responds on `:5012`.
- **Auth smoke** (no token):
  - `GET /api/v1/product-workflows/synqlien` → **401** ✅
  - `GET /api/v1/product-workflows/careconnect` → **401** ✅
  - `GET /api/v1/product-workflows/synqfund` → **401** ✅
  - `GET /api/v1/workflows` (existing) → **401** ✅ (no regression)
  - `GET /health` → **200** ✅
- **EF migration**: `20260417031705_AddProductWorkflowMappingsP3` (regenerated after the round-1 fix; contains `CreateTable` for `flow_product_workflow_mappings` plus all indexes and FKs; snapshot updated). Application of the migration requires the deployed MySQL instance — the Replit dev environment does not host a local MySQL. The deployed runbook step `dotnet ef database update --project src/Flow.Infrastructure --startup-project src/Flow.Api` is documented in `merge-phase-3-notes.md`.

## Code Review Round 1 — Fixes Applied

The first architect review surfaced five real issues; all have been fixed:

| # | Finding | Fix |
|---|---------|-----|
| 1 | Generated EF migration was empty (snapshot did not include `ProductWorkflowMapping`) — the original `dotnet ef migrations add` ran against a stale build before the entity wiring compiled. | Deleted the empty files and regenerated against a clean build → `20260417031705_AddProductWorkflowMappingsP3.{cs,Designer.cs}` (now contains the `CreateTable` + indexes + FKs; snapshot now includes `ProductWorkflowMapping`). |
| 2 | `POST /api/v1/product-workflows/{product}` would 500 because `CreatedAtAction` pointed at a `[NonAction]` helper. | Replaced with `Created($"/api/v1/product-workflows/{slug}/{result.Id}", result)` and a static `ProductSlugFor(productKey)` mapper. |
| 3 | `TaskService.UpdateStatusAsync` workflow-transition branch returned early without emitting `TaskCompletedEvent` when the resulting status was `Done`. | Added the same `TaskCompletedEvent` emit immediately after `SaveChangesAsync` inside the workflow-transition branch. |
| 4 | `ProductWorkflowService.CreateAsync` saved the task and the mapping in two separate `SaveChangesAsync` calls — a mapping failure could orphan a task. | Wrapped both in a single transaction via `IExecutionStrategy.ExecuteAsync` + `BeginTransactionAsync` (compatible with the MySQL retry strategy). |
| 5 | Capability policies allowed access whenever the token had no permission claims at all — a fail-open path that would leak in production. | Gated the "no permissions" fallback behind `builder.Environment.IsDevelopment()`; in non-dev environments, the policy now requires either the permission claim or product-role access on the matching `ProductCode`. |

Post-fix smoke (no token):
- `/health` → **200**
- `/api/v1/product-workflows/synqlien` → **401**
- `/api/v1/product-workflows/careconnect` → **401**
- `/api/v1/product-workflows/synqfund` → **401**
- `/api/v1/workflows` → **401** (no regression)

Build: 0 warnings / 0 errors.

## Known Issues / Gaps

- **Local DB unavailable.** The Replit dev container has no MySQL; full DB-touching smoke (POST a product workflow, then GET) was not run here. The migration must be applied in the deploy pipeline before product clients call POST.
- **Workflow-instance grain.** `WorkflowInstanceTaskId` is a `TaskItem` id today. A dedicated workflow-instance entity is a Phase 4 candidate.
- **Product → Flow only.** Mapping flow is one-directional; no callbacks into SynqLien/CareConnect/SynqFund product services from Flow yet.
- **Capability claim shape.** Permission claim strings (`SYNQ_LIENS.lien:sell`, `SYNQ_FUND.application:refer`) are best-guess based on existing `PermissionCodes` patterns — they should be confirmed and centralised in `BuildingBlocks.PermissionCodes` next phase.
- **`Flow.Api` static product list.** Adding a new product requires a new policy + controller route group; acceptable trade-off for explicit per-product authz.

## Recommendation

Phase 3 is shipped on the Flow side. **Next steps**, in order of priority:

1. Apply the EF migration in deployed environments.
2. Centralise the new permission claim constants in `BuildingBlocks.PermissionCodes` and replace the inline strings in `Flow.Api/Program.cs`.
3. Build the product-side adapters (SynqLien → `POST /product-workflows/synqlien`, CareConnect → `…/careconnect`, SynqFund → `…/synqfund`).
4. Plan Phase 4: dedicated `WorkflowInstance` entity and bidirectional product↔flow callbacks.
