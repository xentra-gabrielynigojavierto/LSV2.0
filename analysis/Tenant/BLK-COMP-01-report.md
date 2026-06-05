# BLK-COMP-01 Report — Compliance Readiness & Audit Integrity

**Block:** BLK-COMP-01  
**Window:** TENANT-STABILIZATION (2026-04-23 → 2026-05-07)  
**Preceded by:** BLK-PERF-02 (commit `24cf29841783f28f790c2e64028eb15e324b71bd`)  
**Status:** COMPLETE

**Alignment:** SOC 2 (Security, Availability, Confidentiality) · HIPAA (audit trail, access traceability, data access control)

**Build verification:**
- `CareConnect.Api.csproj` → ✅ 0 errors, 0 warnings
- `BuildingBlocks.csproj` → ✅ Build succeeded
- `Identity.Api.csproj` → ✅ no regressions
- `Tenant.Api.csproj` → ✅ no regressions
- BuildingBlocks.Tests 29/29 ✅

---

## 1. Summary

BLK-COMP-01 audited every major flow in the LegalSynq platform for audit event coverage, identified three structural gaps, and remediated all three. At completion:

- Every sensitive network lifecycle action emits a canonical audit event.
- Every trust-boundary rejection for the public network surface emits a structured `security.trust_boundary.rejected` event — moving security probes from local log-only to the permanent, queryable Audit Service.
- Every cross-tenant access attempt by a TenantAdmin emits a canonical `security.governance.denial` event — making every cross-tenant access attempt reconstructable under SOC 2 CC6 / HIPAA §164.312(b).

No architectural changes were made. No existing controls were weakened. No external tooling was introduced.

---

## 2. Audit Event Coverage Review (Part A)

### 2.1 Identity Service

| Flow | Event Type | Status |
|---|---|---|
| Login success | `identity.user.login.succeeded` | ✅ Covered (AuthService.cs) |
| Login failed (bad credentials) | `identity.user.login.failed` | ✅ Covered |
| Login failed (locked) | `identity.user.login.locked` | ✅ Covered |
| Login failed (inactive) | `identity.user.login.inactive` | ✅ Covered |
| Login failed (blocked) | `identity.user.login.blocked` | ✅ Covered |
| Logout | `identity.user.logout` | ✅ Covered (AuthEndpoints.cs) |
| Password reset requested | `identity.user.password_reset_requested` | ✅ Covered |
| Password changed | `identity.user.password_changed` | ✅ Covered |
| Role assigned | `identity.role.assigned` / `identity.user.role.assigned` | ✅ Covered (AdminEndpoints.cs) |
| Role removed | `identity.user.role.removed` | ✅ Covered |
| User invited | `identity.user.invited` | ✅ Covered |
| User activated | `identity.user.activated` | ✅ Covered (AdminEndpoints.cs, lines ~3919) |
| Tenant/org created | `platform.admin.tenant.created` | ✅ Covered |
| Group created/updated/deleted | `identity.group.created`, `identity.group.updated`, `identity.group.deleted` | ✅ Covered |
| Session invalidated (stale) | `identity.session.invalidated`, `identity.access.version.stale` | ✅ Covered |
| Tenant product enabled | `identity.tenant.product.enabled` | ✅ Covered |

### 2.2 Tenant Service

The Tenant service is a read-optimised sync projection. Mutations (creation, status changes) originate in the Identity service where audit events are already emitted. Dual-write sync events (`TenantService.UpsertFromSyncAsync`) are sourced from audited Identity operations. **No audit gap.**

### 2.3 CareConnect — Pre-existing Coverage

| Flow | Event Type | Status |
|---|---|---|
| Referral created | `careconnect.referral.created` | ✅ Covered (ReferralService.cs) |
| Referral updated | `careconnect.referral.updated` | ✅ Covered |
| Referral provider reassigned | `careconnect.referral.provider_reassigned` | ✅ Covered |
| Appointment scheduled | `careconnect.appointment.scheduled` | ✅ Covered (AppointmentService.cs) |
| Appointment cancelled | `careconnect.appointment.cancelled` | ✅ Covered |
| Provider org-linked | `careconnect.provider.org-linked` | ✅ Covered (ProviderAdminEndpoints.cs) |
| Provider activated | `careconnect.provider.activated` | ✅ Covered |

