using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.DTOs.Ingest;
using PlatformAuditEventService.Services;
using PlatformAuditEventService.Utilities;

// Disambiguate from the legacy PlatformAuditEventService.DTOs.IngestAuditEventRequest.
using IngestAuditEventRequest = PlatformAuditEventService.DTOs.Ingest.IngestAuditEventRequest;

namespace PlatformAuditEventService.Controllers;

/// <summary>
/// Internal ingestion endpoints for audit and business events.
///
/// Route prefix: /internal/audit
///
/// These endpoints are intended for machine-to-machine communication from trusted
/// internal source systems only. They are NOT part of the public query/read surface.
///
/// Authentication: controlled by <see cref="Configuration.IngestAuthOptions"/>.
/// In production, callers must present a pre-shared API key or mTLS certificate.
/// In development mode (IngestAuth:Mode=None), requests are accepted unauthenticated.
/// </summary>
[ApiController]
[Route("internal/audit")]
[Produces("application/json")]
public sealed class AuditEventIngestController : ControllerBase
{
    private readonly IAuditEventIngestionService          _ingestionService;
    private readonly IValidator<IngestAuditEventRequest>  _singleValidator;
    private readonly IValidator<BatchIngestRequest>        _batchValidator;
    private readonly ILogger<AuditEventIngestController>  _logger;

    public AuditEventIngestController(
        IAuditEventIngestionService          ingestionService,
        IValidator<IngestAuditEventRequest>  singleValidator,
        IValidator<BatchIngestRequest>        batchValidator,
        ILogger<AuditEventIngestController>  logger)
    {
        _ingestionService = ingestionService;
        _singleValidator  = singleValidator;
        _batchValidator   = batchValidator;
        _logger           = logger;
    }

    // ── POST /internal/audit/events ───────────────────────────────────────────

    /// <summary>
    /// Ingest a single audit event from an internal source system.
    ///
    /// The request is structurally validated before reaching the ingestion pipeline.
    /// The pipeline then enforces idempotency, computes the integrity hash, and appends
    /// the record to the tamper-evident audit chain.
    ///
    /// Idempotency: supply a unique <c>IdempotencyKey</c> to make retries safe.
    /// A second submission with the same key returns 409 Conflict.
    ///
    /// Replay: set <c>IsReplay = true</c> when re-submitting a prior event through a
    /// replay mechanism. The record is accepted with a new AuditId and marked as a replay.
    /// </summary>
    /// <param name="request">The audit event to ingest.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">Event accepted and persisted. Body contains the assigned AuditId.</response>
    /// <response code="400">Request failed structural validation. Body lists all field-level errors.</response>
    /// <response code="409">Duplicate IdempotencyKey — the event was already ingested.</response>
    /// <response code="503">Persistence failed due to a transient infrastructure error. Retry with backoff.</response>
    [HttpPost("events")]
    [ProducesResponseType(typeof(ApiResponse<IngestItemResult>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>),           StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>),           StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>),           StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> IngestSingle(
        [FromBody] IngestAuditEventRequest request,
        CancellationToken ct)
    {
        // ── Step 1: Structural validation ─────────────────────────────────────
        var validation = await _singleValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .Select(e => e.ErrorMessage)
                .ToList();

            _logger.LogDebug(
                "Single ingest validation failed: {ErrorCount} error(s). EventType={EventType}",
                errors.Count, request.EventType);

            return BadRequest(
                ApiResponse<object>.ValidationFail(errors, TraceIdAccessor.Current()));
        }

        // ── Step 2: Ingest pipeline ───────────────────────────────────────────
        var result = await _ingestionService.IngestSingleAsync(request, ct);

        // ── Step 3: Map service result to HTTP response ───────────────────────
        if (result.Accepted)
        {
            // 201 Created — event persisted. Location points to the future query endpoint.
            var envelope = ApiResponse<IngestItemResult>.Ok(
                result,
                message: "Event accepted.",
                traceId: TraceIdAccessor.Current());

            return Created($"/internal/audit/events/{result.AuditId}", envelope);
        }

