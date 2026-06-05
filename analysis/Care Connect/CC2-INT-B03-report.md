# CC2-INT-B03 — CareConnect: Documents & Notifications Integration Report

**Date:** 2026-04-21  
**Author:** Task Agent  
**Status:** COMPLETE

---

## 1. Scope

This work wired CareConnect to two platform services:

| Platform Service | Technology | Config key |
|---|---|---|
| Documents | S3-backed REST API | `DocumentsService:BaseUrl` |
| Notifications | SendGrid-backed REST API | `NotificationsService:BaseUrl` |

---

## 2. Changes Delivered

### 2.1 Documents Service Integration

#### `IDocumentServiceClient` (new interface)
Location: `CareConnect.Application/Interfaces/IDocumentServiceClient.cs`

Defines two operations:
- `UploadAsync` — proxies a file stream to `POST /documents` on the Documents service; required fields: `title`, `productId`, `documentTypeId` (configurable). Returns `DocumentUploadResult(success, documentId?, error?)`.
- `GetSignedUrlAsync` — calls `POST /documents/{id}/view-url` (or `/download-url`); returns `DocumentSignedUrlResult(redeemUrl, expiresInSeconds)`.

#### `DocumentServiceClient` (new implementation)
Location: `CareConnect.Infrastructure/Documents/DocumentServiceClient.cs`

- HTTP client name: `"DocumentsService"`.
- Default base URL: `http://localhost:5006` (overridden via `DocumentsService:BaseUrl`).
- **Auth**: Sends `Authorization: Bearer <token>` when `DocumentsService:ServiceToken` is configured.
- **Required fields**: Sends `title` (file name), `productId` (default `"CareConnect"` or `DocumentsService:ProductId`), and `documentTypeId` (from `DocumentsService:DocumentTypeId`) to satisfy Documents API contract.
- Parses `data.id` from the upload response; parses `data.redeemUrl` / `data.expiresInSeconds` from the signed-URL response.
- Non-2xx and unreachable errors return a failure result rather than throwing — callers decide on exception behaviour.

#### `ReferralAttachmentService` (rewritten)
Location: `CareConnect.Application/Services/ReferralAttachmentService.cs`

- **`UploadAsync`**: proxies bytes through `IDocumentServiceClient.UploadAsync`; stores only `documentId` in `ReferralAttachment.ExternalDocumentId`; stores the **access scope** (`request.Scope` = `"shared"` or `"provider-specific"`) in `ExternalStorageProvider`.
- **`GetSignedUrlAsync`**: enforces scope before calling Documents service:
  - `shared` (default): caller must be a referral participant (referring OR receiving org ID).
  - `provider-specific`: caller must be from the receiving org AND org type must be `PROVIDER` or `LAW_FIRM` (task requirement), or be an admin.
  - Admin (`isAdmin=true`) bypasses all scope checks.
  - `UnauthorizedAccessException` on scope violation.

#### `AppointmentAttachmentService` (rewritten)
Location: `CareConnect.Application/Services/AppointmentAttachmentService.cs`

Same upload-proxy + scope-enforcement contract as `ReferralAttachmentService`, adapted for `Appointment` (uses `ReferringOrganizationId` / `ReceivingOrganizationId`).

`EnforceScope` in `AppointmentAttachmentService`:
- `shared`: caller must be an appointment participant (referring or receiving org) or admin.
- `provider-specific`: caller must be from the receiving org AND have an allowed org type (`PROVIDER` or `LAW_FIRM`) or be an admin.

#### Legacy metadata-only endpoints removed
Location: `CareConnect.Api/Endpoints/AttachmentEndpoints.cs`

`POST /api/referrals/{id}/attachments` and `POST /api/appointments/{id}/attachments` (which persisted attachment metadata without proxying bytes to Documents service) now return **410 Gone** with a migration hint pointing to the `/upload` endpoints.

#### New REST endpoints

