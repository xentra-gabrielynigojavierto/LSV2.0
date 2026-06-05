# SynqLiens Task Management Bug Report

**Date**: 2026-04-20  
**Bugs**: (A) Note author shows "Unknown"; (B) Task History tab shows "No history recorded yet"

---

## Bug A — Note author always shows "Unknown"

### Root cause

`TaskNoteEndpoints.CreateNote` received the JSON body as `CreateTaskNoteRequest` from the
frontend.  The frontend's `lien-task-notes.service.ts` sends only `{ content }` — it never
populates `createdByName`.  The service (`LienTaskNoteService.CreateNoteAsync`) falls back to
the literal string `"Unknown"` when `request.CreatedByName` is blank:

```csharp
// Liens.Application/Services/LienTaskNoteService.cs:53-55
createdByName: request.CreatedByName.Trim().Length > 0
                   ? request.CreatedByName.Trim()
                   : "Unknown");
```

The endpoint never resolved the author name from the JWT; it simply forwarded whatever the
client sent.

### Secondary root cause: missing JWT `name` claim

The `JwtTokenService` did not include a `name` (display name) claim in issued tokens.  The
only user-identifying claims in the JWT were `sub` (user GUID) and `email`.  This made it
impossible for any backend endpoint to obtain the user's full name from the token alone.

### Fix applied

1. **`apps/services/identity/Identity.Infrastructure/Services/JwtTokenService.cs`**  
   Added `JwtRegisteredClaimNames.Name` (`"name"`) claim set to `"{FirstName} {LastName}"`.
   This is backward-compatible — nothing breaks if a consumer ignores the new claim.

2. **`apps/services/liens/Liens.Api/Endpoints/TaskNoteEndpoints.cs`**  
   Injected `ClaimsPrincipal user` into `CreateNote`, resolved the author name via:
   ```csharp
   var authorName = user.FindFirstValue(ClaimTypes.Name)
                 ?? user.FindFirstValue("name")   // raw JWT claim (MapInboundClaims=false)
                 ?? ctx.Email;
   ```
   The resolved name is placed into `CreateTaskNoteRequest.CreatedByName` before calling the
   service.  The frontend no longer needs to send the author name.

---

## Bug B — Task History tab shows "No history recorded yet"

### Architecture recap

`LienTaskService` calls `_audit.Publish(entityType: "LienTask", entityId: task.Id)` after
every task mutation (create, update status, complete, cancel, assign, etc.).  `AuditPublisher`
builds an `IngestAuditEventRequest` and fires it via `HttpAuditEventClient.IngestAsync()` to
`http://localhost:5007/internal/audit/events` (fire-and-forget).  The audit service stores the
record in MySQL.  The History tab queries
`GET /audit/audit/entity/LienTask/{taskId}?pageSize=100&sortOrder=asc`, which is proxied through
the BFF → gateway → audit service query controller → EF repository.

### Investigation findings

| Layer | Finding |
|-------|---------|
| Gateway routing | Correct — `audit-service-query` strips `/audit-service` and forwards to `localhost:5007` |
| Audit BFF (`/api/audit/[...path]`) | Correct — segments are joined and forwarded with Bearer token |
| `HttpAuditEventClient` registration | `AddHttpClient<IAuditEventClient, HttpAuditEventClient>` → **transient**; `HttpAuditEventClient` does not implement `IDisposable` → the `HttpClient` is **not disposed** when the request scope ends → fire-and-forget is safe |
| `AuditPublisher.ContinueWith(OnlyOnFaulted)` | Dead code — `IngestAsync` catches all exceptions internally and returns a result object; the task never faults; `HttpAuditEventClient` does its own `LogWarning` on failures |
| Audit query authorization | Anonymous caller gets `CallerScope.PlatformAdmin` with no `TenantId`; `EnforceTenantScope` only applies to non-PlatformAdmin scopes → no tenant restriction on query |
| Entity filter | Repository filters `r.EntityType == "LienTask" AND r.EntityId == taskId` — both values come from `entity.Id.ToString()` (lowercase GUID) consistently |
| Deployment log evidence | `AUDIT_LOG_ACCESSED` log confirms the query reaches the audit service and returns `RecordsAccessed=0` — records are genuinely absent |
| Audit DB connectivity | Startup log shows `MySQL reported not connected (CanConnect=false)` before migrations ran; service recovered and started listening on `:5007` but any task events published during or shortly before this window would have been dropped with HTTP transport errors |

### Root cause

The task and its activity (status change to "Waiting / Blocked", note creation) appear to have
occurred during or immediately after the audit service's brief DB-connectivity failure at
startup.  Transport errors from `HttpAuditEventClient` would be logged at `Warning` level but
were not visible in the captured log window.  No records were persisted.

### Code deficiency identified (secondary)

`AuditPublisher.Publish()` does not log non-success results from `IngestAsync` (the
`ContinueWith(OnlyOnFaulted)` callback is unreachable).  `HttpAuditEventClient` already logs
warnings for non-201 responses and transport errors, so observable failure coverage exists —
but only at the HTTP-client layer, making it harder to correlate failures with specific
business events.

### No code fix required for the root cause

The history recording code path is correct.  New task operations after a stable deployment
will appear in the History tab.  The specific task in the screenshot predates a working audit
service state.

### Improvement: actor name in history events

`AuditEventActorDto.Name` is never populated by `AuditPublisher` (only `Id` is set).  History
events therefore display actor names as `"User {first-8-chars-of-UUID}"`.  Populating `Name`
requires threading the actor's display name through `LienTaskService` method signatures — a
future improvement tracked separately.
