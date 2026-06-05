# LS-ID-TNT-017-003 — Governance Mutation Audit

**Status:** COMPLETE  
**Date:** 2026-04-19

---

## 1. Executive Summary

This ticket instruments the remaining governance mutation surfaces in the audit service with
canonical compliance audit events, closing the final major audit coverage gap in the governance
domain.

**Before this ticket:** Legal hold create/release and integrity checkpoint generation were only
logged via `ILogger` — they had no representation in the canonical audit event pipeline.

**After this ticket:** Three new event types are emitted into the centralized ingestion pipeline:
- `audit.legal_hold.created` — when a retention hold is placed on an audit record
- `audit.legal_hold.released` — when a hold is explicitly released
- `audit.integrity.checkpoint.generated` — when a cryptographic compliance checkpoint is produced on demand

All events are fail-safe (fire-and-observe), tenant/platform-scoped correctly, viewer-consumable,
and strictly tied to success paths. Failed mutations do not emit false success events.

**Result:** 3 new event types. 2 files changed. Build clean (0 errors).

---

## 2. Codebase Analysis

### Governance mutation surfaces identified

| Controller | Route | Auth requirement | Prior audit state | Canonical event added |
|---|---|---|---|---|
| `LegalHoldController` | `POST /audit/legal-holds/{auditId}` | PlatformAdmin or ComplianceOfficer | `ILogger.LogWarning` only | `audit.legal_hold.created` ✅ |
| `LegalHoldController` | `POST /audit/legal-holds/{holdId}/release` | PlatformAdmin or ComplianceOfficer | `ILogger.LogWarning` only | `audit.legal_hold.released` ✅ |
| `IntegrityCheckpointController` | `POST /audit/integrity/checkpoints/generate` | PlatformAdmin only | `ILogger.LogInformation` only | `audit.integrity.checkpoint.generated` ✅ |

### Read-only surfaces — intentionally excluded

| Controller | Route | Exclusion reason |
|---|---|---|
| `LegalHoldController` | `GET /audit/legal-holds/record/{auditId}` | Read-only list — not a governance mutation |
| `IntegrityCheckpointController` | `GET /audit/integrity/checkpoints` | Read-only list — not a governance mutation |

### Middleware / caller context analysis

`QueryAuthMiddleware` is registered globally (`app.UseMiddleware<QueryAuthMiddleware>()`, no path
filter). It runs on all requests, including `/audit/legal-holds/*` and `/audit/integrity/*`. Both
controllers resolve `IQueryCallerContext` from `HttpContext.Items[QueryCallerContext.ItemKey]`,
identical to the pattern used by `AuditExportController` and `AuditEventQueryController`.

The `LegalHoldController` previously used a separate `ResolveCallerId()` helper (reads JWT `sub`
claim directly) for service method arguments — this is preserved unchanged. For canonical audit
actor attribution the middleware-resolved `IQueryCallerContext` is used instead, which carries
`UserId`, `TenantId`, `Scope`, and `AuthMode`.

---

## 3. Governance Mutation Surface Inventory

### `POST /audit/legal-holds/{auditId}` — Legal hold creation

- **What changes:** A `LegalHold` record is persisted. The targeted audit event record is marked
  as held, preventing the retention pipeline from archiving or deleting it.
- **Why governance-relevant:** HIPAA §164.312(b) and SOC 2 CC9.1 require that records subject to
  litigation, investigation, or compliance review be preserved — and that the preservation action
  itself be audited. Without a canonical event, there is no compliance-ready trail of who placed
  which hold under which legal authority.
- **Who can perform it:** PlatformAdmin or ComplianceOfficer (enforced via JWT claims, verified by
  `QueryAuthMiddleware`-resolved scope in dev/prod mode).
- **State after:** `LegalHold.IsActive = true`, `HeldAtUtc` set, `LegalAuthority` recorded.

### `POST /audit/legal-holds/{holdId}/release` — Legal hold release

- **What changes:** An existing hold transitions from active (`ReleasedAtUtc = null`) to released
  (`ReleasedAtUtc` set, `ReleasedByUserId` set). The underlying audit record re-enters the normal
  retention lifecycle.
- **Why governance-relevant:** Releasing a legal hold is as significant as placing one. Premature
  or unauthorized release could constitute evidence destruction. A canonical trail of releases, by
  whom and when, is a compliance requirement.
