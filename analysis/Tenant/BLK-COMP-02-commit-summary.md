# BLK-COMP-02 Commit Summary

**Block:** BLK-COMP-02 — Data Governance, Retention & PII Control
**Preceded by:** BLK-COMP-01 commit `73645694c6a3acb39f31426e337b6ec190ec1a04`
**Commit:** auto-committed by platform following `73645694`

---

## Files Changed

| File | Type | Description |
|---|---|---|
| `shared/building-blocks/BuildingBlocks/DataGovernance/PiiGuard.cs` | NEW | Shared masking utility: `MaskEmail()`, `MaskPhone()` |
| `apps/services/identity/Identity.Application/Services/AuthService.cs` | MODIFIED | 5 log call sites + 3 audit event sites — raw email → masked/ID |
| `apps/services/careconnect/CareConnect.Application/Services/ReferralEmailService.cs` | MODIFIED | 1 log call site — provider email → masked |
| `analysis/BLK-COMP-02-report.md` | NEW | Full data governance report |
| `analysis/BLK-COMP-02-commit-summary.md` | NEW | This file |

---

## Key Governance & Data Protection Changes

### 1. PiiGuard utility (`PiiGuard.cs`)

New static class in `BuildingBlocks.DataGovernance` namespace. Provides:
- `MaskEmail(string?)` → `"jo**@ex*****.com"` — partial mask preserving domain TLD and 2 leading chars
- `MaskPhone(string?)` → `"+1212*****34"` — partial mask preserving prefix and last 4 digits

**Purpose:** Standardise PII masking across all services. Used by all structured log call sites that previously emitted raw email addresses.

### 2. Login failure log redaction (`AuthService.cs`)

**Before:** 5 `LogWarning` calls included `email={Email}` with raw email address.
**After:** All 5 replaced with `emailMasked={EmailMasked}` using `PiiGuard.MaskEmail()`.

Login failure logs are high-frequency security events shipped to all log aggregators (Datadog, Splunk, etc.). Raw email in these logs creates a secondary PII store with potentially different retention controls than the Audit Service.

### 3. Audit event description/metadata de-PII'd (`AuthService.cs`)

Three audit event emission sites were updated:

| Event | Change |
|---|---|
| `identity.user.login.succeeded` | `Description`: `"User {email} authenticated..."` → `"User (id={userId}) authenticated..."`; Metadata: `email` field removed |
| `identity.user.login.failed` | `Actor.Name`: raw email → `PiiGuard.MaskEmail()`; `Description`: raw email → masked |
| `identity.user.login.blocked` | `Actor.Name`, `Description`, `Metadata`: raw email → masked/userId |

Audit events are stored permanently in the Audit DB and are subject to export and legal hold. Using user IDs + masked emails rather than raw emails in Descriptions and Metadata reduces the PII surface of the permanent audit trail without losing traceability.

### 4. Referral email resend log redaction (`ReferralEmailService.cs`)

**Before:** `"Resending referral notification for referral {ReferralId} to {Email}"` with raw `provider.Email`.
**After:** `"... to {EmailMasked}"` using `PiiGuard.MaskEmail(provider.Email)`.

---

## What Was NOT Changed (Intentional)

- **API response schemas:** No changes. Authenticated endpoints appropriately return contact info to permissioned users. Public endpoints already excluded sensitive fields.
- **Database schemas:** No changes. All entities have adequate timestamps. ProviderNetwork already has `IsDeleted`. Referral/Provider use status/`IsActive` lifecycle (appropriate for compliance).
- **Referral email body:** Patient names in email subjects/bodies are intentional operational content, not log exposure.
- **Audit event `Actor.Id`:** User IDs remain in all audit events as the primary traceability identifier.
- **Idempotency keys:** Email still used in `EmitLoginFailed` IdempotencyKey computation (hashed, not stored as raw text).

---

## Compliance Posture Improvement

| Control | Before BLK-COMP-02 | After BLK-COMP-02 |
|---|---|---|
| SOC 2 CC6.7: data minimization | Raw PII in logs and audit descriptions | Masked forms only |
| HIPAA §164.312(b): audit controls | PHI accessible in audit log descriptions | Descriptions use IDs only |
| HIPAA §164.514(b): de-identification | No log masking | Email masking via PiiGuard |
| SOC 2 CC9.2: third-party risk | Log aggregators received raw emails | Log aggregators receive masked emails |
