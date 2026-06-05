# LS-ID-TNT-017-006 — Audit Correlation Engine

**Status:** COMPLETE  
**Date:** 2026-04-19  
**Ticket:** Deterministic correlation capability linking related audit events across a cascade of correlation keys.

---

## Summary

Implemented a four-tier correlation cascade engine that enables operators to navigate from any single audit event to its full investigation chain. The implementation reuses all existing query and authorization infrastructure; no schema migrations are required.

---

## Architecture

### Cascade Strategy

| Tier | Match Condition | Label | Window | Cap |
|------|----------------|-------|--------|-----|
| 1 | `CorrelationId` exact match | `correlation_id` | — | 200 |
| 2 | `SessionId` exact match | `session_id` | — | 200 |
| 3 | `ActorId` + `EntityId` + time window | `actor_entity_window` | ±4 h | 200 |
| 4 | `ActorId` + time window (**fallback only**) | `actor_window` | ±2 h | 20 |

- **Tiers 1–3 are additive**: all three run in parallel (serially ordered) and results are merged.  
- **Tier 4 is a pure fallback**: only executes when tiers 1–3 collectively yield zero results.  
- **Deduplication**: by `AuditId`; the highest-priority tier's `matchedBy` label wins.  
- **Self-exclusion**: the anchor event is always filtered from results.  
- **Tenant isolation**: all sub-queries are scoped to `callerTenantId` (enforced by the existing `IQueryAuthorizer` at the controller level).

---

## Backend Changes

### New files

| File | Purpose |
|------|---------|
| `apps/services/audit/DTOs/Correlation/RelatedAuditEventResult.cs` | Per-event DTO: wraps `AuditEventRecordResponse` + `MatchedBy` + `MatchKey` |
| `apps/services/audit/DTOs/Correlation/RelatedEventsResponse.cs` | Envelope DTO: `AnchorId`, `AnchorEventType`, `StrategyUsed`, `TotalRelated`, `Related[]` |
| `apps/services/audit/Services/IAuditCorrelationService.cs` | Service interface with `GetRelatedAsync(Guid anchorAuditId, string? callerTenantId, CancellationToken)` |
| `apps/services/audit/Services/AuditCorrelationService.cs` | Implementation: four-tier cascade using `IAuditEventQueryService` |

### Modified files

| File | Change |
|------|--------|
| `apps/services/audit/Controllers/AuditEventQueryController.cs` | Added `IAuditCorrelationService` injection; added `GET /audit/events/{auditId}/related` endpoint |
| `apps/services/audit/Program.cs` | Registered `IAuditCorrelationService → AuditCorrelationService` as scoped |

### New endpoint

```
GET /audit/events/{auditId:guid}/related
```

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "anchorId": "01950000-0000-0000-0000-000000000000",
    "anchorEventType": "identity.user.role.assigned",
    "strategyUsed": "correlation_id",
    "totalRelated": 3,
    "related": [
      {
        "matchedBy": "correlation_id",
        "matchKey": "req-abc123",
        "event": { /* AuditEventRecordResponse */ }
      }
    ]
  },
  "traceId": "..."
}
```

**Error responses:**  
- `404` — anchor event not found  
- `401` / `403` — standard auth responses (reuses `AuthorizeQuery` probe pattern)

---

## Frontend Changes

### New files

| File | Purpose |
|------|---------|
| `apps/control-center/src/app/synqaudit/related/[auditId]/page.tsx` | Server component page at `/synqaudit/related/{auditId}` |
| `apps/control-center/src/components/synqaudit/related-events-timeline.tsx` | Client component: renders related events grouped with tier badges |

### Modified files

| File | Change |
|------|--------|
| `apps/control-center/src/types/control-center.ts` | Added `RelatedAuditEvent` and `RelatedEventsData` interfaces |
| `apps/control-center/src/lib/control-center-api.ts` | Added `auditCanonical.relatedEvents(auditId)` — maps the `/related` response |
| `apps/control-center/src/components/synqaudit/investigation-workspace.tsx` | Added "Find related events" link in `EventDetailPanel` → `/synqaudit/related/{id}` |

### Navigation flow

```
Investigation Workspace
  → click any event → EventDetailPanel
    → "Find related events" → /synqaudit/related/{auditId}
      → RelatedEventsTimeline (server-fetched, cascaded correlation result)
        → each event row → "Related chain →" (recursive navigation)
                         → "Trace ID" → /synqaudit/trace?correlationId=X
```

---

## What was NOT changed

- `AuditEventQueryRequest` — all required filter fields (`CorrelationId`, `SessionId`, `ActorId`, `EntityId`, `From`, `To`) already present.  
- `EfAuditEventRecordRepository.ApplyFilters` — already handles all correlation filters.  
- `IQueryAuthorizer` / `QueryAuthMiddleware` — reused unchanged via the `AuthorizeQuery` probe pattern.  
- Database schema — no migrations.

---

## Build verification

- `dotnet build`: ✅ 0 errors (pre-existing JwtBearer version warnings only)  
- `pnpm tsc --noEmit`: ✅ 0 errors  
- Workflow restart: ✅ application serving
