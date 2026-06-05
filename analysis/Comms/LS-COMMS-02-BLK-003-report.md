# LS-COMMS-02-BLK-003 — CC/BCC Participant Handling and Email Thread Expansion Report

## Status
COMPLETE

## Objective
Extend SynqComm to support safe CC/BCC handling, external participant expansion from email recipients, and multi-recipient email thread continuity while preserving SynqComm service boundaries and visibility controls.

## Architecture Requirements
- Independent service under /apps/services/synqcomm
- Continue using separate SynqComm physical database
- No piggybacking on another service database
- No cross-database joins for core logic
- SynqComm owns participants, email references, and thread metadata
- Notifications remains the outbound delivery engine
- Documents remain owned by Documents service
- Preserve Clean Architecture layering from prior blocks

## Steps Completed
- [x] Step 1: Review existing implementation and recipient handling gaps
- [x] Step 2: Design CC/BCC and participant-expansion model
- [x] Step 3: Add domain models and contracts
- [x] Step 4: Implement inbound CC/BCC recipient processing
- [x] Step 5: Implement outbound recipient expansion and reply-all logic
- [x] Step 6: Implement BCC confidentiality rules
- [x] Step 7: Extend APIs and persistence
- [x] Step 8: Extend audit coverage
- [x] Step 9: Add/update migrations
- [x] Step 10: Add automated tests (9 new tests, 75 total passing)
- [x] Step 11: Final review

## Implementation Summary
SynqComm now supports structured recipient tracking per email reference with full CC/BCC handling. Inbound CC recipients are automatically resolved to external participant identities and expanded as conversation participants. Outbound emails support To/Cc/Bcc with BCC stored as Hidden recipients that are never exposed through visible APIs. Reply-all preview reconstructs visible recipients from the latest email reference while excluding BCC and system sender addresses. All recipient data is deduplicated by normalized email address.

## Architecture Alignment
- **SynqComm** remains the system of record for conversations, messages, participants, email references, recipient records, and delivery state
- **Notifications** is the delivery engine — BccAddresses are passed through to Notifications for provider-level BCC delivery
- No SMTP/provider logic exists in SynqComm
- No cross-database access — recipient records are stored in SynqComm's own DB
- Clean Architecture layering preserved: Domain → Application → Infrastructure → Api

## Recipient Expansion Design
### EmailRecipientRecord Entity
- Per-email-reference tracking of individual recipients with normalized email, type, and visibility
- RecipientType: To, Cc, Bcc
- RecipientVisibility: Visible (To/Cc), Hidden (Bcc) — derived automatically from RecipientType
- RecipientSource tracks origin: INBOUND_TO, INBOUND_CC, OUTBOUND_TO, OUTBOUND_CC, OUTBOUND_BCC
- ParticipantId nullable until resolved; IsResolvedToParticipant tracks resolution state

### Inbound Processing
- From sender: resolved as primary message sender and conversation participant (existing behavior)
- To addresses: stored as Visible To recipient records for tracking; not expanded to participants (mailbox addresses typically represent the tenant)
- CC addresses: stored as Visible Cc recipient records; resolved/reused as external identities; expanded as conversation participants
- Sender email excluded from recipient records via deduplication
- Duplicate addresses across To/Cc deduplicated by normalized email

### Outbound Processing
- To/Cc: stored as Visible recipient records, passed to Notifications
- Bcc: stored as Hidden recipient records, passed to Notifications, never returned from visible APIs
- Duplicate addresses across To/Cc/Bcc deduplicated (first occurrence wins)
- Recipient records created after successful Notifications submission (same transactional safety as BLK-002)

## BCC Confidentiality Design
- BCC recipients stored with RecipientVisibility = "Hidden"
- `ListVisibleByEmailReferenceAsync` and `ListVisibleByConversationAsync` filter to Visible only
- Reply-all preview excludes Hidden recipients entirely
- General conversation thread/email reference APIs never expose Hidden recipients
- `ListByEmailReferenceAsync` (all recipients including Hidden) exists for internal/system-safe contexts only
- External participants never infer existence of Hidden recipients from any API response

## Reply-All Design
- Reconstructs recipients from the latest email reference in the conversation
- Includes the latest reference's FromEmail as a To recipient (unless it's the system noreply address)
- Includes all Visible (To/Cc) recipients from that reference
- Excludes Hidden (Bcc) recipients entirely
- Deduplicates by normalized email
- Returns structured `ReplyAllPreviewResponse` with separated To and Cc lists
- Exposed via `GET /api/synqcomm/conversations/{id}/reply-all-preview` with standard conversation-read access

## Audit Integration
New audit events emitted:
- `InboundRecipientRecordsCreated` — recipient records created for inbound email (metadata: toCount, ccCount, participantExpansionCount)
- `ExternalParticipantExpandedFromCc` — new external participant auto-created from CC recipient
- `ExternalIdentityReusedFromCc` — existing external identity reused for CC recipient
- `OutboundRecipientRecordsCreated` — recipient records created for outbound email (metadata: visibleCount, hiddenCount, toCount, ccCount, bccCount)
- `ReplyAllRecipientsResolved` — reply-all visible recipients computed (metadata: toCount, ccCount, sourceEmailReferenceId)
- Existing `OutboundEmailRejected` audit event still fires for visibility/authorization violations

