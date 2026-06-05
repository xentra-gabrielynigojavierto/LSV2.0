# LS-COMMS-02-BLK-001 — Email Intake and Threading Report

## Status
COMPLETE

## Objective
Extend SynqComm to support inbound email intake, thread matching, controlled external participant creation from email identities, and persistence of email metadata while preserving SynqComm service boundaries and prior security/visibility rules.

## Architecture Requirements
- Independent service under /apps/services/synqcomm
- Continue using separate SynqComm physical database
- No piggybacking on another service database
- No cross-database joins for core logic
- Email metadata owned by SynqComm
- Documents remain owned by Documents service
- Preserve Clean Architecture layering from BLK-001 / BLK-002 / BLK-003

## Steps Completed
- [x] Step 1: Review existing implementation and platform patterns
- [x] Step 2: Design email intake and threading extensions
- [x] Step 3: Add email domain models and contracts
- [x] Step 4: Implement inbound email integration seam/client/contracts
- [x] Step 5: Implement email parsing and thread matching logic
- [x] Step 6: Implement external participant/email identity handling
- [x] Step 7: Implement inbound attachment handling/linkage
- [x] Step 8: Extend APIs and persistence
- [x] Step 9: Extend audit coverage
- [x] Step 10: Add/update migrations
- [x] Step 11: Add automated tests (12 new tests, 53 total passing)
- [x] Step 12: Final review

## Findings
- `Conversation.Create` rejects `Guid.Empty` as `createdByUserId` — system-generated entities use sentinel `SystemUserId` (`00000000-0000-0000-0000-000000000001`)
- InMemory provider does not enforce unique indexes — production MySQL will enforce them
- Five-priority thread matching: ConversationToken > InReplyTo > References > ProviderThread > NewConversation
- Only sender (From) participant is resolved per email — CC handling deferred to future block
- No outbound email, no notifications, no UI in this block

## Files Created
- `SynqComm.Domain/Entities/EmailMessageReference.cs` — Email metadata entity with RFC 5322 fields
- `SynqComm.Domain/Entities/ExternalParticipantIdentity.cs` — External email identity entity
- `SynqComm.Domain/Enums/EmailDirection.cs` — Inbound/Outbound enum
- `SynqComm.Domain/Enums/MatchStrategy.cs` — Thread matching strategy enum
- `SynqComm.Application/DTOs/InboundEmailIntakeRequest.cs` — Intake request DTO with attachment descriptors
- `SynqComm.Application/DTOs/InboundEmailIntakeResponse.cs` — Intake response DTO
- `SynqComm.Application/DTOs/EmailReferenceResponse.cs` — Email reference query DTO
- `SynqComm.Application/Repositories/IEmailMessageReferenceRepository.cs` — Email ref repo contract
- `SynqComm.Application/Repositories/IExternalParticipantIdentityRepository.cs` — Identity repo contract
- `SynqComm.Application/Interfaces/IEmailIntakeService.cs` — Service interface
- `SynqComm.Application/Services/EmailIntakeService.cs` — Core orchestration: matching, participant resolution, attachment linkage, audit
- `SynqComm.Infrastructure/Repositories/EmailMessageReferenceRepository.cs` — EF implementation
- `SynqComm.Infrastructure/Repositories/ExternalParticipantIdentityRepository.cs` — EF implementation
- `SynqComm.Infrastructure/Persistence/Configurations/EmailMessageReferenceConfiguration.cs` — Table config with indexes
- `SynqComm.Infrastructure/Persistence/Configurations/ExternalParticipantIdentityConfiguration.cs` — Table config with unique index
- `SynqComm.Api/Endpoints/EmailIntakeEndpoints.cs` — POST /api/synqcomm/email/intake + GET email-references
- `SynqComm.Tests/EmailIntakeTests.cs` — 12 email intake tests

## Files Updated
- `SynqComm.Domain/Enums/Channel.cs` — Added `Email` channel
- `SynqComm.Domain/SynqCommPermissions.cs` — Added `EmailIntake` permission
- `SynqComm.Infrastructure/Persistence/SynqCommDbContext.cs` — Added 2 new DbSets (7 total)
- `SynqComm.Infrastructure/DependencyInjection.cs` — Registered new repos + service
- `SynqComm.Api/Program.cs` — Registered email intake endpoints
- `SynqComm.Tests/TestHelpers.cs` — Added email repo/identity repo helpers

## Database Changes
- Migration: `AddEmailIntakeTables` — creates `comms_EmailMessageReferences` and `comms_ExternalParticipantIdentities` tables
- Indexes: `IX_EmailRefs_TenantId_InternetMessageId` (unique), `IX_EmailRefs_TenantId_ProviderMessageId`, `IX_EmailRefs_TenantId_InReplyToMessageId`, `IX_EmailRefs_TenantId_ConversationId`, `IX_EmailRefs_TenantId_ProviderThreadId`, `IX_ExternalIdentities_TenantId_NormalizedEmail` (unique)

## API Changes
- `POST /api/synqcomm/email/intake` — Accepts `InboundEmailIntakeRequest`, returns `InboundEmailIntakeResponse`. Requires `SYNQ_COMMS.email:intake` permission.
- `GET /api/synqcomm/conversations/{conversationId}/email-references` — Lists email references for a conversation. Requires `SYNQ_COMMS.conversation:read` permission + active participant.

## Test Results
- 53 total tests passing (12 new email intake tests + 41 prior)
- Email intake tests cover: InReplyTo matching, new conversation creation, conversation token matching, external identity reuse, no unsafe heuristic matching, email reference persistence, inbound attachment linkage, audit event emission, duplicate email rejection, References header matching, ProviderThread matching, prior BLK visibility regression

## Issues / Gaps
- CC/BCC recipients not processed (only From sender) — document for future block
- No outbound email sending capability
- No notification integration for incoming email events
- No UI components for email thread visualization
- `InMemoryDatabase` does not enforce unique index constraints in tests

## Next Recommendations
- BLK-002: Outbound email composition and sending (requires SMTP/provider integration)
- BLK-003: CC/BCC participant handling for inbound emails
- BLK-004: Email notification integration (new email → notification to assigned users)
- BLK-005: Email thread visualization UI components
