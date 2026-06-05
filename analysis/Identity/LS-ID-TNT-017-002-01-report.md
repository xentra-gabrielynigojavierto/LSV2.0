# LS-ID-TNT-017-002-01 — Selective Successful Access Audit

**Status:** COMPLETE  
**Date:** 2026-04-19

---

## 1. Executive Summary

This ticket extends the audit system to capture selective successful access events for high-value
protected resources. A thorough codebase analysis found that the platform is already comprehensively
audited for most high-value surfaces:

- Report execution and export are fully audited (started/completed/failed) via the shared `IAuditAdapter` pipeline.
- Audit log queries are fully audited (`audit.log.accessed`) via the `AuditEventQueryController` ingestion loop.
- Identity admin operations (role assignment, invitations, user lifecycle) are fully audited via `IAuditEventClient` in `AdminEndpoints`.

**One genuine gap was identified and closed:** `POST /audit/exports` (audit log data export to file)
emitted no canonical audit event on success. This is a high-value governance data-egress action —
someone downloading the audit log data. The new `audit.log.exported` event now captures this.

**Result:** 1 new canonical event type added. 1 file changed. All other candidate surfaces were
found to be already fully instrumented or correctly excluded as too noisy.

---

## 2. Codebase Analysis

### Already comprehensively audited before this ticket

| Surface | Service | Event types | Audit path |
|---|---|---|---|
| Login success/failure/blocked | Identity | `identity.user.login.*` | `AuthService` → `IAuditEventClient` |
| Logout | Identity | `identity.user.logout` | `AuthEndpoints` → `IAuditEventClient` |
| Password reset requested | Identity | `identity.user.password_reset_requested` | `AuthEndpoints` → `IAuditEventClient` |
| Session invalidated | Identity | `identity.session.invalidated` | `AuthService` → `IAuditEventClient` |
| Access version stale | Identity | `identity.access.version.stale` | `AuthService` → `IAuditEventClient` |
| Product access denied | BuildingBlocks | `security.product.access.denied` | Filter → optional `IAuditEventClient` |
| Product role denied | BuildingBlocks | `security.product.role.denied` | Filter → optional `IAuditEventClient` |
| Permission denied | BuildingBlocks | `security.permission.denied` | Filter → optional `IAuditEventClient` |
| Permission policy denied | BuildingBlocks | `security.permission.policy.denied` | Filter → optional `IAuditEventClient` |
| **Report execution** | Reports | `execution.started`, `execution.completed`, `execution.failed` | `ReportExecutionService.TryAuditAsync` → `IAuditAdapter` → `SharedAuditAdapter` → `IAuditEventClient` |
| **Report export** | Reports | `export.started`, `export.completed`, `export.failed` | `ReportExportService.TryAuditAsync` → `IAuditAdapter` → `SharedAuditAdapter` → `IAuditEventClient` |
| **Audit log queries** | Audit | `audit.log.accessed` | `AuditEventQueryController.LogAuditAccess` → `IAuditEventIngestionService` |
| User/role/group/invite admin ops | Identity | 10+ event types (`identity.user.*`, `identity.group.*`) | `AdminEndpoints` → `IAuditEventClient` |

### Gap identified and closed by this ticket

| Surface | Route | Prior state | New state |
|---|---|---|---|
| Audit log data export | `POST /audit/exports` | ILogger only | `audit.log.exported` canonical event |

---

## 3. Successful Access Surface Inventory

### Included

#### `POST /audit/exports` — Audit log data export
- **Route:** `POST /audit/exports`
- **Why high-value:** Someone is downloading bulk audit log data to a file. This is a governance
  data-egress action with direct compliance relevance — HIPAA §164.312(b) and SOC 2 CC7.2 require
  tracking who initiated audit data exports and when. The exporter could potentially be exfiltrating
  the audit log itself.
- **Protected by:** `QueryAuthMiddleware` — scope resolved per-request via `IQueryAuthorizer`.
  Cross-tenant access is denied for non-PlatformAdmin callers.
- **Volume risk:** LOW — deliberate, explicit governance action. Infrequent.
- **Prior state:** Logged to `ILogger.LogWarning` only. No canonical pipeline event.

### Excluded (correctly, with rationale)

