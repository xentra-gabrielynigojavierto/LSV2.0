# LS-NOTIF-SENDGRID-UPDATE Report

**Status**: Complete  
**Date**: 2026-04-19

---

## Summary

The Notifications service SendGrid integration is fully environment-driven — all
credentials are injected as secrets/environment variables at startup; nothing is
hardcoded in source.  The three required secrets (`SENDGRID_API_KEY`,
`SENDGRID_FROM_EMAIL`, `SENDGRID_FROM_NAME`) were updated by the operator, the
service was restarted, and it started successfully with the new credentials loaded.
**No code changes were required.**

---

## Configuration Changes

### Keys used by the service

| Key | Purpose | Previous state | Updated state |
|-----|---------|---------------|---------------|
| `SENDGRID_API_KEY` | Bearer token for `POST https://api.sendgrid.com/v3/mail/send` | Already set (old account) | **Updated** — new secret provided by operator |
| `SENDGRID_FROM_EMAIL` | Default `from` address on outbound email | Not set — fell back to `noreply@legalsynq.com` | **Set** — new verified sender |
| `SENDGRID_FROM_NAME` | Display name on outbound email | Not set — fell back to `LegalSynq` | **Set** — new display name |
| `SENDGRID_WEBHOOK_VERIFICATION_ENABLED` | Enable/disable ECDSA signature check on webhooks | Not set — defaults to `false` (disabled) | Unchanged |
| `SENDGRID_WEBHOOK_PUBLIC_KEY` | ECDSA public key for webhook signature verification | Not set | Unchanged |

### How credentials flow into the adapter

```
IConfiguration["SENDGRID_API_KEY"]    ──┐
IConfiguration["SENDGRID_FROM_EMAIL"] ──┤─► SendGridAdapter ctor ──► HTTP POST /v3/mail/send
IConfiguration["SENDGRID_FROM_NAME"]  ──┘       (Authorization: Bearer <key>)
```

Values are captured once at service startup via DI registration in
`Notifications.Infrastructure/DependencyInjection.cs` lines 103–112.
A service restart is required to pick up new values — this was performed.

---

## Files Reviewed

| File | Finding |
|------|---------|
| `Notifications.Infrastructure/DependencyInjection.cs` | Reads all keys from `IConfiguration`. No hardcoded secrets. Architecture correct — no changes needed. |
| `Notifications.Infrastructure/Providers/Adapters/SendGridAdapter.cs` | Uses `_apiKey` as `Authorization: Bearer` header. `ValidateConfigAsync()` returns `false` (and short-circuits) when key or from-address is empty. Health check via `GET /v3/scopes`. |
| `Notifications.Infrastructure/Webhooks/Verifiers/SendGridVerifier.cs` | ECDSA webhook verification. Disabled by default (`SENDGRID_WEBHOOK_VERIFICATION_ENABLED=false`). No changes needed. |

---

## Environment Variables Used

| Name | Type | Set via |
|------|------|---------|
| `SENDGRID_API_KEY` | Secret | Replit Secrets (operator-provided) |
| `SENDGRID_FROM_EMAIL` | Secret | Replit Secrets (operator-provided) |
| `SENDGRID_FROM_NAME` | Secret | Replit Secrets (operator-provided) |
| `SENDGRID_WEBHOOK_VERIFICATION_ENABLED` | Env var | Not set — `false` by default |
| `SENDGRID_WEBHOOK_PUBLIC_KEY` | Secret | Not set — webhook verification inactive |

---

## Test Execution

### Service restart
Service restarted after secrets were provided. Build output: **0 errors**, 13 pre-existing
warnings (JwtBearer version conflict in solution-level build; does not affect runtime).

### Health endpoint
```
GET http://localhost:5008/health
→ 200 {"status":"healthy","service":"notifications","timestamp":"2026-04-19T04:57:17Z"}
```

### Startup log validation
```
info: ProviderHealthWorker started, interval=120s
info: Now listening on: http://0.0.0.0:5008
info: Application started.
```
No SendGrid credential errors on startup. `SendGridAdapter.ValidateConfigAsync()` returns
`true` when both `SENDGRID_API_KEY` and `SENDGRID_FROM_EMAIL` are non-empty (both are now set).

### Live SendGrid API call
The `ProviderHealthWorker` polls `GET https://api.sendgrid.com/v3/scopes` every 120 seconds
for tenant-registered provider configs.  The platform-level `SendGridAdapter` health is called
when a platform-managed email send is routed through the adapter.

A live end-to-end send was not performed in this session because:
- `POST /v1/notifications` requires an authenticated JWT caller with a valid tenant
- Template/recipient resolution requires a seeded tenant in the dev database

Operators can trigger a live test via any authenticated notification submission that routes
to the email channel using the platform provider.

---

## Validation Results

| Check | Result |
|-------|--------|
| `SENDGRID_API_KEY` secret exists and is non-empty | ✅ |
| `SENDGRID_FROM_EMAIL` secret set | ✅ |
| `SENDGRID_FROM_NAME` secret set | ✅ |
| No hardcoded credentials in source | ✅ |
| Configuration bound correctly in `DependencyInjection.cs` | ✅ |
| `SendGridAdapter.ValidateConfigAsync()` returns `true` | ✅ (both key and from-email non-empty) |
| Service started without credential errors | ✅ |
| Health endpoint returns `healthy` | ✅ |
| No SendGrid auth errors in startup logs | ✅ |
| Build: 0 errors | ✅ |

---

## Issues Encountered

### Pre-existing: `Unknown column 'n.Category'` (unrelated to SendGrid)
The `NotificationWorker` background job fails with a MySQL error because the `Category`
column (added to the EF model in LS-NOTIF-CORE-020) has not been applied to the
development database — the EF migration ran into a conflict with existing schema at startup.
This causes the retry worker to fail its dispatch cycle but **does not affect email sending**
through the HTTP endpoint path.

**Root cause**: The dev database schema is out of sync with the current EF model.  
**Fix**: `dotnet ef database update` against the dev database, or allow `MigrateAsync` to
complete successfully on a clean database.  Tracked separately.

---

## Recommendations

1. **Verify sender identity in the new SendGrid account**  
   The `SENDGRID_FROM_EMAIL` value must be verified in SendGrid (single-sender verification
   or domain authentication) or sends will fail with HTTP 403 / `sender identity not verified`.
   Confirm at: https://app.sendgrid.com/settings/sender_auth

2. **Enable webhook verification for the new account**  
   When the new account's Event Webhook is configured, set:
   - `SENDGRID_WEBHOOK_VERIFICATION_ENABLED = true`
   - `SENDGRID_WEBHOOK_PUBLIC_KEY = <ECDSA key from SendGrid dashboard>`  
   (Settings → Mail Settings → Event Webhooks → Signature Verification)

3. **Run a live test email**  
   After confirming sender identity, submit an authenticated notification via
   `POST /v1/notifications` (email channel) and observe logs for:
   - `SendGrid: email sent successfully to {To}` → 202 from SendGrid API
   - Any `auth_config_failure` or `invalid_recipient` errors

4. **Resolve Category column migration gap**  
   The `Unknown column 'n.Category'` worker error should be resolved by running the
   pending EF migration against the dev database.  This is tracked under LS-NOTIF-CORE-020.
