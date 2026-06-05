# LS-FLOW-MERGE Phase 5 — Execution Engine Maturity & Scale

This phase moves WorkflowInstance from a passive correlation row to the
**execution authority** for a workflow. It introduces a small step/state
engine, a public execution surface (`/api/v1/workflow-instances/...`),
HS256 service-token auth for service-to-service calls, and matching
passthrough endpoints in the three product services.

Phase 4 (the IFlowClient bridge) is unchanged in shape; Phase 5 *extends*
that surface rather than replacing it.

---

## 1. Domain — `WorkflowInstance` is the execution row

`Flow.Domain/Entities/WorkflowInstance.cs` adds:

| Column                | Purpose                                                              |
|-----------------------|----------------------------------------------------------------------|
| `CurrentStageId`      | Foreign key to the active `WorkflowStage` (the existing graph node). |
| `CurrentStepKey`      | Mirrors `WorkflowStage.Key` for fast lookup and concurrency checks.  |
| `StartedAt`           | Set by `WorkflowEngine.StartAsync` on the first transition.          |
| `AssignedToUserId`    | Optional execution-level assignment (separate from per-task assign). |
| `LastErrorMessage`    | Last failure reason recorded via `FailAsync`.                        |

EF migration: `20260417042541_AddWorkflowInstanceExecutionStateP5`
(produced after a clean rebuild of `Flow.Api` — see *Operational
gotchas* below). Existing instances get NULL `CurrentStepKey` and
remain readable; the engine treats a NULL `CurrentStepKey` on an
`Active` instance as "not yet started" and `StartAsync` is idempotent
on it.

`Status` continues to use the existing `WorkflowInstance.Status` enum
(`Active`, `Completed`, `Cancelled`, `Failed`).

---

## 2. Engine — `IWorkflowEngine` / `WorkflowEngine`

Location: `Flow.Application/Engines/WorkflowEngine/`.

* `StartAsync(instanceId)` — moves an `Active` instance with no current
  step to the *initial* stage of its `WorkflowDefinition` (the lowest
  `Order` stage). Idempotent; returns the current state if the
  instance is already started.
* `AdvanceAsync(instanceId, expectedCurrentStepKey, toStepKey?, payload?)`
  — validates `expectedCurrentStepKey` against the persisted
  `CurrentStepKey` (optimistic concurrency). If `toStepKey` is omitted
  the engine picks the next stage by following the existing
  `WorkflowTransition` graph (`Order` ascending; the first transition
  whose simple `ConditionExpression` evaluates true on the supplied
  payload, with no condition = always-true). Updates
  `CurrentStageId`/`CurrentStepKey` and writes a `WorkflowEvent` for
  the transition.
* `CompleteAsync(instanceId)` — terminal Completed state, sets
  `CompletedAt`.
* `CancelAsync(instanceId, reason)` — terminal Cancelled state.
* `FailAsync(instanceId, errorMessage)` — terminal Failed state, sets
  `LastErrorMessage`.

Failure modes throw `InvalidWorkflowTransitionException` with a stable
machine-readable `Code` (`expected_step_mismatch`, `terminal_state`,
`no_initial_stage`, `no_outgoing_transition`, `unknown_target_step`).
The controller maps that to **HTTP 409**.

The engine reuses Phase-3's `WorkflowStage`/`WorkflowTransition` graph;
no parallel "definition step" table was added — `CurrentStepKey` ==
`WorkflowStage.Key` so the existing definition authoring UI is
already the source of truth.

`ProductWorkflowService.CreateAsync` calls `engine.StartAsync` after
saving the new instance and gracefully no-ops if the definition has
no initial stage (so legacy or partially-authored definitions keep
working).

---

## 3. API — `WorkflowInstancesController`

`Flow.Api/Controllers/V1/WorkflowInstancesController.cs` exposes:

| Verb | Route                                                       | Status codes                  |
|------|-------------------------------------------------------------|-------------------------------|
| GET  | `/api/v1/workflow-instances/{id}`                           | 200 / 404                     |
| GET  | `/api/v1/workflow-instances/{id}/current-step`              | 200 / 404                     |
| POST | `/api/v1/workflow-instances/{id}/advance`                   | 200 / 400 / 404 / 409         |
| POST | `/api/v1/workflow-instances/{id}/complete`                  | 200 / 404 / 409               |
| POST | `/api/v1/workflow-instances/{id}/cancel`                    | 200 / 400 / 404 / 409         |

All routes require auth (the `MultiAuth` policy scheme — see §4) and
filter by tenant. DTOs: `WorkflowInstanceResponse`,
`WorkflowInstanceCurrentStepResponse`, `AdvanceWorkflowRequest`,
`CancelWorkflowRequest`.

---

## 4. Machine-to-machine auth — service tokens

New shared library: `BuildingBlocks/Authentication/ServiceTokens/`.

* `IServiceTokenIssuer` / `ServiceTokenIssuer` — HS256 JWT, 5-minute
  TTL. Claims: `sub=service:<name>`, `aud=flow-service`,
  `tid=<tenantId>` (also `tenant_id`), `role=service`, optional
  `actor=user:<id>` for auditability.
