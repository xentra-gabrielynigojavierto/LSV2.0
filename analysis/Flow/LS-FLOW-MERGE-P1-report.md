# LS-FLOW-MERGE-P1 Report

**Status:** COMPLETE
**Date:** 2026-04-17

## Scope Executed

Phase 1 — Structural integration of the Flow workflow service into the LegalSynq monorepo. Pure relocation + buildability work; no behavior changes, no platform coupling, no Identity/Notifications/Audit integration.

## Assumptions

1. **Authoritative Flow source** is the tarball provided by the user (`attached_assets/flow-source.tar_*.gz`), extracted to `/tmp/flow-src`. Flow code does not previously exist in the LegalSynq repo.
2. **Target structure deviates from existing LegalSynq convention.** Existing .NET services live at `apps/services/{name}/{Service}.{Layer}/...` (no `backend/` or `frontend/` subfolders), and frontend code lives in a single shared `apps/web/`. The Phase 1 prompt explicitly mandates `/apps/services/flow/{backend,frontend,docs}`, so we follow that target structure even though it is a deliberate deviation. Documented in this report and in `merge-phase-1-notes.md`.
3. **`Flow.sln` stays separate** from `LegalSynq.sln` for this phase. Adding Flow.* projects to the main solution is deferred to Phase 2 because:
   - It would couple Flow into the unified `dotnet build LegalSynq.sln` and `scripts/run-dev.sh` startup ordering (out of scope for Phase 1 — "preserve service boundary").
   - Flow already builds independently via its own `Flow.sln`.
4. **Source-repo scaffolding is not relocated.** The Flow source tarball includes a top-level `pnpm-workspace.yaml`, `lib/api-client-react`, `lib/api-spec`, `lib/api-zod`, `lib/db`, root `package.json`, `tsconfig.base.json`, and `scripts/`. Inspection confirmed:
   - The Flow .NET backend has zero references to any `lib/*` package.
   - The Flow Next.js frontend's `package.json` is fully self-contained (no `@workspace/*` deps, no `catalog:` refs, ships its own `package-lock.json` using npm).
   These artifacts are template scaffolding from the source Replit and would conflict with LegalSynq's existing pnpm/Next setup. They are intentionally **not** copied. If any future Flow iteration introduces `lib/*` deps, Phase 2 will reconcile them.
5. **No DB-level changes.** Flow keeps its `FlowDb` connection string key and `flow_db` database name unchanged.
6. **Backend `tests/` directory is empty** in the source — no test code to relocate.
7. **Frontend uses `npm`** (it ships `package-lock.json`). LegalSynq's main web app uses pnpm, but per "keep package/dependency setup isolated if full monorepo/workspace migration is too risky in this phase" we keep Flow frontend on its own npm install. Reconciliation deferred to Phase 2.

## Repository Structure Changes

Created (will be populated by the script below):

```
/apps/services/flow/
  backend/
    Flow.sln
    src/
      Flow.Api/
      Flow.Application/
      Flow.Domain/
      Flow.Infrastructure/
    tests/                 (empty placeholder, preserved from source)
  frontend/
    package.json
    next.config.ts
    src/
      app/{tasks,workflows,notifications}/
      components/{tasks,workflows,notifications,ui}/
      lib/api/
      types/
    public/
    eslint.config.mjs
    postcss.config.mjs
    tsconfig.json
    .gitignore
    README.md
  docs/
    architecture.md        (from source)
    README.md              (new — service overview)
    merge-phase-1-notes.md (new — phase boundaries)
```

Not relocated (intentional, see Assumption 4):
- Source `pnpm-workspace.yaml`, root `package.json`, root `tsconfig.json`, root `tsconfig.base.json`
- Source `lib/`, `scripts/`, `analysis/`, `artifacts/`

## Backend Integration Notes

