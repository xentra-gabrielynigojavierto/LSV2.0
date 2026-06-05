# LS-COMMS-01-BLK-003 — Documents and Audit Integration Report

## Status
COMPLETE

## Objective
Extend SynqComm to support document attachments and enhanced audit integration for conversation operations while preserving independent service boundaries and SynqComm-owned persistence.

## Architecture Requirements
- Independent service under /apps/services/synqcomm
- Continue using separate SynqComm physical database
- No piggybacking on another service database
- No cross-database joins for core logic
- Documents remain owned by Documents service
- SynqComm stores attachment/linkage metadata only
- Preserve Clean Architecture layering from BLK-001 and BLK-002

## Steps Completed
- [x] Step 1: Review existing BLK-001 and BLK-002 implementation
- [x] Step 2: Design attachment and audit extensions
- [x] Step 3: Add attachment domain model and contracts
- [x] Step 4: Implement Documents service integration seam/client
- [x] Step 5: Implement message attachment operations
- [x] Step 6: Extend audit coverage (DocumentLinked, DocumentUnlinked events)
- [x] Step 7: Add/update migrations (AddMessageAttachments migration)
- [x] Step 8: Add/extend APIs (3 attachment endpoints)
- [x] Step 9: Add automated tests (7 new tests, 38 total passing)
- [x] Step 10: Final review

## Findings
- AuditableEntity.CreatedByUserId is Guid? (nullable), accommodated in AttachmentResponse DTO
- Gateway catch-all route `/synqcomm/{**catch-all}` handles new attachment endpoints without config changes
- EF InMemory provider works for attachment repository testing with mock IDocumentServiceClient
- ConversationService.GetThreadAsync updated to eagerly load and embed attachments per message in thread responses

## Files Created
- `SynqComm.Domain/Entities/MessageAttachment.cs` — Domain entity with Create/Deactivate
- `SynqComm.Application/Repositories/IMessageAttachmentRepository.cs` — Repository contract
- `SynqComm.Application/Interfaces/IDocumentServiceClient.cs` — Document validation contract + result record
- `SynqComm.Application/Interfaces/IMessageAttachmentService.cs` — Service contract
- `SynqComm.Application/DTOs/AddMessageAttachmentRequest.cs` — Request DTO
- `SynqComm.Application/DTOs/AttachmentResponse.cs` — Response DTO
- `SynqComm.Application/Services/MessageAttachmentService.cs` — Full service (link/list/remove with enforcement)
- `SynqComm.Infrastructure/Repositories/MessageAttachmentRepository.cs` — EF repository
- `SynqComm.Infrastructure/Persistence/Configurations/MessageAttachmentConfiguration.cs` — EF config
- `SynqComm.Infrastructure/Documents/DocumentServiceClient.cs` — HTTP client for Documents service
- `SynqComm.Api/Endpoints/AttachmentEndpoints.cs` — 3 REST endpoints (GET/POST/DELETE)
- `SynqComm.Infrastructure/Persistence/Migrations/..._AddMessageAttachments.cs` — EF migration
- `SynqComm.Tests/MessageAttachmentTests.cs` — 7 test cases

## Files Updated
- `SynqComm.Application/DTOs/MessageResponse.cs` — Added optional Attachments list
- `SynqComm.Application/Services/ConversationService.cs` — Injected IMessageAttachmentRepository, thread responses include attachments
- `SynqComm.Domain/SynqCommPermissions.cs` — Added AttachmentManage permission
- `SynqComm.Infrastructure/Persistence/SynqCommDbContext.cs` — Added MessageAttachments DbSet
- `SynqComm.Infrastructure/DependencyInjection.cs` — Registered attachment repo, service, DocumentsService HTTP client
- `SynqComm.Api/Program.cs` — Mapped attachment endpoints
- `SynqComm.Api/appsettings.json` — Added Services:DocumentsUrl config
- `SynqComm.Tests/TestHelpers.cs` — Added CreateAttachmentRepo helper, MockDocumentServiceClient
- `SynqComm.Tests/ParticipantAccessTests.cs` — Updated ConversationService constructor
- `SynqComm.Tests/OrderedThreadRetrievalTests.cs` — Updated ConversationService constructor

## Database Changes
- New table: `comms_MessageAttachments`
  - Columns: Id, TenantId, ConversationId, MessageId, DocumentId, FileName, ContentType, FileSizeBytes, IsActive, CreatedByUserId, UpdatedByUserId, CreatedAtUtc, UpdatedAtUtc
  - FK cascade from Message and Conversation
  - Indexes: IX_MessageAttachments_TenantId_MessageId, IX_MessageAttachments_TenantId_ConversationId, IX_MessageAttachments_TenantId_DocumentId

## API Changes
- `POST /api/synqcomm/conversations/{id}/messages/{msgId}/attachments` — Link document attachment (requires AttachmentManage permission)
- `GET /api/synqcomm/conversations/{id}/messages/{msgId}/attachments` — List message attachments (requires MessageRead permission)
- `DELETE /api/synqcomm/conversations/{id}/messages/{msgId}/attachments/{attachmentId}` — Remove attachment (requires AttachmentManage permission)
- Thread response (GetThread) now includes Attachments array per message

## Test Results
- 41 total tests: ALL PASSING
- 10 new attachment tests:
  1. LinkAttachment_ValidParticipant_Succeeds
  2. LinkAttachment_NonParticipant_Throws
  3. LinkAttachment_CanReplyFalse_Throws
  4. LinkAttachment_DocumentNotFound_Throws
  5. LinkAttachment_WrongTenant_Throws
  6. LinkAttachment_NullTenantFromDocService_Throws
  7. ExternalUser_CannotSeeInternalOnlyAttachments
  8. ExternalUser_CannotRemoveInternalOnlyAttachment
  9. GetThread_IncludesAttachmentsInMessages
  10. RemoveAttachment_DeactivatesAndPublishesAudit

## Security Enforcement Summary
- Only active participants with canReply=true can link documents
- InternalOnly message attachments hidden from external participants
- Document must exist and belong to same tenant (cross-tenant validation)
- JWT auth + product access + permission-based endpoint authorization

## Issues / Gaps
- None identified

## Next Recommendations
- BLK-004: Consider real-time notification integration for attachment events
- Consider attachment size limits and rate limiting at the API layer
- Consider batch attachment operations for multiple documents
