# Audit Export API — Operator Reference

## Overview

The export subsystem lets callers request a filtered extract of audit event records
in JSON, CSV, or NDJSON format. Exports are written to a configurable storage backend
(local filesystem in v1; S3 / Azure Blob in future releases).

---

## Endpoints

### `POST /audit/exports`

Submit a new export job. The service processes the export synchronously (v1) and
returns `202 Accepted` with the terminal job status.

**Request body** (`application/json`):

```json
{
  "scopeType": "Tenant",
  "scopeId":   "tenant-abc",
  "format":    "Json",

  "category":    null,
  "minSeverity": null,
  "eventTypes":  ["user.login.succeeded", "user.login.failed"],
  "actorId":     null,
  "entityType":  null,
  "entityId":    null,
  "from":        "2026-01-01T00:00:00Z",
  "to":          "2026-04-01T00:00:00Z",
  "correlationId": null,

  "includeStateSnapshots": true,
  "includeTags":           true,
  "includeHashes":         false
}
```

**Fields:**

| Field | Type | Default | Notes |
|---|---|---|---|
| `scopeType` | string | `Tenant` | `Global`, `Platform`, `Tenant`, `Organization`, `User`, `Service` |
| `scopeId` | string\|null | null | Required when `scopeType` is `Tenant`, `Organization`, `User`, or `Service` |
| `format` | string | `Json` | `Json`, `Csv`, or `Ndjson` |
| `category` | string\|null | null | `Authentication`, `DataChange`, `Access`, etc. |
| `minSeverity` | string\|null | null | `Low`, `Medium`, `High`, `Critical` |
| `eventTypes` | string[]\|null | null | Up to 20 dot-notation event codes |
| `actorId` | string\|null | null | Filter to a specific actor |
| `entityType` | string\|null | null | e.g. `User`, `Document` |
| `entityId` | string\|null | null | Specific resource ID |
| `from` | ISO 8601\|null | null | `OccurredAtUtc` range start (inclusive) |
| `to` | ISO 8601\|null | null | `OccurredAtUtc` range end (exclusive) |
| `correlationId` | string\|null | null | Distributed trace filter |
| `includeStateSnapshots` | bool | `true` | Include `beforeJson` / `afterJson` |
| `includeTags` | bool | `true` | Include `tags` array |
| `includeHashes` | bool | `false` | Include `hash` / `previousHash` (requires `QueryAuth:ExposeIntegrityHash=true`) |

**Response `202 Accepted`** (`ExportStatusResponse`):

```json
{
  "exportId":       "3f4e5a6b-7c8d-...",
  "scopeType":      "Tenant",
  "scopeId":        "tenant-abc",
  "format":         "Json",
  "status":         "Completed",
  "statusLabel":    "Completed",
  "downloadUrl":    "/path/to/exports/audit-export_3f4e5a6b_20260330T161234.json",
  "recordCount":    4821,
  "errorMessage":   null,
  "createdAtUtc":   "2026-03-30T16:12:30Z",
  "completedAtUtc": "2026-03-30T16:12:34Z",
  "isTerminal":     true,
  "isAvailable":    true
}
```

---

### `GET /audit/exports/{exportId}`

Poll the status of an existing export job.

**Path parameters:**

| Parameter | Type | Notes |
|---|---|---|
| `exportId` | GUID | The `exportId` from the `POST` response |

**Response `200 OK`**: same `ExportStatusResponse` body as above.

**Response `404 Not Found`**: when no job with the given `exportId` exists.

---

## Job lifecycle

```
[POST /audit/exports]
       │
       ▼
   Pending  ──→  Processing  ──→  Completed
                                 └──→  Failed
```

| Status | Description |
|---|---|
| `Pending` | Job created, not yet processed. |
| `Processing` | Export worker is streaming and writing the output file. |
| `Completed` | Output file produced. `downloadUrl` and `recordCount` are populated. |
| `Failed` | Processing failed. `errorMessage` contains the reason. |
| `Cancelled` | (Future) Job cancelled before completion. |
| `Expired` | (Future) Output file purged after retention period. |

In v1, the full Pending → Processing → Completed/Failed transition happens
synchronously within the `POST` request. The `GET` endpoint is provided for
clients that prefer a polling pattern and for forward compatibility with async
processing in future releases.

---

## Output formats

### JSON (default)

Full envelope with metadata header and record array:

```json
{
  "exportId": "3f4e5a6b-...",
  "exportedAtUtc": "2026-03-30T16:12:34.000Z",
  "format": "Json",
  "records": [
    {
      "auditId": "...",
      "eventType": "user.login.succeeded",
      "sourceSystem": "identity-service",
      "tenantId": "tenant-abc",
      "actorId": "user-123",
      "actorType": "User",
      "action": "LoginSucceeded",
      "description": "User authenticated successfully.",
      "severity": "Low",
      "occurredAtUtc": "2026-03-30T12:00:00.000+00:00",
      "recordedAtUtc": "2026-03-30T12:00:00.050+00:00",
      "isReplay": false
    }
  ]
}
```

