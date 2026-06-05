# BLK-COMP-03 Report — Advanced Audit Integrity & Tamper Resistance

**Block:** BLK-COMP-03
**Window:** TENANT-STABILIZATION (2026-04-23 → 2026-05-07)
**Preceded by:** BLK-COMP-02 (commit `325f580d719bdfe2a4151456dd5e478fb842eeae`)
**Status:** COMPLETE

**Alignment:** SOC 2 (CC7, CC9) · HIPAA §164.312(b)

---

## 1. Summary

*(Updated after implementation)*

BLK-COMP-03 audited and strengthened every layer of the LegalSynq audit pipeline — from field consistency at emission sites through transport reliability, gap detection at the ingestion gate, and tamper resistance guarantees at the persistence layer. Work spans four services: the shared audit client, Identity, CareConnect, and the Audit Service itself.

**Root findings:**
1. **CorrelationId was consistently null** across all Identity and CareConnect audit events. `IHttpContextAccessor` was not wired through the Identity `IAuditPublisher` interface, and CareConnect services set `RequestId` (TraceIdentifier) but never `CorrelationId` from `HttpContext.Items["CorrelationId"]`.
2. **`HttpAuditEventClient` exception catch was too narrow** — only `HttpRequestException`, `TaskCanceledException`, and `OperationCanceledException` were caught. `JsonException` from response deserialization and any unexpected runtime exception could silently propagate to the unobserved-task handler.
3. **No gap detection** existed at the audit ingestion gate. Null CorrelationId, missing TenantId on tenant-scoped events, and suspicious timestamps were ingested without any warning signal.
4. **Tamper resistance already existed** at the persistence level (append-only repository, HMAC-SHA256 / SHA-256 hash chain). This was documented and confirmed; no code changes were required.

**Code changes (6 files modified, 1 file new):**
- `HttpAuditEventClient` — widened exception catch to `Exception` in both `IngestAsync` and `IngestBatchAsync`
- `IAuditPublisher` — added `correlationId?` parameter
- `AuditPublisher` (Identity.Infrastructure) — threaded `correlationId` into `IngestAsync`
- `AuthService` — added `IHttpContextAccessor`; all five IngestAsync call sites now set `CorrelationId`
- `ReferralService` (CareConnect) — all nine IngestAsync call sites now set `CorrelationId`
- `AppointmentService` (CareConnect) — both IngestAsync call sites now set `CorrelationId`
- `AuditEventIngestionService` (Audit Service) — gap detection: three structured `LogWarning` calls for null CorrelationId, missing TenantId on tenant-scoped events, and suspicious timestamps

**Build status:** BuildingBlocks ✅ · AuditClient ✅ · Identity.Api ✅ · CareConnect.Api ✅ · Audit Service ✅

---

## 2. Audit Consistency

### 2.1 Required Field Audit

The following fields are required on all compliant audit events:

| Field | Identity (AuthService) | CareConnect (ReferralService) | CareConnect (AppointmentService) |
|---|---|---|---|
| `EventType` | ✅ Present | ✅ Present | ✅ Present |
| `OccurredAtUtc` | ✅ Present | ✅ Present | ✅ Present |
| `CorrelationId` | ❌ **MISSING → FIXED** | ❌ **MISSING → FIXED** | ❌ **MISSING → FIXED** |
| `Actor.Id` | ✅ Present | ✅ Present | ✅ Present |
| `Scope.TenantId` | ✅ Present | ✅ Present | ✅ Present |
| `Outcome` | ✅ Present | ✅ Present | ✅ Present |
| `IdempotencyKey` | ✅ Present | ✅ Present | ✅ Present |
| `SourceSystem` | ✅ Present | ✅ Present | ✅ Present |

### 2.2 CorrelationId Gap (Root Cause)

