# Flow — Phase A1 (Hardening) Notes

> Track A hardening of the Phase-5 Flow execution engine. Builds on
> `merge-phase-5-notes.md`. Companion: `analysis/LS-FLOW-HARDEN-A1-report.md`.

## TL;DR

| Concern (Phase-5 status)                                  | Phase-A1 outcome |
| --------------------------------------------------------- | ---------------- |
| Two-step ownership check (TOCTOU window between calls)    | Single atomic DB join inside Flow |
| Product passthroughs called Flow twice per advance        | One round-trip; pre-check removed |
| Service tokens validated only by signature/issuer/audience| Plus `service:` subject + tenant claim required |
| No startup guard on missing service-token secret in prod  | `failFastIfMissingSecret` wired in non-Dev |
| Ad-hoc error strings across the surface                   | `FlowErrorCodes` constants used consistently |
| Per-request correlation context implicit                  | `ICallerContextAccessor` exposes `CallerType`/tenant/sub/actor |

## 1. Atomic Ownership Model

### New endpoints
Route prefix: `/api/v1/product-workflows/{product}/{sourceEntityType}/{sourceEntityId}/{workflowInstanceId:guid}`

| Verb / suffix      | Purpose |
| ------------------ | ------- |
| `GET`              | Current state of the workflow instance |
| `POST /advance`    | Atomic transition (with `expectedCurrentStepKey`, optional `toStepKey`) |
| `POST /complete`   | Mark workflow complete |

`{product}` slug is one of `synqlien` / `careconnect` / `synqfund` and maps
internally to the corresponding `ProductKeys.*`. Unknown slug → 404
`workflow_instance_not_owned` (uniform with mismatch — no info disclosure).

### One DB read

Every request resolves the instance through a single EF query that joins
`ProductWorkflowMappings` against `WorkflowInstances`, filtered by:

1. **Tenant** — implicit, applied by `FlowDbContext`'s tenant query filter
   for both tables.
2. **Product key** — derived from the route slug.
3. **Source entity** — exact (`SourceEntityType`, `SourceEntityId`) match.
4. **Workflow instance id** — must equal the mapping's `WorkflowInstanceId`.

A mismatch on any axis returns `404 { code: "workflow_instance_not_owned" }`.
This closes the TOCTOU window the Phase-5 product-side pre-check left open
(between the `ListBySourceEntity` lookup and the subsequent execution call,
the mapping could have been modified by another transaction or another
caller).

### Capability gating

Per-product capability policies (`CanSellLien`, `CanReferCareConnect`,
`CanReferFund`) are enforced **only for end-user callers**. Service-token
callers (`CallerType.Service` / `ServiceOnBehalfOfUser`) skip the per-product
permission check because:

- Service tokens carry `roles=service` and cannot satisfy per-permission claims.
- The originating product service has already enforced the user's capability
  before forwarding via service token — re-applying it in Flow would break
  the legitimate "service-on-behalf-of-user" pattern.
- Tenant scoping is **non-bypassable** (the query filter enforces it for
  both caller types), and parent ownership is enforced atomically above.

## 2. Product Passthrough Rewire

`BuildingBlocks.FlowClient.IFlowClient` gains three product-scoped methods:

```csharp
GetProductWorkflowAsync     (productSlug, sourceEntityType, sourceEntityId, workflowInstanceId)
AdvanceProductWorkflowAsync (productSlug, sourceEntityType, sourceEntityId, workflowInstanceId, request)
CompleteProductWorkflowAsync(productSlug, sourceEntityType, sourceEntityId, workflowInstanceId)
```

`FlowExecutionEndpoints.MapFlowExecutionPassthrough` now calls these
directly. The product process performs **no** ownership check before the
forward — Flow is the single source of truth. Product `WorkflowEndpoints`
in liens / careconnect / fund did not change shape.

`ListBySourceEntityAsync` is retained on `IFlowClient` for legitimate
list use cases but is no longer used by passthroughs.

## 3. Authentication Hardening

`AddServiceTokenBearer` (BuildingBlocks) was tightened:

- `RequireSignedTokens = true` — refuses `alg=none`.
- `RequireExpirationTime = true`.
- `ClockSkew = 30s` (tight, to bound replay).
- `OnTokenValidated` rejects the token after signature validation if:
  - `sub` is missing or doesn't start with `service:`, or
  - neither `tenant_id` nor `tid` is present.
  Failures log `code=invalid_service_token` / `missing_tenant_context`.

A new `ICallerContextAccessor` projects the current principal into
`{ Type: User|Service|ServiceOnBehalfOfUser|Unknown, TenantId, Subject, Actor }`.
The new controller depends on it for the User-only capability gate and
for the per-request log scope.

`AddServiceTokenBearer` accepts `failFastIfMissingSecret`. Flow.Api wires
it as `!IsDevelopment()`, so a non-Development host that forgets to set
`FLOW_SERVICE_TOKEN_SECRET` (or sets it shorter than 32 chars) crashes on
startup with a clear message instead of silently no-op'ing token validation.

## 4. Diagnostics

`BuildingBlocks.FlowClient.FlowErrorCodes` is the single source of truth:

```
workflow_instance_not_owned
expected_step_mismatch
instance_not_active
concurrent_state_change
flow_unavailable
flow_upstream_error
invalid_service_token
missing_tenant_context
```

The new controller wraps each request in a log scope with
`product`, `entityType`, `entityId`, `instanceId`, `callerType`,
`tenantId`, `subject`, `actor` so operators can correlate any 401 / 403 /
404 / 409 with the originating principal.

## 5. Tests

Unit-test project: `apps/services/flow/backend/tests/Flow.UnitTests`
(xUnit, project ref to `BuildingBlocks`).

| Suite                              | Cases | Status |
| ---------------------------------- | ----- | ------ |
| `CallerContextAccessorTests`       | 5     | ✅ pass |
| `ServiceTokenStartupGuardTests`    | 4     | ✅ pass |

`dotnet test` against the project: **9 passed, 0 failed**.

### Deferred to Phase A1.1

A full `WebApplicationFactory<Program>` integration matrix (cross-parent
ownership, cross-tenant isolation, capability denial, multi-auth rejection,
stale-step / inactive / concurrent transitions, product correlation
mismatch) was scoped but not delivered in Track A. The Flow.Api host has
many production-only dependencies (Postgres, Kafka adapters, audit /
notification clients) that need careful test-host overrides before a
`WebApplicationFactory` is hermetic enough to be useful. Tracked as
**Phase A1.1**.

## 6. End-to-End

The new atomic route was smoked against the running stack:

| Probe                                                       | Result |
| ----------------------------------------------------------- | ------ |
| `GET …/synqlien/lien_case/<guid>/<guid>` no auth            | `401` ✅ |
| Same with `Authorization: Bearer bogus`                     | `401` ✅ |
| Legacy `GET /api/v1/workflow-instances/<guid>` no auth      | `401` ✅ (still works) |

A full positive happy-path run across SynqLien / CareConnect / SynqFund
requires seeded fixtures (tenant, user JWT, product parent records) that
are not available in the local container. The `p5-e2e.sh` script's
`TENANT_ID` and `USER_BEARER` requirements are unchanged — when those
fixtures are present, the script will exercise the new atomic endpoints
because the product passthroughs now route through them.

## 7. Backward Compatibility

- Legacy `/api/v1/workflow-instances/{id}/...` endpoints are unchanged
  and remain available for direct admin / operations callers.
- `IFlowClient.ListBySourceEntityAsync` is retained.
- No DB migration required (the model is identical).
- Service-token wire format is unchanged; the validator just enforces
  more of what the issuer always emitted.