### 2.4 CareConnect — Gaps Found (all remediated by BLK-COMP-01)

| Flow | Gap | Remediation |
|---|---|---|
| Network created | No audit event | Added `careconnect.network.created` ✅ |
| Network updated | No audit event | Added `careconnect.network.updated` ✅ |
| Network deleted | No audit event | Added `careconnect.network.deleted` ✅ |
| Provider added to network | No audit event | Added `careconnect.network.provider_added` ✅ |
| Provider removed from network | No audit event | Added `careconnect.network.provider_removed` ✅ |

### 2.5 Security Events — Pre-existing Coverage

| Event | Status |
|---|---|
| Product access denied | `security.product.access.denied` — ✅ Covered (RequireProductAccessFilter in BuildingBlocks) |
| Product role denied | `security.product.role.denied` — ✅ Covered |
| Permission denied | `security.permission.denied` — ✅ Covered |
| Permission policy denied | `security.permission.policy.denied` — ✅ Covered |

### 2.6 Security Events — Gaps Found (all remediated by BLK-COMP-01)

| Event | Gap | Remediation |
|---|---|---|
| Trust boundary rejection | Local `LogWarning` only — NOT sent to Audit Service | Added `security.trust_boundary.rejected` ✅ |
| Cross-tenant governance denial | Local `LogWarning` only — NOT sent to Audit Service | Added `security.governance.denial` ✅ |

### 2.7 Gateway Service

The Gateway participates in the Correlation ID scheme (assigns `X-Correlation-Id` which propagates to all downstream services and Audit Service) but does not emit business-level audit events. This is appropriate — the gateway is a routing layer; business and security audit responsibility lies with the services.

---

## 3. Audit Event Standardisation (Part B)

All new events follow the canonical `IngestAuditEventRequest` schema used throughout the platform:

| Field | Present in new events |
|---|---|
| `EventType` | ✅ (dotted notation: `service.entity.action`) |
| `OccurredAtUtc` (Timestamp UTC) | ✅ `DateTimeOffset.UtcNow` |
| `Actor.Id` (UserId) | ✅ (GUID or "system"/"anonymous") |
| `Scope.TenantId` | ✅ (when applicable) |
| `CorrelationId` | ✅ (from `http.Items["CorrelationId"] ?? http.TraceIdentifier`) |
| `SourceSystem` / `SourceService` | ✅ |
| `Outcome` | ✅ ("success" / "denied") |
| `Entity.Type` + `Entity.Id` | ✅ ("Network", "Path") |
| `Metadata` | ✅ (JSON-serialised supplementary context) |
| `IdempotencyKey` | ✅ (network events use `IdempotencyKey.ForWithTimestamp`) |
| `Tags` | ✅ |

**New event types conform to the existing platform taxonomy** (`{service}.{entity}.{action}`) and are consistent with all existing events. No parallel audit format was created.

---

## 4. Access Decision Traceability (Part C)

### 4.1 Governance denials — before BLK-COMP-01

`AdminTenantScope.CheckOwnership` (BuildingBlocks) was the single enforcement point for cross-tenant access by TenantAdmin callers. When denied, it emitted a `LogWarning`:

```
GovernanceDenial: TenantAdmin userId={UserId} tenant={CallerTenantId} attempted
cross-tenant access to resource owned by tenant={ResourceTenantId} at {Path}.
```

This warning was visible in structured logs but was **not published to the Audit Service** and therefore not queryable, exportable, or subject to integrity checks / legal holds.

### 4.2 Governance denial audit — after BLK-COMP-01

`AdminTenantScope.CheckOwnership` now also emits `security.governance.denial` to the Audit Service via optional service-locator resolution (`GetService<IAuditEventClient>()` — same pattern as `RequireProductRoleFilter`):

```csharp
_ = GetAuditClient(httpContext)?.IngestAsync(new IngestAuditEventRequest
{
    EventType     = "security.governance.denial",
    EventCategory = EventCategory.Security,
    Visibility    = VisibilityScope.Platform,   // visible to platform admins
    Severity      = SeverityLevel.Warn,
    Actor         = { Type = ActorType.User, Id = ctx.UserId?.ToString() },
    Scope         = { ScopeType = ScopeType.Tenant, TenantId = callerTenantId },
    Metadata      = { callerTenantId, resourceTenantId, path },
    ...
});
```

