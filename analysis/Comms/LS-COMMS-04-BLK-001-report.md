# LS-COMMS-04-BLK-001 — Advanced Filtering and Operational Views Report

## Status
COMPLETE

## Objective
Enable inbox-style filtering and operational views over conversations using database-driven query composition.

## Architecture Requirements
- Independent service under /apps/services/synqcomm
- Continue using separate SynqComm physical database
- No piggybacking on another service database
- No cross-database joins for core logic
- Database-driven filtering only (no large in-memory filtering)
- Preserve Clean Architecture layering from prior blocks

## Steps Completed
- [x] Step 1: Analyze current query limitations
- [x] Step 2: Design filtering model and inbox presets
- [x] Step 3: Extend repository queries (IQueryable composition)
- [x] Step 4: Implement operational view service
- [x] Step 5: API implementation
- [x] Step 6: Performance optimization and indexes
- [x] Step 7: Automated tests (18 new tests, 181 total passing)
- [x] Step 8: Final review

## Implementation Summary
Replaced the N+1 in-memory filtering pattern in `ListOperationalAsync` with a database-driven IQueryable-based query repository (`OperationalConversationQueryRepository`) that composes a single SQL query joining Conversations, Assignments, SLA States, Read States, Queues, and Mentions. All filtering is pushed to the database via conditional WHERE clauses. Results are projected directly into DTOs with pagination, sorting, and efficient subqueries for mention counts and last message snippets.

## Architecture Alignment
- All code lives within the SynqComm service (`/apps/services/synqcomm`)
- Uses only the SynqComm database — no cross-service joins
- Follows existing Clean Architecture layers: Domain → Application → Infrastructure → Api
- Reuses existing domain entities (Conversation, ConversationAssignment, ConversationSlaState, ConversationReadState, MessageMention, ConversationQueue, Message)
- No new domain behavior introduced — purely read/query optimization
- Existing endpoints preserved with no breaking changes

## Filtering Model
### Core Filters
| Filter | Type | Behavior |
|--------|------|----------|
| queueId | Guid? | Match assignment queue |
| assignedUserId | Guid? | Match assigned user |
| assignmentStatus | string? | Match assignment status (e.g., "Assigned", "Accepted") |
| priority | string? | Match SLA priority |
| operationalStatus | string? | Match conversation status |
| waitingState | string? | Match SLA waiting state |

### SLA Filters
| Filter | Type | Behavior |
|--------|------|----------|
| breachedFirstResponse | bool? | Match SLA first response breach flag |
| breachedResolution | bool? | Match SLA resolution breach flag |
| hasWarnings | bool? | Reserved for future use |

### Activity Filters
| Filter | Type | Behavior |
|--------|------|----------|
| unreadOnly | bool? | Conversations where user has no read state OR last read is before last activity |
| mentionedUserId | Guid? | Conversations where specified user has been mentioned (EXISTS subquery) |

### Time Filters
| Filter | Type | Behavior |
|--------|------|----------|
| updatedSince | DateTime? | Conversations updated after this time |
| createdSince | DateTime? | Conversations created after this time |

### Sorting
| Sort Field | Description |
|------------|-------------|
| lastActivityAtUtc (default) | Last activity timestamp DESC |
| firstResponseDueAtUtc | First response SLA due date |
| resolutionDueAtUtc | Resolution SLA due date |
| priority | SLA priority string |
| createdAtUtc | Conversation creation time |

All sorts include a secondary tiebreaker on `Conversation.Id` for deterministic ordering.

### Pagination
- `page` — 1-indexed, minimum 1
- `pageSize` — clamped to 1–200
- Response includes `totalCount`, `hasMore`

### Inbox Presets (logical composition)
| Preset | Filter Combination |
|--------|-------------------|
| My Inbox | `assignedUserId = currentUser` |
| Unassigned | `assignedUserId = null` (not yet filtered — requires explicit query) |
| My Queue | `queueId = selectedQueue` |
| Breached | `breachedFirstResponse = true` OR `breachedResolution = true` |
| High Priority | `priority = "High"` or `"Urgent"` |
| Mentioned Me | `mentionedUserId = currentUser` |

## Query Strategy
### Before (N+1 in-memory)
```
1. Load ALL conversations for tenant
2. For EACH conversation:
   a. Query assignment (individual DB call)
   b. Query SLA state (individual DB call)
3. Filter results in C# memory
4. No pagination — return all
```

### After (single IQueryable composition)
```
1. Build IQueryable with LEFT JOINs:
   Conversation → Assignment → SLA → Queue → ReadState
2. Apply conditional WHERE clauses based on filters
3. Execute COUNT query for totalCount
4. Apply ORDER BY + SKIP/TAKE
5. Project into DTO with subqueries for:
   - MentionCount (COUNT subquery)
   - HasMentionsForFilter (EXISTS subquery)
   - LastMessageSnippet (TOP 1 ordered subquery)
6. Single materialization via ToListAsync
```

All queries use `AsNoTracking` for read-only performance.

## Performance Considerations
### Existing Indexes (reused)
- `IX_Conversations_TenantId_LastActivity` — default sort
- `IX_Assignments_TenantId_AssignedUserId` — My Inbox filter
- `IX_Assignments_TenantId_QueueId` — queue filter
- `IX_SlaState_TenantId_ConversationId` — SLA join
- `IX_SlaState_TenantId_BreachedFirstResponse` — breach filter
- `IX_SlaState_TenantId_BreachedResolution` — breach filter
- `IX_MessageMentions_TenantId_MentionedUserId` — mention filter
- `IX_MessageMentions_TenantId_ConversationId` — mention count
- `IX_ReadStates_TenantId_ConversationId_UserId` — unread join

