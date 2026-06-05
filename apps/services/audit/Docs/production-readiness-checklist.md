# Production Readiness Checklist

**Service:** Platform Audit/Event Service  
**Last updated:** Step 21 hardening pass

Use this checklist before deploying to any non-development environment.
Items are grouped by area. All items must be addressed before a production launch.

---

## Authentication and Authorization

- [ ] **`IngestAuth:Mode`** is NOT `"None"`.  
  Recommended: `ServiceToken` (current production-ready mode).  
  Set via env var: `IngestAuth__Mode=ServiceToken`

- [ ] **`IngestAuth:ServiceTokens`** contains at least one token entry per ingesting service.  
  Each token must be:
  - Generated with `openssl rand -base64 32` or equivalent.
  - Injected via environment variable (never committed to config files).
  - Scoped to exactly one source system.

- [ ] **`IngestAuth:RequireSourceSystemHeader`** is `true` in production to enforce source attribution.

- [ ] **`QueryAuth:Mode`** is NOT `"None"`.  
  Recommended: `Bearer` with a trusted JWT provider.  
  Set via env var: `QueryAuth__Mode=Bearer`

- [ ] **`QueryAuth:TenantIdClaimType`**, `OrganizationIdClaimType`, `UserIdClaimType`, `RoleClaimType`  
  match the JWT claim names issued by your identity provider.

- [ ] **`QueryAuth:EnforceTenantScope`** is `true` so non-PlatformAdmin callers are restricted to their own tenant.

---

## Database

- [ ] **`Database:Provider`** is `MySQL` (not `InMemory`).  
  InMemory storage is volatile and not suitable for any persistent audit log.

- [ ] **Connection string** is provided via:
  - `Database__ConnectionString` env var, OR
  - `ConnectionStrings__AuditEventDb` env var  
  **Never committed to appsettings files.**

- [ ] **`Database:EnableSensitiveDataLogging`** is `false`.  
  Setting this to `true` causes EF Core to log SQL parameter values — including credentials.

- [ ] **`Database:EnableDetailedErrors`** is `false`.  
  Setting this to `true` may surface internal EF Core state in error messages.

- [ ] **`Database:MigrateOnStartup`** is `false` in production unless your deployment pipeline deliberately runs migrations at startup with a single instance.  
  For multi-replica deployments, run migrations as a pre-deploy job instead.

- [ ] **Database user** is granted the minimum required permissions:
  - Tables: `AuditEventRecords`, `IntegrityCheckpoints`, `AuditExportJobs`, `IngestSourceRegistrations`
  - Required: `SELECT`, `INSERT`
  - NOT required: `UPDATE`, `DELETE`, `DROP`, `ALTER`, `CREATE`  
  (The service is append-only; it never updates or deletes audit records.)

---

## Integrity Signing

- [ ] **`Integrity:Algorithm`** is `SHA-256` or `HMAC-SHA256`.  
  Do NOT leave it as `HMAC-SHA256` without also providing `Integrity:HmacKeyBase64`.
  A missing HMAC key silently disables signing (acceptable in dev, not in prod).

- [ ] **`Integrity:HmacKeyBase64`** is set when `Algorithm = "HMAC-SHA256"`.  
  Generate: `openssl rand -base64 32`  
  Inject via env var: `Integrity__HmacKeyBase64=<value>`  
  **Never commit the key to source control.**

- [ ] **`Integrity:VerifyOnRead`** is `true` so tampered records are flagged during query.

- [ ] **`Integrity:FlagTamperedRecords`** is `true`.

---

## Retention

- [ ] **`Retention:DryRun`** is `false` for live retention enforcement.  
  `true` is appropriate for an initial audit run only.

- [ ] **`Retention:LegalHoldEnabled`** is `true` so records under legal hold are never deleted.

- [ ] **`Retention:DefaultRetentionDays`** is set to a non-zero value matching your compliance requirements (e.g. `2555` for 7 years).  
  `0` means indefinite retention — acceptable if intentional.

