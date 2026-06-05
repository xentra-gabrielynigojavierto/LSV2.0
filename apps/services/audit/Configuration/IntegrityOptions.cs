namespace PlatformAuditEventService.Configuration;

/// <summary>
/// Tamper-evidence / integrity hash options.
/// Bound from "Integrity" section in appsettings.
/// Environment variable override prefix: Integrity__
/// </summary>
public sealed class IntegrityOptions
{
    public const string SectionName = "Integrity";

    /// <summary>
    /// Base64-encoded 256-bit (32-byte) HMAC secret used to compute
    /// the integrity hash on every persisted AuditEvent.
    /// Must be set to a cryptographically random value in production.
    /// Generate: openssl rand -base64 32
    /// Environment variable: Integrity__HmacKeyBase64
    /// </summary>
    public string? HmacKeyBase64 { get; set; }

    /// <summary>
    /// Hash algorithm applied to the canonical payload string.
    ///
    /// Supported values:
    ///   "HMAC-SHA256" (default) — HMAC-SHA256 using the secret from HmacKeyBase64.
    ///       Provides integrity AND authentication. Required for production.
    ///       When HmacKeyBase64 is absent, signing is silently skipped (development mode).
    ///   "SHA-256"               — Keyless SHA-256. Provides integrity but not authentication.
    ///       Always active; no secret required. Suitable for development and CI environments.
    ///
    /// PreviousHash is included in the canonical payload for both algorithms, so Hash(N)
    /// depends on Hash(N-1) forming a singly-linked cryptographic chain.
    ///
    /// Environment variable override: Integrity__Algorithm
    /// </summary>
    public string Algorithm { get; set; } = "HMAC-SHA256";

    /// <summary>
    /// When true, integrity hash is verified on every read (GetById, Query).
    /// Failed verification logs a CRITICAL alert but does NOT suppress the record.
    /// Performance impact: one HMAC-SHA256 per record returned.
    /// Recommended: true in production, false in high-throughput dev/test.
    /// </summary>
    public bool VerifyOnRead { get; set; } = false;

    /// <summary>
    /// When true, records with missing or mismatched IntegrityHash are flagged
    /// with a "TamperSuspected" marker in the response.
    /// Only applies when VerifyOnRead = true.
    /// </summary>
    public bool FlagTamperedRecords { get; set; } = true;

    // ── Background checkpoint scheduling ──────────────────────────────────────

    /// <summary>
    /// When true, <see cref="Jobs.IntegrityCheckpointHostedService"/> will run
    /// checkpoint generation automatically on the configured interval.
    /// Default: false (opt-in).
    /// Environment variable: Integrity__AutoCheckpointEnabled
    /// </summary>
    public bool AutoCheckpointEnabled { get; set; } = false;

    /// <summary>
    /// Interval in minutes between automatic checkpoint runs.
    /// Default: 60 (hourly).
    /// Environment variable: Integrity__CheckpointIntervalMinutes
    /// </summary>
    public int CheckpointIntervalMinutes { get; set; } = 60;
}