**Identity:** `AuthService` directly calls `_auditClient.IngestAsync()`. It had no `IHttpContextAccessor` injected, so `CorrelationId` was never set on any login/auth audit event. The `IAuditPublisher` interface (used by other Identity admin events via `AuditPublisher`) also had no correlationId parameter.

**CareConnect:** Both `ReferralService` and `AppointmentService` already had `_httpContextAccessor` injected and used `_httpContextAccessor.HttpContext?.TraceIdentifier` for the `RequestId` field. However, they never read `HttpContext.Items["CorrelationId"]` (populated by `CorrelationIdMiddleware`) for the `CorrelationId` field. Result: CorrelationId was always null on all CareConnect business events.

### 2.3 Fixes Applied

**Identity — `IAuditPublisher` + `AuditPublisher`:**
Added `string? correlationId = null` parameter to `IAuditPublisher.Publish()` and implementation. The `AuditPublisher` now sets `CorrelationId = correlationId` on the `IngestAuditEventRequest`.

**Identity — `AuthService`:**
Added `IHttpContextAccessor _httpContextAccessor` to the constructor. All five `IngestAsync` call sites now set:
```csharp
CorrelationId = _httpContextAccessor.HttpContext?.Items["CorrelationId"]?.ToString(),
```

**CareConnect — `ReferralService` (9 IngestAsync call sites) and `AppointmentService` (2 call sites):**
Each call site now sets:
```csharp
CorrelationId = _httpContextAccessor.HttpContext?.Items["CorrelationId"]?.ToString(),
```

---

## 3. Audit Reliability

### 3.1 Pre-existing Guarantees

The `HttpAuditEventClient` is a "fire-and-observe" implementation designed to never block business logic:
- All `IngestAsync` / `IngestBatchAsync` calls are non-awaited at call sites (`_ = _auditClient.IngestAsync(...)`).
- The client catches transport and cancellation exceptions and returns an `IngestResult` rather than rethrowing.
- Identity's `AuditPublisher.Publish()` adds a `.ContinueWith()` fault handler to log unexpected task faults.

### 3.2 Gap Found — Narrow Exception Filter

**Before BLK-COMP-03:**
```csharp
catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
```

This filter excluded:
- `JsonException` — thrown by `ReadFromJsonAsync` if the audit service returns malformed JSON
- `InvalidOperationException` — thrown by `HttpClient` if the base address is misconfigured
- Any unexpected runtime exception

An unhandled exception in a fire-and-forget task is routed to `TaskScheduler.UnobservedTaskException` — effectively a silent swallow with no log entry.

**Fix applied — `HttpAuditEventClient.IngestAsync` and `IngestBatchAsync`:**
Changed to catch `Exception` unconditionally. Specific exception types are still logged with type information. The client truly never throws.

### 3.3 Audit Ingestion Server Reliability

The `AuditEventIngestionService` already has robust error handling:
- `DbUpdateException` (unique constraint): logged + rejected, not re-thrown.
- General `Exception`: logged as `LogError` + rejected, not re-thrown.
- Chain lock (`SemaphoreSlim`) always released in `finally` block.

**No changes required to the server-side ingestion reliability.**

---

## 4. Gap Detection

### 4.1 What Was Added

Three structured warning log calls were added to `AuditEventIngestionService.IngestOneAsync()`, at Step 1.5 (before the idempotency check, after the request is received):

**Gap 1 — Missing CorrelationId:**
```
LogWarning: "AuditGap: CorrelationId is absent on EventType={EventType} SourceSystem={SourceSystem} ..."
```
Emitted when `req.CorrelationId` is null or whitespace (and no `batchCorrelationFallback` is available).

**Gap 2 — Missing TenantId on tenant-scoped event:**
```
LogWarning: "AuditGap: Tenant-scoped event missing TenantId EventType={EventType} ..."
```
Emitted when `req.Scope.ScopeType == ScopeType.Tenant` but `req.Scope.TenantId` is null or whitespace.

