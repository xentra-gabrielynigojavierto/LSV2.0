using System.Security.Cryptography;
using System.Text;
using PlatformAuditEventService.Entities;

namespace PlatformAuditEventService.Utilities;

/// <summary>
/// Produces deterministic, tamper-evident integrity hashes for <see cref="AuditEventRecord"/>
/// instances using a two-step pipeline.
///
/// ┌─────────────────────────────────────────────────────────────────────────────────────────┐
/// │  Step 1 — BuildPayload()                                                                │
/// │           Assembles the canonical pipe-delimited payload string from fixed fields.      │
/// │           Public so tests and diagnostics can inspect the exact value hashed.           │
/// │                                                                                         │
/// │  Step 2 — ComputeSha256() / ComputeHmacSha256()                                         │
/// │           Applies the selected hash algorithm to the payload.                           │
/// └─────────────────────────────────────────────────────────────────────────────────────────┘
///
/// Canonical field ordering (changes are BREAKING — all stored hashes become invalid):
///   AuditId | EventType | SourceSystem | TenantId | ActorId |
///   EntityType | EntityId | Action | OccurredAtUtc | RecordedAtUtc | PreviousHash
///
/// PreviousHash is the last field in the payload.  Including it ensures Hash(N) depends on
/// Hash(N-1): modifying any historical record invalidates all subsequent hashes in the same
/// (TenantId, SourceSystem) chain, giving the chain true cryptographic tamper-evidence.
///
/// Null optional fields are represented as the empty string (not omitted) to prevent
/// injection attacks that would shift field positions.
///
/// Supported algorithms (select via Integrity:Algorithm in appsettings):
///   "SHA-256"     — keyless; portable; suitable for development and environments
///                   where secrets management is unavailable. Provides integrity but
///                   not authentication — a party with write access could recompute.
///   "HMAC-SHA256" — requires a 256-bit HMAC secret (Integrity:HmacKeyBase64).
///                   Provides both integrity AND authentication: without the secret
///                   a write-privileged attacker still cannot forge a valid hash.
///                   Recommended for production and regulated environments.
///
/// Backward-compatibility note:
///   The legacy <see cref="IntegrityHasher"/> covers the old <c>AuditEvent</c> flat model
///   and is retained for backward compatibility only.  Do not use it for new code.
/// </summary>
public static class AuditRecordHasher
{
    private const char Separator = '|';

    /// <summary>Algorithm identifier for keyless SHA-256.</summary>
    public const string AlgoSha256 = "SHA-256";

    /// <summary>Algorithm identifier for HMAC-SHA256 (requires secret key).</summary>
    public const string AlgoHmacSha256 = "HMAC-SHA256";

    // ── Payload builder ───────────────────────────────────────────────────────
    // Public so callers (tests, integrity verifiers, diagnostics) can inspect
    // the exact string that is fed to the hash function — no black box.

    /// <summary>
    /// Builds the canonical pipe-delimited payload string from individual field values.
    ///
    /// This overload must be called BEFORE the entity is created, because the ingest
    /// service generates <paramref name="auditId"/> and <paramref name="recordedAtUtc"/>
    /// independently to ensure the hash covers the exact values that will be persisted.
    ///
    /// <paramref name="previousHash"/>: pass <c>null</c> for the genesis record in a new
    /// chain; pass the Hash of the immediately preceding record for all subsequent records.
    /// This field is serialised as an empty string when null so the payload length is stable.
    /// </summary>
    public static string BuildPayload(
        Guid           auditId,
        string         eventType,
        string         sourceSystem,
        string?        tenantId,
        string?        actorId,
        string?        entityType,
        string?        entityId,
        string         action,
        DateTimeOffset occurredAtUtc,
        DateTimeOffset recordedAtUtc,
        string?        previousHash)
    {
        return string.Join(Separator,
            auditId.ToString("D"),          // lowercase GUID with hyphens — canonical
            eventType,
            sourceSystem,
            tenantId      ?? string.Empty,
            actorId       ?? string.Empty,
            entityType    ?? string.Empty,
            entityId      ?? string.Empty,
            action,
            occurredAtUtc.ToString("O"),    // ISO 8601 round-trip, offset preserved
            recordedAtUtc.ToString("O"),
            previousHash  ?? string.Empty); // empty = genesis; non-empty chains N→N-1
    }

