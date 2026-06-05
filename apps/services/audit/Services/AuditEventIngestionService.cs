using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PlatformAuditEventService.Configuration;
using PlatformAuditEventService.DTOs.Ingest;
using PlatformAuditEventService.Entities;
using PlatformAuditEventService.Enums;
using PlatformAuditEventService.Mappers;
using PlatformAuditEventService.Repositories;
using PlatformAuditEventService.Services.Forwarding;
using PlatformAuditEventService.Utilities;

namespace PlatformAuditEventService.Services;

/// <summary>
/// Canonical ingestion pipeline for audit event records.
///
/// Pipeline (per event):
///   1. Idempotency check  — probe IdempotencyKey; return DuplicateIdempotencyKey if found.
///   2. AuditId + time     — generate AuditId (Guid.NewGuid) and capture RecordedAtUtc (server UTC).
///   3. Chain lookup       — fetch PreviousHash from the (TenantId, SourceSystem) chain head.
///                           Skipped when integrity signing is disabled.
///   4. Payload + hash     — AuditRecordHasher.BuildPayload() assembles the canonical string
///                           (includes PreviousHash so Hash(N) depends on Hash(N-1));
///                           the selected algorithm then hashes it. Skipped when signing disabled.
///   5. Entity mapping     — AuditEventRecordMapper.ToEntity receives all values including hashes.
///   6. Append             — IAuditEventRecordRepository.AppendAsync (append-only, no updates).
///   7. Result             — IngestItemResult { Accepted, AuditId } or rejection with reason.
///
/// Integrity signing:
///   Enabled when either of these conditions holds:
///     - Integrity:Algorithm = "SHA-256"       → keyless, always enabled, portable.
///     - Integrity:Algorithm = "HMAC-SHA256"   → enabled only when HmacKeyBase64 is set.
///       When the key is absent in HMAC-SHA256 mode, signing is silently skipped so that
///       development environments can run without configuring a secret.
///
///   When signing is disabled: Hash and PreviousHash are null on the persisted record.
///
/// Chain integrity:
///   PreviousHash is included in the canonical payload for Hash computation.
///   This means Hash(N) is a function of Hash(N-1), creating a singly-linked
///   cryptographic chain: modifying any historical record invalidates all subsequent
///   hashes in the same (TenantId, SourceSystem) chain.
///
/// Replay records:
///   <see cref="IngestAuditEventRequest.IsReplay"/> = true marks the record as a replay of a
///   previously ingested event (e.g. during a migration or re-processing run). Replay records:
///     - Bypass idempotency enforcement when no IdempotencyKey is supplied (no key → no check).
///     - Still get a new AuditId and RecordedAtUtc assigned by this server.
///     - Still participate in the integrity chain (linked via PreviousHash).
///     - Are persisted as normal records; the IsReplay flag is a semantic marker only.
///   Callers that supply IdempotencyKey on a replay are still protected against double-submission.
///
/// Transport extensibility:
///   The service delegates persistence exclusively through <see cref="IAuditEventRecordRepository"/>.
///   To switch from direct-to-database ingest to queued or outbox-driven ingest, register a
///   different repository implementation that writes to a queue or outbox table:
///
///     Direct    (default)  — EfAuditEventRecordRepository writes synchronously to AuditEventRecords.
///     Queued    (future)   — QueuedAuditEventRecordRepository enqueues the record; a worker persists.
///     Outbox    (future)   — OutboxAuditEventRecordRepository writes to a transactional outbox;
///                            a relay background service moves records to AuditEventRecords.
///
///   The idempotency probe (ExistsIdempotencyKeyAsync) and chain lookup (GetLatestInChainAsync)
///   are also on the repository interface. For queued transport, the pre-ingestion idempotency
///   probe becomes best-effort (race window exists between probe and consumer write). The consumer
///   must enforce idempotency at the final write using a unique index on IdempotencyKey.
/// </summary>
public sealed class AuditEventIngestionService : IAuditEventIngestionService
{
    // ── Rejection reason constants ─────────────────────────────────────────────
    // Centralised here so callers and tests can reference them without magic strings.

