# LS-COMMS-02-BLK-002 — Outbound Email and Delivery Status Report

## Status
COMPLETE

## Objective
Extend SynqComm to support outbound email orchestration through Notifications service and track delivery status updates while preserving SynqComm as the communication system of record.

## Architecture Requirements
- Independent service under /apps/services/synqcomm
- Continue using separate SynqComm physical database
- No piggybacking on another service database
- No cross-database joins for core logic
- SynqComm owns communication records and email threading metadata
- Notifications owns outbound email delivery and retry/provider behavior
- Documents remain owned by Documents service
- Preserve Clean Architecture layering from prior blocks

## Steps Completed
- [x] Step 1: Review existing implementation and integration patterns
- [x] Step 2: Design outbound email and delivery-status extensions
- [x] Step 3: Add outbound email domain models and contracts
- [x] Step 4: Implement Notifications integration seam/client/contracts
- [x] Step 5: Implement outbound email composition and request flow
- [x] Step 6: Implement delivery status update flow
- [x] Step 7: Extend APIs and persistence
- [x] Step 8: Extend audit coverage
- [x] Step 9: Add/update migrations
- [x] Step 10: Add automated tests (13 new tests, 66 total passing)
- [x] Step 11: Final review

## Implementation Summary
SynqComm now supports full outbound email orchestration. Internal users can compose outbound emails from SharedExternal messages, which are submitted to the Notifications service for delivery. Delivery status flows back through a dedicated integration endpoint. Threading metadata (internetMessageId, inReplyToMessageId, referencesHeader) is generated to ensure future inbound replies thread correctly.

## Architecture Alignment
- **SynqComm** remains the system of record for conversations, messages, email references, and delivery state
- **Notifications** is the delivery engine — SynqComm calls `POST /v1/notifications` via `INotificationsServiceClient` abstraction
- No SMTP/provider logic exists in SynqComm
- No cross-database access — delivery state is stored in SynqComm's own DB
- Clean Architecture layering preserved: Domain → Application → Infrastructure → Api

## Notifications Integration
- `INotificationsServiceClient` abstraction with `SendEmailAsync(OutboundEmailPayload)`
- `NotificationsServiceClient` implementation uses `IHttpClientFactory` with named client `NotificationsService`
- Submits to `POST /v1/notifications` with channel=email, idempotency key, full email metadata
- Returns `NotificationsSendResult` with request ID, provider used, status
- Follows exact same HTTP client pattern used by Liens `NotificationPublisher`

## Outbound Threading Design
- Each outbound email generates a unique `InternetMessageId` via `EmailMessageReference.GenerateInternetMessageId()` format: `<conversationId.uniqueId@synqcomm.legalsynq.com>`
- When replying to an existing thread, `InReplyToMessageId` references the prior email's `InternetMessageId`
- `ReferencesHeader` builds cumulative chain from prior reference's chain + its own InternetMessageId
- Supports explicit `ReplyToEmailReferenceId` or auto-resolves latest conversation reference
- Threading identifiers are passed through to Notifications for provider inclusion in email headers

## Delivery Status Design
- Separate `EmailDeliveryState` entity tracks delivery lifecycle per outbound email
- Statuses: Pending, Queued, Sent, Delivered, Failed, Bounced, Deferred, Suppressed, Unknown
- Terminal status detection prevents re-processing already-completed deliveries
- Correlation by `ProviderMessageId` or `InternetMessageId`
- `SentAtUtc` on `EmailMessageReference` updated when status reaches Sent/Delivered
- Status normalization handles case-insensitive input and provider-specific variants

## Documents Integration
- Outbound email can include attachments from message-linked `MessageAttachment` records
- Attachment metadata (documentId, fileName, contentType, fileSizeBytes) passed to Notifications payload
- Only active attachments from SharedExternal messages are included
- No binary duplication — Notifications/provider responsible for document fetch
- Gap: Notifications service must support document URL resolution for attachment delivery

## Audit Integration
Outbound email audit events emitted:
- `OutboundEmailQueued` — successful outbound email submission
- `OutboundEmailFailed` — failed outbound email submission
- `OutboundEmailRejected` — visibility/authorization rejection
- `OutboundEmailDeliveryUpdate` — delivery status change
- `ConversationReopened` — conversation reopened due to outbound email on closed conversation
- Metadata includes: internetMessageId, conversationId, messageId, toAddresses, attachmentCount, deliveryStatus, notificationsRequestId, provider

## Database Changes
- Migration: `AddOutboundEmailDelivery`
- New table: `comms_EmailDeliveryStates` (id, tenantId, conversationId, messageId, emailMessageReferenceId, deliveryStatus, providerName, providerMessageId, notificationsRequestId, lastStatusAtUtc, lastErrorCode, lastErrorMessage, retryCount, timestamps)
- Indexes: `IX_EmailDelivery_TenantId_EmailMessageReferenceId`, `IX_EmailDelivery_TenantId_ProviderMessageId`, `IX_EmailDelivery_TenantId_ConversationId`, `IX_EmailDelivery_TenantId_NotificationsRequestId`
- FK: EmailDeliveryState → EmailMessageReference (cascade)