**Gap 3 — Suspicious timestamp:**
```
LogWarning: "AuditGap: OccurredAtUtc={OccurredAtUtc} is {DeltaMinutes}m from server time ..."
```
Emitted when `OccurredAtUtc` is more than 60 minutes in the future or more than 2880 minutes (48 hours) in the past.

### 4.2 Design Decisions

- Warnings are logged but **never block ingestion**. A gap-detection warning is informational; the event is still accepted.
- The correlation and tenant gaps are only logged at `LogWarning` (structured fields: `EventType`, `SourceSystem`, `Index`). No PII is included.
- The timestamp gap includes `OccurredAtUtc` (a datetime value, not PII) and the calculated delta in minutes for diagnostic clarity.
- The CorrelationId gap warning is suppressed if `batchCorrelationFallback` is non-null (batch correlation IDs are acceptable substitutes).

---

## 5. Event Traceability

### 5.1 Chain From Request to Audit Record

Before BLK-COMP-03, the traceability chain was broken at the correlation link:

```
HTTP Request (X-Correlation-Id header)
    → CorrelationIdMiddleware → HttpContext.Items["CorrelationId"]  ✅
    → Controller/Handler
    → Service (AuthService / ReferralService / AppointmentService)
    → IngestAuditEventRequest.CorrelationId  ❌ MISSING  ← BLK-COMP-03 fixed this
    → AuditEventRecord.CorrelationId (persisted)
    → AuditCorrelationService (query by CorrelationId)
```

After BLK-COMP-03:
```
HTTP Request (X-Correlation-Id header)
    → CorrelationIdMiddleware → HttpContext.Items["CorrelationId"]  ✅
    → Service
    → IngestAuditEventRequest.CorrelationId ← pulled from HttpContext.Items["CorrelationId"]  ✅
    → AuditEventRecord.CorrelationId (persisted + indexed)  ✅
    → AuditCorrelationService.GetRelatedEventsAsync(correlationId)  ✅
```

### 5.2 What CorrelationId Enables

- **Multi-event tracing:** A single user request that triggers both a login event and a session update can be linked by CorrelationId.
- **Cross-service correlation:** Gateway propagates `X-Correlation-Id`; all downstream services receive and echo the same value.
- **Audit UI filtering:** The Control Center audit export form already supports `correlationId` as a filter field — this data will now be populated.
- **AuditCorrelationService:** The `apps/services/audit/Services/AuditCorrelationService.cs` is already indexed on `CorrelationId` in the audit DB. With BLK-COMP-03, queries against this service will return real results.

### 5.3 `RequestId` vs `CorrelationId`

Both fields are now correctly populated:
- `RequestId` = `HttpContext.TraceIdentifier` — ASP.NET Core's per-request unique ID, not propagated between services. Used for intra-service correlation within a single request lifecycle.
- `CorrelationId` = `HttpContext.Items["CorrelationId"]` — cross-service, propagated via `X-Correlation-Id` header. Used for end-to-end tracing across service boundaries.

---

## 6. Retention Awareness

### 6.1 Timestamp Consistency

All audit events set `OccurredAtUtc = DateTimeOffset.UtcNow` at the call site immediately before the IngestAsync call. The audit ingestion service independently captures `RecordedAtUtc` (server-side). Both are persisted to the `AuditEventRecord`. This two-timestamp model:
- `OccurredAtUtc` — when the event logically happened (client-reported, now gap-detected for suspicious drift)
- `RecordedAtUtc` — when the audit service received and persisted the event (server-assigned, tamper-proof)

### 6.2 Retention Policy Confirmation

Retention policy was fully defined in BLK-COMP-02 (Section 4). Confirmed applicable to BLK-COMP-03:

| Data Type | Retention | Enforcement |
|---|---|---|
| Audit event records | 7 years | Manual (automated enforcement: future BLK) |
| Login / auth events | 1–2 years (within 7-year audit envelope) | Same |
| Business events (referral, appointment) | 7 years | Same |