    /// <summary>Supplied IdempotencyKey already exists in the record store.</summary>
    public const string ReasonDuplicateIdempotencyKey = "DuplicateIdempotencyKey";

    /// <summary>DB write failed due to a transient or permanent persistence error.</summary>
    public const string ReasonPersistenceError        = "PersistenceError";

    /// <summary>
    /// Processing was halted by <see cref="BatchIngestRequest.StopOnFirstError"/> after
    /// a prior item in the same batch failed. This item was never attempted.
    /// </summary>
    public const string ReasonSkipped                 = "Skipped";

    // ── Hash-chain concurrency locks ──────────────────────────────────────────
    //
    // Each (TenantId, SourceSystem) pair has its own SemaphoreSlim(1,1) to ensure
    // that the chain-lookup → hash-compute → append sequence is atomic per chain.
    //
    // Without this lock, two concurrent ingest requests for the same chain could both
    // read the same PreviousHash from GetLatestInChainAsync, compute independent hashes,
    // and append with the same PreviousHash — breaking the singly-linked cryptographic chain.
    //
    // Memory: The dictionary grows at most O(TenantCount × SourceSystemCount). For an
    // enterprise with hundreds of tenants and dozens of source systems, this is bounded
    // and acceptable. There is no eviction policy; the process lifetime is the boundary.
    //
    // Thread-safety: ConcurrentDictionary.GetOrAdd is thread-safe for concurrent readers.
    // The SemaphoreSlim instances are never removed — safe to hold a reference and Wait/Release.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _chainLocks = new();

    private static SemaphoreSlim GetChainLock(string? tenantId, string sourceSystem) =>
        _chainLocks.GetOrAdd(
            $"{tenantId ?? ""}:{sourceSystem}",
            _ => new SemaphoreSlim(1, 1));

    // ── Fields ────────────────────────────────────────────────────────────────

    private readonly IAuditEventRecordRepository         _records;
    private readonly IntegrityOptions                    _integrity;
    private readonly string                              _algorithm;
    private readonly byte[]?                             _hmacSecret;
    private readonly bool                                _signingEnabled;
    private readonly IAuditEventForwarder                _forwarder;
    private readonly ILogger<AuditEventIngestionService> _logger;

    // ── Constructor ───────────────────────────────────────────────────────────

    public AuditEventIngestionService(
        IAuditEventRecordRepository         records,
        IOptions<IntegrityOptions>          integrityOptions,
        IAuditEventForwarder                forwarder,
        ILogger<AuditEventIngestionService> logger)
    {
        _records   = records;
        _integrity = integrityOptions.Value;
        _forwarder = forwarder;
        _logger    = logger;

        // Resolve algorithm — default to HMAC-SHA256 when not specified.
        _algorithm = string.IsNullOrWhiteSpace(_integrity.Algorithm)
            ? AuditRecordHasher.AlgoHmacSha256
            : _integrity.Algorithm;

        // Decode HMAC secret when configured (required for HMAC-SHA256 mode).
        _hmacSecret = _integrity.HmacKeyBase64 is { Length: > 0 }
            ? Convert.FromBase64String(_integrity.HmacKeyBase64)
            : null;

        // Signing is enabled when:
        //   Algorithm = "SHA-256"     → always (keyless, no secret needed), or
        //   Algorithm = "HMAC-SHA256" → only when HmacKeyBase64 is configured.
        // When HMAC-SHA256 is selected but no key is set, signing is silently skipped
        // so development and staging environments can run without secrets management.
        _signingEnabled =
            _algorithm.Equals(AuditRecordHasher.AlgoSha256, StringComparison.OrdinalIgnoreCase) ||
            _hmacSecret is not null;

        if (_signingEnabled)
        {
            _logger.LogInformation(
                "Audit integrity signing ENABLED — Algorithm={Algorithm}", _algorithm);
        }
        else
        {
            _logger.LogWarning(
                "Audit integrity signing DISABLED — Algorithm={Algorithm} but HmacKeyBase64 is absent. " +
                "Set Integrity:Algorithm to SHA-256 for keyless signing, or supply a key for HMAC-SHA256.",
                _algorithm);
        }
    }

