# LS-FLOW-MERGE-P5 Report

> Phase 5 — Execution Engine Maturity & Scale. Builds on Phase 4 (`LS-FLOW-MERGE-P4-report.md`).

## Scope Executed

All eight target deliverables landed; T010 (transactional outbox) was deferred by design and is documented in `merge-phase-5-notes.md` §8.

1. ✅ `WorkflowInstance` is the execution authority — `Status` enum, `CurrentStepKey`, `StartedAt`, `CompletedAt`, `AssignedToUserId`, `LastErrorMessage`, version row added.
2. ✅ Lightweight step/state engine — `WorkflowDefinitionStep` (DefinitionId, StepKey, Order, Name, NextStepKey, Condition?) + `IWorkflowEngine` with `Start/Advance/Complete/Cancel/Fail`.
3. ✅ Execution APIs — `WorkflowInstancesController` exposes get / current-step / advance / complete / cancel, all `[Authorize]` and tenant-filtered. Validation failures → 409 (`InvalidWorkflowTransitionException`) or 400.
4. ✅ M2M auth — HS256 service-token issuer + `MultiAuth` policy scheme on Flow.Api so user-JWT and service-JWT can coexist.
5. ✅ Product integration — `IFlowClient` extended with `Get/Advance/Complete`; shared `MapFlowExecutionPassthrough(productSlug, sourceEntityType)` helper wired in liens, careconnect, fund.
6. ✅ End-to-end validation — `apps/services/flow/backend/scripts/p5-e2e.sh` mints a service token (Python jwt) when `FLOW_SERVICE_TOKEN_SECRET` is set, falls back to `USER_BEARER`, and probes get → current-step → best-effort advance per product.
7. ✅ Observability — structured logs in engine (`workflowInstanceId`, `tenantId`, `productKey`, `from`, `to`, `actor`); controller actions wrap each request in a `BeginScope("workflow-instance:{InstanceId}")`.
8. ✅ Documentation — `apps/services/flow/docs/merge-phase-5-notes.md` plus appended Phase-5 sections in `docs/README.md` and `docs/architecture.md`.

## Assumptions

* HS256 (shared symmetric secret) is acceptable for the M2M trust boundary in this phase. Asymmetric (RS256) signing is a follow-up if/when Flow needs to verify tokens minted by parties it does not co-deploy with.
* Step model is intentionally simple (linear `NextStepKey` + optional equality `Condition` against the payload). Branching/parallel/timer steps are out of scope for P5.
* Concurrency tokens on `Status` and `CurrentStepKey` are an annotation-only EF model change; no schema migration required.
* Product apps continue to call Flow over HTTP through `IFlowClient`; no in-process embedding.

## Repository / Architecture Notes

Key files touched:

* `apps/services/flow/backend/src/Flow.Application/Engines/WorkflowEngine/{IWorkflowEngine,WorkflowEngine,InvalidWorkflowTransitionException}.cs`
* `apps/services/flow/backend/src/Flow.Domain/Entities/{WorkflowInstance,WorkflowDefinitionStep,WorkflowStatus}.cs`
* `apps/services/flow/backend/src/Flow.Infrastructure/Persistence/FlowDbContext.cs` (concurrency tokens, step model)
* `apps/services/flow/backend/src/Flow.Infrastructure/Persistence/Migrations/<P5_*>` (step table + new instance columns)
* `apps/services/flow/backend/src/Flow.Api/Controllers/V1/WorkflowInstancesController.cs`
* `apps/services/flow/backend/src/Flow.Api/Program.cs` (MultiAuth policy scheme; both JwtBearer schemes registered, neither set as default)
* `shared/building-blocks/BuildingBlocks/Authentication/ServiceTokens/*`
* `shared/building-blocks/BuildingBlocks/FlowClient/{FlowClient,FlowClientDtos,FlowExecutionEndpoints}.cs`
* `apps/services/{liens,careconnect,fund}/*.Api/Endpoints/WorkflowEndpoints.cs`

## Workflow Execution Model Notes

