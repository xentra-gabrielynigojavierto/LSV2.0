# LS-FLOW-HARDEN-A1 Report

> Track A hardening of the Phase-5 Flow execution engine. Builds on
> `analysis/LS-FLOW-MERGE-P5-report.md`.

## Scope Executed

- **T01** ✅ Inspect current Phase-5 ownership flow and identify the exact TOCTOU path.
- **T02** ✅ Create report skeleton and session plan.
- **T03** ✅ Add Flow-side atomic ownership-aware execution endpoints under `/api/v1/product-workflows/{product}/{sourceEntityType}/{sourceEntityId}/{workflowInstanceId}` (get / advance / complete).
- **T04** ✅ Update product passthroughs to call the new atomic endpoints; remove the pre-check + second-call pattern.
- **T05** ✅ Tighten service-token validation (issuer, audience, sub, tenant claim, startup config validation in non-dev).
- **T06** ✅ Standardize execution / ownership / auth error codes (`FlowErrorCodes`) and structured per-request log scopes.
- **T07** ⚠️ Partial — `Flow.UnitTests` project added with high-value unit coverage (caller classification + service-token startup guard, **9/9 pass**). The full `WebApplicationFactory<Program>` integration matrix is deferred to Phase A1.1 — see "Known Issues / Gaps".
- **T08** ⚠️ Smoke-only — the new atomic route returns `401` without auth and `401` with bogus bearer, the legacy execution route is unaffected. A live happy-path run through SynqLien / CareConnect / SynqFund requires seeded fixtures (tenant, user JWT, parent records) that are not present in this container; precise blocker recorded below.
- **T09** ✅ Docs updated (`merge-phase-A1-notes.md`, README A1 section, architecture A1 section).
- **T10** ✅ Build green; Flow.Api healthy on its dev port (`/healthz` 200); architect review run on the diff.

## Repository / Architecture Notes

### TOCTOU path that this track closes

Phase-5 product passthroughs (e.g. liens / careconnect / fund `WorkflowEndpoints.cs` →
`BuildingBlocks/FlowClient/FlowExecutionEndpoints.MapFlowExecutionPassthrough`) called Flow twice for every advance / complete:

1. `IFlowClient.ListBySourceEntityAsync(productSlug, sourceEntityType, sourceEntityId)` to confirm
   the workflow instance was correlated to the route's parent entity.
2. `IFlowClient.AdvanceWorkflowAsync(workflowInstanceId, …)` to actually execute the transition.

Between (1) and (2):

- The `ProductWorkflowMappings` row could be deleted, re-pointed, or updated by another caller.
- A different process (or even another in-flight request from the same caller) could mutate the
  instance to a state the pre-check did not anticipate.
- Network latency between the product process and Flow widened the window.

Only the second call (executed inside `WorkflowEngine.AdvanceAsync`) opened a transaction. The
ownership check was therefore non-atomic with the state change it was guarding. Because the
ownership decision lived in the **product** process while the state change lived in **Flow**, no
local DB transaction could span both.

## Atomic Ownership Enforcement Notes

`Flow.Api/Controllers/V1/ProductWorkflowExecutionController.cs` exposes:

| Verb / suffix    | Behaviour |
| ---------------- | --------- |
| `GET`            | Returns the instance state. |
| `POST /advance`  | Applies an engine transition with `expectedCurrentStepKey` (+ optional `toStepKey`). |
| `POST /complete` | Marks the instance complete via the engine. |

Each handler resolves the instance through one EF query that joins
`ProductWorkflowMappings` against `WorkflowInstances`, filtered by `(WorkflowInstanceId,
ProductKey, SourceEntityType, SourceEntityId)`. The `FlowDbContext` tenant query filter applies
to both tables, so cross-tenant reads are impossible at the query layer. Mismatch on **any** axis
returns `404 { code: "workflow_instance_not_owned" }` — uniform output regardless of which axis
failed (no information disclosure).

The controller is decorated with `[Authorize(Policy = Policies.AuthenticatedUser)]` (catches both
the user `Bearer` and `ServiceToken` schemes via the existing `MultiAuth` policy scheme). For
**user** callers it then calls `IAuthorizationService.AuthorizeAsync` with the per-product
capability policy (`CanSellLien` / `CanReferCareConnect` / `CanReferFund`). For **service** callers
it skips the per-product capability check (service tokens cannot satisfy permission claims, and
the originating product service has already enforced them); tenant scoping and parent ownership
remain non-bypassable.

## Product Passthrough Changes

- `IFlowClient` gained `GetProductWorkflowAsync` / `AdvanceProductWorkflowAsync` /
  `CompleteProductWorkflowAsync`, each taking `(productSlug, sourceEntityType, sourceEntityId,
  workflowInstanceId, …)`.
- `FlowExecutionEndpoints.MapFlowExecutionPassthrough` rewritten to call those new methods
  directly. The two-call pattern is gone; product processes no longer perform their own ownership
  pre-check (Flow is now the single source of truth at the request granularity).
- `IFlowClient.ListBySourceEntityAsync` is retained for legitimate list use cases but is no
  longer used by passthroughs.
- Product `WorkflowEndpoints` callers in `synqliens` / `careconnect` / `synqfund` did not change
  shape — the helper signature is identical.

## Authentication / Security Hardening Notes

`shared/building-blocks/BuildingBlocks/Authentication/ServiceTokens/ServiceTokenServiceCollectionExtensions.cs`:

