# LS-COMMS-03-BLK-001 — Operational Queues, Assignment, and SLA Tracking Report

## Status
COMPLETE

## Objective
Extend SynqComm from a communication backbone into an operational communication workflow service by adding queue management, assignment, ownership, response-state tracking, and SLA timing foundations while preserving SynqComm as the communication system of record.

## Architecture Requirements
- Independent service under /apps/services/synqcomm
- Continue using separate SynqComm physical database
- No piggybacking on another service database
- No cross-database joins for core logic
- SynqComm remains the communication system of record
- Notifications remains the outbound delivery engine
- Documents remains owned by Documents service
- Preserve Clean Architecture layering from prior blocks

## Steps Completed
- [x] Step 1: Review existing implementation and operational workflow gaps
- [x] Step 2: Design queue, assignment, and SLA model
- [x] Step 3: Add domain models and contracts
- [x] Step 4: Implement queue and assignment logic
- [x] Step 5: Implement SLA and response-state tracking
- [x] Step 6: Extend APIs and persistence
- [x] Step 7: Extend audit coverage
- [x] Step 8: Add/update migrations
- [x] Step 9: Add automated tests
- [x] Step 10: Final review

## Findings

### Existing Foundation
- 102 tests passing across 8 test classes from prior blocks (BLK-001 through BLK-005)
- SynqCommDbContext had 11 DbSets, now extended to 14
- Clean architecture layering (Api/Application/Domain/Infrastructure) fully preserved
- All existing communication, email, document, and notification boundaries untouched

### Design Decisions
1. **SLA defaults are priority-based constants** (Low: 24h/120h, Normal: 8h/72h, High: 4h/24h, Urgent: 1h/8h) — stored in `SlaDefaults.cs` for easy future externalization
2. **Operational list query uses in-memory filtering** over all tenant conversations — acceptable for v1; can be optimized to DB-level joins in a future block
3. **Auto-queue on inbound**: EmailIntakeService auto-assigns new inbound conversations to the tenant's default queue and initializes SLA with Normal priority
4. **SLA satisfaction hooks**: OutboundEmailService satisfies first-response SLA on SHARED_EXTERNAL sends; ConversationService satisfies resolution SLA on Resolved/Closed status transitions
5. **Breach evaluation order**: `EvaluateBreaches()` is called BEFORE `SatisfyFirstResponse()`/`SatisfyResolution()` to ensure late completions are correctly flagged as breached (caught during code review)

### Audit Event Coverage (12+ event types)
| Event Type | Service | Trigger |
|---|---|---|
| QueueCreated | QueueService | Queue CRUD |
| QueueUpdated | QueueService | Queue update |
| ConversationAssigned | AssignmentService | Initial assignment |
| ConversationReassigned | AssignmentService | Reassignment |
| ConversationUnassigned | AssignmentService | User removal |
| AssignmentAccepted | AssignmentService | User accepts |
| SlaInitialized | OperationalService | SLA creation |
| PriorityChanged | OperationalService | Priority update |
| FirstResponseSlaSatisfied | OperationalService | First response recorded |
| ResolutionSlaSatisfied | OperationalService | Resolution recorded |
| WaitingStateChanged | OperationalService | Waiting state transition |
| InboundOperationalInit | EmailIntakeService | Auto-queue on new inbound |

## Files Created

### Domain Layer
| File | Purpose |
|---|---|
| `SynqComm.Domain/Entities/ConversationQueue.cs` | Queue entity with Create, Update, SetDefault, NormalizeCode |
| `SynqComm.Domain/Entities/ConversationAssignment.cs` | Assignment entity with Create, Reassign, Unassign, Accept |
| `SynqComm.Domain/Entities/ConversationSlaState.cs` | SLA state entity with Initialize, SatisfyFirstResponse, SatisfyResolution, EvaluateBreaches, UpdatePriority, SetWaitingOn |
| `SynqComm.Domain/Enums/AssignmentStatus.cs` | Assigned, Accepted, Unassigned |
| `SynqComm.Domain/Enums/ConversationPriority.cs` | Low, Normal, High, Urgent |
| `SynqComm.Domain/Enums/WaitingState.cs` | None, WaitingInternal, WaitingExternal |
| `SynqComm.Domain/Constants/SlaDefaults.cs` | Priority-based duration constants |

