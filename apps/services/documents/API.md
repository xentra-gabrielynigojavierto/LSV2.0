# Documents Service API Documentation

## Table of Contents

- [Overview](#overview)
- [Authentication & Authorization](#authentication--authorization)
- [Common Models](#common-models)
- [Error Responses](#error-responses)
- [Documents](#documents-endpoints)
- [Access](#access-endpoints)
- [Public Logo](#public-logo-endpoints)

---

## Overview

Base URL prefix: `/documents`

The Documents service handles file upload, versioning, metadata management, access token generation, and content delivery. Upload endpoints accept `multipart/form-data`; all other request and response bodies use `application/json` content type unless otherwise noted.

---

## Authentication & Authorization

### Authenticated Endpoints

All endpoints under `/documents` require an authenticated user. The caller's identity is extracted from the JWT bearer token via `JwtPrincipalExtractor`.

Requests missing authentication receive a `401 Unauthorized` response.

### Anonymous Endpoints

The following endpoints allow anonymous (unauthenticated) access:

| Endpoint | Description |
|---|---|
| `GET /access/{token}` | Token redemption — redirects to file |
| `GET /public/logo/{id}` | Public tenant logo streaming |

### RequestContext

Every authenticated request builds a `RequestContext` from the JWT and request headers:

| Field | Type | Source | Description |
|---|---|---|---|
| `Principal` | `Principal` | JWT claims | Extracted user principal (user ID, tenant ID, roles) |
| `CorrelationId` | `string` | `X-Correlation-Id` header | Request correlation identifier for tracing |
| `IpAddress` | `string` | Connection | Client IP address |
| `UserAgent` | `string` | `User-Agent` header | Client user agent string |
| `TargetTenantId` | `guid` | `X-Admin-Target-Tenant` header | Platform admin override — allows admins to operate on behalf of a target tenant |

The effective tenant ID is resolved as follows: platform admins who supply `X-Admin-Target-Tenant` operate against that tenant; all other callers use their own `Principal.TenantId`.

---

## Common Models

### DocumentStatus

Documents have a lifecycle status represented by the following values:

| Value | Description |
|---|---|
| `DRAFT` | Initial status after upload |
| `ACTIVE` | Document is active and available |
| `ARCHIVED` | Document has been archived |
| `DELETED` | Document has been soft-deleted (system-managed — cannot be set via PATCH) |
| `LEGAL_HOLD` | Document is under legal hold and cannot be deleted |

### ScanStatus

Every document and document version has a virus/malware scan status:

| Value | Description |
|---|---|
| `PENDING` | Scan has not yet completed |
| `CLEAN` | Scan completed — no threats detected |
| `INFECTED` | Scan completed — threats were detected |
| `FAILED` | Scan encountered an error |
| `SKIPPED` | Scan was skipped |

---

## Error Responses

### 400 Bad Request

Returned when the request fails validation.

```json
{
  "error": "VALIDATION_ERROR",
  "message": "Request validation failed",
  "details": {
    "fieldName": ["Error description"]
  }
}
```

### 400 Bad Request — File Validation

Returned when no file is provided or the file is empty.

```json
{
  "error": "FILE_VALIDATION_ERROR",
  "message": "File is required and must not be empty"
}
```

### 401 Unauthorized

Returned when the request lacks valid authentication credentials (authenticated endpoints only).

### 404 Not Found

Returned when a requested resource does not exist.

### 413 Payload Too Large

Returned when an uploaded file exceeds the configured maximum upload size.

```json
{
  "error": "FILE_TOO_LARGE",
  "message": "File size 52,428,800 bytes (50.0 MB) exceeds the maximum upload limit of 25 MB",
  "fileSizeBytes": 52428800,
  "limitMb": 25,
  "correlationId": "abc-123"
}
```

### 403 Forbidden

Returned when the user is authenticated but the operation is not permitted.

| Error Code | Condition |
|---|---|
| `ACCESS_DENIED` | User lacks permission (e.g., document is on legal hold and cannot be deleted) |
| `SCAN_BLOCKED` | File access is blocked because the virus scan detected threats or is not yet clean |
| `TENANT_ISOLATION_VIOLATION` | Cross-tenant access attempt |

### 422 Unprocessable Entity

Returned when the file cannot be processed.

| Error Code | Condition |
|---|---|
| `FILE_EXCEEDS_SCAN_LIMIT` | File exceeds the maximum scannable file size limit |
| `UNSUPPORTED_FILE_TYPE` | File MIME type is not permitted |
| `INFECTED_FILE` | Virus scan detected threats in the uploaded file |

```json
{
  "error": "FILE_EXCEEDS_SCAN_LIMIT",
  "message": "File size 104,857,600 bytes (100.0 MB) exceeds the maximum scannable limit of 50 MB",
  "fileSizeBytes": 104857600,
  "limitMb": 50,
  "correlationId": "abc-123"
}
```

### 503 Service Unavailable

Returned when the scan job queue is saturated and cannot accept new uploads. The response includes a `Retry-After` header.

```json
{
  "error": "QUEUE_SATURATED",
  "message": "Scan queue is saturated — upload rejected. Retry after a short delay.",
  "retryAfter": 30,
  "correlationId": "abc-123"
}
```

### Common Status Codes

| Status | Condition |
|---|---|
| `400 Bad Request` | Invalid request body or missing required fields |
| `401 Unauthorized` | Missing or invalid authentication |
| `403 Forbidden` | Operation not permitted (access denied, scan blocked, tenant isolation) |
| `404 Not Found` | Resource does not exist |
| `413 Payload Too Large` | File exceeds the upload size limit |
| `422 Unprocessable Entity` | File cannot be processed (scan limit, unsupported type, infected) |
| `503 Service Unavailable` | Scan queue saturated — retry after delay |

**Per-endpoint status code summary:**

| Endpoint Type | Success | Possible Errors |
|---|---|---|
| Upload (`POST` multipart) | `201 Created` | `400`, `401`, `403`, `413`, `422`, `503` |
| List (`GET` returning list) | `200 OK` | `401`, `403` |
| Get by ID (`GET` returning single item) | `200 OK` | `401`, `403`, `404` |
| Update (`PATCH`) | `200 OK` | `401`, `403`, `404` |
| Delete (`DELETE`) | `204 No Content` | `401`, `403`, `404` |
| Token issue (`POST` view-url / download-url) | `200 OK` | `401`, `403`, `404` |
| Content redirect (`GET` content) | `302 Found` | `401`, `403`, `404` |
| Token redemption (`GET /access/{token}`) | `302 Found` | `401`, `403`, `404` |
| Public logo (`GET /public/logo/{id}`) | `200 OK` | `404` |

---

## Documents Endpoints

Base path: `/documents`

### POST `/documents`

Upload a new document.

**Authentication:** Required

**Content-Type:** `multipart/form-data`

**Form Fields:**

| Field | Type | Required | Description |
|---|---|---|---|
| `tenantId` | `guid` | Yes | Tenant identifier (UUID) |
| `productId` | `string` | Yes | Product identifier (max 100 chars) |
| `referenceId` | `string` | Yes | Reference identifier (max 500 chars) |
| `referenceType` | `string` | Yes | Reference type (max 100 chars) |
| `documentTypeId` | `guid` | Yes | Document type identifier (UUID) |
| `title` | `string` | Yes | Document title (max 500 chars) |
| `description` | `string` | No | Document description (max 2000 chars) |
| `file` | `binary` | Yes | The file to upload (must not be empty) |

**Response:** `201 Created`

```json
{
  "data": DocumentResponse
}
```

Returns a `Location` header pointing to `/documents/{id}`.

**Errors:**

| Status | Error Code | Condition |
|---|---|---|
| `400` | `VALIDATION_ERROR` | Missing or invalid `tenantId` or `documentTypeId` |
| `400` | `FILE_VALIDATION_ERROR` | No file provided or file is empty |
| `413` | `FILE_TOO_LARGE` | File exceeds the maximum upload size |
| `422` | `FILE_EXCEEDS_SCAN_LIMIT` | File exceeds the maximum scannable size |
| `422` | `UNSUPPORTED_FILE_TYPE` | File MIME type is not permitted |
| `503` | `QUEUE_SATURATED` | Scan queue is full — retry after delay |

---

### GET `/documents`

List documents for the authenticated tenant with optional filters.

**Authentication:** Required

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `productId` | `string` | No | `null` | Filter by product identifier |
| `referenceId` | `string` | No | `null` | Filter by reference identifier |
| `referenceType` | `string` | No | `null` | Filter by reference type |
| `status` | `string` | No | `null` | Filter by document status |
| `limit` | `integer` | No | `50` | Number of items to return (clamped to 1–200) |
| `offset` | `integer` | No | `0` | Number of items to skip (minimum 0) |

**Response:** `200 OK` — `DocumentListResponse`

```json
{
  "data": [DocumentResponse, ...],
  "total": 42,
  "limit": 50,
  "offset": 0
}
```

---

### GET `/documents/{id}`

Get a document by its unique identifier.

**Authentication:** Required

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Document unique identifier |

**Response:** `200 OK`

```json
{
  "data": DocumentResponse
}
```

**Error:** `404 Not Found` — if the document does not exist.

---

### PATCH `/documents/{id}`

Update document metadata.

**Authentication:** Required

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Document unique identifier |

**Request Body: `UpdateDocumentRequest`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `title` | `string` | No | Yes | Document title (max 500 chars) |
| `description` | `string` | No | Yes | Document description (max 2000 chars) |
| `documentTypeId` | `guid` | No | Yes | Document type identifier |
| `status` | `string` | No | Yes | Document status (must be one of: `DRAFT`, `ACTIVE`, `ARCHIVED`, `LEGAL_HOLD`) |
| `retainUntil` | `datetime` | No | Yes | Retention date |

**Response:** `200 OK`

```json
{
  "data": DocumentResponse
}
```

**Error:** `404 Not Found` — if the document does not exist.

---

### DELETE `/documents/{id}`

Soft-delete a document. The document is marked as deleted but not permanently removed.

**Authentication:** Required

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Document unique identifier |

**Response:** `204 No Content`

**Errors:**

- `403 Forbidden` (`ACCESS_DENIED`) — if the document is on legal hold and cannot be deleted.
- `404 Not Found` — if the document does not exist.

---

### POST `/documents/{id}/versions`

Upload a new version of an existing document.

**Authentication:** Required

**Content-Type:** `multipart/form-data`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Document unique identifier |

**Form Fields:**

| Field | Type | Required | Description |
|---|---|---|---|
| `file` | `binary` | Yes | The file to upload (must not be empty) |
| `label` | `string` | No | Optional version label |

**Response:** `201 Created`

```json
{
  "data": DocumentVersionResponse
}
```

Returns a `Location` header pointing to `/documents/{id}/versions/{versionId}`.

**Errors:**

| Status | Error Code | Condition |
|---|---|---|
| `400` | `VALIDATION_ERROR` | Request is not multipart/form-data |
| `400` | `FILE_VALIDATION_ERROR` | No file provided or file is empty |
| `413` | `FILE_TOO_LARGE` | File exceeds the maximum upload size |
| `422` | `FILE_EXCEEDS_SCAN_LIMIT` | File exceeds the maximum scannable size |
| `422` | `UNSUPPORTED_FILE_TYPE` | File MIME type is not permitted |
| `503` | `QUEUE_SATURATED` | Scan queue is full — retry after delay |

---

### GET `/documents/{id}/versions`

List all versions of a document.

**Authentication:** Required

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Document unique identifier |

**Response:** `200 OK`

```json
{
  "data": [DocumentVersionResponse, ...]
}
```

---

### POST `/documents/{id}/view-url`

Request a time-limited access token for viewing the document. The returned token can be redeemed via the anonymous `GET /access/{token}` endpoint.

**Authentication:** Required

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Document unique identifier |

**Response:** `200 OK`

```json
{
  "data": IssuedTokenResponse
}
```

**Errors:**

- `403 Forbidden` (`SCAN_BLOCKED`) — if the document's scan status is `INFECTED`, or a non-clean status configured to block access.
- `404 Not Found` — if the document does not exist.

---

### POST `/documents/{id}/download-url`

Request a time-limited access token for downloading the document. The returned token can be redeemed via the anonymous `GET /access/{token}` endpoint.

**Authentication:** Required

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Document unique identifier |

**Response:** `200 OK`

```json
{
  "data": IssuedTokenResponse
}
```

**Errors:**

- `403 Forbidden` (`SCAN_BLOCKED`) — if the document's scan status is `INFECTED`, or a non-clean status configured to block access.
- `404 Not Found` — if the document does not exist.

---

### GET `/documents/{id}/content`

Direct authenticated file access. Returns a `302` redirect to the file's storage URL.

**Authentication:** Required

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Document unique identifier |

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `type` | `string` | No | `view` | Access type — `view` or `download` |

**Response:** `302 Found` — Redirects to the file content URL.

---

## Access Endpoints

Base path: `/access`

### GET `/access/{token}`

Redeem an opaque access token for a `302` redirect to the file. This endpoint is **anonymous** — no authentication is required. Tokens are single-use and time-limited.

**Authentication:** None (anonymous)

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `token` | `string` | Opaque access token (exactly 64 lowercase hex characters) |

**Response:** `302 Found` — Redirects to the file's storage URL.

**Errors:**

| Status | Error Code | Condition |
|---|---|---|
| `401` | `TOKEN_INVALID` | Token is malformed (not 64 hex chars) or has already been used |
| `401` | `TOKEN_EXPIRED` | Token does not exist or has expired |
| `403` | `SCAN_BLOCKED` | File access is blocked — the document's scan status is `INFECTED`, or a non-clean status (`PENDING`, `FAILED`) that is configured to block access |
| `404` | `NOT_FOUND` | Associated document or file not found |

**Error Response Format:**

```json
{
  "error": "TOKEN_INVALID",
  "message": "Access token is invalid or has already been used",
  "correlationId": "abc-123"
}
```

**Token Format:** Tokens must be exactly 64 lowercase hexadecimal characters (`[0-9a-f]{64}`). Tokens that do not match this format are immediately rejected with `TOKEN_INVALID`. Note: format-validation rejections do not include a `correlationId` field; only service-level errors (expired, used, scan-blocked, not-found) include `correlationId` in the response.

---

## Public Logo Endpoints

Base path: `/public/logo`

### GET `/public/logo/{id}`

Stream a tenant's logo image. This endpoint is **anonymous** — no authentication is required. Access is restricted to documents with the tenant logo document type (`20000000-0000-0000-0000-000000000002`). Documents of any other type will return `404`.

**Authentication:** None (anonymous)

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Document unique identifier |

**Response:** `200 OK` — Streams the logo image with the document's MIME type (defaults to `image/png` if not set).

**Error:** `404 Not Found` — if the document does not exist, has been deleted, is not a tenant logo document type, or the file is not found in storage.

---

## Request & Response DTOs

### CreateDocumentRequest

Submitted as `multipart/form-data` fields (not JSON). See [POST `/documents`](#post-documents) for the full form field table.

| Field | Type | Required | Description |
|---|---|---|---|
| `tenantId` | `guid` | Yes | Tenant identifier |
| `productId` | `string` | Yes | Product identifier (max 100 chars) |
| `referenceId` | `string` | Yes | Reference identifier (max 500 chars) |
| `referenceType` | `string` | Yes | Reference type (max 100 chars) |
| `documentTypeId` | `guid` | Yes | Document type identifier |
| `title` | `string` | Yes | Document title (max 500 chars) |
| `description` | `string` | No | Document description (max 2000 chars) |

---

### ListDocumentsRequest

Passed as query parameters. See [GET `/documents`](#get-documents) for the full query parameter table.

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `productId` | `string` | No | `null` | Filter by product identifier |
| `referenceId` | `string` | No | `null` | Filter by reference identifier |
| `referenceType` | `string` | No | `null` | Filter by reference type |
| `status` | `string` | No | `null` | Filter by document status |
| `limit` | `integer` | No | `50` | Items to return (1–200) |
| `offset` | `integer` | No | `0` | Items to skip |

---

### UpdateDocumentRequest

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `title` | `string` | No | Yes | Document title (max 500 chars) |
| `description` | `string` | No | Yes | Document description (max 2000 chars) |
| `documentTypeId` | `guid` | No | Yes | Document type identifier |
| `status` | `string` | No | Yes | Document status (`DRAFT`, `ACTIVE`, `ARCHIVED`, `LEGAL_HOLD`) |
| `retainUntil` | `datetime` | No | Yes | Retention date |

---

### UploadDocumentVersionRequest

Submitted as `multipart/form-data` fields (not JSON). See [POST `/documents/{id}/versions`](#post-documentsidversions) for the full form field table.

| Field | Type | Required | Description |
|---|---|---|---|
| `label` | `string` | No | Optional version label |

---

### DocumentResponse

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | `guid` | No | Unique identifier |
| `tenantId` | `guid` | No | Tenant identifier |
| `productId` | `string` | No | Product identifier |
| `referenceId` | `string` | No | Reference identifier |
| `referenceType` | `string` | No | Reference type |
| `documentTypeId` | `guid` | No | Document type identifier |
| `title` | `string` | No | Document title |
| `description` | `string` | Yes | Document description |
| `status` | `string` | No | Document status (see [DocumentStatus](#documentstatus)) |
| `mimeType` | `string` | No | File MIME type |
| `fileSizeBytes` | `long` | No | File size in bytes |
| `currentVersionId` | `guid` | Yes | ID of the current (latest) version |
| `versionCount` | `integer` | No | Total number of versions |
| `scanStatus` | `string` | No | Virus scan status (see [ScanStatus](#scanstatus)) |
| `scanCompletedAt` | `datetime` | Yes | When the scan completed |
| `scanThreats` | `string[]` | No | List of detected threat names (empty if clean) |
| `isDeleted` | `boolean` | No | Whether the document has been soft-deleted |
| `deletedAt` | `datetime` | Yes | When the document was deleted |
| `deletedBy` | `guid` | Yes | User ID who deleted the document |
| `retainUntil` | `datetime` | Yes | Retention date |
| `legalHoldAt` | `datetime` | Yes | When legal hold was applied |
| `createdAt` | `datetime` | No | Record creation timestamp |
| `createdBy` | `guid` | No | User ID who created the document |
| `updatedAt` | `datetime` | No | Record last-updated timestamp |
| `updatedBy` | `guid` | No | User ID who last updated the document |

> **Note:** Internal fields `storageKey`, `storageBucket`, and `checksum` are intentionally omitted and never exposed to clients.

---

### DocumentListResponse

| Field | Type | Description |
|---|---|---|
| `data` | `DocumentResponse[]` | Array of document items for the current page |
| `total` | `integer` | Total number of matching documents |
| `limit` | `integer` | Number of items requested |
| `offset` | `integer` | Number of items skipped |

---

### DocumentVersionResponse

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | `guid` | No | Version unique identifier |
| `documentId` | `guid` | No | Parent document identifier |
| `tenantId` | `guid` | No | Tenant identifier |
| `versionNumber` | `integer` | No | Sequential version number |
| `mimeType` | `string` | No | File MIME type |
| `fileSizeBytes` | `long` | No | File size in bytes |
| `scanStatus` | `string` | No | Virus scan status (see [ScanStatus](#scanstatus)) |
| `scanCompletedAt` | `datetime` | Yes | When the scan completed |
| `scanDurationMs` | `integer` | Yes | Scan duration in milliseconds |
| `scanThreats` | `string[]` | No | List of detected threat names (empty if clean) |
| `scanEngineVersion` | `string` | Yes | Version of the scan engine used |
| `label` | `string` | Yes | Optional version label |
| `isDeleted` | `boolean` | No | Whether the version has been soft-deleted |
| `deletedAt` | `datetime` | Yes | When the version was deleted |
| `deletedBy` | `guid` | Yes | User ID who deleted the version |
| `uploadedAt` | `datetime` | No | When the version was uploaded |
| `uploadedBy` | `guid` | No | User ID who uploaded the version |

---

### IssuedTokenResponse

| Field | Type | Nullable | Description |
|---|---|---|---|
| `accessToken` | `string` | No | Opaque access token (64 lowercase hex characters) |
| `redeemUrl` | `string` | No | Relative path to redeem the token (e.g., `/access/{token}`) |
| `expiresInSeconds` | `integer` | No | Token validity duration in seconds |
| `type` | `string` | No | Access type — `view` or `download` |
