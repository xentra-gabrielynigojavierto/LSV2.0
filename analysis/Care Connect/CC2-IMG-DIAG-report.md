# Image Loading Diagnosis Report
## Scope: Tenant logos and user profile pictures not displaying on Tenant Portal

### Investigation Date
April 21, 2026

---

## Summary

Two code-level bugs were found and fixed. The images are also absent from the dev database
(no logos or avatars have been uploaded yet to the LEGALSYNQ test tenant), which is a data
state issue, not a code issue. The image loading pipeline itself is correctly implemented for
the server-side fetch.

---

## Root Cause 1 — TenantLogo hardcoded "public" fallback (BUG — FIXED)

**File:** `apps/web/src/components/shell/top-bar.tsx` → `TenantLogo` component

**Bug:** The sources fallback chain always ended with:
```tsx
sources.push('/api/branding/logo/public');
```
The literal string `"public"` was used as the document ID. Both Documents service
endpoints that could serve a logo require a valid GUID format (`{id:guid}` route
constraint):
- `GET /documents/{id:guid}/content` — authenticated redirect to S3
- `GET /public/logo/{id:guid}` — anonymous public logo streaming

Passing `"public"` caused two 404 round-trips on EVERY page load, and — critically —
meant that tenant logos uploaded with `IsPublishedAsLogo=true` were **never shown
on the login page** (unauthenticated context) even when they existed in the database.

**Fix applied:**
- Removed the broken `sources.push('/api/branding/logo/public')` line.
- Added a non-authenticated fallback that passes the real `logoWhiteDocumentId` /
  `logoDocumentId` GUIDs, so the BFF can reach `/public/logo/{id}` with a valid GUID.
- Added early return of the static default image when `sources.length === 0` (no
  branding configured), eliminating the two unnecessary 404 requests on tenants with
  no custom branding.
- Fixed `isWhiteSrc` detection to check whether the current URL *contains* the white
  logo document ID, instead of relying on the source being at index 0 (fragile after
  reordering sources for the public fallback).

---

## Root Cause 2 — SignedUrlTtlSeconds too short (RISK — FIXED)

**File:** `apps/services/documents/Documents.Api/appsettings.json`

`SignedUrlTtlSeconds` was `30` seconds. The BFF proxy fetches image bytes server-side
immediately after receiving the 302 redirect, so 30 s is usually enough. However:
- Any latency spike, cold-start, or retry would expire the URL.
- Pre-signed URLs are generated for the server-side fetch only (the BFF caches the
  response with `Cache-Control: private, max-age=3600`, so clients never see the raw S3
  URL). Increasing TTL has no security impact.

**Fix applied:** `SignedUrlTtlSeconds` increased from `30` → `300` seconds.

---

## Root Cause 3 — No branding/avatar data in development DB (DATA ISSUE — NOT A BUG)

```json
GET /api/tenants/current/branding (LEGALSYNQ tenant):
{
  "logoDocumentId": null,
  "logoWhiteDocumentId": null,
  "logoUrl": null,
  "primaryColor": null
}
```

No tenant logo has been uploaded and no users have an `avatarDocumentId` set. The
fallback to initials / static LegalSynq logo is working correctly as designed.

**Action required:** Upload a logo via the Platform Admin → Tenant Branding section
to see end-to-end image loading.

---

## Root Cause 4 — Transient React errors (RESOLVED — NOT PERSISTENT)

Browser console logs showed `"Invalid hook call"` and hydration failures. These were
caused by stale webpack chunk files (`Cannot find module './7171.js'`) from a previous
development build. The errors were transient; Hot Module Replacement performed a full
reload and the errors did not recur after rebuild.

---

## Authorization Header Verification (NOT A BUG)

A concern was raised about whether Node.js's `fetch` would leak the `Authorization:
Bearer token` header when following a cross-origin 302 redirect from the Documents
service (`http://127.0.0.1:5006`) to AWS S3 (`https://s3.amazonaws.com`).

Tested: Node.js 22 (undici) **correctly strips** the `Authorization` header on all
redirects between different ports/origins, per the WHATWG fetch specification. S3
receives only the pre-signed URL query parameters.

---

## S3 Credential Configuration (VERIFIED WORKING)

`Documents.Infrastructure/DependencyInjection.cs` lines 43–54 correctly bind AWS
credentials from environment variables:
- `AWS_S3_BUCKET_NAME` → `S3StorageOptions.BucketName`
- `AWS_S3_REGION`      → `S3StorageOptions.Region`
- `AWS_S3_ACCESS_KEY_ID`     → `S3StorageOptions.AccessKeyId`
- `AWS_S3_SECRET_ACCESS_KEY` → `S3StorageOptions.SecretAccessKey`

All four secrets are present in the Replit environment.

---

## Files Changed

| File | Change |
|------|--------|
| `apps/web/src/components/shell/top-bar.tsx` | Fixed TenantLogo fallback chain, `isWhiteSrc` detection, and empty-sources guard |
| `apps/services/documents/Documents.Api/appsettings.json` | `SignedUrlTtlSeconds` 30 → 300 |