### Application Layer
| File | Purpose |
|---|---|
| `SynqComm.Application/DTOs/QueueDtos.cs` | CreateConversationQueueRequest, UpdateConversationQueueRequest, ConversationQueueResponse |
| `SynqComm.Application/DTOs/AssignmentDtos.cs` | AssignConversationRequest, ReassignConversationRequest, ConversationAssignmentResponse |
| `SynqComm.Application/DTOs/SlaDtos.cs` | UpdateConversationPriorityRequest, ConversationSlaStateResponse, ConversationOperationalSummaryResponse, OperationalListQuery |
| `SynqComm.Application/Interfaces/IQueueService.cs` | CreateAsync, UpdateAsync, GetByIdAsync, ListAsync |
| `SynqComm.Application/Interfaces/IAssignmentService.cs` | AssignAsync, ReassignAsync, UnassignAsync, AcceptAsync, GetByConversationAsync |
| `SynqComm.Application/Interfaces/IOperationalService.cs` | GetSlaStateAsync, UpdatePriorityAsync, GetOperationalSummaryAsync, ListOperationalAsync, InitializeSlaAsync, SatisfyFirstResponseAsync, SatisfyResolutionAsync, UpdateWaitingStateAsync |
| `SynqComm.Application/Repositories/IConversationQueueRepository.cs` | AddAsync, UpdateAsync, GetByIdAsync, GetByCodeAsync, GetDefaultAsync, ListByTenantAsync |
| `SynqComm.Application/Repositories/IConversationAssignmentRepository.cs` | AddAsync, UpdateAsync, GetByConversationAsync, ListByQueueAsync, ListByUserAsync |
| `SynqComm.Application/Repositories/IConversationSlaStateRepository.cs` | AddAsync, UpdateAsync, GetByConversationAsync |
| `SynqComm.Application/Services/QueueService.cs` | Queue CRUD with default management and code normalization |
| `SynqComm.Application/Services/AssignmentService.cs` | Assignment lifecycle with SLA initialization on priority-bearing assigns |
| `SynqComm.Application/Services/OperationalService.cs` | SLA lifecycle, breach evaluation, priority recalculation, operational summaries with filtering |

### API Layer
| File | Purpose |
|---|---|
| `SynqComm.Api/Endpoints/QueueEndpoints.cs` | POST/GET/PUT /api/synqcomm/queues |
| `SynqComm.Api/Endpoints/OperationalEndpoints.cs` | POST assign/reassign/unassign/accept/priority, GET summary/list |

### Infrastructure Layer
| File | Purpose |
|---|---|
| `SynqComm.Infrastructure/Persistence/Configurations/ConversationQueueConfiguration.cs` | EF config for comms_ConversationQueues |
| `SynqComm.Infrastructure/Persistence/Configurations/ConversationAssignmentConfiguration.cs` | EF config for comms_ConversationAssignments |
| `SynqComm.Infrastructure/Persistence/Configurations/ConversationSlaStateConfiguration.cs` | EF config for comms_ConversationSlaStates |
| `SynqComm.Infrastructure/Repositories/ConversationQueueRepository.cs` | EF queue repo implementation |
| `SynqComm.Infrastructure/Repositories/ConversationAssignmentRepository.cs` | EF assignment repo implementation |
| `SynqComm.Infrastructure/Repositories/ConversationSlaStateRepository.cs` | EF SLA state repo implementation |
| `SynqComm.Infrastructure/Persistence/Migrations/20260416063921_AddOperationalQueuesAndSLA.cs` | EF migration for 3 new tables |

### Tests
| File | Purpose |
|---|---|
| `SynqComm.Tests/OperationalWorkflowTests.cs` | 15 new operational workflow tests |

## Files Updated