| Surface | Exclusion reason |
|---|---|
| All `GET /audit/events/*` query endpoints | Already audited: `audit.log.accessed` emitted for every query |
| `GET /api/v1/report-executions/{id}` | Low-value read; the outcome was already captured by `execution.completed` |
| `GET /api/v1/tenant-templates/*/views` (list/read) | High-volume polling-like read; not a meaningful access event |
| Every `RequirePermission` ALLOW path | Would fire on every authorized request — explicitly excluded to avoid noise |
| `GET /auth/me` | Background refresh endpoint, not a meaningful access event |
| `GET /audit/exports/{exportId}` (status poll) | Idempotent polling; the submission itself is the meaningful event |
| `LegalHoldController` create/release | Governance mutations, not access. Deferred to future "Governance Mutation Audit" ticket |
| `IntegrityCheckpointController` | System compliance operations, not user data access |
| Document service uploads | Only `RequireAuthorization()` (no product/permission specificity); insufficient value |

---

## 4. Event Taxonomy Design

### New event type: `audit.log.exported`

| Field | Value |
|---|---|
| `EventType` | `"audit.log.exported"` |
| `EventCategory` | `EventCategory.Access` |
| `SourceSystem` | `"audit"` |
| `SourceService` | `"audit-export-api"` |
| `Visibility` | `VisibilityScope.Platform` |
| `Severity` | `SeverityLevel.Warn` — data egress is inherently elevated sensitivity |
| `Action` | `"ExportSubmitted"` |
| `Entity.Type` | `"AuditExport"` |
| `Entity.Id` | The `ExportId` GUID assigned to the job |
| `Metadata` | `{ endpoint, exportId, format, recordCount, callerScope, callerAuthMode, traceId }` |
| `Tags` | `["audit-of-audit", "export", "data-egress"]` |

**Why `Warn` severity?** Exporting audit data is a high-sensitivity egress action. While not
an intrinsically hostile act, the ability to download the full audit log for a tenant represents
a significant data access event that compliance officers should be able to review. This matches
the convention used for other security-adjacent events in the platform.

---

## 5. Capture / Deduplication Strategy

**Strategy: emit once per export submission, at controller level only.**

- Fires after `_exportService.SubmitAsync(...)` returns successfully.
- Fires only on HTTP 202 (success) path — not on 400/401/403/503 error paths.
- `GET /audit/exports/{exportId}` status polls do NOT emit — they are idempotent and the
  submission already captured the meaningful event.
- No deduplication needed: each export submission is an explicit governance action with a unique
  `ExportId`.
- Fire-and-observe: `_ = _ingestionService.IngestSingleAsync(...)` — Task is discarded.
  The 202 response is never gated on audit publish success.
- IdempotencyKey: `"audit-export:{exportId}"` — prevents duplicate events if the submission path
  is somehow retried.

**Why at the controller level?**
The `IQueryCallerContext` with caller identity is resolved by `QueryAuthMiddleware` and placed in
`HttpContext.Items`. This context is only available at the controller layer, not in the
`IAuditExportService` implementation. Placing the emit in the controller mirrors the exact pattern
used by `AuditEventQueryController.LogAuditAccess`.

---

## 6. Coverage Scope Selection

**In scope for this ticket:**
- `audit.log.exported` — `POST /audit/exports` successful submission

**Explicitly deferred:**
- Legal hold create/release canonical audit — governance mutations, not access events.
  Recommend: "LS-ID-TNT-017-003 — Governance Mutation Audit" covering legal holds, integrity
  checkpoint generation, and admin-initiated data lifecycle operations.
- `InsightsReportsView` read-only browsing — noise > value.

---

## 7. Files Changed

| File | Change type | Description |
|---|---|---|
| `apps/services/audit/Controllers/AuditExportController.cs` | Modified | Added `IAuditEventIngestionService`, `LogAuditExport` helper, `audit.log.exported` emission on 202 success |

---

## 8. Backend Implementation

### `AuditExportController` — `audit.log.exported`

**Constructor change:** Added `IAuditEventIngestionService ingestionService` parameter (and field).
This is the same service used by `AuditEventQueryController` for `audit.log.accessed`. No new
service registration required — `IAuditEventIngestionService` is already wired in `Program.cs`.

**New private method:** `LogAuditExport(caller, result, traceId)` — mirrors `LogAuditAccess` in
`AuditEventQueryController`:
- Logs an `ILogger.LogInformation` structured log (unchanged diagnostic path)
- Discards `_ = _ingestionService.IngestSingleAsync(new IngestRequest { ... })` (fire-and-observe)