### New Indexes Added
- `IX_SlaState_TenantId_Priority` — priority filter
- `IX_Assignments_TenantId_AssignmentStatus` — assignment status filter

### Query Safeguards
- PageSize clamped to max 200
- Deterministic ordering (tiebreaker on Id)
- AsNoTracking on all read queries
- No client-side evaluation — all filters are SQL-translatable
- LastMessageSnippet truncated to 120 chars at DB level

## Authorization Model
- Endpoint requires `AuthenticatedUser` policy
- Requires `SynqComm` product access
- Requires `OperationalRead` permission
- All queries scoped by `tenantId` from request context
- External participants blocked at authorization layer (no `OperationalRead` permission)
- No cross-tenant data leakage — tenant filter applied at root of query

## Database Changes
### Migration: `20260416142646_AddMessageMentions` (BLK-004)
- Table: `sc_MessageMentions` with indexes

### Migration: `20260416152313_AddOperationalViewIndexes` (BLK-001)
- Index: `IX_SlaState_TenantId_Priority` on `comms_ConversationSlaStates`
- Index: `IX_Assignments_TenantId_AssignmentStatus` on `comms_ConversationAssignments`

## Files Created
| File | Purpose |
|------|---------|
| `SynqComm.Application/DTOs/OperationalViewDtos.cs` | OperationalQueryRequest, ConversationOperationalListItemResponse, OperationalQueryResponse |
| `SynqComm.Application/Repositories/IOperationalConversationQueryRepository.cs` | Query repository interface |
| `SynqComm.Application/Interfaces/IOperationalViewService.cs` | Service interface |
| `SynqComm.Application/Services/OperationalViewService.cs` | Service with param validation, pagination clamping |
| `SynqComm.Infrastructure/Repositories/OperationalConversationQueryRepository.cs` | IQueryable-based query implementation |
| `SynqComm.Infrastructure/Persistence/Migrations/20260416152313_AddOperationalViewIndexes.cs` | New indexes migration |
| `SynqComm.Tests/OperationalViewTests.cs` | 18 test cases |

## Files Updated
| File | Change |
|------|--------|
| `SynqComm.Infrastructure/DependencyInjection.cs` | Registered query repo + view service |
| `SynqComm.Api/Endpoints/OperationalEndpoints.cs` | Added GET /api/synqcomm/operational/conversations endpoint |
| `SynqComm.Infrastructure/Persistence/Configurations/ConversationSlaStateConfiguration.cs` | Added priority index |
| `SynqComm.Infrastructure/Persistence/Configurations/ConversationAssignmentConfiguration.cs` | Added assignment status index |

## API Changes
### New Endpoint
`GET /api/synqcomm/operational/conversations`

**Authorization:** AuthenticatedUser + SynqComm product + OperationalRead

**Query Parameters:**
| Parameter | Type | Default |
|-----------|------|---------|
| queueId | Guid? | null |
| assignedUserId | Guid? | null |
| assignmentStatus | string? | null |
| priority | string? | null |
| operationalStatus | string? | null |
| waitingState | string? | null |
| breachedFirstResponse | bool? | null |
| breachedResolution | bool? | null |
| hasWarnings | bool? | null |
| mentionedUserId | Guid? | null |
| unreadOnly | bool? | null |
| updatedSince | DateTime? | null |
| createdSince | DateTime? | null |
| page | int | 1 |
| pageSize | int | 50 |
| sortBy | string | "lastActivityAtUtc" |
| sortDirection | string | "desc" |

**Response:** `OperationalQueryResponse` with Items, TotalCount, Page, PageSize, HasMore

### Existing Endpoint Preserved
`GET /api/synqcomm/operations` — unchanged, still functional

## Test Results
```
Passed!  - Failed: 0, Passed: 181, Skipped: 0, Total: 181
```

### New Tests (18)
1. FilterByAssignedUserId_ReturnsOnlyAssignedConversations
2. FilterByQueueId_ReturnsOnlyQueueConversations
3. FilterBySlaBreach_ReturnsBreachedConversations
4. FilterByPriority_ReturnsMatchingConversations
5. FilterByMentionedUserId_ReturnsConversationsWithMentions
6. FilterByUnreadOnly_ReturnsUnreadConversations
7. CombinedFilters_ComposeCorrectly
8. Pagination_WorksCorrectly
9. Sorting_DefaultDescByLastActivity
10. Sorting_AscByCreatedAt
11. TenantIsolation_CrossTenantRecordsNeverAppear
12. MentionCount_ReturnsCorrectCountForCurrentUser
13. LastMessageSnippet_ReturnsLatestMessageBody
14. OperationalViewService_ClampsPagination
15. OperationalViewService_HasMoreFlag
16. RegressionTest_ExistingOperationalListStillWorks
17. FilterByOperationalStatus_ReturnsMatchingConversations
18. NoResults_ReturnsEmptyWithZeroTotal

## Issues / Gaps
1. **`hasWarnings` filter** — reserved but not implemented; would require logic to derive warning state from SLA due dates approaching but not yet breached
2. **Unassigned filter** — "My Inbox: Unassigned" preset requires filtering for `assignedUserId IS NULL` which needs explicit null-check semantics not currently exposed as a separate parameter; clients can query for conversations without assignments via absence of assignment join
3. **EvaluateBreaches at query time** — the original single-summary endpoint calls `EvaluateBreaches()` which mutates SLA state; the list query intentionally does NOT do this (read-only, AsNoTracking) — breach flags reflect last-evaluated state from the most recent write operation

## Next Recommendations
**LS-COMMS-04-BLK-002 — Bulk Actions and Workflow Operations**
- Bulk assign/reassign/unassign conversations
- Bulk priority changes
- Bulk status transitions
- Batch operation atomicity and audit trail