| Method | Path | Description |
|---|---|---|
| `POST` | `/referrals/{id}/attachments/upload` | Multipart upload (replaces client-delegated direct S3) |
| `GET`  | `/referrals/{id}/attachments/{attachmentId}/url` | Signed URL retrieval |
| `POST` | `/appointments/{id}/attachments/upload` | Multipart upload for appointments |
| `GET`  | `/appointments/{id}/attachments/{attachmentId}/url` | Signed URL retrieval for appointments |
| `POST` | `/referrals/{id}/attachments` | ❌ 410 Gone — use `/upload` |
| `POST` | `/appointments/{id}/attachments` | ❌ 410 Gone — use `/upload` |

#### DI registration
Location: `CareConnect.Infrastructure/DependencyInjection.cs`

```csharp
services.AddHttpClient("DocumentsService", client =>
{
    client.BaseAddress = new Uri(docsBaseUrl);
    client.Timeout     = TimeSpan.FromSeconds(30);
});
services.AddScoped<IDocumentServiceClient, DocumentServiceClient>();
```

#### Configuration
Location: `CareConnect.Api/appsettings.json`

```json
"DocumentsService": {
  "BaseUrl": "http://localhost:5006",
  "ServiceToken": "",
  "ProductId": "CareConnect",
  "DocumentTypeId": ""
}
```

---

### 2.2 Referral Token HMAC Secret Hardening

Location: `CareConnect.Application/Services/ReferralEmailService.cs`

**Before:** Missing or blank `ReferralToken:Secret` silently fell back to `"DevFallbackSecret"` in all environments.

**After:** Constructor uses `string.IsNullOrWhiteSpace(secret)` — blank/whitespace-only values are treated the same as absent. Throws `InvalidOperationException` when secret is missing/blank AND `ASPNETCORE_ENVIRONMENT != "Development"`. Dev fallback is preserved only for local development.

```csharp
// Throws in Production/Staging when secret is missing or whitespace:
if (string.IsNullOrWhiteSpace(secret))
{
    var env = configuration["ASPNETCORE_ENVIRONMENT"] ?? string.Empty;
    if (!string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException(
            "ReferralToken:Secret must be configured in non-Development environments.");
    secret = "DevFallbackSecret";
}
```

---

### 2.3 Notifications Service Coverage

Location: `CareConnect.Application/Services/ReferralEmailService.cs`

#### New notification type: `ReferralProviderAssigned`
- Constant: `NotificationType.ReferralProviderAssigned = "ReferralProviderAssigned"`
- Event key mapping: `"referral.provider_assigned"`
- Method: `SendProviderAssignedNotificationAsync(Referral, Provider, Guid? actingUserId, CancellationToken)`
  - Skips (with warning log) when provider has no email.
  - Deduplicates via `TryAddWithDedupeAsync`.
  - Builds and submits `BuildProviderAssignedEmailHtml` email template.
  - Included in `RetryNotificationAsync` switch case.

#### Wired to service flow
`ReferralService.CreateAsync` now fires `SendProviderAssignedNotificationAsync` alongside `SendNewReferralNotificationAsync` in the background Task.Run block. This gives the `referral.provider_assigned` event key a real trigger on initial provider assignment; the Notifications service routes it independently from `referral.created`.

#### Warning logs added
Silent skips in rejection/cancellation flows now log `LogWarning` instead of silently returning, ensuring monitoring can surface missed notifications.

#### `NotificationTypeToEventKey` map
All known notification types now have canonical event key mappings routed to the platform Notifications service via `INotificationsProducer.SubmitAsync`.

---

## 3. Tests Added

File: `CareConnect.Tests/Application/DocumentsIntegrationTests.cs`  
**32 new tests, all passing.**