    // ── IAuditEventIngestionService ───────────────────────────────────────────

    /// <inheritdoc/>
    public Task<IngestItemResult> IngestSingleAsync(
        IngestAuditEventRequest request,
        CancellationToken ct = default) =>
        IngestOneAsync(request, index: 0, batchCorrelationFallback: null, ct);

    /// <inheritdoc/>
    public async Task<BatchIngestResponse> IngestBatchAsync(
        BatchIngestRequest request,
        CancellationToken ct = default)
    {
        var events  = request.Events;
        var results = new List<IngestItemResult>(events.Count);
        var accepted = 0;

        for (var i = 0; i < events.Count; i++)
        {
            var result = await IngestOneAsync(
                events[i],
                index:                    i,
                batchCorrelationFallback: request.BatchCorrelationId,
                ct);

            results.Add(result);

            if (result.Accepted)
            {
                accepted++;
            }
            else if (request.StopOnFirstError)
            {
                // Append Skipped placeholder results for every untried item.
                for (var j = i + 1; j < events.Count; j++)
                {
                    results.Add(new IngestItemResult
                    {
                        Index          = j,
                        EventType      = events[j].EventType,
                        IdempotencyKey = events[j].IdempotencyKey,
                        Accepted       = false,
                        RejectionReason = ReasonSkipped,
                    });
                }

                break;
            }
        }

        var rejected = events.Count - accepted;

        _logger.LogInformation(
            "Batch ingest complete: Submitted={Submitted} Accepted={Accepted} Rejected={Rejected} " +
            "BatchCorrelationId={BatchCorrelationId}",
            events.Count, accepted, rejected, request.BatchCorrelationId);

        return new BatchIngestResponse
        {
            Submitted          = events.Count,
            Accepted           = accepted,
            Rejected           = rejected,
            Results            = results,
            BatchCorrelationId = request.BatchCorrelationId,
        };
    }

    // ── Core single-event pipeline ─────────────────────────────────────────────