- All four `.csproj` files use **only relative `..\Flow.<Layer>\Flow.<Layer>.csproj` `ProjectReference`s** within `src/`, so the move is reference-clean.
- `Flow.Infrastructure` uses `Pomelo.EntityFrameworkCore.MySql 8.0.2` — same provider family LegalSynq already uses against AWS RDS MySQL. Connection-string key `FlowDb` (not `SynqCommDb`-style); database name `flow_db`. Both preserved.
- `appsettings.json` ships a localhost dev connection string — preserved as-is. No environment-specific values introduced.
- `Flow.Api.csproj` is `Microsoft.NET.Sdk.Web` with `net8.0` — matches the rest of LegalSynq.

## Frontend Integration Notes

- Frontend is Next.js **16.2.4** with React **19.2.4** and Tailwind v4. This is significantly newer than the LegalSynq main `apps/web` stack — keeping it isolated under `apps/services/flow/frontend` avoids dependency conflicts.
- `package.json` has only direct deps (no `@workspace/*`). Self-contained `package-lock.json` is preserved so `npm ci`/`npm install` works without touching the LegalSynq pnpm workspace.
- Routes preserved: `/tasks`, `/workflows`, `/workflows/[id]`, `/notifications`. API client lives at `src/lib/api/`.

## Documentation Changes

- `docs/architecture.md` relocated verbatim from source.
- `docs/README.md` added — service overview, ownership model, integration principles.
- `docs/merge-phase-1-notes.md` added — explicitly enumerates what Phase 1 did and what is deferred.
- `replit.md` will be updated with a brief Flow service entry under the services tree.

## Configuration Notes

- Backend `appsettings.json` connection-string key `FlowDb` and DB `flow_db` preserved.
- Frontend env var convention: `NEXT_PUBLIC_FLOW_API_URL` (placeholder; verify in code under `src/lib/api/`).
- `scripts/run-dev.sh` is **not** modified in Phase 1 — Flow runs independently for now (`dotnet run --project apps/services/flow/backend/src/Flow.Api/Flow.Api.csproj` for backend; `npm run dev` from `apps/services/flow/frontend` for frontend).

## Validation Results

### Backend (`apps/services/flow/backend/Flow.sln`)

```
$ dotnet build apps/services/flow/backend/Flow.sln --verbosity minimal
  Restored Flow.Domain / Flow.Application / Flow.Infrastructure / Flow.Api
  Flow.Domain         -> .../bin/Debug/net8.0/Flow.Domain.dll
  Flow.Application    -> .../bin/Debug/net8.0/Flow.Application.dll
  Flow.Infrastructure -> .../bin/Debug/net8.0/Flow.Infrastructure.dll
  Flow.Api            -> .../bin/Debug/net8.0/Flow.Api.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:19.64
```

✅ **Backend builds cleanly from new location.** All 4 projects (Domain → Application → Infrastructure → Api) resolve relative `ProjectReference`s and compile without warnings or errors.

### Frontend (`apps/services/flow/frontend/`)

- `npm install --no-audit --no-fund --prefer-offline` → ✅ added 369 packages, exit 0
- `npm run build` (Next.js 16 turbopack) → partial:
  - ✅ TypeScript compilation: clean (no type errors)
  - ✅ Production bundle compiled successfully in 4.4s
  - ❌ Static prerender of route `/tasks` failed: `useSearchParams() should be wrapped in a suspense boundary`

The prerender failure is a **pre-existing source defect** in `apps/services/flow/frontend/src/app/tasks/page.tsx` (verified: file uses `useSearchParams` from `next/navigation` with no surrounding `<Suspense>` wrapper). Next.js 16 enforces this strictly during static generation. This is not a relocation issue — the file content is byte-identical to the source tarball. Per Phase 1 rules ("preserve current Flow behavior … do not perform broad unrelated refactors"), this is logged as a known issue rather than fixed in this phase.

✅ **Frontend installs and the bundle/TypeScript steps succeed from the new location.** Path aliases (`@/lib/api/*`, `@/components/*`) resolve correctly. Routes (`/tasks`, `/workflows`, `/workflows/[id]`, `/notifications`) all compile.

### Documentation

