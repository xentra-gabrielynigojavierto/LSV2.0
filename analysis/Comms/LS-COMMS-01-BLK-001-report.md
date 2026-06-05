# LS-COMMS-01-BLK-001 — Core Domain Foundation Report

## Status
COMPLETED

## Objective
Implement SynqComm as an independent shared service with its own physically separate database and core domain foundation including conversations, messages, participants, statuses, and context linking.

## Architecture Requirements
- Independent service under /apps/services/synqcomm
- Separate physical database (synqcomm_db) owned by SynqComm
- Separate DB connection/configuration (ConnectionStrings:SynqCommDb)
- Separate migrations (EF Core)
- No piggybacking on another service database
- No cross-database joins for core logic

## Architecture Findings
- **Framework**: .NET 8 minimal API (WebApplication.CreateBuilder pattern)
- **Project structure**: Clean Architecture — Domain / Application / Infrastructure / Api
- **ORM**: Entity Framework Core 8 with Pomelo MySQL provider
- **Auth**: JWT Bearer with ICurrentRequestContext from BuildingBlocks
- **Base entity**: AuditableEntity (CreatedAtUtc, UpdatedAtUtc, CreatedByUserId, UpdatedByUserId)
- **Endpoints**: Static extension methods using MapGroup, RequireAuthorization
- **Repos**: Interface in Application, implementation in Infrastructure
- **DI**: Static extension method in Infrastructure (AddSynqCommServices)
- **Shared libs**: BuildingBlocks (auth, context, domain, exceptions), Contracts (HealthResponse, InfoResponse), AuditClient
- **DB host**: AWS RDS MySQL at legalsynqplatform.cpq48wc2krn5.us-east-2.rds.amazonaws.com:3306
- **Port**: 5011

## Steps Completed
- [x] Step 1: Codebase scan and architecture alignment
- [x] Step 2: Service scaffolding (4 projects + test project)
- [x] Step 3: Domain entities + enums (8 enums, 3 entities)
- [x] Step 4: Application layer (7 DTOs, 3 repo interfaces, 4 service interfaces, 3 service implementations)
- [x] Step 5: Infrastructure layer (DbContext, 3 EF configs, 3 repositories, AuditPublisher, DI)
- [x] Step 6: API layer (Program.cs, 3 endpoint groups, ExceptionHandlingMiddleware, DesignTimeDbContextFactory)
- [x] Step 7: Solution integration (LegalSynq.sln, gateway routes, run-dev.sh)
- [x] Step 8: EF migration (InitialCreate)
- [x] Step 9: Build validation (all projects compile)
- [x] Step 10: Documentation (replit.md, report)

## Files Created

### Domain Layer
- `apps/services/synqcomm/SynqComm.Domain/SynqComm.Domain.csproj`
- `apps/services/synqcomm/SynqComm.Domain/Entities/Conversation.cs`
- `apps/services/synqcomm/SynqComm.Domain/Entities/Message.cs`
- `apps/services/synqcomm/SynqComm.Domain/Entities/ConversationParticipant.cs`
- `apps/services/synqcomm/SynqComm.Domain/Enums/ConversationStatus.cs`
- `apps/services/synqcomm/SynqComm.Domain/Enums/VisibilityType.cs`
- `apps/services/synqcomm/SynqComm.Domain/Enums/Channel.cs`
- `apps/services/synqcomm/SynqComm.Domain/Enums/Direction.cs`
- `apps/services/synqcomm/SynqComm.Domain/Enums/MessageStatus.cs`
- `apps/services/synqcomm/SynqComm.Domain/Enums/ParticipantType.cs`
- `apps/services/synqcomm/SynqComm.Domain/Enums/ParticipantRole.cs`
- `apps/services/synqcomm/SynqComm.Domain/Enums/ContextType.cs`

### Application Layer
- `apps/services/synqcomm/SynqComm.Application/SynqComm.Application.csproj`
- `apps/services/synqcomm/SynqComm.Application/DTOs/CreateConversationRequest.cs`
- `apps/services/synqcomm/SynqComm.Application/DTOs/AddMessageRequest.cs`
- `apps/services/synqcomm/SynqComm.Application/DTOs/AddParticipantRequest.cs`
- `apps/services/synqcomm/SynqComm.Application/DTOs/UpdateConversationStatusRequest.cs`
- `apps/services/synqcomm/SynqComm.Application/DTOs/ConversationResponse.cs`
- `apps/services/synqcomm/SynqComm.Application/DTOs/MessageResponse.cs`
- `apps/services/synqcomm/SynqComm.Application/DTOs/ParticipantResponse.cs`
- `apps/services/synqcomm/SynqComm.Application/Repositories/IConversationRepository.cs`
- `apps/services/synqcomm/SynqComm.Application/Repositories/IMessageRepository.cs`
- `apps/services/synqcomm/SynqComm.Application/Repositories/IParticipantRepository.cs`
- `apps/services/synqcomm/SynqComm.Application/Interfaces/IConversationService.cs`
- `apps/services/synqcomm/SynqComm.Application/Interfaces/IMessageService.cs`
- `apps/services/synqcomm/SynqComm.Application/Interfaces/IParticipantService.cs`
- `apps/services/synqcomm/SynqComm.Application/Interfaces/IAuditPublisher.cs`
- `apps/services/synqcomm/SynqComm.Application/Services/ConversationService.cs`
- `apps/services/synqcomm/SynqComm.Application/Services/MessageService.cs`
- `apps/services/synqcomm/SynqComm.Application/Services/ParticipantService.cs`