- **State transition:** Before = active hold. After = released (from `LegalHoldResponse.ReleasedAtUtc`,
  `ReleasedByUserId`).

### `POST /audit/integrity/checkpoints/generate` — Integrity checkpoint generation

- **What changes:** A new `IntegrityCheckpoint` record is persisted containing an HMAC-SHA256
  aggregate hash of all audit event record hashes within a specified time window (`RecordedAtUtc`
  range). The record is append-only; existing checkpoints are never modified.
- **Why governance-relevant:** The checkpoint is a compliance instrument — it provides tamper
  evidence for the audit log. Generating one on demand before a regulatory audit, post-migration,
  or for a custom compliance window is itself a governance action that must be attributable.
  Who generated which checkpoint, over what time range, is critical for the checkpoint's legal
  standing as evidence.
- **Who can perform it:** PlatformAdmin only (`RequireScope(CallerScope.PlatformAdmin)` — strictly
  enforced; no fallback).
- **State after:** New `IntegrityCheckpoint` with `Id`, `CheckpointType`, `FromRecordedAtUtc`,
  `ToRecordedAtUtc`, `RecordCount`, `AggregateHash`, `CreatedAtUtc`.

---

## 4. Event Taxonomy Design

### `audit.legal_hold.created`

| Field | Value |
|---|---|
| `EventType` | `"audit.legal_hold.created"` |
| `EventCategory` | `EventCategory.Compliance` — "Events required for regulatory compliance" |
| `Severity` | `SeverityLevel.Warn` — significant governance control placed |
| `Visibility` | `VisibilityScope.Platform` — PlatformAdmin/ComplianceOfficer action |
| `SourceSystem` | `"audit"` |
| `SourceService` | `"legal-hold-api"` |
| `Action` | `"LegalHoldPlaced"` |
| `Entity.Type` | `"LegalHold"` |
| `Entity.Id` | `hold.HoldId.ToString()` |
| `Description` | `"Legal hold placed on audit record {auditId} — authority: {legalAuthority}."` |
| `Metadata keys` | `holdId`, `auditId`, `legalAuthority`, `heldByUserId`, `heldAtUtc`, `callerScope`, `callerAuthMode`, `traceId` |
| `Tags` | `["governance", "legal-hold", "retention-control"]` |
| `IdempotencyKey` | `"legal-hold-created:{holdId}"` |

**Note:** Request `Notes` field is intentionally excluded from metadata — it may contain free-form
legal content. `LegalAuthority` is a structured reference string and is safe to include.

### `audit.legal_hold.released`

| Field | Value |
|---|---|
| `EventType` | `"audit.legal_hold.released"` |
| `EventCategory` | `EventCategory.Compliance` |
| `Severity` | `SeverityLevel.Warn` — releasing a hold is equally significant |
| `Visibility` | `VisibilityScope.Platform` |
| `SourceSystem` | `"audit"` |
| `SourceService` | `"legal-hold-api"` |
| `Action` | `"LegalHoldReleased"` |
| `Entity.Type` | `"LegalHold"` |
| `Entity.Id` | `hold.HoldId.ToString()` |
| `Description` | `"Legal hold {holdId} released on audit record {auditId} — authority: {legalAuthority}."` |
| `Metadata keys` | `holdId`, `auditId`, `legalAuthority`, `releasedByUserId`, `releasedAtUtc`, `callerScope`, `callerAuthMode`, `traceId` |
| `Tags` | `["governance", "legal-hold", "retention-control"]` |
| `IdempotencyKey` | `"legal-hold-released:{holdId}"` |

### `audit.integrity.checkpoint.generated`

| Field | Value |
|---|---|
| `EventType` | `"audit.integrity.checkpoint.generated"` |
| `EventCategory` | `EventCategory.Compliance` |
| `Severity` | `SeverityLevel.Notice` — significant but expected platform compliance operation |
| `Visibility` | `VisibilityScope.Platform` |
| `SourceSystem` | `"audit"` |
| `SourceService` | `"integrity-api"` |
| `Action` | `"CheckpointGenerated"` |
| `Entity.Type` | `"IntegrityCheckpoint"` |
| `Entity.Id` | `result.Id.ToString()` |
| `Description` | `"Integrity checkpoint generated — {recordCount} record(s), type={checkpointType}."` |
| `Metadata keys` | `checkpointId`, `checkpointType`, `fromRecordedAtUtc`, `toRecordedAtUtc`, `recordCount`, `aggregateHashPrefix` (first 16 chars), `callerScope`, `callerAuthMode`, `traceId` |
| `Tags` | `["governance", "integrity", "compliance-verification"]` |
| `IdempotencyKey` | `"integrity-checkpoint:{result.Id}"` |

