# LS-COMMS-02-BLK-004 — Tenant-Configurable Sender Addresses and Email Templates Report

## Status
COMPLETE

## Objective
Extend SynqComm to support tenant-configurable sender identities and template-driven outbound email composition while preserving SynqComm as the communication system of record and Notifications as the delivery engine.

## Architecture Requirements
- Independent service under /apps/services/synqcomm
- Continue using separate SynqComm physical database
- No piggybacking on another service database
- No cross-database joins for core logic
- SynqComm owns sender config references, template metadata, and email composition state
- Notifications remains the outbound delivery engine
- Documents remains owned by Documents service
- Preserve Clean Architecture layering from prior blocks

## Steps Completed
- [x] Step 1: Review existing implementation and configuration/template gaps
- [x] Step 2: Design sender identity and template model
- [x] Step 3: Add domain models and contracts
- [x] Step 4: Implement tenant-configurable sender identity handling
- [x] Step 5: Implement template selection and composition flow
- [x] Step 6: Extend Notifications integration payloads
- [x] Step 7: Extend APIs and persistence
- [x] Step 8: Extend audit coverage
- [x] Step 9: Add/update migrations
- [x] Step 10: Add automated tests (13 new tests, 88 total passing)
- [x] Step 11: Final review

## Implementation Summary
SynqComm now supports tenant-configurable sender identities and template-driven outbound email composition. Tenants can create, manage, and validate sender configurations with verification status enforcement. Email templates support tenant-scoped and global scopes with simple token-based rendering. Outbound emails resolve sender identity and template through a clear precedence chain, with full composition metadata persisted on the email reference record.

## Architecture Alignment
- **SynqComm** remains the system of record for conversations, messages, participants, email references, sender configs, template configs, and delivery state
- **Notifications** is the delivery engine — SynqComm renders content (Option A: SynqComm renders, Notifications sends) and passes resolved sender/recipient/content/template data
- No SMTP/provider logic exists in SynqComm
- No cross-database access — all sender/template tables are in SynqComm's own DB
- Clean Architecture layering preserved: Domain → Application → Infrastructure → Api

## Sender Identity Design
### TenantEmailSenderConfig Entity
- Tenant-scoped sender identity with fromEmail, displayName, replyToEmail, senderType, verificationStatus
- SenderType constants: NOREPLY, SUPPORT, OPERATIONS, PRODUCT, CUSTOM
- VerificationStatus constants: PENDING, VERIFIED, REJECTED, DISABLED
- Only VERIFIED + active configs can be used for sending (enforced by `CanSend()`)
- One default sender per tenant (enforced: setting a new default clears existing defaults)
- AllowedForSharedExternal flag for future visibility control

### Sender Resolution Flow
1. If `senderConfigId` is explicitly provided → look up that config, validate it can send
2. If no explicit config → use tenant's active default sender
3. If no tenant sender config exists → fall back to hardcoded `noreply@legalsynq.com` (documented temporary fallback)
4. Rejected if sender config is inactive or unverified

## Template Design
### EmailTemplateConfig Entity
- Supports GLOBAL (no tenantId) and TENANT (with tenantId) scopes
- Template key (normalized lowercase), displayName, subjectTemplate, bodyTextTemplate, bodyHtmlTemplate
- Version tracking (auto-incremented on update)
- isDefault and isActive flags

### Template Resolution Flow
1. If `templateConfigId` is provided → look up by ID
2. If `templateKey` is provided → look up tenant-scoped first, then fall back to global
3. Tenant template overrides global when keys collide
4. Inactive templates are rejected

### Template Rendering
- Simple dictionary-based token replacement: `{{variableName}}`
- Case-insensitive token matching
- Missing variables left as-is (safe rendering)
- Rendering happens in SynqComm (Option A), rendered content passed to Notifications

## Composition Precedence
1. **EXPLICIT_OVERRIDE** — SubjectOverride/BodyTextOverride/BodyHtmlOverride in request take precedence over everything
2. **TEMPLATE** — If a template is resolved, rendered subject/body are used (with fallback to message/conversation content for empty template fields)
3. **MESSAGE_CONTENT** — If no template or override, existing message body and conversation subject are used

CompositionMode is persisted on the EmailMessageReference and returned in the response.

## Notifications Integration
**Option A — SynqComm renders, Notifications sends** (chosen design)
- SynqComm resolves sender config and template, renders subject/body
- Notifications payload now includes:
  - Resolved `fromEmail` and `fromDisplayName` from sender config
  - `replyToEmail` from sender config or request override
  - `templateKey` from resolved template (passed to Notifications for potential secondary rendering/routing)
  - Template variables merged into templateData dict
  - Sender block (`sender.email`, `sender.displayName`, `sender.replyTo`) added to payload
  - All existing threading/recipient/attachment fields preserved

