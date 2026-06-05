# PROD-INVITE-EMAIL-001 — Invite Email Silent Failure

**Status**: Root causes identified; fixes implemented  
**Severity**: High — users see false "Invitation sent" success while no email is dispatched  
**Date**: 2026-04-20

---

## Symptom

Admin invites a user via the "Add User" modal. Frontend shows:
> "Invitation sent to user@example.com."

The user record, role assignment, and invitation token are all created in the identity DB (`idt_Users`, `idt_ScopedRoleAssignments`, `idt_UserInvitations`). However, no email arrives and SendGrid shows zero delivery attempts.

---

## Investigation Trace

### 1. Identity service — DB inserts confirmed

Production log timestamp **1776699887430** shows simultaneous `INSERT INTO`:
- `idt_Users`
- `idt_ScopedRoleAssignments`
- `idt_UserInvitations`

The DB transaction committed successfully. The invite token is persisted.

### 2. Notification HTTP call — succeeds but silently

At timestamp **1776699887482** (52 ms after DB inserts), the **fire-and-forget** audit event call is logged:
```
System.Net.Http.HttpClient.AuditEventClient.LogicalHandler[100]
Start processing HTTP request POST http://localhost:5007/internal/audit/events
```

After this, no further identity service logs appear for **~14 seconds**, consistent with an HTTP call to `localhost:5008/v1/notifications` being made synchronously. The frontend ultimately receives a `201 Created` → shows success toast — confirming the notifications service returned a 2xx status.

### 3. No SendGrid mail.send call logged

The production logs contain **repeated** `GET https://api.sendgrid.com/v3/scopes` (200 OK) from the `ProviderHealthWorker`, confirming the API key is valid and the network can reach SendGrid. However, **zero** `POST https://api.sendgrid.com/v3/mail/send` calls appear anywhere in the production deployment logs — neither immediately after the invite nor in subsequent retry windows.

### 4. Root cause A — Notifications endpoint response contract (confirmed bug)

`NotificationEndpoints.cs`:
```csharp
var result = await service.SubmitAsync(tenantId, request);
return result.Status == "blocked"
    ? Results.Json(result, statusCode: 422)
    : Results.Created($"/v1/notifications/{result.Id}", result);  // ← always 201
```

The endpoint returns **HTTP 201 Created** for every non-`blocked` outcome, including:
- `"sent"` — delivery succeeded  
- `"failed"` — delivery permanently failed  
- `"retrying"` — delivery failed, queued for retry  
- `"dead-letter"` — all retries exhausted  

The response **body** includes `status`, `failureCategory`, and `lastErrorMessage`, but these are never read by the caller.

### 5. Root cause B — Identity client ignores response body (confirmed bug)

`NotificationsEmailClient.SubmitAsync`:
```csharp
if (response.IsSuccessStatusCode)
{
    // Only checks HTTP status — never reads status/failureCategory from body
    return (EmailConfigured: true, Success: true, Error: null);
}
```

The client sees HTTP 201 → reports `Success: true` → invite endpoint returns `201 Created` → frontend shows "Invitation sent" toast — even when delivery status is `"failed"`.

### 6. Why delivery fails — probable cause

The `SendGridAdapter.ValidateConfigAsync()` checks:
```csharp
!string.IsNullOrEmpty(_apiKey) && !string.IsNullOrEmpty(_defaultFromEmail)
```

The `SENDGRID_API_KEY` secret is present and valid (health check confirms). The most likely failure category is one of:
- `auth_config_failure` — FROM email not a verified sender in SendGrid, or  
- `retryable_provider_failure` — transient timeout on `api.sendgrid.com/v3/mail/send`

**This cannot be confirmed directly** until Fix B is deployed: the `failureCategory` and `lastErrorMessage` from the notification response will appear in identity logs on the next invite attempt.

---

## Fixes

### Fix 1 — Identity reads response body status (primary fix)

**File**: `apps/services/identity/Identity.Infrastructure/Services/NotificationsEmailClient.cs`

Updated `SubmitAsync` to read the JSON response body and check the `status` field:
- If `status == "sent"` → success
- Otherwise → logs the `failureCategory` + `lastErrorMessage` at Warning level and returns `Success: false`

The invite endpoint will now properly return **HTTP 502** when delivery fails, and the frontend will show an error rather than a false success.

### Fix 2 — Notifications endpoint diagnostic logging

**File**: `apps/services/notifications/Notifications.Api/Endpoints/NotificationEndpoints.cs`

Added explicit Warning-level log when a submitted notification is not in `"sent"` state. This log will:
- Appear in production deployment logs at Warning level
- Include `NotificationId`, `Status`, `FailureCategory`, and `LastErrorMessage`
- Allow operators to diagnose delivery failures without querying the DB directly

---

## Known limitation not yet fixed

The notifications endpoint still returns HTTP 201 for all non-`blocked` outcomes. This is intentional fire-and-forget semantics for async callers, but breaks synchronous callers that need delivery confirmation. A future task should return HTTP 202 (queued) vs. 201 (sent) to distinguish the two outcomes.

---

## Verification steps after deploy

1. Trigger an invite in production.
2. Check identity logs for:  
   ```
   [LS-ID-TNT-007] Notifications service accepted but delivery failed. Status=... FailureCategory=... Error=...
   ```
3. The specific `failureCategory` value will identify the underlying cause:
   - `auth_config_failure` → verify `SENDGRID_FROM_EMAIL` is a verified sender  
   - `retryable_provider_failure` → transient SendGrid issue, check retry queue  
   - `invalid_recipient` → recipient email rejected by SendGrid
4. Check notifications service logs for:  
   ```
   [NOTIF-WARN] Notification {id} submitted but not sent. Status=... FailureCategory=...
   ```

---

## Related

- `analysis/Prod/PROD-ID-CRASH-001-report.md` — previous identity crash fix
- `apps/services/identity/Identity.Infrastructure/Services/NotificationsEmailClient.cs`
- `apps/services/notifications/Notifications.Api/Endpoints/NotificationEndpoints.cs`