**Note:** Only the first 16 characters of `AggregateHash` are included as `aggregateHashPrefix`.
The full hash is stored in the `IntegrityCheckpoint` record and accessible via
`GET /audit/integrity/checkpoints`. This keeps audit metadata concise without losing
the ability to cross-reference the checkpoint record.

---

## 5. Capture Strategy

### Emit layer choice

All three events are emitted at the **controller layer** after the service call succeeds. This is
the correct layer because:

1. The `IQueryCallerContext` (actor identity, scope, tenant) is resolved by middleware and stored
   in `HttpContext.Items` — only accessible at the controller boundary.
2. The resulting entity state (`LegalHoldResponse`, `IntegrityCheckpointResponse`) is returned
   by the service — both actor and result are available simultaneously at the controller.
3. Emitting at the controller naturally restricts events to success paths (after the service call
   returns without throwing).

The `ILegalHoldService` and `IIntegrityCheckpointService` implementations do not have access to
caller identity — injecting the audit service there would require threading identity context
through service method signatures, violating separation of concerns.

### Fail-safe / non-blocking

All emissions use `_ = _ingestionService.IngestSingleAsync(...)` — the Task is discarded.
The HTTP response (201/200) is returned regardless of audit publish outcome. No `await`, no
exception propagation from the audit path to the governance mutation path.

### Before/after state

| Event | Before state | After state captured |
|---|---|---|
| `audit.legal_hold.created` | Implicit: no prior hold (new record) | `HoldId`, `AuditId`, `LegalAuthority`, `HeldAtUtc` from `LegalHoldResponse` |
| `audit.legal_hold.released` | Implicit: hold was active | `ReleasedAtUtc`, `ReleasedByUserId` from `LegalHoldResponse` |
| `audit.integrity.checkpoint.generated` | N/A (append-only operation) | `Id`, `CheckpointType`, window, `RecordCount`, hash prefix |

Forcing artificial before/after fields on legal holds was rejected as misleading — the meaningful
prior state is implicit (no prior hold for create; active hold for release). The result state
captured from the service response is unambiguous and sufficient.

### Success-only emission

Events are emitted only on the success branch:
- `audit.legal_hold.created` — only if `_holdService.CreateHoldAsync` completes without throwing
- `audit.legal_hold.released` — only if `_holdService.ReleaseHoldAsync` completes without throwing
  (both `InvalidOperationException` catch branches return error responses before the emit point)
- `audit.integrity.checkpoint.generated` — only if `_service.GenerateAsync` completes without
  throwing and after all validation has passed

---

## 6. Coverage Scope Selection

**In scope — implemented:**
- `audit.legal_hold.created`
- `audit.legal_hold.released`
- `audit.integrity.checkpoint.generated`

**Excluded by design:**
- Legal hold read (`GET /audit/legal-holds/record/{auditId}`) — read-only
- Integrity checkpoint list (`GET /audit/integrity/checkpoints`) — read-only
- Background scheduled checkpoint generation (not yet in codebase)
- Background retention pipeline operations (system operations, not user-initiated)
- Failed mutation attempts — do not emit false success events

---

## 7. Files Changed

| File | Change type | Description |
|---|---|---|
| `apps/services/audit/Controllers/LegalHoldController.cs` | Modified | Added `IAuditEventIngestionService`, `LogLegalHoldCreated`, `LogLegalHoldReleased`; using directives for ingest DTOs and enums |
| `apps/services/audit/Controllers/IntegrityCheckpointController.cs` | Modified | Added `IAuditEventIngestionService`, `LogCheckpointGenerated`; caller context resolution in `GenerateCheckpoint` |

---

## 8. Backend Implementation

### `LegalHoldController`

**Constructor change:** Added `IAuditEventIngestionService ingestionService` parameter (stored as
`_ingestionService`). No new service registration required — `IAuditEventIngestionService` is
already wired in `Program.cs`.