## Audit Integration
New audit events emitted:
- `SenderConfigCreated` — new sender config created (metadata: fromEmail, senderType, isDefault, verificationStatus)
- `SenderConfigUpdated` — sender config modified (metadata: fromEmail, isActive, verificationStatus)
- `SenderDefaultChanged` — default sender changed for tenant
- `TemplateCreated` — new template created (metadata: templateKey, templateScope, isDefault)
- `TemplateUpdated` — template modified (metadata: templateKey, version)
- `OutboundEmailSenderResolved` — sender config resolved during outbound send (metadata: fromEmail, senderType)
- `OutboundEmailTemplateResolved` — template resolved during outbound send (metadata: templateKey, version, templateScope)
- `OutboundEmailRejected` — outbound email rejected due to sender/template validation (metadata: reason details)
- Existing audit events (OutboundEmailQueued, OutboundEmailFailed, etc.) continue unchanged

## Database Changes
- Migration: `AddSenderConfigsAndTemplates`
- New table: `comms_TenantEmailSenderConfigs` (id, tenantId, displayName, fromEmail, replyToEmail, senderType, isDefault, isActive, verificationStatus, allowedForSharedExternal, timestamps, audit fields)
- New table: `comms_EmailTemplateConfigs` (id, tenantId nullable, templateKey, displayName, subjectTemplate, bodyTextTemplate, bodyHtmlTemplate, templateScope, isDefault, isActive, version, timestamps, audit fields)
- Extended table: `comms_EmailMessageReferences` — added columns: SenderConfigId, SenderConfigEmail, TemplateConfigId, TemplateKey, CompositionMode
- Indexes:
  - `IX_SenderConfigs_TenantId_FromEmail`
  - `IX_SenderConfigs_TenantId_IsDefault`
  - `IX_SenderConfigs_TenantId_SenderType`
  - `IX_Templates_TenantId_TemplateKey`
  - `IX_Templates_TemplateScope_TemplateKey`

## Files Created
- `SynqComm.Domain/Entities/TenantEmailSenderConfig.cs` — Sender config entity with verification and default management
- `SynqComm.Domain/Entities/EmailTemplateConfig.cs` — Template config entity with token-based rendering
- `SynqComm.Domain/Enums/SenderType.cs` — NOREPLY, SUPPORT, OPERATIONS, PRODUCT, CUSTOM
- `SynqComm.Domain/Enums/VerificationStatus.cs` — PENDING, VERIFIED, REJECTED, DISABLED
- `SynqComm.Domain/Enums/TemplateScope.cs` — GLOBAL, TENANT
- `SynqComm.Application/Repositories/ITenantEmailSenderConfigRepository.cs` — Repository contract
- `SynqComm.Application/Repositories/IEmailTemplateConfigRepository.cs` — Repository contract
- `SynqComm.Application/Interfaces/ISenderConfigService.cs` — Sender config service contract
- `SynqComm.Application/Interfaces/IEmailTemplateService.cs` — Template service contract
- `SynqComm.Application/DTOs/TenantEmailSenderConfigDtos.cs` — Create/Update/Response DTOs
- `SynqComm.Application/DTOs/EmailTemplateConfigDtos.cs` — Create/Update/Response DTOs
- `SynqComm.Application/Services/SenderConfigService.cs` — CRUD + default management + audit
- `SynqComm.Application/Services/EmailTemplateService.cs` — CRUD + tenant ownership validation + audit
- `SynqComm.Infrastructure/Repositories/TenantEmailSenderConfigRepository.cs` — EF implementation
- `SynqComm.Infrastructure/Repositories/EmailTemplateConfigRepository.cs` — EF implementation with tenant/global scope resolution
- `SynqComm.Infrastructure/Persistence/Configurations/TenantEmailSenderConfigConfiguration.cs` — Table/index config
- `SynqComm.Infrastructure/Persistence/Configurations/EmailTemplateConfigConfiguration.cs` — Table/index config
- `SynqComm.Api/Endpoints/SenderConfigEndpoints.cs` — CRUD endpoints for sender configs
- `SynqComm.Api/Endpoints/EmailTemplateEndpoints.cs` — CRUD endpoints for email templates
- `SynqComm.Tests/SenderTemplateTests.cs` — 11 comprehensive sender/template tests

