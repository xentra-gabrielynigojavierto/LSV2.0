# Commit Summary — BLK-OBS-01

## Commit ID
`e269ca2a32f5e1ad3e36ac0a0d8f0b7095f84219`

## Commit Message
`[BLK-OBS-01] Observability & Audit Enforcement — correlation ID, structured logging, audit events`

## Files Changed

### New files
- `apps/services/careconnect/CareConnect.Api/Middleware/CorrelationIdMiddleware.cs`
- `analysis/BLK-OBS-01-report.md`
- `analysis/BLK-OBS-01-commit-summary.md`

### Modified files
- `apps/gateway/Gateway.Api/Program.cs`
- `apps/services/careconnect/CareConnect.Api/Program.cs`
- `apps/services/careconnect/CareConnect.Api/Middleware/ExceptionHandlingMiddleware.cs`
- `apps/services/careconnect/CareConnect.Api/Endpoints/PublicNetworkEndpoints.cs`
- `apps/services/careconnect/CareConnect.Api/Endpoints/InternalProvisionEndpoints.cs`
- `apps/services/careconnect/CareConnect.Api/Endpoints/ProviderAdminEndpoints.cs`
- `apps/services/careconnect/CareConnect.Application/Services/ActivationRequestService.cs`

## Key Changes

### Correlation / Trace Enforcement
- Added inline `X-Correlation-Id` assignment middleware to **Gateway** (`Program.cs`) — assigns at edge, echoes in response
- Added `CorrelationIdMiddleware.cs` to **CareConnect** — reads `X-Correlation-Id`, sanitises, falls back to new GUID, stores in `Items["CorrelationId"]`, echoes in response header
- Registered `CorrelationIdMiddleware` in CareConnect `Program.cs` before `ExceptionHandlingMiddleware`

### Tenant-Aware Structured Logging
- **`ExceptionHandlingMiddleware`** (CareConnect): all branches now log `RequestId`, `Path`, and where available `UserId`; previously silent `ValidationException` (400), `NotFoundException` (404), `ConflictException` (409), `BadHttpRequestException` (400) now emit `Warning` log entries
- **`PublicNetworkEndpoints.ValidateTrustBoundaryAndResolveTenantId`**: all five trust-boundary rejection paths now include `RequestId` in structured log entries
- **`InternalProvisionEndpoints.ProvisionProvider`**: new `LogInformation` entries for provider created and reactivated, including `ProviderId`, `TenantId`, `OrgId`, `RequestId`

### Audit Event Coverage
- **`InternalProvisionEndpoints`**: emits `careconnect.provider.provisioned` audit event on provider creation and reactivation (previously emitted no audit record)
- **`ProviderAdminEndpoints.ActivateForCareConnectAsync`**: emits `careconnect.provider.activated` audit event (previously emitted no audit record)
- **`ProviderAdminEndpoints.LinkOrganizationAsync`**: emits `careconnect.provider.org-linked` audit event (previously emitted no audit record)
- **`ActivationRequestService.EmitApprovalAuditAsync`**: `CorrelationId` field now populated from `X-Correlation-Id` header, not just `TraceIdentifier`

### Security-Event Visibility
- All public trust-boundary denial log entries now include `RequestId` for cross-service traceability
- All exception handler branches now include `RequestId` so security denials (403, 401) can be correlated with request traces
