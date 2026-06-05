# LS-COMMS-03-BLK-003 — Operational Timeline and System Activity Feed Report

## Status
COMPLETE

## Objective
Provide a unified timeline of communication and operational events per conversation, with visibility controls, filtering, pagination, and conversation-scoped authorization.

## Steps Completed
- [x] Step 1: Review event sources — identified 7 services with hookable actions
- [x] Step 2: Design timeline model — ConversationTimelineEntry entity, event type/actor type/visibility constants
- [x] Step 3: Implement timeline projection — ConversationTimelineRepository with tenant-scoped, filtered, paginated queries
- [x] Step 4: Integrate communication events — MessageService (MESSAGE_SENT), EmailIntakeService (EMAIL_RECEIVED), OutboundEmailService (EMAIL_SENT)
- [x] Step 5: Integrate operational/system events — ConversationService (STATUS_CHANGED), AssignmentService (ASSIGNED/REASSIGNED/UNASSIGNED), OperationalService (PRIORITY_CHANGED/SLA_STARTED/FIRST_RESPONSE_SATISFIED/RESOLVED), SlaNotificationService (all SLA trigger types)
- [x] Step 6: API implementation — GET /api/synqcomm/conversations/{id}/timeline with conversation-scoped auth
- [x] Step 7: Tests — 18 timeline tests (146 total, all passing)
- [x] Step 8: Final review — code review completed, authorization and actor attribution fixes applied

## Domain Model

### ConversationTimelineEntry
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| TenantId | Guid | Tenant isolation |
| ConversationId | Guid | Parent conversation |
| EventType | string | Event category (see constants below) |
| EventSubType | string? | Optional sub-classification |
| ActorType | string | SYSTEM, USER, or EXTERNAL |
| ActorId | Guid? | User who triggered the event |
| Summary | string | Human-readable description |
| Visibility | string | INTERNAL_ONLY or SHARED_EXTERNAL_SAFE |
| OccurredAtUtc | DateTime | When the event happened |
| CreatedAtUtc | DateTime | When the entry was recorded |
| RelatedMessageId | Guid? | Associated message |
| RelatedParticipantId | Guid? | Associated participant |
| RelatedSlaId | Guid? | Associated SLA state |
| MetadataJson | string? | Structured JSON payload |

### Event Types
- `MESSAGE_SENT` — New message posted to conversation
- `EMAIL_RECEIVED` — Inbound email matched to conversation
- `EMAIL_SENT` — Outbound email dispatched
- `ASSIGNED` — Conversation assigned to user
- `REASSIGNED` — Assignment transferred to different user
- `UNASSIGNED` — Assignment removed
- `STATUS_CHANGED` — Conversation status transition (e.g., New → Open)
- `PRIORITY_CHANGED` — Priority level changed (e.g., Normal → High)
- `SLA_STARTED` — SLA tracking initialized
- `FIRST_RESPONSE_SATISFIED` — First response SLA met
- `RESOLVED` — Resolution SLA met
- SLA trigger types: `FIRST_RESPONSE_WARNING`, `FIRST_RESPONSE_BREACH`, `RESOLUTION_WARNING`, `RESOLUTION_BREACH`, `ESCALATION`

### Visibility Model
- **INTERNAL_ONLY** — Visible only to internal users (assignments, SLA, priority changes, internal notes)
- **SHARED_EXTERNAL_SAFE** — Visible to external contacts (messages, emails, status changes)

### Actor Types
- **SYSTEM** — Automated/system-triggered events (SLA, escalation)
- **USER** — User-initiated actions (messages, assignments, priority changes, SLA satisfaction)
- **EXTERNAL** — External contact actions (inbound emails)

## Service Integration Hooks

All hooks follow the same resilience pattern — wrapped in `try/catch` with warning-level logging so timeline failures never block primary operations.

| Service | Event Type | Visibility | Actor Type |
|---------|-----------|------------|------------|
| MessageService | MESSAGE_SENT | Based on message visibility | USER |
| ConversationService | STATUS_CHANGED | SHARED_EXTERNAL_SAFE | USER |
| AssignmentService | ASSIGNED | INTERNAL_ONLY | USER |
| AssignmentService | REASSIGNED | INTERNAL_ONLY | USER |
| AssignmentService | UNASSIGNED | INTERNAL_ONLY | USER |
| EmailIntakeService | EMAIL_RECEIVED | SHARED_EXTERNAL_SAFE | EXTERNAL |
| OutboundEmailService | EMAIL_SENT | SHARED_EXTERNAL_SAFE | SYSTEM |
| OperationalService | PRIORITY_CHANGED | INTERNAL_ONLY | USER |
| OperationalService | SLA_STARTED | INTERNAL_ONLY | SYSTEM |
| OperationalService | FIRST_RESPONSE_SATISFIED | INTERNAL_ONLY | USER |
| OperationalService | RESOLVED | INTERNAL_ONLY | USER |
| SlaNotificationService | (all SLA triggers) | INTERNAL_ONLY | SYSTEM |

## API Endpoint

### GET /api/synqcomm/conversations/{id}/timeline

**Authorization:**
- Requires authenticated user + `SYNQ_COMMS` product access + `OperationalRead` permission
- Enforces conversation-scoped access: caller must be an active participant
- External contacts are automatically blocked from seeing `INTERNAL_ONLY` entries regardless of `includeInternal` parameter

**Query Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| fromDate | DateTime? | null | Filter entries after this date |
| toDate | DateTime? | null | Filter entries before this date |
| eventTypes | string? | null | Comma-separated event type filter |
| includeInternal | bool | true | Include INTERNAL_ONLY entries (forced false for external contacts) |
| page | int | 1 | Page number (clamped 1+) |
| pageSize | int | 50 | Page size (clamped 1–200) |