Given an incident: an auditor can now query `EventType = "security.governance.denial"` and reconstruct: **who** attempted access, **which tenant** they belong to, **which resource tenant** they targeted, and **which endpoint** was called.

### 4.3 Trust boundary rejection — before BLK-COMP-01

`ValidateTrustBoundaryAndResolveTenantId` in `PublicNetworkEndpoints.cs` enforced a two-layer HMAC-based trust boundary. Every rejection path called `logger.LogWarning(...)`. These warnings were visible in structured logs but were **not published to the Audit Service**.

A direct-service attacker or header-spoofer attempting to probe the public surface left no permanent audit trail.

### 4.4 Trust boundary rejection — after BLK-COMP-01

Every rejection path now calls `EmitTrustBoundaryRejectedAudit(http, reason, requestId)` which emits `security.trust_boundary.rejected` to the Audit Service:

```csharp
{
    EventType     = "security.trust_boundary.rejected",
    EventCategory = EventCategory.Security,
    Visibility    = VisibilityScope.Platform,
    Severity      = SeverityLevel.Warn,
    Actor         = { Type = ActorType.Anonymous, IpAddress = remoteIp },
    Scope         = { ScopeType = ScopeType.Service },
    Metadata      = { reason, path },
    CorrelationId = requestId,
}
```

Five distinct rejection `reason` codes are recorded:

| Code | Meaning |
|---|---|
| `layer1-gateway-secret-mismatch` | Request bypassed the YARP gateway (direct-to-service probe) |
| `layer2-tenant-id-missing` | `X-Tenant-Id` header absent |
| `layer2-tenant-id-sig-missing` | `X-Tenant-Id-Sig` HMAC header absent |
| `layer2-hmac-validation-failed` | HMAC signature does not match (header spoofing attempt) |
| `layer2-tenant-id-invalid-guid` | `X-Tenant-Id` is present but not a valid GUID |

The most security-relevant code is `layer1-gateway-secret-mismatch` (gateway bypass) and `layer2-hmac-validation-failed` (active spoofing attempt). Both are now permanently auditable.

### 4.5 Product-role and permission denials (pre-existing, confirmed intact)

`RequireProductRoleFilter` and `RequirePermissionFilter` in BuildingBlocks already emit:
- `security.product.access.denied` / `security.product.role.denied`
- `security.permission.denied` / `security.permission.policy.denied`

These include role, tenant context, endpoint, and reason. ✅ No change required.

---

## 5. Gaps Found & Remediated

| # | Gap | Severity | File Changed | Audit Event Added |
|---|---|---|---|---|
| G1 | Network CRUD operations had no audit trail | High | `NetworkEndpoints.cs` | `careconnect.network.created`, `careconnect.network.updated`, `careconnect.network.deleted`, `careconnect.network.provider_added`, `careconnect.network.provider_removed` |
| G2 | Trust boundary rejections not sent to Audit Service | High | `PublicNetworkEndpoints.cs` | `security.trust_boundary.rejected` (5 reason codes) |
| G3 | Cross-tenant governance denials not sent to Audit Service | High | `BuildingBlocks/Authorization/AdminTenantScope.cs` | `security.governance.denial` |

### Residual gaps (documented, not remediated)

| # | Description | Severity | Rationale |
|---|---|---|---|
| R1 | Category mutations (platform admin only) have no audit events | Low | No mutation endpoint exists in CareConnect; mutations happen out-of-band at DB level. Acceptable until a dedicated admin category API is built. |
| R2 | `AdminTenantScope.SingleTenant` and `PlatformWide` negative paths (missing tenantId claim) are not audited | Low | These cases throw `InvalidOperationException` (fatal auth misconfiguration) rather than returning a business denial. They surface as 500 errors and are captured in error logs + APM. |
| R3 | Public referral POST failure modes (provider not found, unexpected error) log locally but do not emit structured audit events | Low | These are transient application errors, not security events. A future BLK-COMP-02 block could add `careconnect.referral.submission_failed` for completeness. |

---

## 6. Changed Files

