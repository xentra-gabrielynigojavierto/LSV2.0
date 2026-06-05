# Audit Event Integrity Model

Platform Audit/Event Service — tamper-evidence and hash-chain specification.

---

## Overview

Every persisted `AuditEventRecord` carries two integrity fields:

| Field          | Type     | Purpose |
|----------------|----------|---------|
| `Hash`         | `string?` | Cryptographic hash of this record's canonical fields plus `PreviousHash`. |
| `PreviousHash` | `string?` | Hash of the immediately preceding record in the same `(TenantId, SourceSystem)` chain. |

Together they form a **singly-linked hash chain** per `(TenantId, SourceSystem)` scope. Modifying any historical record invalidates all subsequent hashes in the chain — without requiring a global ledger or consensus mechanism.

---

## Hash Algorithm

Two algorithms are supported, selected via `Integrity:Algorithm` in `appsettings.json`:

### `HMAC-SHA256` (default — production)

- Applies HMAC-SHA256 using a 256-bit secret key (`Integrity:HmacKeyBase64`).
- Provides **integrity** (tamper detection) AND **authentication** (forgery resistance).  
  Even a write-privileged attacker who can modify database rows cannot forge a valid hash without the secret.
- Requires key provisioning. Generate: `openssl rand -base64 32`.
- When `HmacKeyBase64` is absent, signing is silently disabled (records get `Hash = null`).

### `SHA-256` (portable — development / CI)

- Applies standard SHA-256 with no key.
- Provides **integrity** only — a write-privileged attacker can replace a record and recompute a matching hash.
- Always active; no configuration required.
- Set `Integrity:Algorithm = SHA-256` in development or air-gapped environments.

Both algorithms produce a **lowercase hexadecimal string (64 characters)**.

---

## Canonical Payload

The hash is computed over a **pipe-delimited canonical string** assembled by `AuditRecordHasher.BuildPayload()`.

### Field order (must not change — changes break all stored hashes)

```
{AuditId}|{EventType}|{SourceSystem}|{TenantId}|{ActorId}|{EntityType}|{EntityId}|{Action}|{OccurredAtUtc}|{RecordedAtUtc}|{PreviousHash}
```

| Position | Field           | Format / Notes |
|----------|-----------------|----------------|
| 0        | `AuditId`       | `Guid.ToString("D")` — lowercase with hyphens. |
| 1        | `EventType`     | Verbatim string. |
| 2        | `SourceSystem`  | Verbatim string. |
| 3        | `TenantId`      | Empty string when null (single-tenant deployments). |
| 4        | `ActorId`       | Empty string when null. |
| 5        | `EntityType`    | Empty string when null. |
| 6        | `EntityId`      | Empty string when null. |
| 7        | `Action`        | Verbatim string. |
| 8        | `OccurredAtUtc` | `DateTimeOffset.ToString("O")` — ISO 8601 round-trip, offset preserved. |
| 9        | `RecordedAtUtc` | `DateTimeOffset.ToString("O")` — ISO 8601 round-trip, offset preserved. |
| 10       | `PreviousHash`  | Hash of the preceding record; empty string for the genesis (first) record. |

**Null-to-empty-string substitution** is applied for all nullable fields (positions 3–7, 10).  
This prevents injection attacks that would otherwise shift field positions by omitting a null field.

### Example payload

```
a1b2c3d4-e5f6-7890-abcd-ef1234567890|user.login.succeeded|identity-service|tenant-42|user-99||User|LoginSucceeded|2026-03-30T12:00:00.0000000+00:00|2026-03-30T12:00:00.1234567+00:00|
```

(Last field is empty — genesis record, no predecessor.)

---

## Chain Logic

### Chain scope

The chain is scoped to `(TenantId, SourceSystem)`. Isolation by tenant and source system prevents cross-tenant or cross-service chain entanglement.

### Chain head lookup

Before computing `Hash(N)`, the ingest service queries `GetLatestInChainAsync(tenantId, sourceSystem)` — returns the record with the highest surrogate `Id` (insertion order) in the chain. This record's `Hash` becomes `PreviousHash` for the new record.

### Genesis record

The first record in a new `(TenantId, SourceSystem)` chain has `PreviousHash = null` (stored) and `""` (in the payload). It still receives a `Hash` over its own fields.

### Chain traversal

```
Record 1 (genesis):   PreviousHash = null   Hash = H1 = sha256("...|")
Record 2:             PreviousHash = H1     Hash = H2 = sha256("...|H1")
Record 3:             PreviousHash = H2     Hash = H3 = sha256("...|H2")
```

