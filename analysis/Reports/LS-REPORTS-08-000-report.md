# LS-REPORTS-08-000 — Launch Readiness & Platform Integration

## Objective
Replace all mock identity/tenant usage with real platform context and validate the system end-to-end for real user operation.

## Current Issue Summary (Before)
- Frontend pages use hardcoded `MOCK_TENANT_ID = 'tenant-001'` and `MOCK_USER_ID = 'user-001'`
- Backend Reports service has no JWT auth — all endpoints are anonymous
- Identity/Tenant/Entitlement adapters are all mock implementations
- No tenant isolation validation (request body tenantId not checked against JWT)
- Audit events contain mock user/tenant IDs

## Architecture
- **Frontend**: `useSessionContext()` provides real `session.tenantId` / `session.userId` from JWT via BFF
- **Gateway**: YARP validates JWT, passes token through to downstream services
- **Backend**: Reports service receives requests with tenantId/userId in body DTOs
- **Shared**: `BuildingBlocks.Context.CurrentRequestContext` extracts claims from JWT (used by other services)

## Implementation Plan
1. Frontend: Replace mock IDs with real session context (5 pages)
2. Backend: Add JWT authentication to Reports API
3. Backend: Register `ICurrentRequestContext` for JWT claim extraction
4. Backend: Create real identity/tenant/entitlement adapters
5. Backend: Add auth requirements to all endpoints (except health)
6. Backend: Add tenant isolation validation middleware
7. Backend: Role-based authorization policies on admin endpoints
8. Validate gateway context flow
9. Code review + security hardening

---

## Execution Log

| Step | Files Modified | Change | Status |
|------|---------------|--------|--------|
| S1 | 5 UI page files | Replace MOCK_TENANT_ID/MOCK_USER_ID with `useSessionContext()` | COMPLETE |
| S2 | `Reports.Api/Program.cs` | Add JWT auth, authorization policies, register ICurrentRequestContext | COMPLETE |
| S3 | `Reports.Api.csproj`, `Reports.Infrastructure.csproj` | Add BuildingBlocks + JwtBearer references | COMPLETE |
| S4 | 3 new adapter files | Create ClaimsIdentityAdapter, ClaimsTenantAdapter, ClaimsEntitlementAdapter | COMPLETE |
| S5 | `DependencyInjection.cs` | Conditional adapter registration (real vs mock based on Jwt:SigningKey) | COMPLETE |
| S6 | 7 endpoint files | Add `.RequireAuthorization()` to all non-health endpoint groups | COMPLETE |
| S7 | `TemplateEndpoints.cs`, `AssignmentEndpoints.cs` | Admin endpoints require PlatformOrTenantAdmin policy | COMPLETE |
| S8 | `TenantValidationMiddleware.cs` | Validate tenantId in query params AND request bodies against JWT claim | COMPLETE |
| S9 | `appsettings.json` | Add Jwt config section (matching gateway/other services) | COMPLETE |
| S10 | Code review | Addressed security findings: admin policy enforcement, query param isolation | COMPLETE |

---

## Files Modified

### Frontend (5 pages — mock ID replacement)
- `apps/web/src/app/(platform)/insights/reports/reports-catalog-client.tsx`
- `apps/web/src/app/(platform)/insights/reports/[id]/report-viewer-client.tsx`
- `apps/web/src/app/(platform)/insights/reports/[id]/builder/report-builder-client.tsx`
- `apps/web/src/app/(platform)/insights/schedules/schedules-list-client.tsx`
- `apps/web/src/app/(platform)/insights/schedules/[id]/schedule-detail-client.tsx`

### Backend — New files
- `reports/src/Reports.Infrastructure/Adapters/ClaimsIdentityAdapter.cs`
- `reports/src/Reports.Infrastructure/Adapters/ClaimsTenantAdapter.cs`
- `reports/src/Reports.Infrastructure/Adapters/ClaimsEntitlementAdapter.cs`
- `reports/src/Reports.Api/Middleware/TenantValidationMiddleware.cs`

