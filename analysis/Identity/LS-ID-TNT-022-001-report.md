# LS-ID-TNT-022-001 — Define Insights Permission Codes

## 1. Executive Summary

Adds the complete `SYNQ_INSIGHTS` permission catalog to close the authorization gap left by
LS-ID-TNT-015-001, which deferred Insights gate-points because no capability codes existed.

Seven new capabilities across three categories (Dashboard, Reports, Schedules) are seeded via an
idempotent EF migration. Frontend constants in `permission-codes.ts` mirror every code.
Both product-code translation dictionaries in `AdminEndpoints.cs` (and `AuthService.cs`, which
already had the mapping) are now consistent.

**Status: COMPLETE** — migration created, SeedIds updated, frontend constants added, AdminEndpoints
product-code maps updated, TypeScript clean.

---

## 2. Codebase Analysis

### Insights frontend pages inventoried

| Route | Client component | Action surfaces |
|---|---|---|
| `/insights` | dashboard | View metrics widgets |
| `/insights/reports` | `reports-catalog-client.tsx` | Run, Export, Customize/Build, Schedule |
| `/insights/reports/[id]` | report viewer | Export |
| `/insights/reports/builder` | report builder | Create / Save report definition |
| `/insights/schedules` | `schedules-list-client.tsx` | Run-now, Deactivate |
| `/insights/schedules/[id]` | schedule detail | Edit, Activate/Deactivate |
| `/insights/schedules/new` | new schedule form | Create schedule |

### Identity service — capability catalog architecture

* Table: `idt_Capabilities` — `(Id, ProductId, Code, Name, Description, Category, IsActive, ...)`
* IDs follow sequential decimal-in-UUID pattern: `60000000-0000-0000-0000-NNNNNNNNNNNN`
  where `N` is a zero-padded decimal counter (0001–0037 were in use).
* Code format: `SYNQ_PRODUCT.entity:action` (established by `UpdatePermissionCodesToNamespaced`
  migration `20260414000001`).
* Role → capability assignments: `idt_RoleCapabilityAssignments (RoleId, CapabilityId)`.
* Migration pattern: pure-SQL `migrationBuilder.Sql()` with `INSERT IGNORE` for idempotency.
  No Designer.cs required for data-only migrations (confirmed by `20260414000001`).

---

## 3. Existing Insights Product / Role / API Analysis

* `SYNQ_INSIGHTS` was **not** seeded in `idt_Products` — no row with that code existed.
  Product IDs 1–6 were: Fund, Liens, CareConnect, Pay, AI, Platform.
* `AuthService.DbToFrontendProductCode` already contained `["SYNQ_INSIGHTS"] = "SynqInsights"` —
  added during an earlier UI task — so the JWT claim translation was ready.
* `AdminEndpoints.FrontendToDbProductCode` and `AdminEndpoints.DbToFrontendProductCode` were
  **missing** the `SynqInsights ↔ SYNQ_INSIGHTS` entry; these have now been added.
* No Insights ProductRoles or OrgTypeRules are defined. Insights access is gated at the system-
  role level (TenantAdmin / StandardUser) via `idt_RoleCapabilityAssignments`, consistent with
  the TENANT.* pattern established in LS-ID-TNT-011.
* Reports backend service (`apps/services/reports`) exposes report execution / export APIs but
  has no identity-service capability enforcement today. Enforcement is a future backend ticket.

---

## 4. Existing Authorization Model Alignment

The TENANT.* permissions (0030–0037) set the precedent for product-agnostic, role-driven capability
grants without requiring product subscription or ProductRole assignment. Insights capabilities follow
the same model:

* **SYNQ_INSIGHTS** product is created as a real `idt_Products` row (not a pseudo-product) so
  future ProductRole and OrgTypeRule modelling remains possible.
* System roles (TenantAdmin, StandardUser) receive default grants at seed time.
* Per-tenant custom role assignment is handled by the existing `idt_RoleCapabilityAssignments`
  table and the tenant-admin UI already built for LS-ID-TNT-011.

---

## 5. Proposed Insights Permission Catalog

| ID (UUID suffix) | Code | Name | Description | Category |
|---|---|---|---|---|
| `...0038` | `SYNQ_INSIGHTS.dashboard:view` | View Insights Dashboard | View the Insights analytics and metrics dashboard | Dashboard |
| `...0039` | `SYNQ_INSIGHTS.reports:view` | View Reports | Browse the report catalog and view report results | Reports |
| `...0040` | `SYNQ_INSIGHTS.reports:run` | Run Reports | Execute a report to generate current results | Reports |
| `...0041` | `SYNQ_INSIGHTS.reports:export` | Export Reports | Export report results to CSV, XLSX, or PDF | Reports |
| `...0042` | `SYNQ_INSIGHTS.reports:build` | Build Reports | Create and customise report definitions | Reports |
| `...0043` | `SYNQ_INSIGHTS.schedules:manage` | Manage Schedules | Create, edit, activate, and deactivate report schedules | Schedules |
| `...0044` | `SYNQ_INSIGHTS.schedules:run` | Run Schedules | Trigger a scheduled report to run immediately | Schedules |

**Design decisions:**

* `dashboard:view` and `reports:view` are kept separate so read-only users can browse without
  being able to trigger execution.