Modifying Record 2's fields changes H2, which invalidates H3 (since H3 includes H2 in its payload), and so on for all subsequent records.

### Replay records

Records with `IsReplay = true` participate in the chain identically to normal records — they get a new `AuditId`, new `RecordedAtUtc`, and are chained after the current head. The `IsReplay` flag is a semantic marker only.

---

## Ingest Pipeline (Step 4 detail)

```
Step 2: auditId      = Guid.NewGuid()
        recordedAtUtc = DateTimeOffset.UtcNow

Step 3: chainHead    = repository.GetLatestInChainAsync(tenantId, sourceSystem)
        previousHash = chainHead?.Hash          // null for genesis

Step 4: payload      = AuditRecordHasher.BuildPayload(auditId, ..., previousHash)
        hash         = algorithm == "SHA-256"
                       ? AuditRecordHasher.ComputeSha256(payload)
                       : AuditRecordHasher.ComputeHmacSha256(payload, hmacSecret)

Step 5: entity = AuditEventRecordMapper.ToEntity(req, auditId, now,
                     hash: hash, previousHash: previousHash)

Step 6: repository.AppendAsync(entity)          // insert-only, no UPDATE
```

No record is ever mutated after `AppendAsync`. The `Hash` and `PreviousHash` fields on `AuditEventRecord` are `init`-only.

---

## Verification

`AuditRecordHasher.Verify(record, algorithm, hmacSecret?)` rebuilds the payload from the persisted record (including `record.PreviousHash`) and compares the result against `record.Hash` using **constant-time comparison** (`CryptographicOperations.FixedTimeEquals`) to resist timing-based side-channel attacks.

When `IntegrityOptions.VerifyOnRead = true`, verification is performed on every record returned by the query and get-by-id endpoints. Failed verification logs a `CRITICAL` alert but does not suppress the record from the response — the caller receives a `TamperSuspected` marker when `FlagTamperedRecords = true`.

---

## Configuration Reference

```json
"Integrity": {
  "Algorithm": "HMAC-SHA256",
  "HmacKeyBase64": "<openssl rand -base64 32>",
  "VerifyOnRead": true,
  "FlagTamperedRecords": true
}
```

| Key                  | Default        | Description |
|----------------------|----------------|-------------|
| `Algorithm`          | `HMAC-SHA256`  | Hash algorithm. `SHA-256` or `HMAC-SHA256`. |
| `HmacKeyBase64`      | `""`           | Base64-encoded 32-byte HMAC secret. Required for `HMAC-SHA256`. |
| `VerifyOnRead`       | `false`        | Recompute and verify hash on every read. Performance: one hash per record. |
| `FlagTamperedRecords`| `true`         | Include `tamperSuspected: true` in query responses when verification fails. |

---

## Security Properties and Limitations

| Property | SHA-256 | HMAC-SHA256 |
|----------|---------|-------------|
| Tamper detection (single record) | ✓ | ✓ |
| Chain tamper detection | ✓ | ✓ |
| Authentication (forgery resistance) | ✗ | ✓ |
| Key rotation supported | N/A | ✗ (planned) |
| Works without configuration | ✓ | ✗ |

### Known limitations

1. **Key rotation not yet supported.** Rotating the HMAC key invalidates all existing hashes. A future `HashVersion` column and multi-key verification strategy is planned.
2. **Race window in batch ingest.** Two concurrent batches for the same `(TenantId, SourceSystem)` may both read the same chain head. Both will store the same `PreviousHash`, creating a fork rather than a linear chain. This creates a detectably branched chain but does not prevent verification of either branch individually. A table-level chain lock is a future mitigation.
3. **In-memory provider for tests.** The `GetLatestInChainAsync` query relies on `Id` ordering (auto-increment surrogate). In-memory EF does not guarantee Id ordering — use MySQL in integration tests if chain ordering matters.
4. **Fields outside the canonical set are not hashed.** `Description`, `BeforeJson`, `AfterJson`, `MetadataJson`, `TagsJson`, `CorrelationId`, `SessionId`, `ActorName`, `ActorIpAddress`, and `ActorUserAgent` are NOT included in the hash. Modification of these fields is not detectable through the hash chain. This is intentional — these fields may need to be redacted for PII compliance without invalidating the integrity record. A separate redaction log pattern is recommended for those use cases.
