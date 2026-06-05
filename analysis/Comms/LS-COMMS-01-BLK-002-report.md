# LS-COMMS-01-BLK-002 — In-App Conversation Operations Report

## Status
COMPLETE

## Objective
Extend SynqComm to support usable in-app conversation operations including visibility enforcement, participant-based access, read/unread tracking, reply permissions, ordered thread retrieval, and improved conversation lifecycle behavior.

## Architecture Requirements
- Independent service under /apps/services/synqcomm ✅
- Continue using separate SynqComm physical database ✅
- No piggybacking on another service database ✅
- No cross-database joins for core logic ✅
- Preserve Clean Architecture layering from BLK-001 ✅

## Steps Completed
- [x] Step 1: Review existing BLK-001 implementation
- [x] Step 2: Design BLK-002 domain extensions
- [x] Step 3: Add read/unread and access-control data model
- [x] Step 4: Extend application contracts and services
- [x] Step 5: Implement visibility and participant access rules
- [x] Step 6: Implement read/unread tracking APIs
- [x] Step 7: Improve conversation lifecycle behavior
- [x] Step 8: Add/update migrations
- [x] Step 9: Add automated tests
- [x] Step 10: Final review

## Implementation Summary

BLK-002 extends BLK-001 with operational behavior for in-app messaging:

1. **Participant-Based Access Enforcement** — All read/write operations validate that the authenticated user is an active participant in the conversation. Non-participants are rejected with UnauthorizedAccessException. Inactive participants cannot post messages.

2. **Reply Permission Enforcement** — Participants with `canReply = false` are blocked from posting messages. External contacts and System participants cannot post in-app messages.

3. **Visibility Enforcement** — Internal-only messages (InternalOnly) are hidden from external participants. External participants only see SharedExternal messages. SystemNote channel is hardcoded to reject SharedExternal visibility at the domain level.

4. **Ordered Thread Retrieval** — Messages are returned in deterministic ascending order using `SentAtUtc` then `Id` as tiebreaker. New `GET /api/synqcomm/conversations/{id}/thread` endpoint returns conversation + ordered messages + participants + read state.

5. **Read/Unread Tracking** — New `ConversationReadState` entity tracks per-user read position. Endpoints for marking read/unread. Conversation list and detail responses include `isUnread` and `lastReadAtUtc` metadata. Unread is computed by comparing latest visible message timestamp against user's last-read timestamp.

6. **Conversation Lifecycle** — Status transitions are now validated against a defined transition map. Invalid transitions throw `InvalidOperationException`. Auto-transition from New→Open on first message. Reopen from Closed when a new message is posted by an authorized internal participant.

## Architecture Alignment

BLK-002 builds incrementally on BLK-001 without restructuring:
- New entity `ConversationReadState` follows the same `AuditableEntity` pattern
- New repository `IConversationReadStateRepository` follows existing repository patterns
- New service `ReadTrackingService` follows existing service patterns with DI, logging, and audit publishing
- Existing services (ConversationService, MessageService) were extended with participant access checks
- Status transition validation was added to the domain entity directly (Conversation.UpdateStatus)
- EF configurations follow the same pattern with table prefix `comms_`, FK relationships, and named indexes

## Database Changes

**Migration:** `InitialCreateWithBLK002` (replaces previous InitialCreate)

**New table:**
- `comms_ConversationReadStates` — Per-user read tracking per conversation
  - Columns: Id, TenantId, ConversationId, UserId, LastReadMessageId (nullable), LastReadAtUtc (nullable), CreatedByUserId, UpdatedByUserId, CreatedAtUtc, UpdatedAtUtc
  - FK: ConversationId → comms_Conversations (cascade delete)
  - Unique index: `IX_ReadStates_TenantId_ConversationId_UserId`

**New indexes on existing tables:**
- `IX_Participants_ConversationId_UserId_IsActive` on comms_ConversationParticipants — optimizes participant access lookups

**Existing indexes preserved:**
- `IX_Conversations_TenantId_Context`
- `IX_Conversations_TenantId_OrgId_Status`
- `IX_Conversations_TenantId_LastActivity`
- `IX_Messages_TenantId_ConversationId_SentAt`
- `IX_Messages_TenantId_ConversationId`
- `IX_Participants_TenantId_ConversationId_Active`
- `IX_Participants_TenantId_UserId_Active`

## Files Created

### Domain
- `SynqComm.Domain/Entities/ConversationReadState.cs` — New entity for read tracking

### Application
- `SynqComm.Application/DTOs/ConversationThreadResponse.cs` — Thread response with messages + participants + read state
- `SynqComm.Application/DTOs/MarkConversationReadRequest.cs` — Request DTO for mark-read endpoint
- `SynqComm.Application/DTOs/ReadStateResponse.cs` — Read state response DTO
- `SynqComm.Application/Interfaces/IReadTrackingService.cs` — Read tracking service interface
- `SynqComm.Application/Repositories/IConversationReadStateRepository.cs` — Read state repository interface
- `SynqComm.Application/Services/ReadTrackingService.cs` — Read tracking service implementation

### Infrastructure
- `SynqComm.Infrastructure/Persistence/Configurations/ConversationReadStateConfiguration.cs` — EF config for read states
- `SynqComm.Infrastructure/Repositories/ConversationReadStateRepository.cs` — Read state repository implementation