* `reports:run` and `reports:export` are separated: a viewer can run but the export action
  (which may be metered / costly) can be restricted independently.
* `reports:build` covers both the builder UI and "customise existing" flows (they share the same
  write path).
* `schedules:manage` covers full CRUD + activate/deactivate (one permission, same as
  `TENANT.users:manage` pattern). `schedules:run` covers the run-now / ad-hoc trigger action.

---

## 6. Role-Permission Mapping Strategy

| Capability | TenantAdmin | StandardUser | Notes |
|---|---|---|---|
| `dashboard:view` | ✓ | ✓ | All authenticated users can see the dashboard |
| `reports:view` | ✓ | ✓ | All authenticated users can browse the catalog |
| `reports:run` | ✓ | — | StandardUser read-only by default |
| `reports:export` | ✓ | — | Potentially metered; operator grants as needed |
| `reports:build` | ✓ | — | Power-user capability |
| `schedules:manage` | ✓ | — | Admin-level action |
| `schedules:run` | ✓ | — | Admin-level action |

Tenants can expand StandardUser grants via the existing role-capability assignment UI without
any code changes (infrastructure already exists from LS-ID-TNT-011).

---

## 7. Files Changed

| File | Change |
|---|---|
| `apps/services/identity/Identity.Infrastructure/Persistence/Migrations/20260419000001_AddInsightsPermissionCatalog.cs` | **New** — EF migration seeding SYNQ_INSIGHTS product + 7 capabilities + role assignments |
| `apps/services/identity/Identity.Infrastructure/Data/SeedIds.cs` | Added `ProductSynqInsights` + 7 `PermInsights*` GUID constants |
| `apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs` | Added `SynqInsights ↔ SYNQ_INSIGHTS` to both `FrontendToDbProductCode` and `DbToFrontendProductCode` |
| `apps/web/src/lib/permission-codes.ts` | Added `PermissionCodes.Insights` namespace with 7 constants; extended `PermissionCode` union type |

---

## 8. Backend Implementation

Migration `20260419000001_AddInsightsPermissionCatalog`:

```sql
-- Product
INSERT IGNORE INTO idt_Products VALUES ('10000000-...-0007', 'SYNQ_INSIGHTS', ...)

-- 7 capabilities (IDs 0038–0044)
INSERT IGNORE INTO idt_Capabilities VALUES (...)

-- Default role assignments
INSERT IGNORE INTO idt_RoleCapabilityAssignments VALUES (
  TenantAdmin → all 7,
  StandardUser → dashboard:view + reports:view
)
```

All statements use `INSERT IGNORE` so the migration is safe to replay if interrupted before
being recorded in `__EFMigrationsHistory`. No DDL (schema changes) are made, so no partial-
commit guard in `Program.cs` is required.

---

## 9. Frontend / Shared Constant Implementation

```typescript
// apps/web/src/lib/permission-codes.ts
Insights: {
  DashboardView:   'SYNQ_INSIGHTS.dashboard:view',
  ReportsView:     'SYNQ_INSIGHTS.reports:view',
  ReportsRun:      'SYNQ_INSIGHTS.reports:run',
  ReportsExport:   'SYNQ_INSIGHTS.reports:export',
  ReportsBuild:    'SYNQ_INSIGHTS.reports:build',
  SchedulesManage: 'SYNQ_INSIGHTS.schedules:manage',
  SchedulesRun:    'SYNQ_INSIGHTS.schedules:run',
},
```

Usage in Insights components (future LS-ID-TNT-022-002 gates):

```typescript
import { PermissionCodes } from '@/lib/permission-codes';
import { usePermission } from '@/hooks/use-permission';

const canRun    = usePermission(PermissionCodes.Insights.ReportsRun);
const canExport = usePermission(PermissionCodes.Insights.ReportsExport);
const canBuild  = usePermission(PermissionCodes.Insights.ReportsBuild);
```

---

## 10. Verification / Testing Results

* `npx tsc --noEmit` — **0 errors** after all changes.
* Migration SQL verified: `INSERT IGNORE` on both `idt_Products` and `idt_Capabilities` with
  IDs that do not overlap any existing seed row (0001–0037 in use; new: 0038–0044).
* `SeedIds.cs` constant names follow the `Perm{Product}{Entity}{Action}` naming convention
  already established in the file.
* `AdminEndpoints` both maps (`FrontendToDb`, `DbToFrontend`) and `AuthService` all now agree on
  `SYNQ_INSIGHTS ↔ SynqInsights`.

---

## 11. Known Issues / Gaps

| Gap | Tracking |
|---|---|
| Insights UI components do not yet call `usePermission()` against these codes | LS-ID-TNT-022-002 (gate-point wiring) |
| Reports backend API (`apps/services/reports`) has no capability enforcement middleware | Future backend enforcement ticket |
| No Insights ProductRoles or OrgTypeRules seeded; Insights is accessible to all org types | Intentional for now; restrict per business requirements when multi-org Insights launches |

---

## 12. Final Status

**COMPLETE.** The Insights permission catalog is fully defined, seeded (pending next DB migrate
on startup), mirrored in frontend constants, and both product-code translation dictionaries are
consistent. The codes are ready to be referenced by LS-ID-TNT-022-002 (UI gate-point wiring).