    /// <summary>
    /// Overload that builds the payload directly from a persisted <see cref="AuditEventRecord"/>.
    ///
    /// Used during verification on read (<c>IntegrityOptions.VerifyOnRead = true</c>).
    /// Uses the record's own <see cref="AuditEventRecord.PreviousHash"/> so the recomputed
    /// hash is deterministic and matches the hash written at ingest time.
    /// </summary>
    public static string BuildPayload(AuditEventRecord record) =>
        BuildPayload(
            record.AuditId,
            record.EventType,
            record.SourceSystem,
            record.TenantId,
            record.ActorId,
            record.EntityType,
            record.EntityId,
            record.Action,
            record.OccurredAtUtc,
            record.RecordedAtUtc,
            record.PreviousHash);

    // ── Hash functions ────────────────────────────────────────────────────────
    // Public so algorithm choice can be made by the calling layer (ingest service,
    // verification service) without tying it to this class's internals.

    /// <summary>
    /// Applies SHA-256 to the given payload string.
    ///
    /// Keyless — no secret required.  Portable across all deployment environments.
    /// Returns a lowercase hexadecimal string (64 chars).
    ///
    /// Security note: SHA-256 without a key provides INTEGRITY (tamper detection)
    /// but not AUTHENTICATION.  A write-privileged attacker could replace a record
    /// and recompute a matching hash.  Use <see cref="ComputeHmacSha256"/> in production
    /// environments where authentication is required.
    /// </summary>
    public static string ComputeSha256(string payload)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Applies HMAC-SHA256 to the given payload string using the provided secret.
    ///
    /// Provides both INTEGRITY and AUTHENTICATION: without the secret an attacker
    /// with write access to the record store cannot forge a valid hash.
    /// <paramref name="hmacSecret"/> should be a cryptographically random 32-byte value.
    /// Generate: <c>openssl rand -base64 32</c>
    ///
    /// Returns a lowercase hexadecimal string (64 chars).
    /// </summary>
    public static string ComputeHmacSha256(string payload, byte[] hmacSecret)
    {
        using var hmac = new HMACSHA256(hmacSecret);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // ── Verification ──────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that a persisted record's <see cref="AuditEventRecord.Hash"/> matches
    /// a freshly recomputed hash using the specified algorithm.
    ///
    /// Uses constant-time comparison to resist timing-based side-channel attacks.
    ///
    /// Returns <c>false</c> when:
    ///   - The record's Hash is null (signing was disabled at ingest time).
    ///   - Algorithm is <c>"HMAC-SHA256"</c> but <paramref name="hmacSecret"/> is null.
    ///   - The algorithm identifier is unrecognised.
    ///   - The recomputed hash does not match the stored hash.
    /// </summary>
    /// <param name="record">The persisted record to verify.</param>
    /// <param name="algorithm">
    /// Hash algorithm used at ingest time.  Must be <c>"SHA-256"</c> or <c>"HMAC-SHA256"</c>.
    /// </param>
    /// <param name="hmacSecret">
    /// HMAC secret bytes.  Required when <paramref name="algorithm"/> is <c>"HMAC-SHA256"</c>;
    /// ignored for <c>"SHA-256"</c>.
    /// </param>
    public static bool Verify(
        AuditEventRecord record,
        string           algorithm,
        byte[]?          hmacSecret = null)
    {
        if (record.Hash is null)
            return false;

        var payload = BuildPayload(record);

        string? expected;
        if (algorithm.Equals(AlgoSha256, StringComparison.OrdinalIgnoreCase))
        {
            expected = ComputeSha256(payload);
        }
        else if (algorithm.Equals(AlgoHmacSha256, StringComparison.OrdinalIgnoreCase))
        {
            if (hmacSecret is null)
                return false;

            expected = ComputeHmacSha256(payload, hmacSecret);
        }
        else
        {
            // Unknown algorithm — cannot verify; treat as unverifiable (not tampered)
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(record.Hash));
    }
}
