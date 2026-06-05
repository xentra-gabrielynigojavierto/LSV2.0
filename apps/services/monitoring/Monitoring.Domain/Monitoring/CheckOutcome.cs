namespace Monitoring.Domain.Monitoring;

/// <summary>
/// Stable, explicit classification of a single per-entity check
/// execution. The values are the single source of truth shared
/// between executors (which produce them), the cycle aggregation
/// (which counts them), and structured logs (which include them).
///
/// <para>Numeric values are explicit so the enum stays stable across
/// reorderings; <see cref="UnexpectedFailure"/> uses 99 to mirror the
/// "<c>Other = 99</c>" convention used by the domain enums and to
/// reserve the low numbers for expected outcomes.</para>
/// </summary>
public enum CheckOutcome
{
    /// <summary>HTTP 2xx (or future protocol equivalent) — the target is healthy.</summary>
    Success = 1,

    /// <summary>HTTP non-2xx response — the target is reachable but unhealthy.</summary>
    NonSuccessStatusCode = 2,

    /// <summary>The check did not complete within the configured timeout.</summary>
    Timeout = 3,

    /// <summary>The configured target is not a valid/usable address for this check type.</summary>
    InvalidTarget = 4,

    /// <summary>Transport-level failure (connection refused, DNS, TLS, etc.).</summary>
    NetworkFailure = 5,

    /// <summary>The entity was not executed by this adapter (e.g. non-HTTP entity for the HTTP executor).</summary>
    Skipped = 6,

    /// <summary>An exception escaped the executor that did not match any classified failure mode.</summary>
    UnexpectedFailure = 99,
}