* `AddServiceTokenIssuer(configuration, serviceName)` — binds
  `ServiceTokenOptions` from the `ServiceToken` config section.
  Signing key is read from env `FLOW_SERVICE_TOKEN_SECRET`
  (configuration key `ServiceToken:SigningKey`).
* `AddServiceTokenBearer(...)` — registers a JwtBearer scheme named
  `ServiceToken` that validates the issuer/audience/key.

`Flow.Api/Program.cs` defines a `MultiAuth` PolicyScheme as the
default. It inspects the inbound JWT's `aud` claim: tokens with
`aud == "flow-service"` are forwarded to the `ServiceToken` scheme,
all other tokens to the user `Bearer` scheme. Both schemes produce a
`ClaimsPrincipal`; the existing `AuthenticatedUser` policy
(`RequireAuthenticatedUser`) accepts either, so no per-endpoint
changes were required.

`FlowClient` (BuildingBlocks) prefers a freshly-minted service token
when `IServiceTokenIssuer.IsConfigured` and the caller has a tenant
claim, forwarding the user id as `actor`. Falls back to bearer
pass-through otherwise — dev and test setups without the secret keep
working.

---

## 5. Product integration

`IFlowClient` gains:

* `GetWorkflowInstanceAsync(workflowInstanceId)`
* `AdvanceWorkflowAsync(workflowInstanceId, FlowAdvanceWorkflowRequest)`
* `CompleteWorkflowAsync(workflowInstanceId)`

Each product (synqlien, careconnect, synqfund) registers
`AddFlowClient(configuration, serviceName: "<product>")` in
`Program.cs` and calls
`group.MapFlowExecutionPassthrough(productSlug, sourceEntityType)` from
its `Endpoints/WorkflowEndpoints.cs`. The helper enforces
**parent-ownership** before forwarding: it asks Flow for the workflows
mapped to `{id}` and returns 404 (`workflow_instance_not_owned`) if the
URL's `{workflowInstanceId}` is not in that list. This prevents a
known-but-unrelated workflow id from being driven through any parent
route in the same tenant. That helper (in
`BuildingBlocks/FlowClient/FlowExecutionEndpoints.cs`) adds:

* `GET  ./{workflowInstanceId:guid}`
* `POST ./{workflowInstanceId:guid}/advance`
* `POST ./{workflowInstanceId:guid}/complete`

so e.g. SynqLien clients can call
`POST /api/liens/cases/{caseId}/workflows/{wfId}/advance` without ever
touching Flow directly.

---

## 6. Configuration

| Env / config key                       | Used by                | Notes                                                   |
|----------------------------------------|------------------------|---------------------------------------------------------|
| `FLOW_SERVICE_TOKEN_SECRET`            | Flow.Api + product apps | Shared HS256 key. Identical across all participants.    |
| `ServiceToken:Issuer` (default ok)     | issuer + validator     | `legalsynq.servicetokens`                               |
| `ServiceToken:Audience` (default ok)   | issuer + validator     | `flow-service`                                          |
| `ServiceToken:LifetimeSeconds`         | issuer                 | Default 300.                                            |

If `FLOW_SERVICE_TOKEN_SECRET` is unset the issuer reports
`IsConfigured = false`, the FlowClient falls back to bearer
pass-through, and the Flow.Api validator simply has no `ServiceToken`
scheme registered — user bearers still work end-to-end.

---

## 7. Observability

* `WorkflowEngine` logs at INFO with structured properties
  `workflowInstanceId`, `tenantId`, `productKey`, `from`, `to`,
  `actor` for every transition; failures log at WARN with the
  `Code`.
* `WorkflowInstancesController` adds a per-action log scope so the
  engine logs are correlated to the inbound request.
* `FlowClient` logs every outbound call with method/URI and the
  upstream status; transport failures become
  `FlowClientUnavailableException` (mapped by the product endpoints
  to 503 via `FlowEndpointResults.MapFailure`).

---

## 8. Outbox — deferred

A transactional outbox for transition events was scoped but **not**
implemented in this phase. Today the engine writes a `WorkflowEvent`
in the same DbContext save as the instance update — atomic with
respect to MySQL but not delivered to external subscribers. Phase 6
is the natural home for an outbox + relay; revisit once a concrete
subscriber (notifications? reporting?) lands.

---

## 9. End-to-end validation

Script: `apps/services/flow/backend/scripts/p5-e2e.sh`. Drives a real
running instance via the gateway — see the script header for required
env vars. Auth modes: minted service token (when
`FLOW_SERVICE_TOKEN_SECRET` is exported) or pass-through `USER_BEARER`.

Probes per product: GET → current-step → best-effort advance.

---

## 10. Operational gotchas

* **Stale-DLL on `dotnet ef migrations add`.** With `--no-build`, EF
  loads the *previously*-built `Flow.Infrastructure` DLL — entity
  changes that aren't reflected there silently produce empty Up/Down
  bodies. Workflow that worked: `pkill -f 'dotnet.*\.Api'` →
  `dotnet build src/Flow.Api/Flow.Api.csproj` → `pkill` again →
  `dotnet ef migrations add ... --no-build`.
* **Two JwtBearer schemes.** Don't make either of them the *default*.
  The default is `MultiAuth` (the PolicyScheme), which dispatches by
  audience. Setting either bearer as default re-routes user tokens
  through the wrong validator and you'll see 401s on previously
  working endpoints.