    /// <summary>
    /// Executes the full ingestion pipeline for a single event.
    /// Called by both <see cref="IngestSingleAsync"/> and <see cref="IngestBatchAsync"/>.
    /// </summary>
    /// <param name="req">The validated ingest request for this event.</param>
    /// <param name="index">Zero-based index of this item in the enclosing batch (0 for single ingest).</param>
    /// <param name="batchCorrelationFallback">
    /// BatchCorrelationId from the enclosing batch request, used as a CorrelationId fallback
    /// when the individual item does not supply one. Null for single-event ingest.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    private async Task<IngestItemResult> IngestOneAsync(
        IngestAuditEventRequest req,
        int                     index,
        string?                 batchCorrelationFallback,
        CancellationToken       ct)
    {
        // ── Step 0.5: Gap detection — log warnings for suspicious fields ─────
        //
        // Warnings are informational only. Events are never rejected here.
        // No PII is included in the log fields (EventType, SourceSystem, ScopeType, timestamps only).
        var effectiveCorrelationId = string.IsNullOrWhiteSpace(req.CorrelationId)
            ? batchCorrelationFallback
            : req.CorrelationId;

        if (string.IsNullOrWhiteSpace(effectiveCorrelationId))
        {
            _logger.LogWarning(
                "AuditGap: CorrelationId is absent on EventType={EventType} SourceSystem={SourceSystem} Index={Index}",
                req.EventType, req.SourceSystem, index);
        }

        if (req.Scope.ScopeType == ScopeType.Tenant && string.IsNullOrWhiteSpace(req.Scope.TenantId))
        {
            _logger.LogWarning(
                "AuditGap: Tenant-scoped event missing TenantId EventType={EventType} SourceSystem={SourceSystem} Index={Index}",
                req.EventType, req.SourceSystem, index);
        }

        if (req.OccurredAtUtc.HasValue)
        {
            var serverNow    = DateTimeOffset.UtcNow;
            var deltaMinutes = (req.OccurredAtUtc.Value - serverNow).TotalMinutes;
            if (deltaMinutes > 60 || deltaMinutes < -2880)
            {
                _logger.LogWarning(
                    "AuditGap: OccurredAtUtc={OccurredAtUtc} is {DeltaMinutes:F0}m from server time " +
                    "EventType={EventType} SourceSystem={SourceSystem} Index={Index}",
                    req.OccurredAtUtc.Value, deltaMinutes, req.EventType, req.SourceSystem, index);
            }
        }

        // ── Step 1: Idempotency check ─────────────────────────────────────────
        //
        // Only check when an IdempotencyKey was supplied. Callers without a key get
        // no deduplication guard (intentional — they are responsible for retrying safely).
        // Replay records with a key are still protected against double submission.
        if (!string.IsNullOrWhiteSpace(req.IdempotencyKey))
        {
            var isDuplicate = await _records.ExistsIdempotencyKeyAsync(req.IdempotencyKey, ct);
            if (isDuplicate)
            {
                _logger.LogDebug(
                    "Duplicate IdempotencyKey rejected: Key={Key} EventType={EventType}",
                    req.IdempotencyKey, req.EventType);

                return Rejected(index, req, ReasonDuplicateIdempotencyKey);
            }
        }

        // ── Step 2: Server-side identity and timestamp ────────────────────────
        //
        // AuditId and RecordedAtUtc are generated here, not by the mapper, because the
        // integrity hash (step 4) must cover their exact values. If the mapper generated
        // them internally we would need to construct the entity first and then recompute
        // the hash — requiring either mutable fields or a two-allocation pattern.
        //
        // TODO: replace Guid.NewGuid() with a UUIDv7 factory once available. UUIDv7
        //       GUIDs are time-ordered, which improves clustered-index insert locality
        //       on MySQL / MariaDB (Pomelo target) significantly for high-volume append.
        var auditId = Guid.NewGuid();
        var now     = DateTimeOffset.UtcNow;

        // ── Steps 3–6: Chain-locked critical section ─────────────────────────
        //
        // The hash chain relies on a read-then-write pattern:
        //   Step 3: read chain head  →  Step 4: compute hash (includes PreviousHash)  →
        //   Step 5: build entity     →  Step 6: append to DB
        //
        // Concurrent ingest for the same (TenantId, SourceSystem) chain would allow two
        // requests to read the same chain head, each compute a hash against it, and append
        // two records with identical PreviousHash — breaking the singly-linked chain.
        //
        // A per-chain SemaphoreSlim(1,1) serialises the critical section so that only
        // one request progresses through Steps 3–6 per chain at a time.
        // Step 7 (forwarding) is performed AFTER releasing the lock to maximise throughput.
        var chainLock = GetChainLock(req.Scope.TenantId, req.SourceSystem);
        await chainLock.WaitAsync(ct);

        AuditEventRecord? persisted = null;
        IngestItemResult? earlyResult = null;

        try
        {
            // ── Step 3: Integrity chain lookup ────────────────────────────────
            string? previousHash = null;
            if (_signingEnabled)
            {
                var chainHead = await _records.GetLatestInChainAsync(
                    req.Scope.TenantId, req.SourceSystem, ct);
                previousHash = chainHead?.Hash;
            }

            // ── Step 4: Payload assembly + hash computation ───────────────────
            string? hash = null;
            if (_signingEnabled)
            {
                var occurredAtUtc = req.OccurredAtUtc ?? now;

                var payload = AuditRecordHasher.BuildPayload(
                    auditId:       auditId,
                    eventType:     req.EventType,
                    sourceSystem:  req.SourceSystem,
                    tenantId:      req.Scope.TenantId,
                    actorId:       req.Actor.Id,
                    entityType:    req.Entity?.Type,
                    entityId:      req.Entity?.Id,
                    action:        req.Action,
                    occurredAtUtc: occurredAtUtc,
                    recordedAtUtc: now,
                    previousHash:  previousHash);

                hash = _algorithm.Equals(AuditRecordHasher.AlgoSha256, StringComparison.OrdinalIgnoreCase)
                    ? AuditRecordHasher.ComputeSha256(payload)
                    : AuditRecordHasher.ComputeHmacSha256(payload, _hmacSecret!);
            }

            // ── Step 5: Entity construction ───────────────────────────────────
            var correlationIdOverride = req.CorrelationId is null ? batchCorrelationFallback : null;

            var entity = AuditEventRecordMapper.ToEntity(
                req,
                auditId:               auditId,
                now:                   now,
                correlationIdOverride: correlationIdOverride,
                hash:                  hash,
                previousHash:          previousHash);

            // ── Step 6: Append-only persistence ──────────────────────────────
            persisted = await _records.AppendAsync(entity, ct);

            _logger.LogInformation(
                "AuditEvent ingested: AuditId={AuditId} EventType={EventType} " +
                "SourceSystem={SourceSystem} TenantId={TenantId} IsReplay={IsReplay} Signed={Signed}",
                persisted.AuditId, persisted.EventType, persisted.SourceSystem,
                persisted.TenantId, persisted.IsReplay, hash is not null);
        }
        catch (DbUpdateException dbEx) when (IsUniqueConstraintViolation(dbEx))
        {
            _logger.LogWarning(
                "Concurrent duplicate IdempotencyKey detected at commit: Key={Key} EventType={EventType}",
                req.IdempotencyKey, req.EventType);
            earlyResult = Rejected(index, req, ReasonDuplicateIdempotencyKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Persistence failure for AuditEvent: AuditId={AuditId} EventType={EventType} " +
                "SourceSystem={SourceSystem}",
                auditId, req.EventType, req.SourceSystem);
            earlyResult = Rejected(index, req, ReasonPersistenceError);
        }
        finally
        {
            chainLock.Release();
        }

        // Return early (rejection) if persistence failed inside the lock.
        if (earlyResult is not null)
            return earlyResult;

        // ── Step 7: Event forwarding (post-lock, best-effort) ────────────────
        //
        // Forwarding happens outside the chain lock so it does not block concurrent
        // ingest for the same chain during broker I/O. The record is already durable.
        try
        {
            await _forwarder.ForwardAsync(persisted!, ct);
        }
        catch (Exception fwdEx)
        {
            _logger.LogWarning(fwdEx,
                "Event forwarding failed (non-fatal): AuditId={AuditId} " +
                "EventType={EventType} SourceSystem={SourceSystem}. " +
                "The record was persisted successfully. Forwarding is best-effort.",
                persisted!.AuditId, persisted.EventType, persisted.SourceSystem);
        }

        return new IngestItemResult
        {
            Index          = index,
            EventType      = req.EventType,
            IdempotencyKey = req.IdempotencyKey,
            Accepted       = true,
            AuditId        = persisted!.AuditId,
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Builds a rejected <see cref="IngestItemResult"/> with the given reason.
    /// </summary>
    private static IngestItemResult Rejected(
        int                     index,
        IngestAuditEventRequest req,
        string                  reason) =>
        new()
        {
            Index           = index,
            EventType       = req.EventType,
            IdempotencyKey  = req.IdempotencyKey,
            Accepted        = false,
            RejectionReason = reason,
        };

    /// <summary>
    /// Returns true when a <see cref="DbUpdateException"/> is caused by a unique-constraint
    /// violation on any column. Uses the inner exception message heuristic since EF Core
    /// does not expose a typed exception for constraint violations.
    ///
    /// Pomelo (MySQL) surfaces unique violations as MySqlException with ErrorCode 1062.
    /// The string check is a portable fallback for other providers (SQLite in tests).
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner is null) return false;

        // Pomelo / MySQL: check the numeric error code to avoid string matching
        var innerTypeName = inner.GetType().Name;
        if (innerTypeName.Contains("MySql", StringComparison.OrdinalIgnoreCase))
        {
            // MySqlException.ErrorCode 1062 = ER_DUP_ENTRY
            var errorCodeProp = inner.GetType().GetProperty("ErrorCode") ??
                                inner.GetType().GetProperty("Number");
            if (errorCodeProp?.GetValue(inner) is int code && (code == 1062 || code == 1169))
                return true;
        }

        // Generic fallback: covers SQLite (in unit tests) and other providers
        var msg = inner.Message ?? string.Empty;
        return msg.Contains("unique", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase);
    }
}
