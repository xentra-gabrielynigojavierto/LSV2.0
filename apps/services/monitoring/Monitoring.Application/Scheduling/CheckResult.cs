using Monitoring.Domain.Monitoring;

namespace Monitoring.Application.Scheduling;

/// <summary>
/// Structured, transient outcome of a single per-entity check
/// execution. Produced by every <see cref="IMonitoredEntityExecutor"/>
/// implementation and aggregated in memory by the cycle executor for
/// the current cycle's summary log.
///
/// <para><b>Transient</b>: this type is not persisted in this feature.
/// It carries no audit fields, no identity, and no rowversion. Adding
/// persistence is a deliberate later feature (status model / history).</para>
///
/// <para><b>Safety</b>: <see cref="Message"/> and <see cref="ErrorType"/>
/// are short, stable, operator-facing strings. They must not contain
/// secrets, raw URLs/query strings, or raw exception messages — the
/// existing per-executor sanitization rules continue to apply.</para>
/// </summary>
/// <param name="EntityId">Stable identifier of the executed entity.</param>
/// <param name="EntityName">Display name (already trimmed by domain invariant).</param>
/// <param name="MonitoringType">Monitoring type the executor saw.</param>
/// <param name="Target">Raw target string from the entity. Aggregation
/// does not log this — only per-executor sanitized logs reference the
/// target. Carried here so future consumers (persistence, status)
/// don't have to re-fetch the entity.</param>
/// <param name="Succeeded">Convenience flag equivalent to
/// <c>Outcome == CheckOutcome.Success</c>. Producers must set it consistently.</param>
/// <param name="Outcome">Stable classification — the source of truth.</param>
/// <param name="StatusCode">Protocol status code where applicable
/// (HTTP today). Null for outcomes where it does not apply
/// (timeout, invalid target, network failure, skipped).</param>
/// <param name="ElapsedMs">Wall-clock duration of the executor call.
/// Zero for outcomes that exit before any I/O (skipped, invalid target).</param>
/// <param name="CheckedAtUtc">Wall-clock UTC time the result was finalized.</param>
/// <param name="Message">Short, safe, human-readable summary.</param>
/// <param name="ErrorType">Optional stable classifier for failure
/// rows (e.g. <c>HttpRequestError</c> enum name, exception type name).</param>
public sealed record CheckResult(
    Guid EntityId,
    string EntityName,
    MonitoringType MonitoringType,
    string Target,
    bool Succeeded,
    CheckOutcome Outcome,
    int? StatusCode,
    long ElapsedMs,
    DateTime CheckedAtUtc,
    string Message,
    string? ErrorType = null);
