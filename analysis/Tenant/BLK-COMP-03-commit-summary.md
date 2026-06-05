# BLK-COMP-03 Commit Summary

**Block:** BLK-COMP-03 — Advanced Audit Integrity & Tamper Resistance
**Preceded by:** BLK-COMP-02 commit `325f580d719bdfe2a4151456dd5e478fb842eeae`
**Commit:** `42a78225acf8d7ccb7b41dd0fec1ffa08785aab2`

---

## Files Changed

| File | Type | Description |
|---|---|---|
| `shared/audit-client/LegalSynq.AuditClient/HttpAuditEventClient.cs` | MODIFIED | Widened exception catch to `Exception` in both methods |
| `apps/services/identity/Identity.Application/Interfaces/IAuditPublisher.cs` | MODIFIED | Added `string? correlationId = null` parameter |
| `apps/services/identity/Identity.Infrastructure/Services/AuditPublisher.cs` | MODIFIED | Threaded `correlationId` into `IngestAuditEventRequest` |
| `apps/services/identity/Identity.Application/Services/AuthService.cs` | MODIFIED | Added `IHttpContextAccessor`; `CurrentCorrelationId` property; 5 call sites updated |
| `apps/services/careconnect/CareConnect.Application/Services/ReferralService.cs` | MODIFIED | 10 IngestAsync call sites: added `CorrelationId` from `HttpContext.Items` |
| `apps/services/careconnect/CareConnect.Application/Services/AppointmentService.cs` | MODIFIED | 2 IngestAsync call sites: added `CorrelationId` from `HttpContext.Items` |
| `apps/services/audit/Services/AuditEventIngestionService.cs` | MODIFIED | Gap detection Step 0.5 added; `using PlatformAuditEventService.Enums` added |
| `analysis/BLK-COMP-03-report.md` | NEW | Full report (Parts A–H) |
| `analysis/BLK-COMP-03-commit-summary.md` | NEW | This file |
| `analysis/BLK-COMP-03-commit.diff.txt` | NEW | Diff reference |

---

## Key Changes

### 1. `HttpAuditEventClient` — Full Exception Coverage (Part B)

**Before:**
```csharp
catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
{
    _logger.LogWarning(ex, "AuditEvent ingest transport error: EventType={EventType}", request.EventType);
    return new IngestResult(false, null, "TransportError", 0);
}
```

**After (both `IngestAsync` and `IngestBatchAsync`):**
```csharp
catch (Exception ex)
{
    _logger.LogWarning(ex, "AuditEvent ingest error: EventType={EventType} ExceptionType={ExType}",
        request.EventType, ex.GetType().Name);
    return new IngestResult(false, null, "ClientError", 0);
}
```

Previously, `JsonException` (from `ReadFromJsonAsync`) and other unexpected runtime exceptions could escape the fire-and-observe client and reach the unobserved-task exception handler silently. Now all exceptions are caught, logged with the exception type, and return a failure result — preserving the non-blocking contract.

---

### 2. `IAuditPublisher` + `AuditPublisher` — CorrelationId Support (Parts A + D)

**Before:**
```csharp
void Publish(string eventType, ..., string? metadata = null);
```

**After:**
```csharp
void Publish(string eventType, ..., string? metadata = null, string? correlationId = null);
```

`AuditPublisher.Publish()` now sets `CorrelationId = correlationId` on the request. Default is `null` — backwards-compatible with all existing callers that don't supply a correlation ID.

---

### 3. `AuthService` — CorrelationId Wired In (Parts A + D)

Added `IHttpContextAccessor _httpContextAccessor` (already registered in DI at `Identity.Infrastructure/DependencyInjection.cs:49`).

Added `CurrentCorrelationId` convenience property:
```csharp
private string? CurrentCorrelationId =>
    _httpContextAccessor.HttpContext?.Items["CorrelationId"]?.ToString();
```

All 5 `IngestAsync` call sites updated:

| Event | Call site |
|---|---|
| `identity.user.login.succeeded` | `CorrelationId = CurrentCorrelationId` |
| `identity.user.login.failed` | `CorrelationId = CurrentCorrelationId` |
| `identity.session.invalidated` | `CorrelationId = CurrentCorrelationId` |
| `identity.access.version.stale` | `CorrelationId = CurrentCorrelationId` |
| `identity.user.login.blocked` | `CorrelationId = CurrentCorrelationId` |

---

### 4. CareConnect `ReferralService` + `AppointmentService` — CorrelationId Wired In (Parts A + D)

Both services already had `IHttpContextAccessor` injected and set `RequestId = _httpContextAccessor.HttpContext?.TraceIdentifier`. This is the ASP.NET Core internal trace ID — useful for single-request correlation but not propagated across services.

Added alongside it at all call sites:
```csharp
CorrelationId = _httpContextAccessor.HttpContext?.Items["CorrelationId"]?.ToString(),
```

This reads the value set by `CorrelationIdMiddleware` from the `X-Correlation-Id` request header — the platform-wide distributed tracing value.

**Call sites updated:**
- `ReferralService`: 10 IngestAsync call sites (referral.created, referral.updated ×2, referral.viewed, referral.cancelled, referral.assigned, referral.token.renewed, provider.activation.requested, provider.activated, provider.reactivated)
- `AppointmentService`: 2 IngestAsync call sites (appointment.scheduled, appointment.cancelled)

---

### 5. `AuditEventIngestionService` — Gap Detection (Part C)

Added Step 0.5 to `IngestOneAsync`. Three non-blocking gap checks run before the idempotency probe:

```csharp
// Gap 1: Missing CorrelationId
var effectiveCorrelationId = string.IsNullOrWhiteSpace(req.CorrelationId)
    ? batchCorrelationFallback : req.CorrelationId;
if (string.IsNullOrWhiteSpace(effectiveCorrelationId))
    _logger.LogWarning("AuditGap: CorrelationId is absent on EventType={EventType} ...");

// Gap 2: Tenant-scoped event with no TenantId
if (req.Scope.ScopeType == ScopeType.Tenant && string.IsNullOrWhiteSpace(req.Scope.TenantId))
    _logger.LogWarning("AuditGap: Tenant-scoped event missing TenantId ...");

// Gap 3: OccurredAtUtc drift (>60 min future or >48 hours past)
if (deltaMinutes > 60 || deltaMinutes < -2880)
    _logger.LogWarning("AuditGap: OccurredAtUtc={OccurredAtUtc} is {DeltaMinutes}m from server time ...");
```

Events are **never rejected** by gap detection — ingestion proceeds normally after the warnings are logged.

---

## What Was NOT Changed

- **Audit DB schema** — no migrations required. All changes are application-layer only.
- **API contracts** — no request/response shape changes.
- **Tamper resistance infrastructure** — the HMAC-SHA256 hash chain, append-only repository, and chain-lock semaphores are confirmed complete and unmodified.
- **Retention policy** — confirmed aligned with BLK-COMP-02 definitions. No code changes required.

---

## Compliance Posture

| Control | Before BLK-COMP-03 | After BLK-COMP-03 |
|---|---|---|
| SOC 2 CC7.2: correlated event detection | CorrelationId null on all auth/referral/appointment events | CorrelationId populated from request context on all events |
| SOC 2 CC9.1: complete audit trail | Audit client could silently swallow JsonException | All exceptions caught and logged |
| HIPAA §164.312(b): activity review | Auth events unlinked from request chain | Auth events now traceable by CorrelationId |
| SOC 2 CC7.3: anomaly detection | No gap detection at ingestion | 3 structured gap warnings added |
