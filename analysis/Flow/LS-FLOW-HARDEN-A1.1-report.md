# LS-FLOW-HARDEN-A1.1 Report

> Validation track that follows LS-FLOW-HARDEN-A1. Builds the integration-test
> host, the high-risk test matrix, and proves positive happy-path execution per
> product against the hardened atomic ownership controller.

## Scope Executed

_(updated as work progresses)_

- **T01** ✅ Orient on A1 surface and identify the integration gap.
- **T02** ✅ Report skeleton and session plan written.
- **T03** ✅ `public partial class Program {}` appended to `Flow.Api/Program.cs`.
- **T04** ✅ `Flow.IntegrationTests` project created and added to `Flow.sln`; restores + builds clean (Mvc.Testing 8.0.*, EF Sqlite 8.0.*, JwtBearer pinned 8.0.26, DI pinned 8.0.1 to clear NU1605).
- **T05** ✅ Test infrastructure complete: `TestAuthDefaults`/`TestAuthHandler`, `FlowApiFactory` (SQLite shared-keep-alive + `PostConfigure<AuthenticationOptions>` to force TestAuth as every default scheme; `Development` env to bypass the service-token startup guard already covered in unit tests), `SeedFixture`, `HttpClientExtensions`, `SmokeTests`.
- **T06** ✅ Six suites — `OwnershipIdorTests`, `TenantIsolationTests`, `ProductCorrelationTests`, `TransitionIntegrityTests`, `AuthTests`, `ErrorContractTests` — all green.
- **T07** ✅ `HappyPathExecutionTests` proves GET → advance → complete for SynqLien, CareConnect, SynqFund.
- **T08** ✅ `apps/services/flow/backend/scripts/harden-a1-e2e.sh` added (atomic-endpoint probe + optional other-tenant / wrong-parent negative probes). Live-stack on `:5010`/`:5050` unreachable in this container — same blocker as A1; integration matrix is the authoritative correctness signal.
- **T09** ✅ `apps/services/flow/docs/merge-phase-A1.1-notes.md` added; A1.1 sections appended to `apps/services/flow/docs/README.md` and `apps/services/flow/docs/architecture.md`.
- **T10** ✅ `dotnet test Flow.IntegrationTests` → **28/28 pass** in ~1 s. `dotnet test Flow.UnitTests` → **9/9 pass**. Architect review pending.

## Assumptions

- The integration host is the **authoritative** validator of the controller
  contract. The full live stack (Identity / RDS / S3 / per-product DBs) cannot
  be primed in this container, so live happy-path E2E remains a smoke probe
  unless seeded fixtures are supplied. The integration host runs the **real**
  controller, **real** `WorkflowEngine`, **real** EF (SQLite-in-memory), and the
  **real** auth pipeline (multi-scheme + capability policies). Only the auth
  scheme handler and the DB provider are test substitutes.
- SQLite-in-memory (single shared keep-alive connection) is the right test
  database: it preserves relational behavior the joins rely on (FK, query
  filters, owned types, concurrency tokens) and is reset per fixture.
- `TestAuth` is a single auth handler that classifies callers based on the
  `X-Test-Sub` header (`service:*` → service token shape, anything else →
  user). It produces a `ClaimsPrincipal` with the right claims so the
  production `ClaimsTenantProvider`, `CallerContextAccessor`, and capability
  policies run unmodified — i.e. the policies under test are the production
  policies.
- The MultiAuth `ForwardDefaultSelector` is overridden in the test factory to
  always forward to `TestAuth`. The production multi-scheme is therefore
  exercised as the wiring it is, but the underlying handler is deterministic.
  This keeps tests reproducible without minting JWTs.
- Service-token semantic validation (signed-tokens / sub shape / tenant claim)
  is already covered by `Flow.UnitTests.ServiceTokenStartupGuardTests` and the
  unit-level `OnTokenValidated` behavior; A1.1 layers integration coverage on
  top by exercising the controller via the multi-scheme pipeline.

## Repository / Architecture Notes

The hardened atomic ownership controller (A1) is at:

- `apps/services/flow/backend/src/Flow.Api/Controllers/V1/ProductWorkflowExecutionController.cs`

It exposes:

| Verb / suffix | Behaviour |
| ------------- | --------- |
| `GET` | Returns the instance state. |
| `POST /advance` | Applies an engine transition with `expectedCurrentStepKey`. |
| `POST /complete` | Marks the instance complete. |

Each handler resolves the instance via one EF join over
`ProductWorkflowMappings × WorkflowInstances`, both tenant-filtered by
`FlowDbContext.HasQueryFilter`. Mismatch on tenant / product / parent /
instance is collapsed to `404 { code: "workflow_instance_not_owned" }`.
Capability policies are applied for end-user callers only; service tokens skip
per-product capability (the originating product service has already enforced
it) but cannot bypass tenant or parent ownership.

The product passthrough helper at
`shared/building-blocks/BuildingBlocks/FlowClient/FlowExecutionEndpoints.cs`
forwards directly to those atomic endpoints.

## Test Infrastructure Notes

_(populated in T03–T05)_

## Integration Test Coverage

_(populated in T06)_

## E2E Fixture / Seed Notes

_(populated in T05)_

## Positive End-to-End Validation Results

_(populated in T07/T08)_

## Diagnostics / Error Assertion Notes

_(populated in T06)_

## Documentation Changes

_(populated in T09)_

## Known Issues / Gaps

_(populated at end)_

## Recommendation

_(populated at end)_

## Architect Review Outcome

- **First pass:** FAIL (Medium) — flagged (1) tautological `User_with_matching_product_role_can_get` (carried `LienSell`), (2) overly permissive `harden-a1-e2e.sh` classifier (200/404 both treated as success on positive product probes), (3) deferred wire-bearer negative-semantic slice.
- **Fixes applied:**
  - `AuthTests.User_with_matching_product_role_can_get` now carries only `AppointmentCreate` so the `product_roles` claim is the sole grant.
  - `harden-a1-e2e.sh` now uses a strict-200 classifier by default; permissive 200/404 is opt-in via `HARDEN_E2E_ALLOW_NOT_OWNED=1`; a `probes_executed` guard exits 2 when nothing ran.
- **Re-review:** **PASS for A1.1 scope.** Two FAIL drivers resolved; deferral of the real `JwtBearer` negative-semantic integration slice is acknowledged as acceptable for A1.1.

## Deferred to A1.2

- **Wire-bearer negative semantics under real `JwtBearer`** — invalid issuer / invalid audience / missing tenant claim / malformed `service:` subject. Today these are covered at the unit boundary (`Flow.UnitTests.ServiceTokenStartupGuardTests` + `BuildingBlocks.Authentication.ServiceTokens` `OnTokenValidated` logic) and TestAuth stands in for the wire scheme inside the integration host. A1.2 should add a small parallel integration slice that boots the host with the real bearer schemes and an in-process token issuer.
- **CI default for `harden-a1-e2e.sh`** — wire the script into the dev-stack pipeline in strict mode (no `HARDEN_E2E_ALLOW_NOT_OWNED`) once a seeded gateway is available.