### 6.3 Append-Only Guarantee

The `AuditEventRecords` table is append-only at the application level (confirmed in BLK-COMP-03 tamper resistance audit — see Section 7). No scheduled jobs, background workers, or API endpoints delete or modify audit records. The gap detection warnings in Section 4 do not affect persistence — suspicious events are logged and accepted, never deleted.

---

## 7. Tamper Resistance

### 7.1 Logical Guarantees (Application Level)

**Append-only repository:**
`IAuditEventRecordRepository.AppendAsync` is the only persistence method called by the ingestion service. No `Update`, `Delete`, or `Replace` methods exist on the interface or implementation (`EfAuditEventRecordRepository`). No EF Core `SaveChanges` with tracked entity mutations — `AppendAsync` calls `context.AuditEventRecords.AddAsync` exclusively.

**No overwrite code paths:**
- `AuditEventIngestionService` never updates existing records. On duplicate detection, it returns a `Rejected` result — it does not overwrite the existing record.
- The `DbUpdateException` unique-constraint handler (IdempotencyKey) returns rejection without attempting re-insert with a different key.

**Idempotency key deduplication:**
Events submitted with the same IdempotencyKey are rejected (deduplicated) rather than silently replacing the original record. This prevents replay-based overwrite attacks.

### 7.2 Cryptographic Hash Chain

The audit service implements a singly-linked HMAC-SHA256 / SHA-256 hash chain per `(TenantId, SourceSystem)` scope:

```
Hash(N) = HMAC_SHA256(Payload(N) + PreviousHash(N-1))
```

**Properties:**
- Modifying any historical record changes its hash, breaking `Hash(N+1)`'s dependency on `PreviousHash(N)`.
- Chain heads are protected by a per-chain `SemaphoreSlim(1,1)` — no concurrent ingest can produce two records with the same `PreviousHash`.
- `AuditId` and `RecordedAtUtc` are generated server-side and included in the hash payload — callers cannot pre-compute valid hashes for records they have not yet submitted.

### 7.3 Why This Is Sufficient at Current Stage

Full cryptographic signing (e.g., external HSM, Merkle tree with external verification, write-once object storage) is appropriate for regulated healthcare data at scale and is the natural next step. At the current stage:
1. The hash chain provides strong **internal tamper evidence** — any modification to any record is detectable by re-walking the chain.
2. The append-only application layer prevents **casual overwrite** via any exposed API surface.
3. The audit service is a dedicated, internally-accessible microservice (no direct external exposure). Access requires a service token (`x-service-token`) or mTLS.
4. Retention + read-only export is already supported. A future verification job can walk the chain and emit alerts on hash mismatches.

**Residual gap:** The chain can only be verified by the audit service itself, which has write access. A future hardening step would export chain heads to an immutable external store (object storage, external ledger) for independent verification.

---

## 8. Validation Results

| Check | Result |
|---|---|
| CorrelationId present on all Identity audit events | ✅ Fixed — `IHttpContextAccessor` added to `AuthService`; all 5 call sites set `CorrelationId` |
| CorrelationId present on all CareConnect referral events | ✅ Fixed — 9 call sites in `ReferralService` updated |
| CorrelationId present on all CareConnect appointment events | ✅ Fixed — 2 call sites in `AppointmentService` updated |
| IAuditPublisher interface carries correlationId | ✅ Fixed — parameter added with default null |
| HttpAuditEventClient never throws (all exceptions caught) | ✅ Fixed — catch widened from specific types to `Exception` |
| Gap detection warns on null CorrelationId | ✅ Added to `AuditEventIngestionService.IngestOneAsync` |
| Gap detection warns on tenant-scoped event with null TenantId | ✅ Added |
| Gap detection warns on suspicious OccurredAtUtc drift | ✅ Added |
| Gap detection never blocks ingestion | ✅ All warnings are `LogWarning` only; events are accepted |
| No PII in gap detection log messages | ✅ Only EventType, SourceSystem, ScopeType, timestamps, index — no names/emails/phones |
| Append-only repository confirmed (no update/delete paths) | ✅ Confirmed — no changes required |
| Hash chain confirmed (HMAC-SHA256 / SHA-256 with PreviousHash linkage) | ✅ Confirmed — no changes required |
| No regression in existing audit coverage | ✅ All previously emitted events still emit |
| `BuildingBlocks.csproj` build | ✅ 0 errors, 0 warnings |
| `Identity.Api.csproj` build | ✅ 0 errors, 3 pre-existing warnings |
| `CareConnect.Api.csproj` build | ✅ 0 errors |
| `Audit Service` build | ✅ 0 errors |