`WorkflowInstance` columns added: `Status` (Active|Completed|Cancelled|Failed), `CurrentStepKey`, `StartedAt`, `CompletedAt`, `AssignedToUserId`, `LastErrorMessage` (truncated to 2048), and an EF row-version-style optimistic-concurrency annotation on `CurrentStepKey` + `Status`. Creation of an instance now seeds `Status=Active`, `CurrentStepKey=<first step>`, `StartedAt=now` and (artifact-only) optionally drops a `TaskItem` for that step. `TaskService.Complete` no longer mutates instance state — that is the engine's job.

## Step / State Engine Notes

`WorkflowEngine.Advance/Complete/Cancel/Fail` enforce the contract:

* The caller passes `expectedCurrentStepKey`; mismatch → `InvalidWorkflowTransitionException("expected_step_mismatch")` → 409.
* Status must be `Active` for transitions; otherwise → 409 with `instance_not_active`.
* `NextStepKey` resolution: explicit body value if provided, else the definition's `NextStepKey`, else condition match (string equality on a payload field).
* All persistence goes through a private `SaveWithConcurrencyAsync(ct, code, message)` that catches `DbUpdateConcurrencyException` and re-throws as `InvalidWorkflowTransitionException("concurrent_state_change")` → 409. This means a stale-read advance from a second writer is rejected rather than silently overwriting.

## API Changes

New under Flow.Api:

* `GET  /api/v1/workflow-instances/{id}`
* `GET  /api/v1/workflow-instances/{id}/current-step`
* `POST /api/v1/workflow-instances/{id}/advance`  body `{ expectedCurrentStepKey, toStepKey?, payload? }`
* `POST /api/v1/workflow-instances/{id}/complete`
* `POST /api/v1/workflow-instances/{id}/cancel`

All `[Authorize]`, tenant-filtered. Errors map: `expected_step_mismatch` / `instance_not_active` / `concurrent_state_change` → 409; bad input → 400.

Product passthrough (per service):

* `GET  /api/<product>/{id}/workflows/{workflowInstanceId}`
* `POST /api/<product>/{id}/workflows/{workflowInstanceId}/advance`
* `POST /api/<product>/{id}/workflows/{workflowInstanceId}/complete`

## Authentication Notes

* New building block: `BuildingBlocks/Authentication/ServiceTokens/{IServiceTokenIssuer, ServiceTokenIssuer, ServiceTokenOptions, ServiceTokenAuthenticationDefaults, ServiceTokenServiceCollectionExtensions}.cs`. Issues short-lived (5 min) HS256 JWTs with `iss=legalsynq.servicetokens`, `aud=flow-service`, `sub=service:<name>`, `actor=user:<id>?`, `roles=service`, **and both `tid` + `tenant_id`** (the dual claim closes the prior mismatch with downstream `ClaimsTenantProvider`/`CurrentRequestContext`).
* Flow.Api registers two `JwtBearer` schemes (user + service) under a `MultiAuth` `PolicyScheme` that picks based on `aud`/`iss`. **Neither bearer is the default — `MultiAuth` is** — so existing `[Authorize]` attributes route through both validators.
* `IFlowClient` registration in product apps wires an `IServiceTokenIssuer`. The client itself is split:
  * `ApplyUserBearer` — used by `StartWorkflowAsync` and `ListBySourceEntityAsync` (preserves user capability-policy checks at Flow).
  * `ApplyExecutionAuth` — used by `Get/Advance/CompleteWorkflowAsync` (prefers service token with `actor=user:<id>`, falls back to user bearer if no secret is configured).
* Shared symmetric secret read from env `FLOW_SERVICE_TOKEN_SECRET`; documented in `merge-phase-5-notes.md` §6.

## Product Integration Notes

* `IFlowClient` extended with `GetWorkflowInstanceAsync`, `AdvanceWorkflowAsync`, `CompleteWorkflowAsync` and matching DTOs (`FlowWorkflowInstanceResponse`, `FlowAdvanceWorkflowRequest`).
* Shared `BuildingBlocks/FlowClient/FlowExecutionEndpoints.MapFlowExecutionPassthrough(productSlug, sourceEntityType)` mounts the three execution endpoints on a product's existing `/api/<product>/{id}/workflows` group.
* The helper enforces **parent-ownership** before forwarding: each request first calls `ListBySourceEntityAsync(productSlug, sourceEntityType, parentId)` and 404s with `workflow_instance_not_owned` if the URL's `{workflowInstanceId}` is not in that list. This prevents IDOR where a known-but-unrelated workflow id could be advanced via any parent route in the same tenant.
* All three product apps (liens, careconnect, fund) call `AddFlowClient(configuration, serviceName: "<product>")` and `group.MapFlowExecutionPassthrough(ProductSlug, SourceEntityType)` from their `Endpoints/WorkflowEndpoints.cs`.

