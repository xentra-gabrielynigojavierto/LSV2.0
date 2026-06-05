# Threat Model

## Project Overview

LegalSynq is a multi-tenant platform for legal, healthcare, funding, audit, workflow, document, and notification operations. The production stack is primarily ASP.NET Core 8 microservices behind a YARP gateway (`apps/gateway`), with two Next.js 15 frontends (`apps/web` and `apps/control-center`) acting as browser-facing BFFs. Authentication is JWT-based with HttpOnly session cookies set by the BFFs; authorization is enforced through shared role, permission, product-access, and policy helpers in `shared/building-blocks`.

Production assumptions for this scan:
- Only production-reachable code is in scope.
- `NODE_ENV=production` in deployment.
- Replit provides TLS for browser-to-app transport.
- `_archived/`, tests, docs/analysis-only content, `.next/`, `bin/`, and `obj/` are dev or historical unless a production path explicitly references them.

## Assets

- **User accounts and sessions** — JWTs, session cookies, password-reset tokens, invite tokens, access versions, and session versions. Compromise enables impersonation and privilege misuse.
- **Tenant-scoped business data** — liens, referrals, appointments, funding applications, workflow state, tasks, audit history, notifications, and reports. Cross-tenant disclosure or tampering would be a major platform failure.
- **Sensitive documents and derived access links** — uploaded files, logos, avatars, document metadata, and opaque document access tokens. Exposure may leak regulated or confidential records.
- **Administrative capabilities** — platform-admin and tenant-admin actions in identity, control-center, monitoring, audit, and workflow surfaces. Abuse can affect many tenants at once.
- **Application secrets and internal trust tokens** — JWT signing keys, internal service tokens, notification/provider credentials, database connection strings, and other service-to-service credentials.
- **Outbound integration authority** — privileges to call notifications, documents, flow, audit, and other internal services, plus external providers such as email/SMS infrastructure.

## Trust Boundaries

- **Browser to BFF** — both Next.js apps accept untrusted input from browsers and translate cookies, headers, and request bodies into upstream service calls.
- **Gateway/BFF to microservices** — authenticated user context or internal-service credentials cross from edge components to backend APIs.
- **Service to database** — each service persists tenant and product data. Query scoping and update authorization are critical for tenant isolation.
- **Service to service** — several internal endpoints rely on bearer tokens or shared-secret headers rather than end-user JWTs. These boundaries are high risk because compromise can bypass normal user authorization.
- **Public to authenticated/admin surfaces** — login, invite acceptance, password reset, referral activation/view, public monitoring/status, public logos, and document access-token redemption are intentionally unauthenticated; all other protected operations must enforce authz server-side.
- **Tenant admin to platform admin** — control-center and admin endpoints must preserve strict privilege separation so tenant-scoped actors cannot access platform-wide controls.

## Scan Anchors

- **Production entry points**
  - `apps/web/src/app/api/**`
  - `apps/control-center/src/app/api/**`
  - `apps/gateway/Gateway.Api/Program.cs`
  - `apps/services/*/*Api*/**` and `apps/services/*/Program.cs`
  - `apps/services/flow/backend/src/Flow.Api/**`
- **Highest-risk code areas**
  - `apps/services/identity/**`
  - `apps/services/documents/**`
  - `apps/services/notifications/**`
  - `apps/services/careconnect/**`
  - `apps/services/liens/**`
  - `apps/services/task/**`
  - `apps/services/flow/backend/**`
  - `shared/building-blocks/BuildingBlocks/Authorization/**`
  - `shared/building-blocks/BuildingBlocks/Authentication/**`
- **Public surfaces**
  - Login, invite, password-reset, referral activation/view, health/info endpoints, monitoring/status endpoints, document access redemption, public logos, and third-party webhook endpoints.
- **Usually out of scope unless production-reachable**
  - `_archived/**`, `analysis/**`, `docs/**`, test projects, `.next/**`, `bin/**`, `obj/**`.

## Confirmed Hotspot Anchors

These areas produced real production findings in the current scan and should remain top-priority anchors for future review:
- `apps/web/src/app/api/public/careconnect/[...path]/route.ts`
- `apps/services/careconnect/CareConnect.Api/Endpoints/PublicNetworkEndpoints.cs`
- `apps/services/identity/Identity.Api/Endpoints/TenantBrandingEndpoints.cs`
- `apps/services/careconnect/CareConnect.Api/Endpoints/ReferralNoteEndpoints.cs`
- `apps/services/careconnect/CareConnect.Api/Endpoints/AppointmentNoteEndpoints.cs`
- `apps/services/careconnect/CareConnect.Api/Endpoints/AttachmentEndpoints.cs`
- `apps/services/careconnect/CareConnect.Api/Endpoints/NotificationEndpoints.cs`
- `apps/services/documents/Documents.Api/Endpoints/PublicLogoEndpoints.cs`
- `apps/services/documents/Documents.Application/Services/{DocumentService,AccessTokenService}.cs`
- `apps/services/documents/Documents.Infrastructure/DependencyInjection.cs`
- `shared/building-blocks/BuildingBlocks/Context/CurrentRequestContext.cs`
- `apps/services/task/Task.Api/Program.cs`
 - `apps/web/src/app/api/auth/login/route.ts`
 - `apps/web/src/app/api/identity/[...path]/route.ts`
 - `apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs`
 - `apps/services/careconnect/CareConnect.Application/Services/ReferralService.cs`