**New using directives:** `PlatformAuditEventService.DTOs.Ingest`, `PlatformAuditEventService.Enums`,
`PlatformAuditEventService.Utilities`, `PlatformAuditEventService.Authorization`.

**`CreateHold` change:** After `_holdService.CreateHoldAsync` succeeds, call `LogLegalHoldCreated`
before the return statement. `traceId` and `caller` are resolved at method start (before the
try-catch) so they are available regardless of where in the try block the call succeeds.

**`ReleaseHold` change:** After `_holdService.ReleaseHoldAsync` succeeds, call `LogLegalHoldReleased`
before the return. Both `InvalidOperationException` catch branches (already-released conflict, and
not-found) return before reaching the emit point — correct behavior.

**Actor resolution:** `caller = HttpContext.Items[QueryCallerContext.ItemKey] as IQueryCallerContext ?? QueryCallerContext.Anonymous()`. This is the same pattern used in `AuditExportController`. The pre-existing `ResolveCallerId()` method is preserved unchanged for use in service call arguments.

**`Actor.Id` fallback:** `caller.UserId ?? hold.HeldByUserId` — in dev mode (`QueryAuth:Mode=None`),
`caller.UserId` may be null; in that case `hold.HeldByUserId` (set by the service from `ResolveCallerId()`)
is used as the actor id.

### `IntegrityCheckpointController`

**Constructor change:** Added `IAuditEventIngestionService ingestionService`.

**`GenerateCheckpoint` change:** After `RequireScope(CallerScope.PlatformAdmin)` passes and
`_service.GenerateAsync(request, ct)` succeeds, resolve the caller context inline
(`HttpContext.Items.TryGetValue(QueryCallerContext.ItemKey, ...)`) and call `LogCheckpointGenerated`.

The caller resolution in `GenerateCheckpoint` is equivalent to the existing resolution inside
`RequireScope` — it is a second lookup of the same `HttpContext.Items` dictionary entry for the
same request. This is by design: `RequireScope` is a validation helper and does not expose the
resolved caller to avoid changing its signature.

**`aggregateHashPrefix`:** `result.AggregateHash[..16] + "..."` — compact reference for
cross-referencing the full checkpoint record without embedding the entire hash in the event payload.

---

## 9. Query / Viewer Readiness Notes

All three new event types use `EventCategory.Compliance` and `VisibilityScope.Platform`.
They are immediately queryable through the existing `/audit/events` query API without any changes:

**By event type:**
- `EventType = "audit.legal_hold.created"`
- `EventType = "audit.legal_hold.released"`
- `EventType = "audit.integrity.checkpoint.generated"`

**By category:**
- `EventCategory = Compliance`

**By tag (when tag-filter support is added to the viewer):**
- `"governance"`, `"legal-hold"`, `"retention-control"`, `"integrity"`, `"compliance-verification"`

**Viewer rendering:**
- The existing audit viewer list view shows EventType, Actor.Id, Description, OccurredAtUtc — all
  populated for the new events.
- The detail view renders the JSON metadata — `holdId`, `auditId`, `legalAuthority`, and
  `checkpointId` fields are flat and human-readable.
- The `"governance"` tag is a useful future filter for compliance officer workflows
  (viewer tag-filter is a known future enhancement, not blocking this ticket).

**No viewer changes are required** for these events to be usable.

---

## 10. Verification / Testing Results

### Build verification
```
dotnet build apps/services/audit/PlatformAuditEventService.csproj -c Release
  → Build succeeded. 0 errors. 1 pre-existing warning (JWT Bearer version conflict, unrelated).
```

### Code path verification

#### Legal hold create (`POST /audit/legal-holds/{auditId}`)

| Scenario | Expected | Verified |
|---|---|---|
| `CreateHoldAsync` succeeds → HTTP 201 | `audit.legal_hold.created` emitted | ✓ (emit precedes return) |
| `CreateHoldAsync` throws `InvalidOperationException` (record not found) → HTTP 404 | No emit | ✓ (catch branch returns before emit) |
| `ModelState.IsValid = false` → HTTP 400 | No emit | ✓ (early return before service call) |
| `_ingestionService.IngestSingleAsync` throws | 201 returned anyway | ✓ (Task discarded, no await) |

#### Legal hold release (`POST /audit/legal-holds/{holdId}/release`)