| File | Changes |
|---|---|
| `SynqComm.Domain/SynqCommPermissions.cs` | Added QueueManage, QueueRead, AssignmentManage, OperationalRead permissions |
| `SynqComm.Application/Repositories/IConversationRepository.cs` | Added ListByTenantAsync method |
| `SynqComm.Application/Services/EmailIntakeService.cs` | Added IOperationalService, IConversationQueueRepository, IConversationAssignmentRepository; auto-routes new inbound to default queue + initializes SLA |
| `SynqComm.Application/Services/OutboundEmailService.cs` | Added IOperationalService; satisfies first-response SLA on SHARED_EXTERNAL send; sets WaitingExternal |
| `SynqComm.Application/Services/ConversationService.cs` | Added IOperationalService; satisfies resolution SLA on Resolved/Closed status |
| `SynqComm.Infrastructure/DependencyInjection.cs` | Registered all 3 new repos + 3 new services in DI |
| `SynqComm.Infrastructure/Persistence/SynqCommDbContext.cs` | Added 3 new DbSets (ConversationQueues, ConversationAssignments, ConversationSlaStates) |
| `SynqComm.Infrastructure/Repositories/ConversationRepository.cs` | Added ListByTenantAsync implementation |
| `SynqComm.Api/Program.cs` | Registered QueueEndpoints and OperationalEndpoints |
| `SynqComm.Tests/TestHelpers.cs` | Added CreateQueueRepo, CreateAssignmentRepo, CreateSlaStateRepo, CreateOperationalService factory methods |
| `SynqComm.Tests/CcBccRecipientTests.cs` | Updated constructors for new service dependencies |
| `SynqComm.Tests/EmailIntakeTests.cs` | Updated constructors for new service dependencies |
| `SynqComm.Tests/OutboundEmailTests.cs` | Updated constructors for new service dependencies |
| `SynqComm.Tests/SenderTemplateTests.cs` | Updated constructors for new service dependencies |
| `SynqComm.Tests/E2ENotificationsIntegrationTests.cs` | Updated constructors for new service dependencies |
| `SynqComm.Tests/ParticipantAccessTests.cs` | Updated constructors for new service dependencies |
| `SynqComm.Tests/OrderedThreadRetrievalTests.cs` | Updated constructors for new service dependencies |
| `SynqComm.Tests/MessageAttachmentTests.cs` | Updated constructors for new service dependencies |

## Database Changes

### New Tables
| Table | Columns | Indexes |
|---|---|---|
| `comms_ConversationQueues` | Id, TenantId, Name, Code, Description, IsDefault, IsActive, CreatedByUserId, CreatedAtUtc, UpdatedAtUtc, UpdatedByUserId | IX_TenantId, IX_TenantId_Code (unique), IX_TenantId_IsDefault |
| `comms_ConversationAssignments` | Id, TenantId, ConversationId, QueueId, AssignedUserId, AssignedByUserId, AssignmentStatus, AssignedAtUtc, LastAssignedAtUtc, AcceptedAtUtc, UnassignedAtUtc, CreatedByUserId, CreatedAtUtc, UpdatedAtUtc, UpdatedByUserId | IX_TenantId, IX_TenantId_ConversationId (unique), IX_TenantId_QueueId, IX_TenantId_AssignedUserId, IX_TenantId_AssignmentStatus |
| `comms_ConversationSlaStates` | Id, TenantId, ConversationId, Priority, FirstResponseDueAtUtc, ResolutionDueAtUtc, FirstResponseAtUtc, ResolvedAtUtc, BreachedFirstResponse, BreachedResolution, WaitingOn, LastEvaluatedAtUtc, SlaStartedAtUtc, CreatedByUserId, CreatedAtUtc, UpdatedAtUtc, UpdatedByUserId | IX_TenantId, IX_TenantId_ConversationId (unique), IX_TenantId_Priority, IX_TenantId_BreachedFirstResponse, IX_TenantId_BreachedResolution |

### Migration
- `20260416063921_AddOperationalQueuesAndSLA` — creates all 3 tables with specified indexes

### DbContext
- SynqCommDbContext now has 14 DbSets (was 11)

## API Changes

### Queue Endpoints (`/api/synqcomm/queues`)
| Method | Path | Permission | Description |
|---|---|---|---|
| POST | `/api/synqcomm/queues` | QueueManage | Create a new queue |
| GET | `/api/synqcomm/queues` | QueueRead | List all queues for tenant |
| GET | `/api/synqcomm/queues/{queueId}` | QueueRead | Get queue by ID |
| PUT | `/api/synqcomm/queues/{queueId}` | QueueManage | Update queue |