### Backend — Modified files
- `reports/src/Reports.Api/Program.cs`
- `reports/src/Reports.Api/Reports.Api.csproj`
- `reports/src/Reports.Infrastructure/Reports.Infrastructure.csproj`
- `reports/src/Reports.Infrastructure/DependencyInjection.cs`
- `reports/src/Reports.Api/appsettings.json`
- `reports/src/Reports.Api/Endpoints/TemplateEndpoints.cs`
- `reports/src/Reports.Api/Endpoints/AssignmentEndpoints.cs`
- `reports/src/Reports.Api/Endpoints/OverrideEndpoints.cs`
- `reports/src/Reports.Api/Endpoints/ExecutionEndpoints.cs`
- `reports/src/Reports.Api/Endpoints/ExportEndpoints.cs`
- `reports/src/Reports.Api/Endpoints/ScheduleEndpoints.cs`
- `reports/src/Reports.Api/Endpoints/MetricsEndpoints.cs`

---

## Identity Integration Summary
- Frontend: `useSessionContext()` → `session.tenantId` / `session.userId`
- Backend: `ICurrentRequestContext` from JWT claims (`sub` → userId, `tenant_id` → tenantId)
- Adapters: `ClaimsIdentityAdapter` reads user context from JWT via `ICurrentRequestContext`

## Tenant Integration Summary
- Frontend: `session.tenantId` passed in all API request bodies
- Backend: `ClaimsTenantAdapter` resolves tenant from JWT `tenant_id` claim
- Validation: `TenantValidationMiddleware` checks both query params and body tenantId against JWT claim
- Isolation: 403 Forbidden returned for cross-tenant access attempts

## Authorization Enforcement Summary
- All non-health endpoints: `.RequireAuthorization()` (requires authenticated JWT)
- Template management (CRUD): `PlatformOrTenantAdmin` policy
- Assignment management (CRUD): `PlatformOrTenantAdmin` policy
- Tenant catalog, overrides, execution, export, schedules, metrics: authenticated user
- Health/Ready endpoints: `AllowAnonymous()`
- Entitlement adapters: `ClaimsEntitlementAdapter` checks `IsAuthenticated` + has `TenantId`

## Security Controls
1. **JWT validation**: Same signing key, issuer, audience as gateway (defense-in-depth)
2. **Tenant isolation**: Middleware validates tenantId on query params (GET) and body (POST/PUT/PATCH)
3. **Admin protection**: Template/assignment endpoints gated by role policy
4. **Claim-based identity**: Real user/tenant resolved from JWT, not client-supplied
5. **Mock fallback gating**: Real adapters when Jwt:SigningKey configured, mock only for local dev

## API Validation Results
- All `/reports/api/v1/*` endpoints require auth (except health/ready)
- Gateway passes JWT token through to Reports service (YARP `reports-protected` route)
- Reports service validates JWT using same signing key as gateway
- Tenant isolation enforced at middleware level before endpoint execution

## Known Gaps (Future Work)
- `IEntitlementAdapter` does basic auth check; granular per-report feature gating requires entitlement service integration
- Actor IDs in request bodies (RequestedByUserId, CreatedByUserId) are still client-supplied — future: derive from ICurrentRequestContext at service layer
- ID-based lookups (GetExecutionById, GetScheduleById) don't have tenant ownership checks at the application service layer — the tenant isolation middleware covers query/body params but not direct ID access
- Mock adapters preserved for local development when Jwt:SigningKey not configured

## Final Summary
- Zero remaining MOCK_TENANT_ID/MOCK_USER_ID references in frontend
- Reports API now requires JWT authentication (matching all other services)
- Admin endpoints protected by role-based authorization policies
- Tenant isolation enforced at middleware level (query + body validation)
- Real identity/tenant/entitlement adapters resolve from JWT claims
- Config-driven: real adapters when Jwt:SigningKey present, mock fallback for dev
- Both .NET build and TypeScript compilation pass cleanly