| Category | Tests | Result |
|---|---|---|
| Startup config validation: missing secret, non-Dev throws | 3 theory cases | ✅ Pass |
| Startup config validation: whitespace secret, non-Dev throws | 2 theory cases | ✅ Pass |
| Startup config validation: missing secret, Dev allowed | 1 | ✅ Pass |
| Startup config validation: all required set, Production allowed | 1 | ✅ Pass |
| Startup config validation: missing DocumentTypeId, non-Dev throws | 2 theory cases | ✅ Pass |
| Startup config validation: missing DocumentTypeId, Dev allowed | 1 | ✅ Pass |
| Token secret hardening (scoped service, non-Dev throws) | 4 theory cases | ✅ Pass |
| Token secret hardening (Dev fallback works) | 1 | ✅ Pass |
| Token secret hardening (explicit secret accepted) | 1 | ✅ Pass |
| Upload → documentId persisted; ExternalStorageProvider = scope | 1 | ✅ Pass |
| Upload → Documents service failure throws | 1 | ✅ Pass |
| Referral signed URL: admin bypasses scope | 1 | ✅ Pass |
| Referral signed URL: shared doc, participant | 1 | ✅ Pass |
| Referral signed URL: shared doc, non-participant denied | 1 | ✅ Pass |
| Referral signed URL: provider-specific, receiving PROVIDER org | 1 | ✅ Pass |
| Referral signed URL: provider-specific, referring org denied | 1 | ✅ Pass |
| Referral signed URL: provider-specific, LAW_FIRM receiving org | 1 | ✅ Pass |
| Appointment signed URL: shared, participant | 1 | ✅ Pass |
| Appointment signed URL: shared, non-participant denied | 1 | ✅ Pass |
| Appointment signed URL: provider-specific, receiving PROVIDER org | 1 | ✅ Pass |
| Appointment signed URL: provider-specific, referring org denied | 1 | ✅ Pass |
| Appointment signed URL: provider-specific, LAW_FIRM receiving org | 1 | ✅ Pass |
| `SendProviderAssignedNotificationAsync` with email | 1 | ✅ Pass |
| `SendProviderAssignedNotificationAsync` no email (skip) | 1 | ✅ Pass |
| `SendProviderAssignedNotificationAsync` duplicate (dedupe) | 1 | ✅ Pass |

### Pre-existing test failures (not caused by CC2-INT-B03)
9 tests remain failing in unrelated areas:
- `ActivationQueueTests` — `LinkOrganizationAsync` vs `LinkOrganizationGlobalAsync` interface rename (tracked separately).
- `ReferralClientEmailTests` — notification deduplication test setup (tracked separately).
- `ProviderAvailabilityServiceTests` — provider not-found in mock setup (tracked separately).

**Total suite:** 489 tests — 480 passing, 9 pre-existing failures.

---

## 4. Security Considerations

| Concern | Mitigation |
|---|---|
| Client-side direct S3 access removed | Replaced with server-side `POST /upload` endpoint; CareConnect holds no S3 credentials |
| Signed URL scope enforcement | `UnauthorizedAccessException` thrown before any Documents service call for unauthorized callers |
| `ExternalStorageProvider` stores scope | Field stores `"shared"` or `"provider-specific"` — read by `EnforceScope` to gate signed URL access |
| HMAC token secret exposure | Blank/whitespace `DevFallbackSecret` blocked in Production/Staging via `IsNullOrWhiteSpace` constructor guard |
| Documents service auth | Bearer token from `DocumentsService:ServiceToken` sent on all service-to-service requests |
| Document IDs only (no raw S3 keys) | `ExternalDocumentId` stores Documents service document ID; platform manages S3 keys |

---

## 5. Configuration Summary

```json
// CareConnect.Api/appsettings.json additions
{
  "DocumentsService": {
    "BaseUrl": "http://localhost:5006",
    "ServiceToken": "",
    "ProductId": "CareConnect",
    "DocumentTypeId": ""
  }
}
```

Environment variables required in Production:
- `ReferralToken__Secret` — HMAC signing key (32+ chars recommended)
- `DocumentsService__BaseUrl` — Documents service endpoint
- `DocumentsService__ServiceToken` — Bearer token for service-to-service auth
- `DocumentsService__DocumentTypeId` — UUID required by Documents API
- `NotificationsService__BaseUrl` — Notifications service endpoint (existing)