**Call site:** After `var result = await _exportService.SubmitAsync(request, caller, ct)` — before
the `StatusCode(202, ...)` return.

**Tenant isolation:** `Scope.TenantId = caller.TenantId` — non-PlatformAdmin callers are already
scoped to their tenant by `IQueryAuthorizer.Authorize` before this point. The event faithfully
reflects the caller's tenant scope.

**Actor attribution:** `Actor.Id = caller.UserId` — the caller identity is always server-derived
from the JWT via middleware, never trusted from the request body.

---

## 9. Query / Viewer Readiness Notes

The new `audit.log.exported` event is immediately queryable via the existing audit pipeline:

**Via `/audit/events` query API:**
- Filter by `EventType = "audit.log.exported"`
- Filter by `EventCategory = Access`
- Filter by tag `"data-egress"` or `"export"` (when tag-filtering is supported)

**Via Control Center audit viewer (`/synqaudit/permissions`):**
- Event type `audit.log.exported` will appear in the event type dropdown
- Actor, description, and metadata fields are populated for list/detail rendering
- The `audit-of-audit` tag distinguishes these from tenant-business events

No viewer changes are required — the event shape is identical to existing `audit.log.accessed`
events and renders correctly in the existing list/detail audit viewer.

---

## 10. Verification / Testing Results

### Build
```
dotnet build apps/services/audit/PlatformAuditEventService.csproj -c Release
  → 0 errors, 1 pre-existing warning (JWT Bearer version conflict, unrelated)
```

### Behavioral expectations (verified by code path analysis)

| Scenario | Expected outcome | Verified |
|---|---|---|
| `POST /audit/exports` → 202 | `audit.log.exported` emitted via fire-and-observe | ✓ (code path confirmed) |
| `POST /audit/exports` → 400 (validation fail) | No `audit.log.exported` emitted | ✓ (emit only on success branch) |
| `POST /audit/exports` → 403/401 | No `audit.log.exported` emitted | ✓ (catch branch has no emit) |
| `POST /audit/exports` → 503 (disabled) | No `audit.log.exported` emitted | ✓ (early return before emit) |
| `GET /audit/exports/{id}` (status poll) | No `audit.log.exported` emitted | ✓ (only Submit emits) |
| `_ingestionService` publish fails | 202 returned anyway, no exception propagated | ✓ (Task discarded, no await) |
| Existing `audit.log.accessed` events | Unchanged on all query endpoints | ✓ (no modification to QueryController) |
| Existing denied-access audit | Unchanged | ✓ (no modification to filters) |
| Report execution/export audit | Unchanged | ✓ (no modification to reports service) |

---

## 11. Known Issues / Gaps

- **Legal hold mutations** — `POST /audit/legal-holds/{auditId}` (create) and `DELETE ...` (release)
  emit `ILogger.LogWarning` only. These are governance mutations (not access events) and are
  deferred to a future ticket.
- **Integrity checkpoint generation** — `POST /audit/integrity/checkpoints/generate` is PlatformAdmin-only
  and system-internal. Deferred.
- **Audit export service internal failure path** — if `_exportService.SubmitAsync` throws a
  non-`UnauthorizedAccessException`, the exception propagates normally (ASP.NET 500 handler). In
  this case `audit.log.exported` is not emitted — correct behavior (no successful access occurred).

---

## 12. Final Status

**COMPLETE**

### What was implemented
- **1 new canonical event type:** `audit.log.exported`
- **1 file changed:** `AuditExportController.cs`
- **Capture strategy:** endpoint-level, success-only, fire-and-observe, no deduplication needed
- **Tenant isolation:** enforced via `caller.TenantId` from middleware-resolved `IQueryCallerContext`
- **Viewer readiness:** queryable immediately via existing `/audit/events` API and viewer

### What was found to be already in place (no changes needed)
- Report execution audit: `execution.started`, `execution.completed`, `execution.failed`
- Report export audit: `export.started`, `export.completed`, `export.failed`
- Audit log query audit: `audit.log.accessed` on every query endpoint
- Identity admin mutation audit: 10+ event types for all user/role/group management

### Deferred
- Legal hold governance mutations audit (recommended: new ticket)
- Integrity checkpoint generation audit (recommended: new ticket)