### Tests
- `SynqComm.Tests/TestHelpers.cs` — Shared test utilities and NoOpAuditPublisher
- `SynqComm.Tests/OrderedThreadRetrievalTests.cs` — Test 1
- `SynqComm.Tests/ParticipantAccessTests.cs` — Test 2 (3 assertions)
- `SynqComm.Tests/VisibilityEnforcementTests.cs` — Test 3 (2 assertions)
- `SynqComm.Tests/ReadTrackingTests.cs` — Test 4
- `SynqComm.Tests/UnreadAfterNewMessageTests.cs` — Test 5
- `SynqComm.Tests/StatusTransitionTests.cs` — Test 6 (20 parameterized + 2 unit)
- `SynqComm.Tests/ClosedConversationTests.cs` — Test 7 (3 assertions)

## Files Updated

### Domain
- `SynqComm.Domain/Enums/ConversationStatus.cs` — Added `ValidTransitions` map and `IsValidTransition()` method
- `SynqComm.Domain/Entities/Conversation.cs` — Added `AutoTransitionToOpen()`, `ReopenFromClosed()`, and transition validation in `UpdateStatus()`

### Application
- `SynqComm.Application/DTOs/ConversationResponse.cs` — Added `IsUnread` and `LastReadAtUtc` optional fields
- `SynqComm.Application/Interfaces/IConversationService.cs` — Added `GetThreadAsync()`, updated signatures with `currentUserId`
- `SynqComm.Application/Interfaces/IMessageService.cs` — Updated `ListByConversationAsync` to require `userId` for visibility filtering
- `SynqComm.Application/Repositories/IMessageRepository.cs` — Renamed to `ListByConversationOrderedAsync`, added `GetLatestByConversationAsync`
- `SynqComm.Application/Repositories/IParticipantRepository.cs` — Added `GetActiveByUserIdAsync`
- `SynqComm.Application/Services/ConversationService.cs` — Full rewrite with participant access, read state, visibility filtering, thread retrieval
- `SynqComm.Application/Services/MessageService.cs` — Added participant/reply/visibility enforcement, auto-transition, reopen-on-message

### Infrastructure
- `SynqComm.Infrastructure/DependencyInjection.cs` — Registered `IConversationReadStateRepository` and `IReadTrackingService`
- `SynqComm.Infrastructure/Persistence/SynqCommDbContext.cs` — Added `ConversationReadStates` DbSet
- `SynqComm.Infrastructure/Persistence/Configurations/ConversationParticipantConfiguration.cs` — Added `IX_Participants_ConversationId_UserId_IsActive` index
- `SynqComm.Infrastructure/Repositories/MessageRepository.cs` — Renamed method, added `ThenBy(Id)` for deterministic ordering, added `GetLatestByConversationAsync`
- `SynqComm.Infrastructure/Repositories/ParticipantRepository.cs` — Added `GetActiveByUserIdAsync`

### API
- `SynqComm.Api/Endpoints/ConversationEndpoints.cs` — Added `/thread`, `/read`, `/unread` endpoints; pass `userId` to services
- `SynqComm.Api/Endpoints/MessageEndpoints.cs` — Pass `userId` to message listing for visibility filtering

## API Changes

### New Endpoints
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/synqcomm/conversations/{id}/thread` | Returns conversation + ordered messages + participants + read state |
| POST | `/api/synqcomm/conversations/{id}/read` | Marks conversation as read for authenticated user |
| POST | `/api/synqcomm/conversations/{id}/unread` | Resets read state (marks unread) for authenticated user |

### Enhanced Endpoints
| Method | Route | Change |
|--------|-------|--------|
| GET | `/api/synqcomm/conversations/` | Now includes `isUnread` and `lastReadAtUtc` per conversation; filters by participant access |
| GET | `/api/synqcomm/conversations/{id}` | Now includes `isUnread` and `lastReadAtUtc`; enforces participant access |
| GET | `/api/synqcomm/conversations/{id}/messages` | Now enforces participant access; filters by visibility |
| POST | `/api/synqcomm/conversations/{id}/messages` | Now enforces participant active status, canReply, and visibility rules |
| PATCH | `/api/synqcomm/conversations/{id}/status` | Now validates transitions and requires participant access |

## Test Results

```
Command: dotnet test apps/services/synqcomm/SynqComm.Tests/SynqComm.Tests.csproj --configuration Debug --verbosity normal

Test Run Successful.
Total tests: 31
     Passed: 31
 Total time: 3.9951 Seconds
```

### Test Breakdown
| # | Test File | Tests | Result |
|---|-----------|-------|--------|
| 1 | OrderedThreadRetrievalTests | 1 | ✅ Passed |
| 2 | ParticipantAccessTests | 3 | ✅ Passed (non-participant, inactive, canReply=false) |
| 3 | VisibilityEnforcementTests | 2 | ✅ Passed (internal-only hidden, SystemNote rejection) |
| 4 | ReadTrackingTests | 1 | ✅ Passed |
| 5 | UnreadAfterNewMessageTests | 1 | ✅ Passed |
| 6 | StatusTransitionTests | 20 | ✅ Passed (18 parameterized + 2 unit) |
| 7 | ClosedConversationTests | 3 | ✅ Passed (auto-open, reopen, no-op) |

## Issues / Gaps
- No email support (by design — deferred to BLK-003+)
- No outbound notifications (by design — deferred)
- No documents integration (by design — deferred)
- No UI (by design)
- External contacts cannot post in-app messages (no authenticated external portal pattern exists yet)
- Full unread-count dashboard not implemented (per-conversation unread state is available)
- ConnectionStrings__SynqCommDb secret needs to be set with valid RDS credentials before runtime database connectivity

## Next Recommendations

**LS-COMMS-01-BLK-003 — Documents and Audit Integration**
- Attach documents to conversations/messages
- Integrate with the Documents microservice
- Enhanced audit trail for conversation operations
- Rich message content support