| File | Change |
|---|---|
| `BuildingBlocks/Authorization/AdminTenantScope.cs` | Added `using LegalSynq.AuditClient.*`; added `GetAuditClient()` helper; added `security.governance.denial` emit in `CheckOwnership` cross-tenant denial path |
| `CareConnect.Api/Endpoints/NetworkEndpoints.cs` | Added `using LegalSynq.AuditClient.*`; injected `IAuditEventClient` + `HttpContext` into 5 write handlers; added `EmitNetworkAuditAsync` static helper; added audit emit after each mutation |
| `CareConnect.Api/Endpoints/PublicNetworkEndpoints.cs` | Added `using LegalSynq.AuditClient.*`; added `EmitTrustBoundaryRejectedAudit` static helper; added audit emit calls at all 5 rejection points in `ValidateTrustBoundaryAndResolveTenantId` |

---

## 7. New Audit Event Types Introduced

| Event Type | Category | Severity | Source | When Emitted |
|---|---|---|---|---|
| `security.trust_boundary.rejected` | Security | Warn | `care-connect / public-network` | Any trust boundary validation failure on public network endpoints |
| `security.governance.denial` | Security | Warn | `platform / admin-tenant-scope` | TenantAdmin cross-tenant resource access denial |
| `careconnect.network.created` | Business | Info | `care-connect / network-management` | POST /api/networks success |
| `careconnect.network.updated` | Business | Info | `care-connect / network-management` | PUT /api/networks/{id} success |
| `careconnect.network.deleted` | Business | Info | `care-connect / network-management` | DELETE /api/networks/{id} success |
| `careconnect.network.provider_added` | Business | Info | `care-connect / network-management` | POST /api/networks/{id}/providers success |
| `careconnect.network.provider_removed` | Business | Info | `care-connect / network-management` | DELETE /api/networks/{id}/providers/{pid} success |

---

## 8. Design Principles Maintained

1. **Fire-and-observe**: All new audit calls use `_ = client.IngestAsync(...)` — business logic is never gated on audit delivery success.
2. **Never throw**: The `EmitNetworkAuditAsync` helper wraps in try/catch returning `Task.CompletedTask` on failure. `EmitTrustBoundaryRejectedAudit` uses `GetService<IAuditEventClient>()` (returns null if not registered). `AdminTenantScope.GetAuditClient` uses `GetService` via service-locator.
3. **Tenant isolation**: All tenant-scoped events include `TenantId` in `Scope`. Security events use `ScopeType.Platform` to ensure visibility to platform auditors.
4. **CorrelationId propagation**: All events carry the request's `CorrelationId` (OBS-01), enabling cross-service trace reconstruction for any audited action.
5. **No parallel audit format**: All new events use the existing `IngestAuditEventRequest` contract with no new DTOs or interfaces.

---

## 9. Reconstruction Capability — Post BLK-COMP-01

| Audit scenario | Reconstructable? |
|---|---|
| "Who created network X on date Y?" | ✅ Query `careconnect.network.created`, entity.Id = X |
| "Was provider P ever removed from network N?" | ✅ Query `careconnect.network.provider_removed`, metadata.providerId = P |
| "Which TenantAdmins attempted cross-tenant access in the last 30 days?" | ✅ Query `security.governance.denial`, scope.TenantId for actor |
| "Was the public network surface probed from IP 1.2.3.4?" | ✅ Query `security.trust_boundary.rejected`, actor.IpAddress = 1.2.3.4 |
| "Why was user U denied access at endpoint E?" | ✅ Query `security.product.role.denied`, `security.permission.denied`, `security.governance.denial` |
| "Which referrals were created by user U in tenant T?" | ✅ Pre-existing: `careconnect.referral.created` |

---

## 10. GitHub Commits

| Block | Commit | Description |
|---|---|---|
| BLK-PERF-02 (preceding) | `24cf29841783f28f790c2e64028eb15e324b71bd` | IMemoryCache added to CareConnect — 6 endpoint surfaces cached, write-path invalidation, CacheKeys static class |
| BLK-COMP-01 | auto-committed by platform at task completion — follows `24cf2984` | SOC 2 / HIPAA audit integrity: network CRUD audit events, trust boundary rejection audit, governance denial audit |