## Files Updated
- `SynqComm.Domain/Entities/EmailMessageReference.cs` — Added SenderConfigId, SenderConfigEmail, TemplateConfigId, TemplateKey, CompositionMode fields + SetCompositionMetadata method
- `SynqComm.Domain/SynqCommPermissions.cs` — Added EmailConfigManage permission
- `SynqComm.Application/DTOs/SendOutboundEmailRequest.cs` — Added SenderConfigId, TemplateKey, TemplateConfigId, TemplateVariables, ReplyToOverride parameters
- `SynqComm.Application/DTOs/SendOutboundEmailResponse.cs` — Added SenderConfigId, SenderEmail, TemplateKey, TemplateConfigId, RenderedSubject, CompositionMode
- `SynqComm.Application/Interfaces/INotificationsServiceClient.cs` — Added ReplyToEmail, TemplateKey, TemplateData to OutboundEmailPayload
- `SynqComm.Application/Services/OutboundEmailService.cs` — Added sender/template resolution (ResolveSenderAsync, ResolveCompositionAsync); replaced hardcoded sender with config-driven resolution; stores composition metadata on email reference
- `SynqComm.Infrastructure/Persistence/SynqCommDbContext.cs` — Added TenantEmailSenderConfigs and EmailTemplateConfigs DbSets (11 total)
- `SynqComm.Infrastructure/DependencyInjection.cs` — Registered sender/template repos and services
- `SynqComm.Infrastructure/Notifications/NotificationsServiceClient.cs` — Added sender block, replyTo, templateKey, and templateData to Notifications payload
- `SynqComm.Api/Program.cs` — Registered sender config and template endpoint mappers
- `SynqComm.Tests/TestHelpers.cs` — Added CreateSenderConfigRepo and CreateTemplateConfigRepo helpers
- `SynqComm.Tests/OutboundEmailTests.cs` — Updated OutboundEmailService constructor with 2 new dependencies
- `SynqComm.Tests/CcBccRecipientTests.cs` — Updated OutboundEmailService constructor with 2 new dependencies

## API Changes
- `POST /api/synqcomm/email/sender-configs` — Create tenant sender config (requires EmailConfigManage)
- `GET /api/synqcomm/email/sender-configs` — List tenant sender configs
- `GET /api/synqcomm/email/sender-configs/{id}` — Get sender config by ID
- `PATCH /api/synqcomm/email/sender-configs/{id}` — Update sender config
- `POST /api/synqcomm/email/templates` — Create email template config
- `GET /api/synqcomm/email/templates` — List templates (tenant + global)
- `GET /api/synqcomm/email/templates/{id}` — Get template by ID
- `PATCH /api/synqcomm/email/templates/{id}` — Update template
- `POST /api/synqcomm/email/send` — Extended: now supports SenderConfigId, TemplateKey, TemplateConfigId, TemplateVariables, ReplyToOverride; response includes SenderConfigId, SenderEmail, TemplateKey, TemplateConfigId, RenderedSubject, CompositionMode

## Test Results
- 88 total tests passing (13 new sender/template + 9 CC/BCC + 13 outbound + 12 email intake + 41 prior)
- BLK-004 tests cover:
  1. Default sender config resolution uses verified default
  2. Invalid (unverified/inactive) sender config is rejected
  3. Template resolution by key works correctly
  4. Tenant template overrides global template with same key
  5. Template rendering applies variables correctly (subject + body text + body HTML)
  6. Explicit override precedence beats template
  7. Sender/template metadata persisted on email reference
  8. Notifications payload includes resolved sender data (fromEmail, displayName, replyTo)
  9. Authorization/visibility rules still hold (InternalOnly cannot be sent)
  10. Audit events emitted for sender/template config changes and composition actions
  11. Cross-tenant template access denied (IDOR protection)
  12. Global template creation blocked from tenant endpoints
  13. Prior outbound threading regression test (threading metadata correct after sender/template additions)

## Issues / Gaps
- Sender verification is config-level only; no actual DNS/DKIM/SPF verification workflow yet (would require Notifications service integration)
- No unique constraint on (tenantId, fromEmail) for sender configs — allows duplicates currently
- Template rendering is simple token replacement; no conditionals, loops, or HTML escaping
- No template preview/dry-run endpoint yet
- Global template management (scope=GLOBAL) requires same EmailConfigManage permission as tenant; no separate global admin permission
- Sender fallback to hardcoded noreply@legalsynq.com when no tenant config exists is a temporary design, documented as a gap

## Next Recommendations
- LS-COMMS-02-BLK-005 — End-to-End Notifications Integration Testing
