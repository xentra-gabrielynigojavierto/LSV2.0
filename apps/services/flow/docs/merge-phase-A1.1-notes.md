# Flow — Phase A1.1 (Hardening Validation Track) Notes

> Validation track to A1. Does not change runtime behaviour — adds the
> integration-test host, the high-risk security/integrity test matrix, and
> per-product positive happy-path coverage that A1's unit-only suite was
> structurally unable to assert. Companion: `analysis/LS-FLOW-HARDEN-A1.1-report.md`.

## TL;DR

| A1 validation gap (unit-only)                                  | A1.1 outcome |
| -------------------------------------------------------------- | ------------ |
| No real Flow.Api host exercised in CI                          | `WebApplicationFactory<Program>` host w/ SQLite + TestAuth |
| Atomic ownership only proven by mocked controller test         | End-to-end ownership IDOR matrix exercises real EF + auth pipeline |
| Tenant isolation only asserted at unit boundary                | Cross-tenant GET / advance / service-token paths covered live |
| Product correlation ("slug ≠ mapping product") never end-to-end| 4 cross-product probes, all 404 `workflow_instance_not_owned` |
| Transition guards never crossed the controller seam            | Stale step + completed instance + blank-key 400/409 covered |
| Auth scheme behaviour proven only by reading code              | Anonymous 401, capability denial 403, permission + product-role pass |
| Error-contract code stability not asserted from the wire       | `code` field asserted on every denial body |
| Positive happy-paths never executed under the real engine      | GET → advance → complete proven for all 3 products |

## 1. Integration-Test Host

`apps/services/flow/backend/tests/Flow.IntegrationTests`

| Component                | Role |
| ------------------------ | ---- |
| `FlowApiFactory`         | `WebApplicationFactory<Program>` — boots the real Flow.Api (controllers, middleware, engine, EF, capability policies) with two surgical substitutions: SQLite-in-memory DB + TestAuth scheme. |
| `TestAuthHandler` (+ `TestAuthDefaults`) | Header-driven auth (`X-Test-Sub`, `X-Test-Tenant`, `X-Test-Role`, `X-Test-Permissions`, `X-Test-ProductRoles`, `X-Test-Actor`, `X-Test-Aud`) producing the exact `ClaimsPrincipal` shape consumed by `ClaimsTenantProvider`, `CallerContextAccessor`, and the capability policies. Replaces every default scheme via `PostConfigure<AuthenticationOptions>` so `MultiAuth` and `JwtBearer` are bypassed. |
| `SeedFixture` (xUnit `IClassFixture`) | Owns the factory; seeds 2 tenants × 3 products × multiple instances (happy-path, completed, cross-tenant, wrong-parent, decoy). Exposes deterministic ids via `TestIds`. |
| `HttpClientExtensions`   | `AsUser(...)` / `AsService(...)` / `Anonymous()` and `AdvanceAsync` / `CompleteAsync` helpers — every test reads as a wire script. |

The factory deliberately runs in `Development` so the service-token startup
guard (already proven by `Flow.UnitTests.ServiceTokenStartupGuardTests`) does
not require a real secret. The "no permissions at all" capability dev-fallback
never engages because every test caller carries either the right permission,
the right product role, or an unrelated permission (capability-denial cases).

## 2. Test Matrix (28 tests, all green)

| Suite | Asserts |
| ----- | ------- |
| `OwnershipIdorTests`     | GET / advance / complete with wrong source-entity id, wrong instance id, unrelated entity all return 404 `workflow_instance_not_owned`. |
| `TenantIsolationTests`   | Tenant-A user cannot read or advance tenant-B instance (404, not 403/leak); tenant-A service token cannot mutate tenant-B; body `tenantId` ≠ JWT → 403 by `TenantValidationMiddleware`. |
| `ProductCorrelationTests`| Lien slug pointed at CareConnect mapping → 404; CareConnect→Fund → 404; Fund→Lien → 404; unknown slug → 404 (no info disclosure). |
| `TransitionIntegrityTests`| Stale `expectedCurrentStepKey` → 409 `stale_current_step`; advance on completed → 409 `instance_not_active`; complete on completed is idempotent 200; blank `expectedCurrentStepKey` → 400. |
| `AuthTests`              | Anonymous 401; user with permission claim → 200; user with matching product role → 200; user lacking either → 403; service-token caller → 200 but still tenant-bounded. |
| `ErrorContractTests`     | Every denial response carries the canonical `code` (`workflow_instance_not_owned`, `instance_not_active`, etc.) and the canonical message; no internal entity ids leaked. |
| `HappyPathExecutionTests`| For each product (SynqLien / CareConnect / SynqFund): GET → advance with the engine-supplied `currentStepKey` → assert progression. |

Run locally:

```bash
cd apps/services/flow/backend
dotnet test tests/Flow.IntegrationTests/Flow.IntegrationTests.csproj
dotnet test tests/Flow.UnitTests/Flow.UnitTests.csproj
```

## 3. Live-Stack E2E Probe

`apps/services/flow/backend/scripts/harden-a1-e2e.sh`

Mirrors `p5-e2e.sh` but routes through the new atomic A1 endpoints:

```
GET  /api/v1/product-workflows/{slug}/{type}/{id}/{instance}
POST /api/v1/product-workflows/{slug}/{type}/{id}/{instance}/advance
```

Auth modes match `p5-e2e.sh` (HS256 mint via `FLOW_SERVICE_TOKEN_SECRET` +
`python3`, or `USER_BEARER` fallback). Two optional negative probes
(`WORKFLOW_INSTANCE_ID__OTHER_TENANT`, `WORKFLOW_INSTANCE_ID__WRONG_PARENT`)
assert that ownership denial returns the canonical 404 `workflow_instance_not_owned`
without leaking existence.

The probe is gated on a runnable dev stack with seed instance ids; in this
session the dev stack on `:5010`/`:5050` was unreachable (same blocker as
A1's live-stack attempt). The matrix above is therefore the authoritative
correctness signal — the script remains for the next dev environment that
exposes a Flow gateway with seed data.

## 4. What Did Not Change

A1.1 is a validation-only track. No production code path moved; the only
runtime addition is `public partial class Program { }` appended to
`Flow.Api/Program.cs` so `WebApplicationFactory<Program>` can reach the
entry point. The atomic ownership controller, passthrough rewire, and
service-token hardening from A1 stand unchanged.