**Response:**
```json
{
  "entries": [
    {
      "id": "guid",
      "eventType": "MESSAGE_SENT",
      "eventSubType": null,
      "actorType": "USER",
      "actorId": "guid",
      "summary": "Message sent by John Doe",
      "visibility": "SHARED_EXTERNAL_SAFE",
      "occurredAtUtc": "2026-04-16T13:00:00Z",
      "relatedMessageId": "guid",
      "relatedParticipantId": null,
      "relatedSlaId": null,
      "metadataJson": null
    }
  ],
  "totalCount": 42,
  "page": 1,
  "pageSize": 50,
  "hasMore": false
}
```

## Database

### Table: `sc_ConversationTimelineEntries`
- Indexes on: (TenantId, ConversationId, OccurredAtUtc), (TenantId, ConversationId, EventType), (TenantId, ConversationId, Visibility)
- Migration: `20260416132304_AddSlaTriggerStatesEscalationAndTimeline` (consolidated with BLK-002 tables)

## DI Registration
- `IConversationTimelineRepository` → `ConversationTimelineRepository` (scoped)
- `IConversationTimelineService` → `ConversationTimelineService` (scoped)
- Registered in `DependencyInjection.cs` → `AddSynqCommServices()`

## Test Coverage

18 timeline-specific tests (146 total, all passing):

| Test | Description |
|------|-------------|
| RecordAsync_CreatesTimelineEntry | Basic entry creation |
| GetTimeline_ReturnsDescendingOrder | Chronological ordering |
| GetTimeline_VisibilityFiltering | Internal-only exclusion |
| GetTimeline_EventTypeFiltering | Event type filter |
| GetTimeline_Pagination_WorksCorrectly | Page/pageSize behavior |
| GetTimeline_DateRangeFiltering | From/to date filtering |
| GetTimeline_TenantIsolation | Cross-tenant isolation |
| RecordAsync_AllRelatedIds_Persisted | Message/participant/SLA IDs stored |
| RecordAsync_EventSubType_Persisted | Subtype persistence |
| PageSizeClamping_EnforcedLimits | 1–200 range enforcement |
| MessageService_RecordsTimelineEntry_OnAdd | Message hook integration |
| AssignmentService_RecordsTimelineEntry_OnAssign | Assignment hook integration |
| SlaNotificationService_RecordsTimelineEntry_OnTrigger | SLA hook integration |
| EmailIntakeService_RecordsTimelineEntry_OnIngest | Email intake hook integration |
| OutboundEmailService_RecordsTimelineEntry_OnSend | Outbound email hook integration |
| ConversationService_RecordsTimelineEntry_OnStatusChange | Status change hook integration |
| OperationalService_RecordsTimelineEntry_OnPriorityChange | Priority change hook integration |
| OperationalService_RecordsTimelineEntry_OnSlaInitialize | SLA init hook integration |

## Code Review Fixes Applied
1. **Conversation-scoped authorization** — Timeline endpoint verifies caller is an active conversation participant (not just tenant-level permission)
2. **External visibility gating** — External contacts automatically see only `SHARED_EXTERNAL_SAFE` entries regardless of query parameters
3. **Actor type attribution** — `FirstResponseSatisfied` and `Resolved` events correctly use `ActorType.User` when triggered by a user (was incorrectly `System`)

## Files Created/Modified
| File | Action |
|------|--------|
| `SynqComm.Domain/Entities/ConversationTimelineEntry.cs` | Created |
| `SynqComm.Domain/Constants/TimelineEventTypes.cs` | Created |
| `SynqComm.Application/DTOs/TimelineDtos.cs` | Created |
| `SynqComm.Application/Interfaces/IConversationTimelineService.cs` | Created |
| `SynqComm.Application/Repositories/IConversationTimelineRepository.cs` | Created |
| `SynqComm.Application/Services/ConversationTimelineService.cs` | Created |
| `SynqComm.Infrastructure/Repositories/ConversationTimelineRepository.cs` | Created |
| `SynqComm.Infrastructure/Persistence/Configurations/ConversationTimelineEntryConfiguration.cs` | Created |
| `SynqComm.Api/Endpoints/TimelineEndpoints.cs` | Created |
| `SynqComm.Tests/ConversationTimelineTests.cs` | Created |
| `SynqComm.Application/Services/MessageService.cs` | Modified — added MESSAGE_SENT hook |
| `SynqComm.Application/Services/ConversationService.cs` | Modified — added STATUS_CHANGED hook |
| `SynqComm.Application/Services/AssignmentService.cs` | Modified — added ASSIGNED/REASSIGNED/UNASSIGNED hooks |
| `SynqComm.Application/Services/EmailIntakeService.cs` | Modified — added EMAIL_RECEIVED hook |
| `SynqComm.Application/Services/OutboundEmailService.cs` | Modified — added EMAIL_SENT hook |
| `SynqComm.Application/Services/OperationalService.cs` | Modified — added PRIORITY_CHANGED/SLA_STARTED/FIRST_RESPONSE_SATISFIED/RESOLVED hooks |
| `SynqComm.Application/Services/SlaNotificationService.cs` | Modified — added all SLA trigger type hooks |
| `SynqComm.Infrastructure/DependencyInjection.cs` | Modified — registered timeline repo/service |
| `SynqComm.Infrastructure/Persistence/SynqCommDbContext.cs` | Modified — added DbSet |
| `SynqComm.Api/Program.cs` | Modified — registered MapTimelineEndpoints() |
| `SynqComm.Tests/TestHelpers.cs` | Modified — added NoOpTimelineService |