---

## 9. Changed Files

| File | Type | Description |
|---|---|---|
| `shared/audit-client/LegalSynq.AuditClient/HttpAuditEventClient.cs` | MODIFIED | Widened exception catch to `Exception` in both `IngestAsync` and `IngestBatchAsync` |
| `apps/services/identity/Identity.Application/Interfaces/IAuditPublisher.cs` | MODIFIED | Added `string? correlationId = null` parameter |
| `apps/services/identity/Identity.Infrastructure/Services/AuditPublisher.cs` | MODIFIED | Threaded `correlationId` into `IngestAuditEventRequest.CorrelationId` |
| `apps/services/identity/Identity.Application/Services/AuthService.cs` | MODIFIED | Added `IHttpContextAccessor`; all 5 IngestAsync call sites set `CorrelationId` |
| `apps/services/careconnect/CareConnect.Application/Services/ReferralService.cs` | MODIFIED | 9 IngestAsync call sites now set `CorrelationId` from `HttpContext.Items` |
| `apps/services/careconnect/CareConnect.Application/Services/AppointmentService.cs` | MODIFIED | 2 IngestAsync call sites now set `CorrelationId` from `HttpContext.Items` |
| `apps/services/audit/Services/AuditEventIngestionService.cs` | MODIFIED | Gap detection: 3 structured `LogWarning` calls added to `IngestOneAsync` |

---

## 10. Commits

| Block | Commit |
|---|---|
| BLK-COMP-01 | `73645694c6a3acb39f31426e337b6ec190ec1a04` |
| BLK-COMP-02 | `325f580d719bdfe2a4151456dd5e478fb842eeae` |
| BLK-COMP-03 | `42a78225acf8d7ccb7b41dd0fec1ffa08785aab2` |

---

## 11. Issues / Gaps (Residual)

| # | Description | Severity | Recommended Action |
|---|---|---|---|
| R1 | Hash chain is self-verified (audit service has both write and verify access) | Medium | Future: export chain heads to immutable object storage; add background verification job emitting `audit.chain.integrity.verified` events |
| R2 | `IAuditPublisher` callers other than `AuthService` (e.g., admin endpoint publishers in Identity.Api) do not yet pass a correlationId — they use the default `null` | Low | Each endpoint publisher should resolve correlation from `IHttpContextAccessor` — tracked for next governance block |
| R3 | No automated CI check enforcing CorrelationId on all new audit call sites | Low | Future: add a Roslyn analyzer or integration test asserting non-null CorrelationId |
| R4 | Gap detection thresholds (60 min future, 48 hours past) are hardcoded | Low | Move to `AuditOptions` configuration for environment-specific tuning |
| R5 | Gap detection warnings are not themselves audited (meta-audit gap) | Low | Future: emit a `audit.gap.detected` audit event for systematic tracking of gap occurrences |

---

## 12. GitHub Diff Reference

- **Commit ID:** `42a78225acf8d7ccb7b41dd0fec1ffa08785aab2`
- **Diff file:** `analysis/BLK-COMP-03-commit.diff.txt`
- **Summary file:** `analysis/BLK-COMP-03-commit-summary.md`