        return result.RejectionReason switch
        {
            AuditEventIngestionService.ReasonDuplicateIdempotencyKey =>
                Conflict(ApiResponse<object>.Fail(
                    $"Duplicate IdempotencyKey — this event has already been ingested. " +
                    $"Key: '{request.IdempotencyKey}'.",
                    traceId: TraceIdAccessor.Current())),

            AuditEventIngestionService.ReasonPersistenceError =>
                StatusCode(StatusCodes.Status503ServiceUnavailable,
                    ApiResponse<object>.Fail(
                        "The event could not be persisted due to a transient infrastructure error. " +
                        "Retry with exponential backoff.",
                        traceId: TraceIdAccessor.Current())),

            _ =>
                UnprocessableEntity(ApiResponse<object>.Fail(
                    $"Event rejected: {result.RejectionReason ?? "unknown reason"}.",
                    traceId: TraceIdAccessor.Current())),
        };
    }

    // ── POST /internal/audit/events/batch ─────────────────────────────────────

    /// <summary>
    /// Ingest a batch of audit events in a single request.
    ///
    /// Default semantics (StopOnFirstError = false):
    ///   All events are validated and attempted independently. Per-item results report
    ///   the outcome for each event. Partial acceptance is possible — some items may be
    ///   accepted while others are rejected.
    ///
    /// StopOnFirstError = true:
    ///   Processing halts after the first rejected item. Remaining items are marked Skipped
    ///   and are not attempted. The response reports the same mixed-result shape.
    ///
    /// Batch size: 1–500 events. Recommend ≤ 100 per batch for optimal throughput.
    ///
    /// Idempotency: supply an <c>IdempotencyKey</c> per item to make retries safe.
    /// Duplicate keys return per-item rejection (409 semantics) in the results list.
    ///
    /// BatchCorrelationId: when set, used as the CorrelationId fallback for any item
    /// that does not supply its own, enabling end-to-end tracing of a batch operation.
    /// </summary>
    /// <param name="request">The batch of audit events to ingest.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">All events accepted and persisted.</response>
    /// <response code="207">Partial success — some events accepted, some rejected. Inspect per-item Results.</response>
    /// <response code="400">Batch-level structural validation failed before any events were attempted.</response>
    /// <response code="422">All events rejected — no events were persisted.</response>
    [HttpPost("events/batch")]
    [ProducesResponseType(typeof(ApiResponse<BatchIngestResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BatchIngestResponse>), StatusCodes.Status207MultiStatus)]
    [ProducesResponseType(typeof(ApiResponse<object>),              StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<BatchIngestResponse>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> IngestBatch(
        [FromBody] BatchIngestRequest request,
        CancellationToken ct)
    {
        // ── Step 1: Structural validation (batch + per-item) ──────────────────
        //
        // BatchIngestRequestValidator validates:
        //   - Events collection is non-null, non-empty, within the 500-item hard cap.
        //   - Each item is validated by IngestAuditEventRequestValidator via RuleForEach.
        //   - BatchCorrelationId length (when provided).
        //
        // When any item fails structural validation, the full per-item error list is returned
        // with field paths including the zero-based index (e.g. "Events[2].EventType").
        // This lets callers identify and fix specific items without re-submitting the batch.
        var validation = await _batchValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .Select(e => $"{e.PropertyName}: {e.ErrorMessage}")
                .ToList();

            _logger.LogDebug(
                "Batch ingest validation failed: {ErrorCount} error(s). BatchSize={BatchSize}",
                errors.Count, request.Events?.Count ?? 0);

            return BadRequest(
                ApiResponse<object>.ValidationFail(errors, TraceIdAccessor.Current()));
        }

        // ── Step 2: Ingest pipeline ───────────────────────────────────────────
        var response = await _ingestionService.IngestBatchAsync(request, ct);

        // ── Step 3: Map aggregate result to HTTP status ───────────────────────
        //
        // 200 OK          — all events accepted (response.Accepted == response.Submitted).
        // 207 Multi-Status — partial success (some accepted, some rejected).
        // 422 Unprocessable Entity — no events accepted (response.Accepted == 0).
        //
        // In all three cases the body is identical in shape — callers always inspect
        // the per-item Results list regardless of status code.
        var envelope = ApiResponse<BatchIngestResponse>.Ok(
            response,
            message: BuildBatchMessage(response),
            traceId: TraceIdAccessor.Current());

        if (response.Accepted == response.Submitted)
            return Ok(envelope);

        if (response.Accepted == 0)
            return UnprocessableEntity(envelope);

        return StatusCode(StatusCodes.Status207MultiStatus, envelope);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static string BuildBatchMessage(BatchIngestResponse response) =>
        response.Accepted == response.Submitted
            ? $"All {response.Submitted} event(s) accepted."
            : response.Accepted == 0
                ? $"All {response.Submitted} event(s) rejected. No records were persisted."
                : $"{response.Accepted} of {response.Submitted} event(s) accepted; " +
                  $"{response.Rejected} rejected. Inspect Results for per-item detail.";
}