- `apps/services/monitoring/Monitoring.Api/Authentication/AuthenticationServiceCollectionExtensions.cs`
 - `apps/services/monitoring/Monitoring.Api/Endpoints/MonitoringReadEndpoints.cs`
 - `apps/services/monitoring/Monitoring.Api/Endpoints/MonitoringAlertHistoryEndpoints.cs`
 - `apps/services/monitoring/Monitoring.Api/Endpoints/UptimeReadEndpoints.cs`
 - `apps/gateway/Gateway.Api/appsettings.json`
- `apps/services/monitoring/Monitoring.Api/Endpoints/MonitoredEntityEndpoints.cs`
- `apps/services/monitoring/Monitoring.Api/Endpoints/MonitoringAlertEndpoints.cs`
- `apps/control-center/src/app/api/auth/login/route.ts`
- `apps/control-center/src/app/api/monitoring/latency/route.ts`
- `apps/control-center/src/app/api/monitoring/alerts/history/route.ts`
- `apps/control-center/src/app/api/reports/summary/route.ts`
- `apps/services/reports/src/Reports.Application/Overrides/TenantReportOverrideService.cs`
- `apps/services/reports/src/Reports.Application/Scheduling/ReportScheduleService.cs`
- `apps/services/reports/src/Reports.Application/Execution/ReportExecutionService.cs`
- `apps/services/audit/Controllers/AuditEventQueryController.cs`
- `apps/services/audit/Services/AuditEventQueryService.cs`
- `apps/services/audit/Controllers/AuditExportController.cs`
- `apps/services/audit/Services/AuditExportService.cs`

## Deterministic Scan Calibration

The repo generates recurring scanner noise that should be revalidated before proposing future findings:
- `_archived/**` Node/legacy services are out of production scope unless a live production path references them.
- `scripts/**` dev proxy / local tooling findings are out of scope unless the script is part of the deployed runtime.
- Seeded bcrypt hashes in migrations and similar test/demo seed artifacts are not automatically active credential leaks in production.
- React/HTML template heuristics and logging-content heuristics should not be reported unless they reach a real rendering sink, privileged browser context, or materially sensitive disclosure path.
- `apps/services/reports/src/Reports.Api/Middleware/TenantValidationMiddleware.cs` does block mismatched `tenantId` values in authenticated query/body inputs; future reports should avoid claiming raw tenant-parameter spoofing there unless they also prove a path around that middleware. The remaining reports risk is unscoped GUID-based object access.

These notes are calibration guidance, not blanket dismissals. Future scans should still report any instance from these classes when it is production-reachable and exploitable.

## Threat Categories

### Spoofing

The platform must ensure that protected BFF routes, gateway routes, and service endpoints accept only valid authenticated identities or valid internal-service credentials. JWTs must be signed and validated consistently, session invalidation fields (`session_version`, `access_version`) must be enforced, and any webhook or internal-service endpoint that bypasses user JWT auth must require a non-guessable, rotation-friendly secret or equivalent strong authentication.

### Tampering

Untrusted request parameters, headers, bodies, uploaded files, and cross-service callbacks must not let attackers alter tenant data, workflow state, provider records, notification settings, or document metadata outside allowed rules. Business-critical decisions such as tenant resolution, product access, provider provisioning, and workflow transitions must be enforced server-side and must not trust client-only state.

### Information Disclosure

Tenant isolation is a core guarantee: requests for documents, referrals, applications, tasks, workflow instances, notifications, and audit records must be filtered by the authenticated tenant and authorized organization/user context. Public endpoints must expose only intentionally public data, and logs, error messages, and API responses must not leak secrets, internal topology, or other tenants' records.

### Denial of Service

Public and authentication-adjacent endpoints must resist brute force and resource exhaustion through rate limits, bounded uploads, input validation, and conservative external call behavior. Document upload/retrieval, monitoring/status surfaces, auth endpoints, and any anonymous webhook or token-redemption path are especially sensitive to abuse if request cost is not bounded.

### Elevation of Privilege

The platform must prevent users or tenants from escalating from ordinary product access to tenant-admin or platform-admin privileges, and must prevent internal-only service endpoints from becoming externally reachable privilege-escalation paths. Every admin, internal, and cross-tenant operation must be guarded by strong server-side authorization, scoped database queries, and secure service-to-service authentication rather than guessable or hardcoded shared secrets.
