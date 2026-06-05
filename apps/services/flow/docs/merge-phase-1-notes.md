# Flow → LegalSynq Merge — Phase 1 Notes

This document captures **what Phase 1 did and did not do** so future contributors and reviewers can understand the staging.

## What Phase 1 Did (Structural Integration Only)

- Relocated the Flow codebase from its standalone Replit project into the LegalSynq monorepo at `apps/services/flow/`.
- Preserved Flow as a **detachable service**:
  - Backend, frontend, and docs colocated under one service folder but operationally independent.
  - Backend continues to build via its own `Flow.sln` (not yet added to `LegalSynq.sln`).
  - Frontend continues to build via its own `npm install` (not yet folded into the LegalSynq pnpm workspace).
- Preserved API surface, DB ownership (`flow_db`), and connection-string key (`FlowDb`) verbatim.
- Added service-level documentation (`README.md`, `architecture.md`, this file).
- Did **not** modify `scripts/run-dev.sh`, `LegalSynq.sln`, or any other LegalSynq product code.

## Deliberate Deviations from Existing LegalSynq Convention

- LegalSynq's existing .NET services live at `apps/services/{name}/{Service}.{Layer}/...` (no `backend/` subfolder).
- LegalSynq's frontend lives in a single shared `apps/web/`.
- This merge places Flow under `apps/services/flow/{backend,frontend,docs}/` per the Phase 1 prompt's mandated target structure. This is intentional and isolates Flow's dual-stack (.NET + Next.js) layout from the rest of the monorepo until Phase 2 reconciliation.

## Source-Repo Scaffolding That Was NOT Relocated

The Flow source tarball included monorepo scaffolding that Flow itself does not depend on:

- Root `pnpm-workspace.yaml`, `package.json`, `tsconfig.base.json`, `tsconfig.json`
- `lib/api-client-react`, `lib/api-spec`, `lib/api-zod`, `lib/db`
- Top-level `scripts/` and `artifacts/`

Verification:
- The Flow `.csproj` files have zero references to any `lib/*` package.
- The Flow `frontend/package.json` has no `@workspace/*` deps and no `catalog:` references; it ships its own `package-lock.json`.

These scaffolding artifacts would conflict with LegalSynq's existing conventions and were intentionally omitted.

## Explicitly Deferred (NOT in Phase 1)

- Identity v2 integration (auth, tenant context, JWT plumbing)
- Notifications service integration
- Audit service integration
- Shared-DB merge or cross-service joins
- Product-specific workflow mapping (SynqLien / CareConnect / SynqFund)
- Cross-service event bus
- API contract redesign / auth rewrite
- UI redesign / design-system unification
- Adding Flow.* to `LegalSynq.sln`
- Folding Flow frontend into the LegalSynq pnpm workspace
- Wiring Flow into `scripts/run-dev.sh` (unified dev startup)
- Gateway routing for `/flow/*` (will be added in Phase 2 alongside auth integration)

## Phase 2 Suggested Entry Points

1. Decide the .sln strategy (keep `Flow.sln` separate vs. add to `LegalSynq.sln`).
2. Decide the workspace strategy for the frontend (own npm install vs. pnpm workspace member).
3. Add gateway routes (`/flow/health`, `/flow/info`, `/flow/{**catch-all}`) once auth integration is in place.
4. Wire Identity v2 into `Flow.Api/Program.cs` (JWT, tenant header propagation).
5. Replace adapter no-op stubs with real `Notifications` / `Audit` clients.