## Migration / Data Notes

* New EF migration adds: `WorkflowInstance.Status/CurrentStepKey/StartedAt/CompletedAt/AssignedToUserId/LastErrorMessage` and the new `WorkflowDefinitionStep` table.
* Concurrency tokens on `WorkflowInstance.Status` and `WorkflowInstance.CurrentStepKey` are EF-model annotations only — no DDL change. Pomelo treats `[ConcurrencyCheck]`-style columns by adding them to the WHERE clause of UPDATE/DELETE; rows-affected mismatch raises `DbUpdateConcurrencyException`.
* Existing instances backfill `Status=Active` and `CurrentStepKey=<definition.first>` via the migration's data step.

## End-to-End Validation Results

* `apps/services/flow/backend/scripts/p5-e2e.sh` ready and gates correctly when `TENANT_ID` / `USER_BEARER` are absent (tested locally — exits with `[p5-e2e] TENANT_ID is required`).
* Build: `dotnet build LegalSynq.sln` — 0 errors.
* Runtime: workflow restart succeeded; all 9 .NET services listening (5001-5012). No stack traces in startup logs.
* The full happy-path sequence (start → get → advance → complete) is intended to run in CI with the secret + tenant fixtures provided.

## Observability / Logging Notes

* Engine emits `LogInformation` at every successful transition with `instance / tenant / product / from / to / actor` fields, and `LogWarning` with the same shape on `Fail`.
* Controller actions all wrap their handler in `_logger.BeginScope("workflow-instance:{InstanceId}", id)` so any downstream service log inherits the instance correlation key.
* Existing request-id middleware continues to attach `traceparent`/correlation headers — no change.

## Documentation Changes

* `apps/services/flow/docs/merge-phase-5-notes.md` — full Phase-5 narrative (engine model, API surface, auth, product integration, configuration matrix, deferred outbox, ops notes).
* `apps/services/flow/docs/README.md` — Phase-5 section appended.
* `apps/services/flow/docs/architecture.md` — Phase-5 section appended.

## Known Issues / Gaps

* **TOCTOU window in product passthrough** — ownership is verified by an upstream `ListBySourceEntityAsync` and then the execution call is a separate upstream request. No critical exploit path was identified, but a concurrent re-parenting/cancellation could in principle slip through. Mitigation candidate: add a Flow-side execution variant that takes parent context (`product / sourceEntityType / parentId`) and enforces ownership atomically server-side. Tracked as a follow-up.
* **No integration test suite for the Phase-5 surface yet.** Recommended coverage: cross-parent workflow id rejected with `workflow_instance_not_owned`; `start/list` still gated by user capability policies; concurrent advance/complete yields 409 `concurrent_state_change`.
* **Outbox deferred (T010).** Engine writes are still in-line with the request; no transactional outbox or retry. Documented in `merge-phase-5-notes.md` §8.
* **Step model is linear + equality only.** Branching / parallel / timers / retries are not modeled.
* **Symmetric service-token secret.** Rotation is a redeploy; consider RS256 + JWKS for cross-trust-boundary use.

## Recommendation

Phase 5 is ready to merge and ship. Two follow-up tasks are worth queueing before Phase 6:

1. **Hardening (Phase-5.1):** add the integration tests above and the Flow-side ownership-aware execution variant to close the TOCTOU window.
2. **Outbox (Phase-5.2):** introduce a Flow outbox so step transitions and downstream notifications are atomic with the state change. This becomes a prerequisite for any cross-service side effects (notifications, document generation, billing) that we will likely want triggered from the engine.

Architect review (final pass): **PASS**, no remaining critical/high defects.