### Operational Endpoints (`/api/synqcomm/operational`)
| Method | Path | Permission | Description |
|---|---|---|---|
| POST | `/api/synqcomm/operational/conversations/{id}/assign` | AssignmentManage | Assign conversation to queue/user |
| POST | `/api/synqcomm/operational/conversations/{id}/reassign` | AssignmentManage | Reassign conversation |
| POST | `/api/synqcomm/operational/conversations/{id}/unassign` | AssignmentManage | Unassign user from conversation |
| POST | `/api/synqcomm/operational/conversations/{id}/accept` | AssignmentManage | Accept assignment (assigned user only) |
| POST | `/api/synqcomm/operational/conversations/{id}/priority` | AssignmentManage | Update conversation priority |
| GET | `/api/synqcomm/operational/conversations/{id}/summary` | OperationalRead | Get operational summary |
| GET | `/api/synqcomm/operational/conversations` | OperationalRead | List conversations with operational filters |

### New Permissions
| Permission | Purpose |
|---|---|
| QueueManage | Create, update queues |
| QueueRead | List, view queues |
| AssignmentManage | Assign, reassign, unassign, accept, set priority |
| OperationalRead | View operational summaries and lists |

## Test Results

### Summary
- **Total tests: 118** (was 102 before this block)
- **All 118 passing**
- **15 new tests** in `OperationalWorkflowTests.cs`
- **0 regressions** — all 103 prior tests continue to pass

### New Test Cases
| Test | Validates |
|---|---|
| CreateQueue_ReturnsQueueWithCode | Queue creation with code normalization |
| CreateQueue_DuplicateCode_Throws | Duplicate queue code rejection |
| AssignConversation_CreatesAssignment | Full assignment with queue + user + priority |
| AssignConversation_AlreadyAssigned_Throws | Double-assign prevention |
| ReassignConversation_UpdatesUser | Reassignment updates target user |
| AcceptAssignment_SetsAcceptedStatus | Accept sets status + timestamp |
| AcceptAssignment_WrongUser_Throws | Only assigned user can accept |
| UnassignConversation_SetsUnassignedStatus | Unassign sets status + timestamp |
| InitializeSla_SetsDueDates | SLA creates with correct due dates |
| UpdatePriority_RecalculatesDueDates | Priority change recalculates due dates |
| SatisfyFirstResponse_SetsTimestamp | On-time first response recorded |
| LateFirstResponse_SetsBreachFlag | Late first response correctly flagged as breached |
| LateResolution_SetsBreachFlag | Late resolution correctly flagged as breached |
| OperationalSummary_IncludesAllComponents | Summary aggregates queue + assignment + SLA |
| AuditEvents_RecordedForOperations | Audit events emitted for key operations |

## Issues / Gaps

### Resolved During Implementation
1. **SLA breach logic bug (critical)**: Original implementation called `SatisfyFirstResponse`/`SatisfyResolution` before `EvaluateBreaches`, which set the satisfaction timestamp first — causing `EvaluateBreaches` to skip breach detection (it checks `FirstResponseAtUtc is null`). Fixed by reordering: evaluate breaches first, then record satisfaction. Regression tests added.
2. **NullReferenceException in NormalizeCode**: `ConversationQueue.NormalizeCode` called `.Trim()` without null guard. Added `ArgumentException` for null/whitespace input.

### Known Limitations (acceptable for v1)
1. **Operational list uses in-memory filtering**: `ListOperationalAsync` loads all tenant conversations then filters in-memory. Acceptable for v1 workloads; should be optimized to DB-level query with joins for high-volume tenants.
2. **Auto-assignment audit gap**: `EmailIntakeService.InitializeOperationalStateAsync` creates default queue assignments directly via repository, bypassing `AssignmentService`. This means no `ConversationAssigned` audit event is emitted for auto-assignments (an `InboundOperationalInit` event is emitted instead). Consider routing through `AssignmentService` in a future block for audit consistency.
3. **No queue deletion endpoint**: Queues can be deactivated via update but not deleted. This is by design to preserve referential integrity.
4. **No SLA pause/resume**: SLA timers run continuously regardless of waiting state. Pause/resume semantics can be added in a future block.

## Next Recommendations
1. **LS-COMMS-03-BLK-002**: Add SLA breach notification triggers (email/in-app alerts when SLA due dates approach or are breached)
2. **LS-COMMS-03-BLK-003**: Add queue-based routing rules (auto-assign based on conversation metadata, context type, or participant domain)
3. **Optimize operational list**: Replace in-memory filtering with DB-level query joins for ListOperationalAsync
4. **SLA pause/resume**: Add timer pause when WaitingOn=WaitingExternal and resume on state change
5. **Queue analytics**: Add aggregation endpoints for queue depth, average response time, breach rates