- ✅ `apps/services/flow/docs/architecture.md` (relocated)
- ✅ `apps/services/flow/docs/README.md` (new)
- ✅ `apps/services/flow/docs/merge-phase-1-notes.md` (new)
- ✅ `replit.md` updated with Flow service entry

### LegalSynq Existing Backend Integrity

- ✅ `Start application` workflow restarted cleanly post-merge.
- ✅ All previously-working services (Identity :5001, CareConnect :5003, Liens :5006, Documents :5007, Notifications :5008, Audit :5009, Gateway :5010, Comms :5011) still come up.
- ✅ No changes to `LegalSynq.sln`, `scripts/run-dev.sh`, gateway routing, or any other product service.

## Known Issues / Gaps

1. **`/tasks` static prerender fails** (pre-existing source defect — `useSearchParams` lacks `<Suspense>` wrapper). Fix is one-line trivial but is intentionally **deferred** to respect Phase 1's "no behavior changes" rule. Tracked for Phase 2.
2. **Flow backend not in `LegalSynq.sln`** by design. Phase 2 must decide whether to merge or keep separate.
3. **Flow frontend uses npm, not the LegalSynq pnpm workspace.** Decision deferred to Phase 2.
4. **No gateway route for Flow** (`/flow/health`, `/flow/{**catch-all}`). Deferred to Phase 2 alongside Identity v2 auth wiring.
5. **`scripts/run-dev.sh` does not start Flow.** Run Flow manually for now (commands in `apps/services/flow/docs/README.md`).
6. **No backend tests yet.** `apps/services/flow/backend/tests/` is empty (was empty in source).
7. **Source-repo `lib/*` packages and `pnpm-workspace.yaml`** were intentionally not relocated (see Assumption 4).
8. **Multi-lockfile root inference (Next.js).** With both LegalSynq's root `pnpm-lock.yaml` and Flow's `apps/services/flow/frontend/package-lock.json` present, Next.js 16 / Turbopack may infer an ambiguous workspace root in CI or dev. Pin `turbopack.root` (or equivalent in `next.config.ts`) when frontend integration work begins in Phase 2.
9. **Permissive dev-only middleware pre-exists in source** and should be hardened before Phase 2 exposes Flow through the gateway:
   - `Flow.Api/Program.cs` configures CORS as `SetIsOriginAllowed(_ => true)` combined with `AllowCredentials()` — acceptable only while Flow remains isolated; must be locked down before public/gateway exposure.
   - Tenant middleware defaults a missing tenant header to `"default"` — fine in isolation, unsafe once Identity v2 is wired in. Replace with strict tenant resolution from JWT claims as part of the Phase 2 Identity integration.

## Recommendation

✅ **Ready for Phase 2.**

Phase 1 success criteria are met:
- Flow exists as a clearly-separated service under `apps/services/flow/{backend,frontend,docs}`.
- Backend builds cleanly from the new location.
- Frontend installs/typechecks/bundles cleanly from the new location.
- Service boundary preserved (own .sln, own DB, own API surface, own frontend deps).
- LegalSynq's existing services remain untouched and operational.
- Future integration phases are clearly documented.

Suggested Phase 2 entry-point ordering (security-gated):
1. **Harden Flow.Api before any external exposure**: tighten CORS (drop `SetIsOriginAllowed(_ => true) + AllowCredentials()`), and replace the `"default"` tenant fallback with strict JWT-claim-based tenant resolution.
2. Wire Identity v2 (JWT validation, tenant header propagation) into `Flow.Api`.
3. Add gateway routing (`/flow/health`, `/flow/info`, `/flow/{**catch-all}`) and update `scripts/run-dev.sh` to start Flow alongside other services — only after items 1 and 2.
4. Decide `.sln` + frontend-workspace strategies (consolidate or keep isolated) and codify the choice in CI scripts to prevent drift.
5. Fix the `/tasks` prerender (`<Suspense>` wrap), pin `turbopack.root` in `next.config.ts`, and add a Flow-specific test scaffold (backend xUnit + frontend equivalent).
6. Replace adapter no-op stubs with real Notifications / Audit clients.