### NDJSON (Newline-Delimited JSON)

One JSON record per line, no envelope. Best for streaming ingest (Spark, BigQuery,
Firehose, etc.).

```
{"auditId":"...","eventType":"user.login.succeeded",...}
{"auditId":"...","eventType":"document.uploaded",...}
```

### CSV

Flat tabular format. Header row + one data row per record.
Nested JSON fields (`beforeJson`, `afterJson`, `metadataJson`) are included
as raw JSON strings in their respective columns. Fields containing commas or
double-quotes are RFC 4180-escaped.

```csv
auditId,eventId,eventType,eventCategory,sourceSystem,...,isReplay
3f4e5a6b-...,,"user.login.succeeded","Authentication","identity-service",...,false
```

---

## Authorization

Export endpoints are gated by the same `QueryAuth` middleware that protects the
query endpoints. The caller's resolved scope determines which records are accessible:

| Scope | Allowed exports |
|---|---|
| `PlatformAdmin` | Global / any tenant |
| `TenantAdmin` | Own tenant only |
| `OrganizationAdmin` | Own organization only |
| `TenantUser` | Own tenant, user-visible records only |
| `UserSelf` | Own actor records only |

Cross-tenant export requests from non-PlatformAdmin callers are denied with `403 Forbidden`.

Hash fields (`hash`, `previousHash`) are only included when:
- `includeHashes: true` in the request, **and**
- `QueryAuth:ExposeIntegrityHash: true` in configuration.

---

## Configuration

```json
{
  "Export": {
    "Provider":         "None",
    "LocalOutputPath":  "exports",
    "MaxRecordsPerFile": 100000,
    "SupportedFormats": ["Json", "Csv", "Ndjson"],
    "FileNamePrefix":   "audit-export",

    "S3BucketName":     null,
    "S3KeyPrefix":      null,
    "AwsRegion":        null,

    "AzureBlobConnectionString": null,
    "AzureContainerName":        null
  }
}
```

| Key | Default | Notes |
|---|---|---|
| `Provider` | `None` | `None` (disabled), `Local`, `S3` (future), `AzureBlob` (future) |
| `LocalOutputPath` | `exports` | Relative or absolute directory path |
| `MaxRecordsPerFile` | 100000 | Record limit per file. Documented; not yet enforced in v1. |
| `SupportedFormats` | all three | Instance-level format whitelist |
| `FileNamePrefix` | `audit-export` | Used in output file naming |

**To enable local exports in development:**

```json
{
  "Export": {
    "Provider": "Local",
    "LocalOutputPath": "exports"
  }
}
```

**To disable exports entirely:**

```json
{
  "Export": { "Provider": "None" }
}
```

When `Provider = "None"`, all export endpoints return `503 Service Unavailable`.

---

## File naming

```
{FileNamePrefix}_{ExportId:N}_{yyyyMMddTHHmmss}.{ext}
```

Examples:
```
audit-export_3f4e5a6b7c8d9e0f11223344aabbccdd_20260330T161234.json
audit-export_3f4e5a6b7c8d9e0f11223344aabbccdd_20260330T161234.csv
audit-export_3f4e5a6b7c8d9e0f11223344aabbccdd_20260330T161234.ndjson
```

---

## Extension points

### Plugging in S3 / Azure Blob storage

1. Implement `IExportStorageProvider` in a new class (e.g. `S3ExportStorageProvider`).
2. Register it in `Program.cs` as a singleton, conditionally on `Export:Provider`:

```csharp
builder.Services.AddSingleton<IExportStorageProvider>(sp =>
    exportProvider.ToUpperInvariant() switch
    {
        "S3"        => sp.GetRequiredService<S3ExportStorageProvider>(),
        "AZUREBLOB" => sp.GetRequiredService<AzureBlobExportStorageProvider>(),
        _           => sp.GetRequiredService<LocalExportStorageProvider>(),
    });
```

3. The rest of the pipeline (service, formatter, controller) requires no changes.

### Asynchronous processing

To process exports asynchronously (queue-based):

1. Extract `ProcessJobAsync` from `AuditExportService` into a `BackgroundService` or Quartz.NET job.
2. In `SubmitAsync`, only create the `AuditExportJob` in Pending state and return immediately.
3. The background job picks up Pending jobs via `IAuditExportJobRepository.ListActiveAsync()`.
4. The `GET /audit/exports/{exportId}` endpoint is already designed for polling — no API changes needed.

### Adding new output formats

1. Add the format string to `ExportOptions.SupportedFormats`.
2. Add a new `Write{Format}Async` method to `AuditExportFormatter`.
3. Add a new case to the `format.ToUpperInvariant() switch` in `AuditExportFormatter.WriteAsync`.
4. Add the file extension mapping to `LocalExportStorageProvider.ExtensionFor`.
