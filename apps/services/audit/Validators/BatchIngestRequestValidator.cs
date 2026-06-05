using FluentValidation;
using PlatformAuditEventService.DTOs.Ingest;

namespace PlatformAuditEventService.Validators;

/// <summary>
/// Validates a <see cref="BatchIngestRequest"/> submitted to the batch ingest endpoint.
///
/// Validation strategy:
/// - Structural checks (count limits, BatchCorrelationId length) are applied first.
/// - Each item in <see cref="BatchIngestRequest.Events"/> is independently validated
///   using <see cref="IngestAuditEventRequestValidator"/> via RuleForEach.
/// - Per-item validation errors are returned with their zero-based index prefix
///   (e.g. "Events[2].EventType: EventType is required.") so callers can identify
///   exactly which items failed without re-submitting the entire batch.
///
/// Batch size limits:
///   Hard max:    500 events per batch (absolute ceiling — rejected regardless of config)
///   Recommended: ≤ 100 events for optimal throughput and error isolation
///
/// BatchCorrelationId propagation:
///   When set, the batch correlation ID is used as a fallback CorrelationId for
///   any item that does not supply its own. The validator does not enforce propagation —
///   that is handled by the ingest service layer.
/// </summary>
public sealed class BatchIngestRequestValidator : AbstractValidator<BatchIngestRequest>
{
    /// <summary>Hard upper limit on events per batch request.</summary>
    public const int MaxBatchSize = 500;

    private const int MaxBatchCorrelationIdLength = 200;

    public BatchIngestRequestValidator(IValidator<IngestAuditEventRequest> itemValidator)
    {
        // ── Events collection ──────────────────────────────────────────────────
        RuleFor(x => x.Events)
            .NotNull()
            .WithMessage("Events is required and must not be null.")
            .NotEmpty()
            .WithMessage("Events must contain at least one item.")
            .Must(events => events.Count <= MaxBatchSize)
            .WithMessage($"Batch must not exceed {MaxBatchSize} events per request. " +
                         $"Split large workloads into multiple smaller batches.");

        // ── Per-item validation ────────────────────────────────────────────────
        // RuleForEach propagates validation errors with the index prefix Events[n].FieldName
        // so callers can identify the exact failing item.
        RuleForEach(x => x.Events)
            .SetValidator(itemValidator)
            .When(x => x.Events is { Count: > 0 });

        // ── BatchCorrelationId ─────────────────────────────────────────────────
        RuleFor(x => x.BatchCorrelationId)
            .MaximumLength(MaxBatchCorrelationIdLength)
            .WithMessage($"BatchCorrelationId must not exceed {MaxBatchCorrelationIdLength} characters.")
            .When(x => x.BatchCorrelationId is not null);
    }
}