- `RequireSignedTokens = true` (no `alg=none`).
- `RequireExpirationTime = true`.
- `ClockSkew = 30s` (tight; bounds replay).
- `OnTokenValidated` rejects tokens that lack a `service:*` `sub` or any of `tenant_id` / `tid`.
  Failures log `code=invalid_service_token` / `missing_tenant_context`.
- `failFastIfMissingSecret` parameter — when true, throws on startup with a clear error if the
  HS256 secret is missing or shorter than 32 chars. Flow.Api wires this as
  `!builder.Environment.IsDevelopment()`.

`shared/building-blocks/BuildingBlocks/Authentication/ServiceTokens/CallerContext.cs`:

- `enum CallerType { Unknown, User, Service, ServiceOnBehalfOfUser }`.
- `record CallerContext(Type, TenantId, Subject, Actor)`.
- `ICallerContextAccessor` + `CallerContextAccessor` (reads `IHttpContextAccessor`,
  classifies based on `sub` prefix + presence of the `actor` claim).

Flow.Api `Program.cs` registers the accessor and wires the startup guard.

## Diagnostics / Error Handling Notes

`shared/building-blocks/BuildingBlocks/FlowClient/FlowErrorCodes.cs`:

```
workflow_instance_not_owned    expected_step_mismatch       instance_not_active
concurrent_state_change        flow_unavailable             flow_upstream_error
invalid_service_token          missing_tenant_context
```

The new controller wraps each request in a log scope carrying `product`, `entityType`,
`entityId`, `instanceId`, `callerType`, `tenantId`, `subject`, `actor`. Engine exceptions
(`InvalidWorkflowTransitionException`) surface as `409` with their existing structured `code`
field (already aligned with `expected_step_mismatch` / `instance_not_active` /
`concurrent_state_change`).

## Integration Test Coverage

Project: `apps/services/flow/backend/tests/Flow.UnitTests` (xUnit + project ref to
`BuildingBlocks`).

| Suite                              | Cases | Outcome |
| ---------------------------------- | ----- | ------- |
| `CallerContextAccessorTests`       | 5     | ✅ pass  |
| `ServiceTokenStartupGuardTests`    | 4     | ✅ pass  |

`dotnet test` summary: **Failed: 0, Passed: 9, Skipped: 0, Total: 9**.

Sufficient-secret happy path, missing-secret throw, short-secret throw, dev-mode no-throw,
anonymous → Unknown, user → User, service → Service, service-with-actor →
ServiceOnBehalfOfUser, `tid` accepted as tenant fallback — all asserted.

## End-to-End Validation Results

Smoke against the running Flow.Api dev port:

| Probe                                                                  | Status |
| ---------------------------------------------------------------------- | ------ |
| `GET /api/v1/product-workflows/synqlien/lien_case/<guid>/<guid>`       | `401`  |
| Same with `Authorization: Bearer bogus`                                | `401`  |
| Legacy `GET /api/v1/workflow-instances/<guid>` (regression)            | `401`  |

A full positive happy-path through SynqLien → CareConnect → SynqFund could not be executed in
this environment. Precise blocker:

- `apps/services/flow/backend/scripts/p5-e2e.sh` requires `TENANT_ID` and `USER_BEARER` env vars.
  These represent a tenant id seeded against the dev RDS plus a valid user JWT minted by Identity.
- The container does not have either fixture, and the seed scripts are not part of the dev
  workflow. Standing them up requires a tenant provisioning run in Identity (which depends on the
  full Identity / RDS / S3 chain being primed).
- The product side also needs concrete parent records (`lien_case`, `referral`, `fund_application`)
  whose ids were correlated to live `ProductWorkflowMappings` rows.

When those fixtures exist the script will exercise the new atomic endpoints unchanged, because the
product passthroughs now route through them on every advance / complete.

## Documentation Changes

- New: `apps/services/flow/docs/merge-phase-A1-notes.md` — full A1 changelog.
- Appended: `apps/services/flow/docs/README.md` — A1 summary section.
- Appended: `apps/services/flow/docs/architecture.md` — atomic ownership layer section.

## Known Issues / Gaps

- **Phase A1.1 — full integration matrix.** A `WebApplicationFactory<Program>` rig for Flow.Api
  needs to override `FlowDbContext` (Postgres → InMemory or SQLite), the audit / notification
  adapters, and the multi-auth scheme (with a `TestAuth` handler that injects a deterministic
  principal). Once that scaffolding lands, the full matrix from the task brief — cross-parent
  ownership, cross-tenant isolation, capability denial, multi-auth rejection, stale-step,
  inactive-instance, concurrent-advance, product-correlation mismatch — should be straightforward.
  Track A delivers the unit-level guarantees that the new helpers behave correctly; the integration
  matrix would assert end-to-end wiring.
- **E2E fixtures.** Same blocker as Phase 5 — the local container lacks seeded tenants / users /
  parent records.
- **HS256 → JWKS.** Service-token signing is still HS256 with a shared secret. RS256 + JWKS
  rotation remains explicitly out of scope for Track A.
- **Outbox.** `WorkflowEvent` rows are still emitted in the same DbContext save (atomic in
  Postgres but not yet relayed). Unchanged from Phase 5.

## Recommendation

Track A delivers the security-critical fix the merge brief called out: the cross-tenant /
cross-parent ownership window between the product pre-check and the Flow execution call is
closed by construction. Service-token validation now refuses tokens that don't carry a tenant
claim or aren't service-issued, and non-Development hosts crash on startup if their secret is
missing or weak. Diagnostics are standardized.

The recommended next track is **Phase A1.1** — stand up the `WebApplicationFactory` integration
suite so the contract is enforced by CI, and prepare the E2E fixture story so a positive
happy-path run can be re-recorded in a controlled environment.