### Infrastructure Layer
- `apps/services/synqcomm/SynqComm.Infrastructure/SynqComm.Infrastructure.csproj`
- `apps/services/synqcomm/SynqComm.Infrastructure/Persistence/SynqCommDbContext.cs`
- `apps/services/synqcomm/SynqComm.Infrastructure/Persistence/Configurations/ConversationConfiguration.cs`
- `apps/services/synqcomm/SynqComm.Infrastructure/Persistence/Configurations/MessageConfiguration.cs`
- `apps/services/synqcomm/SynqComm.Infrastructure/Persistence/Configurations/ConversationParticipantConfiguration.cs`
- `apps/services/synqcomm/SynqComm.Infrastructure/Persistence/Migrations/` (InitialCreate migration)
- `apps/services/synqcomm/SynqComm.Infrastructure/Repositories/ConversationRepository.cs`
- `apps/services/synqcomm/SynqComm.Infrastructure/Repositories/MessageRepository.cs`
- `apps/services/synqcomm/SynqComm.Infrastructure/Repositories/ParticipantRepository.cs`
- `apps/services/synqcomm/SynqComm.Infrastructure/Audit/AuditPublisher.cs`
- `apps/services/synqcomm/SynqComm.Infrastructure/DependencyInjection.cs`

### API Layer
- `apps/services/synqcomm/SynqComm.Api/SynqComm.Api.csproj`
- `apps/services/synqcomm/SynqComm.Api/Program.cs`
- `apps/services/synqcomm/SynqComm.Api/DesignTimeDbContextFactory.cs`
- `apps/services/synqcomm/SynqComm.Api/Endpoints/ConversationEndpoints.cs`
- `apps/services/synqcomm/SynqComm.Api/Endpoints/MessageEndpoints.cs`
- `apps/services/synqcomm/SynqComm.Api/Endpoints/ParticipantEndpoints.cs`
- `apps/services/synqcomm/SynqComm.Api/Middleware/ExceptionHandlingMiddleware.cs`
- `apps/services/synqcomm/SynqComm.Api/appsettings.json`
- `apps/services/synqcomm/SynqComm.Api/Properties/launchSettings.json`

### Test Project
- `apps/services/synqcomm/SynqComm.Tests/SynqComm.Tests.csproj`

## Files Updated
- `LegalSynq.sln` — 5 new projects added (Domain, Application, Infrastructure, Api, Tests)
- `apps/gateway/Gateway.Api/appsettings.json` — 3 routes (health, info, protected) + synqcomm-cluster
- `scripts/run-dev.sh` — SynqComm.Api added to .NET startup block
- `replit.md` — SynqComm service documentation added

## Database Changes
- **Database**: `synqcomm_db` (new, separate from all other service databases)
- **Tables** (created via InitialCreate migration):
  - `comms_Conversations` — primary conversation entity with context linking
  - `comms_Messages` — message storage with channel/direction/visibility
  - `comms_ConversationParticipants` — participant tracking with roles
- **Indexes**:
  - `IX_Conversations_TenantId_Context` (tenantId, contextType, contextId)
  - `IX_Conversations_TenantId_OrgId_Status`
  - `IX_Conversations_TenantId_LastActivity`
  - `IX_Messages_TenantId_ConversationId_SentAt`
  - `IX_Messages_TenantId_ConversationId`
  - `IX_Participants_TenantId_ConversationId_Active`
  - `IX_Participants_TenantId_UserId_Active`

## API Endpoints
| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/synqcomm/conversations?contextType=&contextId=` | List conversations by context |
| GET | `/api/synqcomm/conversations/{id}` | Get conversation by ID |
| POST | `/api/synqcomm/conversations` | Create conversation |
| PATCH | `/api/synqcomm/conversations/{id}/status` | Update conversation status |
| GET | `/api/synqcomm/conversations/{id}/messages` | List messages |
| POST | `/api/synqcomm/conversations/{id}/messages` | Add message |
| GET | `/api/synqcomm/conversations/{id}/participants` | List participants |
| POST | `/api/synqcomm/conversations/{id}/participants` | Add participant |
| DELETE | `/api/synqcomm/conversations/{id}/participants/{participantId}` | Deactivate participant |
| GET | `/health` | Health check (anonymous) |
| GET | `/info` | Service info (anonymous) |
| GET | `/context` | Request context debug (authenticated) |

## Test Results
- Build: All 5 projects compile successfully (0 warnings, 0 errors)
- EF Migration: InitialCreate generated with all 3 tables + 7 indexes

## Issues / Gaps
- Connection string password in appsettings.json uses REPLACE_VIA_SECRET placeholder (requires env secret for runtime)
- No unit tests written yet (test project scaffolded, ready for test implementation)
- No email/notification/document integration (out of scope per spec)
- No UI components (out of scope per spec)

## Next Recommendations
- Set `ConnectionStrings__SynqCommDb` environment secret with actual RDS credentials
- Implement unit tests for service layer (ConversationService, MessageService, ParticipantService)
- Build SynqComm UI components in the Case Detail panel
- Add email channel support (future block)
- Add notification integration for new messages
