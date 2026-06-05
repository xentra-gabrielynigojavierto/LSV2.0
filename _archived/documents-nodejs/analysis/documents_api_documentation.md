# Documents Service — API Documentation

> **Version:** 1.0  
> **Service port:** 5005 (default)  
> **Base path:** `/` (routes are `/health`, `/documents`, `/access`)  
> **Data sensitivity:** Legal and medical documents — treat all requests as HIPAA-applicable

---

## Table of Contents

1. [Overview](#overview)
2. [Authentication](#authentication)
3. [Tenant Isolation](#tenant-isolation)
4. [Access Mediation](#access-mediation)
5. [File Upload & Scanning](#file-upload--scanning)
6. [Rate Limiting](#rate-limiting)
7. [Base URLs](#base-urls)
8. [Endpoints](#endpoints)
   - [Health](#health-endpoints)
   - [POST /documents](#post-documents)
   - [GET /documents](#get-documents)
   - [GET /documents/:id](#get-documentsid)
   - [PATCH /documents/:id](#patch-documentsid)
   - [DELETE /documents/:id](#delete-documentsid)
   - [POST /documents/:id/versions](#post-documentsidversions)
   - [GET /documents/:id/versions](#get-documentsidversions)
   - [POST /documents/:id/view-url](#post-documentsidview-url)
   - [POST /documents/:id/download-url](#post-documentsiddownload-url)
   - [GET /documents/:id/content](#get-documentsidcontent)
   - [GET /access/:token](#get-accesstoken)
9. [Request & Response Examples](#request--response-examples)
10. [Error Model](#error-model)
11. [Security & Behaviour Notes](#security--behaviour-notes)

---

## Overview

The Documents Service is a multi-tenant document management microservice that handles:

- **Secure document upload** with MIME and magic-byte validation, malware scanning, and SHA-256 checksums
- **Version management** — each document supports multiple named versions
- **Access mediation** — clients never receive direct storage URLs; access is brokered via short-lived opaque tokens
- **Full audit trail** — every access, creation, deletion, scan result, and token redemption is recorded in an immutable log
- **Tenant isolation** — documents are strictly scoped to tenants at three independent layers (database predicate, service ABAC, route pre-flight)

The service is cloud-agnostic. Storage backends (S3, GCS, local filesystem) are swappable via environment variable.

---

## Authentication

All routes under `/documents` require a **Bearer JWT** in the `Authorization` header.

```
Authorization: Bearer <jwt>
```

### Required JWT claims

| Claim | Type | Description |
|-------|------|-------------|
| `sub` or `userId` | string | Unique identifier of the calling user |
| `tenantId` or `tenant_id` | string (UUID) | The tenant the caller belongs to — used for all data scoping |
| `roles` or `role` | string[] | One or more role names — see RBAC section |
| `email` | string | Optional; stored for audit context |

### Validation

The service validates:
- JWT signature (JWKS RS256/ES256 or symmetric HS256)
- Issuer (`iss`) if `JWT_ISSUER` is configured
- Audience (`aud`) if `JWT_AUDIENCE` is configured
- Token expiry (`exp`)

An expired, tampered, or missing token returns `401 AUTHENTICATION_REQUIRED`.

### Public endpoints (no JWT required)

| Endpoint | Auth |
|----------|------|
| `GET /health` | None |
| `GET /health/ready` | None |
| `GET /access/:token` | None (uses opaque access token, not JWT) |

---

## Tenant Isolation

All document data is scoped to the caller's `tenantId` extracted from the verified JWT. Clients cannot access documents belonging to other tenants.

**Cross-tenant behaviour:** A request for a document that exists but belongs to a different tenant always returns `404 NOT_FOUND` — the service never reveals whether a document exists in another tenant's scope.

**PlatformAdmin cross-tenant access:** The `PlatformAdmin` role may access any tenant's documents by sending the header:
```
X-Admin-Target-Tenant: <targetTenantId>
```
Every cross-tenant access by an admin is recorded as an `ADMIN_CROSS_TENANT_ACCESS` audit event. Non-admin callers supplying this header have it silently ignored.

---

## Access Mediation

The service does **not** return direct cloud storage URLs to clients. Instead:

1. Client calls `POST /documents/:id/view-url` or `POST /documents/:id/download-url`
2. Service validates RBAC, tenant scope, and scan status
3. Service returns an **opaque access token** and a `redeemUrl` path
4. Client calls `GET /access/<token>` — the service internally generates a 30-second presigned storage URL and responds with `HTTP 302 Redirect`
5. Client follows the redirect to fetch the file

Storage keys, bucket names, and presigned URLs are **never** present in any API response body.

**One-time use:** By default, each access token is one-time-use. Presenting the same token twice returns `401 TOKEN_INVALID`.

**Token TTL:** Configurable (default: 300 seconds / 5 minutes). The redirect URL it generates is valid for 30 seconds.

Alternatively, for clients with a session JWT, `GET /documents/:id/content` provides direct authenticated access (bypassing the token issuance step) via a `302 Redirect`.

---

## File Upload & Scanning

### Allowed MIME types

| MIME type | Extension |
|-----------|-----------|
| `application/pdf` | `.pdf` |
| `application/msword` | `.doc` |
| `application/vnd.openxmlformats-officedocument.wordprocessingml.document` | `.docx` |
| `application/vnd.ms-excel` | `.xls` |
| `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` | `.xlsx` |
| `image/jpeg` | `.jpg` |
| `image/png` | `.png` |
| `image/tiff` | `.tiff` |
| `text/plain` | `.txt` |
| `text/csv` | `.csv` |

### File size limit

Default maximum: **50 MB** (configurable via `MAX_FILE_SIZE_MB`).

### Validation order

1. `Content-Type` header MIME check (multer `fileFilter`)
2. Size limit enforced by multer
3. Empty file check
4. Magic-byte / file signature validation — the actual binary content is inspected; a `.pdf` file containing an executable will be rejected as a MIME mismatch

### Malware scanning

Files are scanned **before** being written to storage. Behaviour depends on the configured scanner:

| `FILE_SCANNER_PROVIDER` | Behaviour |
|------------------------|-----------|
| `none` (default) | All files pass with `scanStatus: SKIPPED` |
| `mock` | Configurable result for testing |
| `clamav` | Real ClamAV scan via TCP socket |

Scan gate for access:
- `CLEAN` or `SKIPPED` → access allowed
- `INFECTED` → access always blocked (regardless of other settings)
- `PENDING` or `FAILED` → access blocked when `REQUIRE_CLEAN_SCAN_FOR_ACCESS=true`

A blocked access attempt due to scan status returns `403 SCAN_BLOCKED`.

---

## Rate Limiting

Rate limits are applied at three independent dimensions:

| Dimension | Applied to |
|-----------|-----------|
| IP address | All requests |
| User ID | Authenticated requests |
| Tenant ID | Authenticated requests |

Three limit profiles apply to different endpoint classes:

| Profile | Endpoints | IP limit | User limit | Tenant limit |
|---------|-----------|----------|------------|-------------|
| General | All `/documents` routes | 100/min | 100/min | 200/min |
| Upload | `POST /documents`, `POST /documents/:id/versions` | 10/min | 10/min | 20/min |
| Signed URL | `POST /documents/:id/view-url`, `POST /documents/:id/download-url` | 30/min | 30/min | 60/min |

Exceeded limits return `429 RATE_LIMIT_EXCEEDED` with:
- `Retry-After` header (seconds)
- `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset` headers

---

## RBAC — Roles and Permissions

| Role | Read | Write | Delete | Admin |
|------|------|-------|--------|-------|
| `DocReader` | ✅ | ❌ | ❌ | ❌ |
| `DocUploader` | ✅ | ✅ | ❌ | ❌ |
| `DocManager` | ✅ | ✅ | ✅ | ❌ |
| `TenantAdmin` | ✅ | ✅ | ✅ | ❌ |
| `PlatformAdmin` | ✅ | ✅ | ✅ | ✅ |

- **Read:** List, get by ID, list versions, request access URLs, get content
- **Write:** Upload (create), update metadata, upload new version
- **Delete:** Soft-delete a document
- **Admin:** Cross-tenant access via `X-Admin-Target-Tenant` header

A role mismatch returns `403 ACCESS_DENIED`.

---

## Base URLs

| Environment | Base URL |
|-------------|----------|
| Local development | `http://localhost:5005` |
| Via API Gateway | `http://localhost:5000/docs` (if gateway proxies to this service) |

Routes are served directly without an `/api/v1` prefix. All document endpoints are under `/documents`.

```
/health
/health/ready
/documents
/documents/:id
/documents/:id/versions
/documents/:id/view-url
/documents/:id/download-url
/documents/:id/content
/access/:token
```

---

## Endpoints

### Health Endpoints

---

#### `GET /health`

Liveness check. Returns immediately without touching the database.

**Auth:** None  
**Rate limit:** None

**Response `200`:**
```json
{
  "status": "ok",
  "service": "docs-service",
  "timestamp": "2026-03-29T12:00:00.000Z"
}
```

---

#### `GET /health/ready`

Readiness check. Pings the database and reports storage provider status.

**Auth:** None  
**Rate limit:** None

**Response `200`:**
```json
{
  "status": "ready",
  "checks": {
    "database": "ok",
    "storage": "local"
  },
  "timestamp": "2026-03-29T12:00:00.000Z"
}
```

**Response `503`** (database unreachable):
```json
{
  "status": "degraded",
  "checks": {
    "database": "fail",
    "storage": "s3"
  },
  "timestamp": "2026-03-29T12:00:00.000Z"
}
```

---

### `POST /documents`

Upload a new document. The file is scanned before being written to storage.

**Auth:** Required (Bearer JWT)  
**Permission:** `write`  
**Rate limit:** Upload limiter (10/min per IP/user; 20/min per tenant)  
**Content-Type:** `multipart/form-data`

#### Request headers

| Header | Required | Description |
|--------|----------|-------------|
| `Authorization` | ✅ | `Bearer <jwt>` |
| `Content-Type` | ✅ | `multipart/form-data` (set by HTTP client automatically) |
| `X-Correlation-Id` | ❌ | Optional idempotency/tracing ID; echoed in responses |

#### Request body (multipart/form-data)

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `file` | file | ✅ | The document file |
| `tenantId` | string (UUID) | ✅ | Must match the caller's JWT `tenantId` |
| `productId` | string | ✅ | Product context (e.g. `careconnect`, `fund`) |
| `referenceId` | string | ✅ | ID of the entity this document belongs to |
| `referenceType` | string | ✅ | Type of the referenced entity (e.g. `application`, `member`) |
| `documentTypeId` | string (UUID) | ✅ | ID of the document type from the `document_types` table |
| `title` | string (1–500 chars) | ✅ | Human-readable document title |
| `description` | string (≤2000 chars) | ❌ | Optional description |

> **Note:** `multipart/form-data` body fields are sent as text parts alongside the file. When using `curl`, pass them with `-F` flags. The `tenantId` in the body is validated against the JWT `tenantId` — mismatches return `403`.

#### Response `201`

```json
{
  "data": {
    "id": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
    "tenantId": "aaaa0000-0000-0000-0000-000000000001",
    "productId": "careconnect",
    "referenceId": "mbr-001",
    "referenceType": "member",
    "documentTypeId": "10000000-0000-0000-0000-000000000001",
    "title": "Medical History 2026",
    "description": null,
    "status": "DRAFT",
    "mimeType": "application/pdf",
    "fileSizeBytes": 204800,
    "currentVersionId": null,
    "versionCount": 0,
    "scanStatus": "SKIPPED",
    "scanCompletedAt": "2026-03-29T12:00:01.000Z",
    "scanThreats": [],
    "isDeleted": false,
    "deletedAt": null,
    "deletedBy": null,
    "retainUntil": null,
    "legalHoldAt": null,
    "createdAt": "2026-03-29T12:00:01.000Z",
    "createdBy": "user-uuid-here",
    "updatedAt": "2026-03-29T12:00:01.000Z",
    "updatedBy": "user-uuid-here"
  }
}
```

> `storageKey`, `storageBucket`, and `checksum` are stripped from all responses.

#### Error responses

| Status | Code | Condition |
|--------|------|-----------|
| 400 | `VALIDATION_ERROR` | Missing or invalid body fields |
| 400 | `FILE_VALIDATION_ERROR` | File is empty or MIME mismatch detected |
| 401 | `AUTHENTICATION_REQUIRED` | Missing or invalid JWT |
| 403 | `ACCESS_DENIED` | Role does not have `write` permission |
| 403 | `ACCESS_DENIED` | `tenantId` in body does not match JWT `tenantId` |
| 413 | `FILE_TOO_LARGE` | File exceeds `MAX_FILE_SIZE_MB` |
| 422 | `UNSUPPORTED_FILE_TYPE` | MIME type not in allowed list |
| 422 | `INFECTED_FILE` | Malware detected by scanner |
| 429 | `RATE_LIMIT_EXCEEDED` | Upload rate limit exceeded |

---

### `GET /documents`

List documents for the authenticated tenant.

**Auth:** Required  
**Permission:** `read`  
**Rate limit:** General limiter (100/min)

#### Query parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `productId` | string | ❌ | Filter by product |
| `referenceId` | string | ❌ | Filter by referenced entity ID |
| `referenceType` | string | ❌ | Filter by referenced entity type |
| `status` | string | ❌ | Filter by document status (`DRAFT`, `ACTIVE`, `ARCHIVED`, `DELETED`, `LEGAL_HOLD`) |
| `limit` | integer (1–200) | ❌ | Page size (default: 50) |
| `offset` | integer (≥0) | ❌ | Pagination offset (default: 0) |

> The `tenantId` filter is **always** applied from the JWT — it cannot be overridden via query parameters.

#### Response `200`

```json
{
  "data": [
    {
      "id": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
      "tenantId": "aaaa0000-0000-0000-0000-000000000001",
      "productId": "careconnect",
      "referenceId": "mbr-001",
      "referenceType": "member",
      "documentTypeId": "10000000-0000-0000-0000-000000000001",
      "title": "Medical History 2026",
      "description": null,
      "status": "DRAFT",
      "mimeType": "application/pdf",
      "fileSizeBytes": 204800,
      "scanStatus": "CLEAN",
      "isDeleted": false,
      "createdAt": "2026-03-29T12:00:01.000Z",
      "updatedAt": "2026-03-29T12:00:01.000Z"
    }
  ],
  "total": 1,
  "limit": 50,
  "offset": 0
}
```

#### Error responses

| Status | Code | Condition |
|--------|------|-----------|
| 401 | `AUTHENTICATION_REQUIRED` | Missing or invalid JWT |
| 403 | `ACCESS_DENIED` | Role does not have `read` permission |
| 429 | `RATE_LIMIT_EXCEEDED` | General rate limit exceeded |

---

### `GET /documents/:id`

Retrieve a single document by ID.

**Auth:** Required  
**Permission:** `read`  
**Rate limit:** General limiter

#### Path parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | UUID | Document ID |

#### Response `200`

```json
{
  "data": {
    "id": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
    "tenantId": "aaaa0000-0000-0000-0000-000000000001",
    "productId": "careconnect",
    "referenceId": "mbr-001",
    "referenceType": "member",
    "documentTypeId": "10000000-0000-0000-0000-000000000001",
    "title": "Medical History 2026",
    "description": "Annual health review",
    "status": "ACTIVE",
    "mimeType": "application/pdf",
    "fileSizeBytes": 204800,
    "currentVersionId": "bb2c10b-58cc-4372-a567-0e02b2c3d480",
    "versionCount": 2,
    "scanStatus": "CLEAN",
    "scanCompletedAt": "2026-03-29T12:00:05.000Z",
    "scanThreats": [],
    "isDeleted": false,
    "deletedAt": null,
    "deletedBy": null,
    "retainUntil": null,
    "legalHoldAt": null,
    "createdAt": "2026-03-29T12:00:01.000Z",
    "createdBy": "user-uuid-here",
    "updatedAt": "2026-03-29T12:01:00.000Z",
    "updatedBy": "user-uuid-here"
  }
}
```

#### Error responses

| Status | Code | Condition |
|--------|------|-----------|
| 401 | `AUTHENTICATION_REQUIRED` | Missing or invalid JWT |
| 403 | `ACCESS_DENIED` | Role does not have `read` permission |
| 404 | `NOT_FOUND` | Document does not exist, is deleted, or belongs to another tenant |
| 429 | `RATE_LIMIT_EXCEEDED` | General rate limit exceeded |

> **Important:** A document belonging to another tenant returns `404 NOT_FOUND`, never `403`. This prevents tenant ID enumeration.

---

### `PATCH /documents/:id`

Update document metadata. Only the supplied fields are updated.

**Auth:** Required  
**Permission:** `write`  
**Rate limit:** General limiter  
**Content-Type:** `application/json`

#### Path parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | UUID | Document ID |

#### Request body (all fields optional)

```json
{
  "title": "Updated Title",
  "description": "Revised description",
  "documentTypeId": "10000000-0000-0000-0000-000000000002",
  "status": "ACTIVE",
  "retainUntil": "2032-03-29T00:00:00.000Z"
}
```

| Field | Type | Validation |
|-------|------|-----------|
| `title` | string | 1–500 characters |
| `description` | string | Max 2000 characters |
| `documentTypeId` | UUID | Must reference a valid document type |
| `status` | string | One of: `DRAFT`, `ACTIVE`, `ARCHIVED`, `LEGAL_HOLD` |
| `retainUntil` | ISO 8601 datetime | Retention deadline |

> Status `DELETED` cannot be set via PATCH — use `DELETE /documents/:id`.

#### Response `200`

Returns the updated document object in the same shape as `GET /documents/:id`.

#### Error responses

| Status | Code | Condition |
|--------|------|-----------|
| 400 | `VALIDATION_ERROR` | Invalid field values |
| 401 | `AUTHENTICATION_REQUIRED` | Missing or invalid JWT |
| 403 | `ACCESS_DENIED` | Role does not have `write` permission |
| 404 | `NOT_FOUND` | Document not found or belongs to another tenant |
| 429 | `RATE_LIMIT_EXCEEDED` | Rate limit exceeded |

---

### `DELETE /documents/:id`

Soft-delete a document. The document is marked as deleted (`isDeleted: true`, `status: DELETED`) but data is retained in the database. Deleted documents do not appear in list or get-by-ID responses.

**Auth:** Required  
**Permission:** `delete`  
**Rate limit:** General limiter

**Business rules:**
- A document on legal hold (`legalHoldAt` is set) **cannot** be deleted — returns `403 ACCESS_DENIED`
- Only `DocManager`, `TenantAdmin`, and `PlatformAdmin` roles may delete

#### Path parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | UUID | Document ID |

#### Response `204`

Empty body.

#### Error responses

| Status | Code | Condition |
|--------|------|-----------|
| 401 | `AUTHENTICATION_REQUIRED` | Missing or invalid JWT |
| 403 | `ACCESS_DENIED` | Role does not have `delete` permission |
| 403 | `ACCESS_DENIED` | Document is on legal hold |
| 404 | `NOT_FOUND` | Document not found or belongs to another tenant |
| 429 | `RATE_LIMIT_EXCEEDED` | Rate limit exceeded |

---

### `POST /documents/:id/versions`

Upload a new version of an existing document. The new version is scanned before storage. The parent document's `currentVersionId`, `versionCount`, and scan status are updated atomically.

**Auth:** Required  
**Permission:** `write`  
**Rate limit:** Upload limiter (10/min per IP/user)  
**Content-Type:** `multipart/form-data`

#### Path parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | UUID | Parent document ID |

#### Request body (multipart/form-data)

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `file` | file | ✅ | The new version file |
| `label` | string | ❌ | Optional version label (e.g. `"v2-revised"`) |

#### Response `201`

```json
{
  "data": {
    "id": "cc3d10b-58cc-4372-a567-0e02b2c3d481",
    "documentId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
    "tenantId": "aaaa0000-0000-0000-0000-000000000001",
    "versionNumber": 2,
    "mimeType": "application/pdf",
    "fileSizeBytes": 215040,
    "scanStatus": "CLEAN",
    "scanCompletedAt": "2026-03-29T12:05:00.000Z",
    "scanDurationMs": 142,
    "scanThreats": [],
    "scanEngineVersion": null,
    "uploadedAt": "2026-03-29T12:05:00.000Z",
    "uploadedBy": "user-uuid-here",
    "label": "v2-revised",
    "isDeleted": false,
    "deletedAt": null,
    "deletedBy": null
  }
}
```

#### Error responses

Same as `POST /documents` plus:

| Status | Code | Condition |
|--------|------|-----------|
| 404 | `NOT_FOUND` | Parent document not found or belongs to another tenant |

---

### `GET /documents/:id/versions`

List all non-deleted versions of a document, ordered by version number descending (newest first).

**Auth:** Required  
**Permission:** `read`  
**Rate limit:** General limiter

#### Response `200`

```json
{
  "data": [
    {
      "id": "cc3d10b-58cc-4372-a567-0e02b2c3d481",
      "documentId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
      "tenantId": "aaaa0000-0000-0000-0000-000000000001",
      "versionNumber": 2,
      "mimeType": "application/pdf",
      "fileSizeBytes": 215040,
      "scanStatus": "CLEAN",
      "uploadedAt": "2026-03-29T12:05:00.000Z",
      "uploadedBy": "user-uuid-here",
      "label": "v2-revised",
      "isDeleted": false
    }
  ]
}
```

---

### `POST /documents/:id/view-url`

Request a view access token for a document. Returns an opaque token the client must redeem at `GET /access/:token`.

**Auth:** Required  
**Permission:** `read`  
**Rate limit:** Signed URL limiter (30/min)  

**Business rules:**
- Document must have `scanStatus` of `CLEAN` or `SKIPPED` (configurable)
- Document must not be deleted

#### Response `200`

```json
{
  "data": {
    "accessToken": "a3f9c2d1e8b74506...(64 hex chars)",
    "redeemUrl": "/access/a3f9c2d1e8b74506...(64 hex chars)",
    "expiresInSeconds": 300,
    "type": "view"
  }
}
```

| Field | Description |
|-------|-------------|
| `accessToken` | 64-character lowercase hex string (256-bit entropy) |
| `redeemUrl` | Relative path to redeem — prefix with the service base URL |
| `expiresInSeconds` | Token TTL in seconds (default 300) |
| `type` | Always `"view"` for this endpoint |

#### Error responses

| Status | Code | Condition |
|--------|------|-----------|
| 403 | `SCAN_BLOCKED` | Document scan status is `INFECTED`, `PENDING`, or `FAILED` |
| 404 | `NOT_FOUND` | Document not found or belongs to another tenant |

---

### `POST /documents/:id/download-url`

Identical to `POST /documents/:id/view-url` but returns `"type": "download"`. The distinction affects the Content-Disposition header when the storage URL is served.

#### Response `200`

Same shape as view-url with `"type": "download"`.

---

### `GET /documents/:id/content`

Direct authenticated access — bypasses the access token flow. Validates the session JWT, checks scan status, generates a 30-second storage URL, and immediately responds with `HTTP 302 Redirect`.

**Auth:** Required (Bearer JWT)  
**Permission:** `read`  
**Rate limit:** General limiter

#### Query parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `type` | `view` or `download` | `view` | Content disposition type |

#### Response `302`

Redirect to an internal or cloud storage URL (valid for 30 seconds). The response body is empty. The storage key is never present in the redirect URL visible to the client (in local mode, it is an opaque token URL; in S3/GCS mode it is a presigned URL).

#### Error responses

| Status | Code | Condition |
|--------|------|-----------|
| 403 | `SCAN_BLOCKED` | Scan status blocks access |
| 404 | `NOT_FOUND` | Document not found or belongs to another tenant |

---

### `GET /access/:token`

Redeem an access token. No JWT required. The token was previously issued by `POST /documents/:id/view-url` or `POST /documents/:id/download-url`.

**Auth:** None (opaque access token in path)  
**Rate limit:** None (see security note below)

#### Path parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `token` | string (64 hex chars) | Opaque access token |

**Validation:** Token must be exactly 64 lowercase hex characters. Any other format returns `401 TOKEN_INVALID` immediately (before store lookup).

#### Response `302`

Redirect to the file. The redirect target is valid for **30 seconds**.

```
HTTP/1.1 302 Found
Location: /internal/files?token=<storage-token>   (local)
         or
Location: https://bucket.s3.amazonaws.com/...?X-Amz-Expires=30&...   (S3)
         or
Location: https://storage.googleapis.com/...&Expires=...   (GCS)
```

The response body is empty.

#### Error responses

| Status | Code | Condition |
|--------|------|-----------|
| 401 | `TOKEN_INVALID` | Token format is wrong (not 64 lowercase hex chars) |
| 401 | `TOKEN_EXPIRED` | Token has expired or was not found |
| 401 | `TOKEN_INVALID` | Token has already been used (one-time-use enforcement) |
| 403 | `SCAN_BLOCKED` | Document was re-scanned and found infected after token issuance |
| 404 | `NOT_FOUND` | Document was deleted after token was issued |

---

## Request & Response Examples

### Upload a document (curl)

```bash
curl -X POST http://localhost:5005/documents \
  -H "Authorization: Bearer <jwt>" \
  -H "X-Correlation-Id: req-abc-123" \
  -F "file=@/path/to/document.pdf;type=application/pdf" \
  -F "tenantId=aaaa0000-0000-0000-0000-000000000001" \
  -F "productId=careconnect" \
  -F "referenceId=mbr-001" \
  -F "referenceType=member" \
  -F "documentTypeId=10000000-0000-0000-0000-000000000001" \
  -F "title=Medical History 2026"
```

---

### List documents with filters (curl)

```bash
curl "http://localhost:5005/documents?productId=careconnect&referenceId=mbr-001&limit=20" \
  -H "Authorization: Bearer <jwt>"
```

---

### Request a view URL and redeem it

```bash
# Step 1 — request access token
RESPONSE=$(curl -s -X POST \
  http://localhost:5005/documents/f47ac10b-58cc-4372-a567-0e02b2c3d479/view-url \
  -H "Authorization: Bearer <jwt>")

TOKEN=$(echo $RESPONSE | jq -r '.data.accessToken')

# Step 2 — redeem (follow redirect to get file)
curl -L http://localhost:5005/access/$TOKEN \
  --output document.pdf
```

---

### Error response (generic)

```json
{
  "error": "NOT_FOUND",
  "message": "Document not found: f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "correlationId": "req-abc-123"
}
```

---

### Rate limit response (429)

```json
{
  "error": "RATE_LIMIT_EXCEEDED",
  "message": "Rate limit exceeded. Retry after 42 second(s).",
  "retryAfter": 42,
  "limitDimension": "user",
  "correlationId": "req-abc-123"
}
```

Headers:
```
Retry-After: 42
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 0
X-RateLimit-Reset: 1743250842
```

---

### Scan-blocked response (403)

```json
{
  "error": "SCAN_BLOCKED",
  "message": "Access denied: file scan status is INFECTED. Only CLEAN files may be accessed.",
  "correlationId": "req-abc-123"
}
```

---

### Validation error response (400)

```json
{
  "error": "VALIDATION_ERROR",
  "message": "Request validation failed",
  "details": {
    "title": ["Required"],
    "tenantId": ["Invalid uuid"]
  },
  "correlationId": "req-abc-123"
}
```

---

## Error Model

All error responses share this structure:

```json
{
  "error": "<ERROR_CODE>",
  "message": "<human-readable description>",
  "correlationId": "<uuid or custom value from X-Correlation-Id header>",
  "details": { }   // optional — present on 400 validation errors
}
```

### Error codes

| Code | HTTP Status | Description |
|------|-------------|-------------|
| `VALIDATION_ERROR` | 400 | Request body failed schema validation |
| `FILE_VALIDATION_ERROR` | 400 | File is empty or magic-byte mismatch |
| `AUTHENTICATION_REQUIRED` | 401 | Missing or invalid JWT |
| `TOKEN_EXPIRED` | 401 | Access token expired or not found |
| `TOKEN_INVALID` | 401 | Access token already used, malformed, or invalid |
| `ACCESS_DENIED` | 403 | RBAC permission denied, tenant scope mismatch, or legal hold |
| `SCAN_BLOCKED` | 403 | File access blocked due to scan status |
| `TENANT_ISOLATION_VIOLATION` | 403 | Cross-tenant access attempt detected (see security note) |
| `NOT_FOUND` | 404 | Resource does not exist or belongs to another tenant |
| `FILE_TOO_LARGE` | 413 | File exceeds maximum size |
| `UNSUPPORTED_FILE_TYPE` | 422 | MIME type not in allowed list |
| `INFECTED_FILE` | 422 | Malware detected during upload scan |
| `RATE_LIMIT_EXCEEDED` | 429 | Rate limit exceeded |
| `REDIS_UNAVAILABLE` | 503 | Redis backend unavailable (rate limit or token store) |
| `STORAGE_ERROR` | 500 | Storage backend error |
| `DATABASE_ERROR` | 500 | Database error |
| `INTERNAL_SERVER_ERROR` | 500 | Unhandled error |

> `TENANT_ISOLATION_VIOLATION` is only returned in edge cases where the service-layer ABAC catches a cross-tenant access that bypassed the DB layer. In normal operation, cross-tenant requests return `404 NOT_FOUND`.

---

## Security & Behaviour Notes

### Correlation IDs

Every request is assigned a `correlationId` (UUID). If the caller sends `X-Correlation-Id` in the request header, that value is used instead. The `correlationId` is echoed in every response body and in all audit log entries.

### Security headers

The service sets the following security headers on every response (via Helmet):
- `Content-Security-Policy`
- `Strict-Transport-Security` (HSTS, max-age 1 year)
- `X-Content-Type-Options: nosniff`
- `X-XSS-Protection`
- `Referrer-Policy: no-referrer`
- `Cross-Origin-Embedder-Policy`

### Audit trail

All significant actions are written to an immutable `document_audits` table. Events include: document creation, update, deletion, version upload, scan results, URL generation, token issuance, token redemption, access denied, and cross-tenant access. The audit table has a database-level trigger that prevents UPDATE and DELETE operations.

### One-time-use tokens

Access tokens are one-time-use by default. Attempting to redeem a token that has already been used returns `401 TOKEN_INVALID`. This is enforced atomically in Redis to prevent replay attacks in distributed deployments.

### Legal hold

A document with `legalHoldAt` set cannot be soft-deleted. Attempting to delete such a document returns `403 ACCESS_DENIED` with the message `"Document is on legal hold and cannot be deleted"`.

### Deleted documents

Soft-deleted documents are not returned by `GET /documents` or `GET /documents/:id`. Requesting a deleted document ID returns `404 NOT_FOUND`.

### CORS

CORS is restricted to origins configured via `CORS_ORIGINS` environment variable. `Authorization`, `Content-Type`, and `X-Correlation-Id` are the only allowed request headers. `X-Correlation-Id` is exposed as a response header.