## Files Created
- `SynqComm.Domain/Entities/EmailDeliveryState.cs` — Delivery state entity with status update logic
- `SynqComm.Domain/Enums/DeliveryStatus.cs` — Delivery status constants with terminal detection
- `SynqComm.Application/Interfaces/INotificationsServiceClient.cs` — Notifications abstraction + payload/result records
- `SynqComm.Application/Interfaces/IOutboundEmailService.cs` — Service interface
- `SynqComm.Application/DTOs/SendOutboundEmailRequest.cs` — Outbound send request DTO
- `SynqComm.Application/DTOs/SendOutboundEmailResponse.cs` — Outbound send response DTO
- `SynqComm.Application/DTOs/DeliveryStatusUpdateRequest.cs` — Delivery status callback DTO
- `SynqComm.Application/DTOs/EmailDeliveryStateResponse.cs` — Delivery state query DTO
- `SynqComm.Application/Repositories/IEmailDeliveryStateRepository.cs` — Delivery state repo contract
- `SynqComm.Application/Services/OutboundEmailService.cs` — Core orchestration: validation, threading, send, delivery updates
- `SynqComm.Infrastructure/Repositories/EmailDeliveryStateRepository.cs` — EF implementation
- `SynqComm.Infrastructure/Notifications/NotificationsServiceClient.cs` — HTTP client for Notifications service
- `SynqComm.Infrastructure/Persistence/Configurations/EmailDeliveryStateConfiguration.cs` — Table/index config
- `SynqComm.Api/Endpoints/OutboundEmailEndpoints.cs` — 3 new endpoints
- `SynqComm.Tests/OutboundEmailTests.cs` — 11 outbound email tests

## Files Updated
- `SynqComm.Domain/Entities/EmailMessageReference.cs` — Added SetProviderMessageId, SetSentAtUtc, GenerateInternetMessageId
- `SynqComm.Domain/SynqCommPermissions.cs` — Added EmailSend, EmailDeliveryUpdate permissions
- `SynqComm.Application/Repositories/IEmailMessageReferenceRepository.cs` — Added GetLatestByConversationAsync, FindByMessageIdAsync, UpdateAsync
- `SynqComm.Application/Repositories/IMessageRepository.cs` — Added GetByIdAsync
- `SynqComm.Infrastructure/Repositories/EmailMessageReferenceRepository.cs` — Implemented new methods
- `SynqComm.Infrastructure/Repositories/MessageRepository.cs` — Implemented GetByIdAsync
- `SynqComm.Infrastructure/Persistence/SynqCommDbContext.cs` — Added EmailDeliveryStates DbSet (8 total)
- `SynqComm.Infrastructure/DependencyInjection.cs` — Registered delivery repo, outbound service, NotificationsService HTTP client
- `SynqComm.Api/Program.cs` — Registered outbound email endpoints
- `SynqComm.Tests/TestHelpers.cs` — Added delivery state repo helper, MockNotificationsServiceClient

## API Changes
- `POST /api/synqcomm/email/send` — Send outbound email via Notifications. Requires `SYNQ_COMMS.email:send` permission + authenticated internal participant.
- `POST /api/synqcomm/email/delivery-status` — Process delivery status callback from Notifications. Requires `SYNQ_COMMS.email:delivery-update` permission.
- `GET /api/synqcomm/conversations/{conversationId}/email-delivery` — List delivery states for a conversation. Requires `SYNQ_COMMS.conversation:read` + active participant.

## Test Results
- 66 total tests passing (13 new outbound + 12 email intake + 41 prior)
- Outbound tests cover: authorized send, non-participant rejection, canReply=false rejection, InternalOnly rejection, reply threading metadata, attachment inclusion, delivery status persistence, idempotent status updates, audit event emission, prior inbound threading regression, unmatched delivery status, failed send retry behavior, terminal status regression rejection

## Issues / Gaps
- Notifications service must implement `synqcomm_outbound_email` template for proper rendering
- Attachment file delivery requires Notifications to support document URL/fetch — seam is clean but untested end-to-end
- CC addresses are passed to Notifications but actual multi-recipient delivery depends on Notifications/provider support
- BCC not implemented (deferred to future block)
- From address hardcoded to `noreply@legalsynq.com` — future: tenant-configurable sender addresses
- Delivery status callback endpoint uses standard auth — future: consider internal/service-account-only auth for system integration
- No retry orchestration in SynqComm — retries owned by Notifications

## Next Recommendations
- LS-COMMS-02-BLK-003 — CC/BCC Participant Handling and Email Thread Expansion
- LS-COMMS-02-BLK-004 — Tenant-Configurable Sender Addresses and Email Templates
- LS-COMMS-02-BLK-005 — End-to-End Notifications Integration Testing
