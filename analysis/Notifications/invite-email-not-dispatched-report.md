# Invite Email Not Dispatched — Root-Cause Analysis

**Date:** 2026-04-20  
**Ticket:** LS-ID-TNT-007 / LS-NOTIF-CORE-024  
**Status:** Fix implemented

---

## Summary

Tenant invite emails are not reaching recipients in production. Two bugs were
identified and fixed:

1. **Critical — HTML sent as `text/plain`**: The invite email body (full HTML) is
   transmitted to SendGrid under the `text/plain` MIME type with no `text/html`
   part. Most spam filters flag this immediately; recipients who do receive it
   see raw HTML markup rather than a rendered email.

2. **Secondary — 5-second HTTP timeout too short**: `NotificationsEmailClient`
   sets a 5-second HTTP timeout for the call to the Notifications service. In
   production the Notifications service makes a synchronous SendGrid HTTP call
   within that window; if the combined DB write + SendGrid round-trip exceeds 5 s,
   the Identity service gets a `TaskCanceledException`, logs the invite as failed
   (502 to CC BFF → 500 to frontend), and the user never receives an email even
   though the email may eventually be dispatched by Notifications.

---

## Trace

```
InviteUser (AdminEndpoints.cs:3669)
  └─ emailClient.SendInviteEmailAsync(...)
       └─ NotificationsEmailClient.SubmitAsync(...)
            ├─ BaseUrl check (NotificationsService:BaseUrl must be non-empty)
            ├─ POST http://localhost:5008/v1/notifications        ← 5 s timeout
            └─ Result: status must be "sent" to return Success=true

Notifications.Api POST /v1/notifications
  └─ DispatchSingleAsync(...)
       └─ ExecuteSendLoopAsync(...)
            ├─ _routingService.ResolveRoutesAsync → [platform:sendgrid, platform:smtp]
            ├─ msg.TryGetProperty("html",...) → null (bug: field is "body" not "html")
            ├─ _sendGridAdapter.SendAsync({ Body=<full HTML>, Html=null })
            └─ SendGrid: content=[{type:"text/plain", value:"<!DOCTYPE html>..."}]
                                                         ^^^ WRONG MIME TYPE
```

---

## Bug 1 — HTML as `text/plain`

### Root cause

`NotificationsEmailClient.SendInviteEmailAsync` constructs:

```csharp
var body = new
{
    type    = InviteEventKey,
    subject = InviteSubject,
    body    = BuildInviteHtmlBody(displayName, activationLink),   // HTML in "body" key
};
```

`ExecuteSendLoopAsync` extracts:

```csharp
body = msg.TryGetProperty("body", out var b) ? b.GetString() : "";   // gets HTML
html = msg.TryGetProperty("html", out var h) ? h.GetString() : null; // null — key absent
```

`SendGridAdapter.SendAsync` then sends:

```csharp
content = [
  { type: "text/plain", value: "<full HTML string>" },
  // text/html entry omitted because Html == null
]
```

### Fix

Change the message object to use `"html"` for the rendered HTML and `"body"` for
a plain-text fallback. `ExecuteSendLoopAsync` already extracts both keys; no
changes required there or in the adapter.

---

## Bug 2 — 5-second timeout

### Root cause

`NotificationsServiceOptions.TimeoutSeconds` defaults to 5 s. The synchronous
send path (Notifications → DB write → SendGrid HTTP call) can exceed 5 s under
load, causing `TaskCanceledException` in the Identity client before the
Notifications service finishes.

### Fix

Raise the default to 30 s. The SendGrid adapter already imposes its own 10 s
timeout on the outbound HTTP call, so the total latency is bounded.

---

## Files changed

- `apps/services/identity/Identity.Infrastructure/Services/NotificationsEmailClient.cs`
  — bug 1 + bug 2