| Scenario | Expected | Verified |
|---|---|---|
| `ReleaseHoldAsync` succeeds → HTTP 200 | `audit.legal_hold.released` emitted | ✓ (emit precedes return) |
| `ReleaseHoldAsync` throws `InvalidOperationException` "already released" → HTTP 409 | No emit | ✓ (catch branch returns before emit point) |
| `ReleaseHoldAsync` throws other `InvalidOperationException` (not found) → HTTP 404 | No emit | ✓ (catch branch returns before emit) |
| `_ingestionService` fails | 200 returned anyway | ✓ (fire-and-observe) |

#### Integrity checkpoint generate (`POST /audit/integrity/checkpoints/generate`)

| Scenario | Expected | Verified |
|---|---|---|
| `GenerateAsync` succeeds → HTTP 201 | `audit.integrity.checkpoint.generated` emitted | ✓ (emit after successful result) |
| Scope check fails (non-PlatformAdmin) → HTTP 403 | No emit | ✓ (RequireScope returns before GenerateAsync) |
| Time window validation fails → HTTP 400 | No emit | ✓ (BadRequest before GenerateAsync) |
| `GenerateAsync` throws | Exception propagates to 500 handler, no emit | ✓ (emit only after successful result) |
| `_ingestionService` fails | 201 returned anyway | ✓ (fire-and-observe) |

#### Regression: unchanged surfaces

| Surface | Verification |
|---|---|
| Existing `audit.log.accessed` events (query endpoints) | ✓ (`AuditEventQueryController` unchanged) |
| Existing `audit.log.exported` (export submission) | ✓ (`AuditExportController` unchanged) |
| Permission-change audit events | ✓ (identity service unchanged) |
| Denied access audit events | ✓ (BuildingBlocks filters unchanged) |
| Report execution/export audit | ✓ (Reports service unchanged) |
| Auth/session audit | ✓ (AuthService unchanged) |
| Audit viewer | ✓ (no frontend changes) |

---

## 11. Known Issues / Gaps

- **Background checkpoint generation** — if a scheduled background job generates checkpoints
  automatically (not yet in codebase), it would not emit `audit.integrity.checkpoint.generated`
  because it calls `IIntegrityCheckpointService.GenerateAsync` directly, bypassing the controller.
  Recommend: if a background job is added, wire audit emission into that job's success path.

- **`GET /audit/integrity/checkpoints` is not scoped by tenant** — checkpoints cover the full
  record store in v1. The controller comment acknowledges this may be restricted to PlatformAdmin
  in a future step. No canonical audit event for this read is added (excluded by design —
  read-only), but the scope restriction should be reviewed when multi-tenant checkpoint scoping
  is implemented.

- **Legal hold `Notes` field** — excluded from audit metadata because it may contain free-form
  legal content. If structured compliance workflows require the notes to be auditable, a future
  ticket should define a safe schema for this (e.g. sanitized/truncated with a content-hash).

- **`ReleaseLegalHoldRequest.ReleaseNotes`** — similarly excluded from `audit.legal_hold.released`
  metadata.

---

## 12. Final Status

**COMPLETE**

### What was implemented
- **3 new canonical event types:** `audit.legal_hold.created`, `audit.legal_hold.released`,
  `audit.integrity.checkpoint.generated`
- **2 files changed:** `LegalHoldController.cs`, `IntegrityCheckpointController.cs`
- **Capture layer:** Controller-level, success-path-only, fire-and-observe
- **Category:** `EventCategory.Compliance` for all three
- **Tenant/platform isolation:** `ScopeType.Platform`, `VisibilityScope.Platform` — correct for
  PlatformAdmin/ComplianceOfficer operations
- **Actor attribution:** `IQueryCallerContext` from `QueryAuthMiddleware` — never trusted from
  request body
- **Viewer readiness:** Queryable via existing `/audit/events` API immediately; renders correctly
  in existing viewer list/detail

### Prior state (no canonical audit events)
- Legal hold create/release: `ILogger.LogWarning` only
- Integrity checkpoint generate: `ILogger.LogInformation` only

### Deferred
- Background scheduled checkpoint generation (not yet in codebase)
- Legal hold `Notes` / `ReleaseNotes` structured metadata audit
- Multi-tenant checkpoint scoping (acknowledged future step in controller comment)