- [ ] If `Retention:JobEnabled = true`, a scheduler (cron / Quartz.NET) is wired to invoke `RetentionPolicyJob`.

---

## Export

- [ ] If exports are enabled, **`Export:Provider`** is `S3` or `AzureBlob` (not `Local`).  
  `Local` writes to the pod filesystem — data is lost on pod restart.

- [ ] Export bucket/container credentials are injected via environment variables.

---

## Event Forwarding

- [ ] If `EventForwarding:Enabled = true`, a real broker type is configured (`RabbitMq`, `Kafka`, etc.) and the connection string is injected via env var.

---

## Secrets Management

- [ ] No secrets, tokens, passwords, or connection strings are stored in committed config files.

- [ ] Secret rotation procedure is documented and tested (HMAC key, service tokens, DB credentials).

---

## Observability

- [ ] Structured logs are shipped to a log aggregation system (e.g. Datadog, Splunk, OpenSearch, Loki).

- [ ] Alerts are configured for log entries at `Error` or `Fatal` level. The service emits `Log.Error` on startup for critical misconfigurations (auth mode `None`, sensitive data logging enabled).

- [ ] `X-Correlation-ID` is tracked by the API gateway or ingress and propagated to upstream callers for end-to-end request tracing.

- [ ] The `/health` endpoint (ASP.NET Core built-in) is configured as the k8s liveness and readiness probe.

- [ ] The `/health/detail` endpoint is accessible to internal monitoring systems only (network policy or API gateway rule). It returns a live `COUNT(*)` on the audit table — restrict to prevent information disclosure and reduce query load from external probes.

---

## Network and Security

- [ ] The `/internal/audit/*` endpoints are NOT exposed via the public-facing API gateway. They are reserved for internal microservice-to-microservice traffic only.

- [ ] **`AuditService:AllowedCorsOrigins`** is empty (default) unless the service is accessed by browser clients. If CORS is needed, list exact origins — never use `["*"]` in production.

- [ ] **`AuditService:ExposeSwagger`** is `false`. Swagger UI should not be reachable on production hosts.

- [ ] TLS is terminated at the ingress / load balancer. All internal service-to-service traffic uses mutual TLS (mTLS) or a service mesh identity plane.

- [ ] Security response headers are emitted by the service on every response:
  - `X-Content-Type-Options: nosniff`
  - `X-Frame-Options: DENY`
  - `Referrer-Policy: strict-origin-when-cross-origin`
  - `X-XSS-Protection: 0`  
  These are added automatically by `ExceptionMiddleware` pipeline in Step 21.

- [ ] **`AllowedHosts`** in `appsettings.Production.json` should be restricted to known hostnames if the service is directly exposed (without a host-filtering proxy in front).  
  If behind an ingress/load-balancer that enforces host headers, `"*"` is acceptable.

---

## Pre-Deploy Verification Steps

1. Run `dotnet build -c Release` — **0 errors, 0 warnings** required.
2. Run `dotnet test` against the service test project if present.
3. Review startup logs for any `Error` or `Fatal` level entries.
4. Send a test ingest request and verify:
   - `X-Correlation-ID` is echoed back in the response header.
   - `CorrelationId` appears in server-side structured logs.
   - The record appears in `GET /audit/events`.
5. Verify `GET /health` returns HTTP 200.
6. Verify `GET /health/detail` returns service metadata with correct `Version` and `Service`.
7. Verify `GET /swagger` returns HTTP 404 (Swagger must be disabled in production).

---

## Compliance Notes (HIPAA)

- Audit records are append-only. No `UPDATE` or `DELETE` permissions should be granted on the audit table except via a separate, tightly controlled retention job.
- `LegalHoldEnabled = true` ensures records subject to litigation hold are not purged.
- `VerifyOnRead = true` ensures tampered records are detected and flagged.
- Audit records must be retained for a minimum of 6 years per HIPAA §164.530(j).  
  Set `Retention:DefaultRetentionDays = 2190` (6 years) as a minimum baseline.
- For PHI-adjacent event metadata, confirm with your compliance officer whether the audit log itself is considered PHI.
