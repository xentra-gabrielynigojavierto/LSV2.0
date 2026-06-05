# Retention and Archival

> **Status**: v1 — Evaluation foundation. No records are deleted or archived automatically.
> All operations are read-only until production hardening is complete (see [What Remains](#what-remains-for-production-hardening)).

---

## Overview

The retention and archival system answers two questions:

1. **Retention** — How long must a given audit record be kept in the primary (hot) database?
2. **Archival** — Where do records go when they leave the primary store before being permanently deleted?

These are governed independently:

- **Retention** is policy-driven: default + per-category + per-tenant overrides, configured in `appsettings.json`.
- **Archival** is provider-driven: a pluggable `IArchivalProvider` (currently `NoOpArchivalProvider`) writes records to cold storage before the primary store removes them.

Neither system is automatic in v1. The `RetentionPolicyJob` evaluates the policy and logs results; it does not modify any data.

---

## Storage Tiers

Every audit record is classified into one of five storage tiers based on its age and the resolved retention policy:

| Tier | Condition | Action |
|---|---|---|
| **Hot** | Age ≤ `Retention:HotRetentionDays` | Full primary-store access. No action. |
| **Warm** | Age > `HotRetentionDays` AND within full retention window | Candidate for archival to secondary storage. Still queryable. |
| **Cold** | Age > full retention window (`DefaultRetentionDays` / override) | Eligible for archival + deletion. Requires explicit compliance workflow. |
| **Indefinite** | `RetentionDays = 0` (no limit) | Never purge. Equivalent to a permanent platform hold. |
| **LegalHold** | Explicitly placed on hold (future) | Exempt from all retention enforcement regardless of age. |

### Tier boundaries example

```
DefaultRetentionDays = 2555  (7 years)
HotRetentionDays     = 365   (1 year)

Age 0–365d       → Hot     (primary store, full access)
Age 366–2555d    → Warm    (eligible for archival)
Age > 2555d      → Cold    (eligible for deletion after archival)
```

---

## Retention Policy Resolution

When computing the expiration date for a record, the following priority order applies:

1. **Legal hold** — if the record is on a legal hold (future), it never expires.
2. **Per-tenant override** — `Retention:TenantOverrides[tenantId]` (days).
3. **Per-category override** — `Retention:CategoryOverrides[categoryName]` (days).
4. **Default** — `Retention:DefaultRetentionDays`.
5. **0 → Indefinite** — a resolved value of 0 means retain forever.

Override values are in days. A value of `0` in any position means "retain indefinitely for this scope."

---

## Configuration Reference

### Retention section

| Key | Type | Default | Description |
|---|---|---|---|
| `DefaultRetentionDays` | int | `0` | Platform-wide default. `0` = indefinite. |
| `HotRetentionDays` | int | `365` | Days before a record moves from Hot → Warm tier. |
| `CategoryOverrides` | dict | `{}` | Per-category overrides. Key = category name string. |
| `TenantOverrides` | dict | `{}` | Per-tenant overrides. Key = tenantId. |
| `JobEnabled` | bool | `false` | Whether the retention evaluation job runs on schedule. |
| `JobCronUtc` | string | `"0 2 * * *"` | Cron expression for job schedule (02:00 UTC daily). |
| `MaxDeletesPerRun` | int | `10000` | Sample limit for one evaluation pass (future: max deletes). |
| `ArchiveBeforeDelete` | bool | `false` | When true, archive before deleting from hot store. |
| `DryRun` | bool | `true` | When true, evaluate only — no archival or deletion. **Always true in v1.** |
| `LegalHoldEnabled` | bool | `false` | Reserved. Legal hold is not implemented in v1. |

### Archival section

| Key | Type | Default | Description |
|---|---|---|---|
| `Strategy` | enum | `NoOp` | `None`, `NoOp`, `LocalCopy`, `S3`, `AzureBlob`. |
| `BatchSize` | int | `10000` | Records per archival batch. |
| `LocalOutputPath` | string | `"archive"` | Root directory when `Strategy=LocalCopy`. |
| `FileNamePrefix` | string | `"audit-archive"` | Prefix for archive file names. |
| `S3BucketName` | string? | `null` | S3 bucket when `Strategy=S3`. |
| `S3KeyPrefix` | string? | `null` | S3 key prefix (folder). |
| `AwsRegion` | string? | `null` | AWS region for S3. |
| `AzureBlobConnectionString` | string? | `null` | Azure connection string (inject via env var). |
| `AzureContainerName` | string? | `null` | Azure container when `Strategy=AzureBlob`. |

### Example production-like configuration

```json
"Retention": {
  "DefaultRetentionDays": 2555,
  "HotRetentionDays": 365,
  "CategoryOverrides": {
    "Security": 3650,
    "Debug": 90
  },
  "TenantOverrides": {
    "tenant-hipaa-001": 3650
  },
  "JobEnabled": true,
  "JobCronUtc": "0 2 * * *",
  "MaxDeletesPerRun": 50000,
  "ArchiveBeforeDelete": true,
  "DryRun": true,
  "LegalHoldEnabled": false
},
"Archival": {
  "Strategy": "S3",
  "BatchSize": 10000,
  "S3BucketName": "legalsynq-audit-archive",
  "S3KeyPrefix": "audit/",
  "AwsRegion": "us-east-1"
}
```

---

## Archival Provider Model

The `IArchivalProvider` interface decouples the retention engine from any specific storage backend.

```
RetentionPolicyJob
  └── IRetentionService.EvaluateAsync()     ← always runs (dry-run safe)
        └── classify records by tier
        └── return RetentionEvaluationResult

(future, when DryRun=false && ArchiveBeforeDelete=true)
  └── IArchivalProvider.ArchiveAsync()      ← streams Cold-tier records to cold store
        └── NoOpArchivalProvider            ← v1: counts records, logs, does nothing
        └── LocalCopyArchivalProvider       ← writes to local directory (planned)
        └── S3ArchivalProvider              ← uploads to S3 (planned)
        └── AzureBlobArchivalProvider       ← uploads to Azure Blob (planned)
  └── (purge from hot store)                ← future: requires explicit compliance workflow
```

To implement a new archival backend:

1. Implement `IArchivalProvider` in a new class under `Services/Archival/`.
2. In `Program.cs`, add a conditional registration:
   ```csharp
   var strategy = archivalOpts.Strategy;
   builder.Services.AddSingleton<IArchivalProvider>(
       strategy == ArchivalStrategy.S3      ? new S3ArchivalProvider(...)      :
       strategy == ArchivalStrategy.AzureBlob ? new AzureBlobArchivalProvider(...) :
       new NoOpArchivalProvider(logger));
   ```
3. No other code changes are needed — `RetentionService` depends only on the interface.

---

## Legal Hold Compatibility

Legal hold prevents records from being archived or deleted regardless of their age or configured retention policy. It is the audit equivalent of an e-discovery litigation hold.

**v1 status**: The `LegalHold` storage tier and `Retention:LegalHoldEnabled` configuration key are defined, but no per-record hold tracking is implemented. No records are classified as `LegalHold` in v1.

**Future implementation requires**:

1. A `LegalHold` database entity:
   ```
   LegalHoldId   | AuditEventId | HeldBy     | HeldAtUtc | ReleasedAtUtc | LegalAuthority
   ─────────────────────────────────────────────────────────────────────────────────────
   uuid (PK)     | FK → AuditEventRecord | string | utc | utc? | string
   ```

2. A pre-check in `IRetentionService.ComputeExpirationDate`:
   ```csharp
   if (_opts.LegalHoldEnabled && await _holdRepo.IsOnHoldAsync(record.Id, ct))
       return null;  // Never expires while on hold
   ```

3. A compliance workflow for hold creation and release (not in scope for v1):
   - Hold creation: legal/compliance officer initiates via admin API.
   - Hold release: requires authorization from legal counsel + audit trail of the release itself.
   - Records must not be deleted until all holds on them are released.

---

## Evaluation Job

`RetentionPolicyJob.ExecuteAsync()` is the entry point for a scheduled retention run.

In v1, each run:
1. Checks `Retention:JobEnabled` — exits immediately if false.
2. Calls `IRetentionService.EvaluateAsync()` with `SampleLimit = MaxDeletesPerRun`.
3. Logs structured counts: `TotalInStore`, `Hot`, `Warm`, `Cold`, `Indefinite`.
4. Issues a `Warning` log for any Cold-tier (expired) records found.
5. Returns — no archival, no deletion.

**Scheduling**: The job is registered as a Transient service. A scheduler (Quartz.NET, Hangfire, or a simple `BackgroundService`) must call `ExecuteAsync()` using the cron expression in `Retention:JobCronUtc`. This wiring is not included in v1.

---

## What Remains for Production Hardening

See [`analysis/step17_retention.md`](../analysis/step17_retention.md) for the full implementation backlog.

Short list:

| Item | Priority |
|---|---|
| Wire `RetentionPolicyJob` to a real cron scheduler | High |
| Implement `LocalCopyArchivalProvider` | High |
| Implement deletion of Cold-tier records after successful archival | High |
| Add integrity checkpoint requirement before archival | High |
| Per-record legal hold entity + compliance workflow | High (HIPAA) |
| Implement `S3ArchivalProvider` / `AzureBlobArchivalProvider` | Medium |
| Archive-only mode (no deletion) for WORM-like compliance | Medium |
| Soft-delete before hard-delete (tombstone pattern) | Medium |
| Admin API: `GET /audit/retention/evaluation` | Low |
| Admin API: `POST /audit/retention/jobs` (manual trigger) | Low |