## Database Changes
- Migration: `AddEmailRecipientRecords`
- New table: `comms_EmailRecipientRecords` (id, tenantId, conversationId, emailMessageReferenceId, participantId, normalizedEmail, displayName, recipientType, recipientVisibility, isResolvedToParticipant, recipientSource, timestamps, audit fields)
- Indexes:
  - `IX_EmailRecipients_TenantId_EmailMessageReferenceId`
  - `IX_EmailRecipients_TenantId_ConversationId`
  - `IX_EmailRecipients_TenantId_NormalizedEmail`
  - `IX_EmailRecipients_TenantId_EmailMessageReferenceId_Visibility`

## Files Created
- `SynqComm.Domain/Entities/EmailRecipientRecord.cs` — Recipient record entity with type/visibility/participant linking
- `SynqComm.Domain/Enums/RecipientType.cs` — To, Cc, Bcc constants with validation
- `SynqComm.Domain/Enums/RecipientVisibility.cs` — Visible, Hidden constants with type derivation
- `SynqComm.Application/Repositories/IEmailRecipientRecordRepository.cs` — Repository contract
- `SynqComm.Application/DTOs/EmailRecipientRecordResponse.cs` — Response DTO
- `SynqComm.Application/DTOs/ReplyAllPreviewResponse.cs` — Reply-all preview response with To/Cc lists
- `SynqComm.Infrastructure/Repositories/EmailRecipientRecordRepository.cs` — EF implementation with visible-only filtering
- `SynqComm.Infrastructure/Persistence/Configurations/EmailRecipientRecordConfiguration.cs` — Table/index config
- `SynqComm.Tests/CcBccRecipientTests.cs` — 9 comprehensive CC/BCC tests

## Files Updated
- `SynqComm.Application/DTOs/SendOutboundEmailRequest.cs` — Added BccAddresses parameter
- `SynqComm.Application/Interfaces/INotificationsServiceClient.cs` — Added BccAddresses to OutboundEmailPayload
- `SynqComm.Application/Interfaces/IOutboundEmailService.cs` — Added GetReplyAllPreviewAsync method
- `SynqComm.Application/Services/EmailIntakeService.cs` — Added IEmailRecipientRecordRepository dependency; added ProcessInboundRecipientsAsync for CC recipient processing and participant expansion
- `SynqComm.Application/Services/OutboundEmailService.cs` — Added IEmailRecipientRecordRepository dependency; added BuildRecipientRecords helper; added GetReplyAllPreviewAsync; BccAddresses passed through to Notifications payload
- `SynqComm.Infrastructure/Persistence/SynqCommDbContext.cs` — Added EmailRecipientRecords DbSet (9 total)
- `SynqComm.Infrastructure/DependencyInjection.cs` — Registered IEmailRecipientRecordRepository
- `SynqComm.Infrastructure/Notifications/NotificationsServiceClient.cs` — Added bcc to recipient payload
- `SynqComm.Api/Endpoints/OutboundEmailEndpoints.cs` — Added reply-all-preview endpoint
- `SynqComm.Tests/TestHelpers.cs` — Added CreateRecipientRepo helper
- `SynqComm.Tests/EmailIntakeTests.cs` — Updated constructor call with recipient repo
- `SynqComm.Tests/OutboundEmailTests.cs` — Updated constructor calls with recipient repo

## API Changes
- `POST /api/synqcomm/email/send` — Now supports BccAddresses parameter; recipient records persisted for To/Cc/Bcc
- `POST /api/synqcomm/email/intake` — Now processes CC recipients with participant expansion; stores To/Cc recipient records
- `GET /api/synqcomm/conversations/{conversationId}/reply-all-preview` — NEW: Returns computed visible recipients for reply-all operation, excluding BCC and system sender. Requires conversation-read permission + active participant.

## Test Results
- 75 total tests passing (9 new CC/BCC + 13 outbound + 12 email intake + 41 prior)
- CC/BCC tests cover:
  1. Inbound CC recipient records persist as visible
  2. External identity reuse from CC
  3. Outbound To/Cc/Bcc passed to Notifications correctly
  4. BCC not exposed in visible recipient APIs
  5. Reply-all preview excludes BCC and current sender
  6. Duplicate recipient deduplication
  7. InternalOnly message cannot expand external participants
  8. Audit events emitted for recipient expansion
  9. Prior inbound/outbound threading regression test

## Issues / Gaps
- Inbound BCC data: not reliably available from email provider webhooks; model is ready but no inbound BCC processing implemented (by design)
- Display names for CC recipients: not parsed from raw email addresses; future enhancement to extract "Name <email>" format
- Reply-all preview uses latest email reference only; may need enhancement for complex multi-branch threads
- Notifications service must support BCC in its provider integration for actual BCC delivery
- No per-recipient delivery tracking (one delivery state per outbound email, not per recipient)

## Next Recommendations
- LS-COMMS-02-BLK-004 — Tenant-Configurable Sender Addresses and Email Templates
