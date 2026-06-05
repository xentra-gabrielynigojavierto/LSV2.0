using System.Text;
using Microsoft.Extensions.Options;
using PlatformAuditEventService.Configuration;
using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.DTOs.Integrity;
using PlatformAuditEventService.Entities;
using PlatformAuditEventService.Repositories;
using PlatformAuditEventService.Utilities;

namespace PlatformAuditEventService.Services;

/// <summary>
/// Canonical implementation of <see cref="IIntegrityCheckpointService"/>.
///
/// Checkpoint generation algorithm:
/// <list type="number">
///   <item>Stream Hash values from all <c>AuditEventRecord</c> rows where
///     <c>RecordedAtUtc ∈ [from, to)</c>, ordered by ascending surrogate Id.</item>
///   <item>Concatenate the hashes in order. Null hashes (signing was disabled) contribute
///     an empty string at their position — the count remains accurate.</item>
///   <item>Apply the configured algorithm (HMAC-SHA256 or SHA-256) to the concatenated
///     string to produce the <c>AggregateHash</c>.</item>
///   <item>Persist the resulting <see cref="IntegrityCheckpoint"/> record.</item>
/// </list>
///
/// The aggregate hash is computed over concatenated individual hashes (not payloads)
/// so the computation is fast (no need to reload canonical fields) and the
/// aggregate can be independently verified by any party with the same HMAC key.
///
/// Registered as a scoped service — uses <see cref="IAuditEventRecordRepository"/>
/// (scoped) and <see cref="IIntegrityCheckpointRepository"/> (scoped).
/// </summary>
public sealed class IntegrityCheckpointService : IIntegrityCheckpointService
{
    private readonly IAuditEventRecordRepository       _recordRepo;
    private readonly IIntegrityCheckpointRepository    _checkpointRepo;
    private readonly IntegrityOptions                  _integrityOpts;
    private readonly ILogger<IntegrityCheckpointService> _logger;

    // Resolved once at construction — avoids repeated Base64 decoding on every call.
    private readonly byte[]? _hmacKey;

    public IntegrityCheckpointService(
        IAuditEventRecordRepository         recordRepo,
        IIntegrityCheckpointRepository      checkpointRepo,
        IOptions<IntegrityOptions>          integrityOpts,
        ILogger<IntegrityCheckpointService> logger)
    {
        _recordRepo      = recordRepo;
        _checkpointRepo  = checkpointRepo;
        _integrityOpts   = integrityOpts.Value;
        _logger          = logger;

        if (!string.IsNullOrWhiteSpace(_integrityOpts.HmacKeyBase64))
        {
            try
            {
                _hmacKey = Convert.FromBase64String(_integrityOpts.HmacKeyBase64);
            }
            catch (FormatException)
            {
                _logger.LogError(
                    "IntegrityCheckpointService: Integrity:HmacKeyBase64 is not valid Base64. " +
                    "Checkpoint generation will fall back to SHA-256.");
            }
        }
    }

    /// <inheritdoc/>
    public async Task<IntegrityCheckpointResponse> GenerateAsync(
        GenerateCheckpointRequest request,
        CancellationToken         ct = default)
    {
        if (request.ToRecordedAtUtc <= request.FromRecordedAtUtc)
        {
            throw new ArgumentException(
                "ToRecordedAtUtc must be after FromRecordedAtUtc.",
                nameof(request));
        }

        _logger.LogInformation(
            "Generating integrity checkpoint. Type={Type} Window={From:u}-{To:u}",
            request.CheckpointType, request.FromRecordedAtUtc, request.ToRecordedAtUtc);

        // ── Step 1: Stream hashes and build concatenated string ───────────────
        // StringBuilder avoids repeated string allocation for large windows.
        var sb    = new StringBuilder();
        long count = 0;

        await foreach (var hash in _recordRepo.StreamHashesForWindowAsync(
            request.FromRecordedAtUtc, request.ToRecordedAtUtc, ct))
        {
            // Null hash = signing was disabled for this record at ingest time.
            // Contribute empty string so the count and position are preserved.
            sb.Append(hash ?? string.Empty);
            count++;
        }

        _logger.LogDebug(
            "Checkpoint hash stream complete. RecordCount={Count} Type={Type}",
            count, request.CheckpointType);

        // ── Step 2: Compute aggregate hash ────────────────────────────────────
        var concatenated = sb.ToString();
        var aggregateHash = ComputeAggregateHash(concatenated);

        // ── Step 3: Persist ───────────────────────────────────────────────────
        var checkpoint = new IntegrityCheckpoint
        {
            CheckpointType    = request.CheckpointType,
            FromRecordedAtUtc = request.FromRecordedAtUtc,
            ToRecordedAtUtc   = request.ToRecordedAtUtc,
            AggregateHash     = aggregateHash,
            RecordCount       = count,
            CreatedAtUtc      = DateTimeOffset.UtcNow,
        };

        var persisted = await _checkpointRepo.AppendAsync(checkpoint, ct);

        _logger.LogInformation(
            "Integrity checkpoint created. Id={Id} Type={Type} RecordCount={Count} " +
            "AggregateHash={Hash} Window={From:u}-{To:u}",
            persisted.Id, persisted.CheckpointType, persisted.RecordCount,
            persisted.AggregateHash, persisted.FromRecordedAtUtc, persisted.ToRecordedAtUtc);

        return ToResponse(persisted);
    }

    /// <inheritdoc/>
    public async Task<PagedResult<IntegrityCheckpointResponse>> ListAsync(
        CheckpointListQuery query,
        CancellationToken   ct = default)
    {
        var raw = await _checkpointRepo.ListAsync(
            checkpointType: string.IsNullOrWhiteSpace(query.Type) ? null : query.Type,
            from:           query.From,
            to:             query.To,
            page:           query.Page,
            pageSize:       query.PageSize,
            ct:             ct);

        return new PagedResult<IntegrityCheckpointResponse>
        {
            Items      = raw.Items.Select(ToResponse).ToList(),
            TotalCount = raw.TotalCount,
            Page       = raw.Page,
            PageSize   = raw.PageSize,
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Computes the aggregate hash over the concatenated record-hash string.
    /// Uses HMAC-SHA256 when the key is available and the algorithm is configured
    /// as "HMAC-SHA256"; falls back to SHA-256 otherwise.
    /// </summary>
    private string ComputeAggregateHash(string concatenatedHashes)
    {
        var useHmac = _hmacKey is not null
                   && _integrityOpts.Algorithm.Equals(
                       AuditRecordHasher.AlgoHmacSha256,
                       StringComparison.OrdinalIgnoreCase);

        return useHmac
            ? AuditRecordHasher.ComputeHmacSha256(concatenatedHashes, _hmacKey!)
            : AuditRecordHasher.ComputeSha256(concatenatedHashes);
    }

    /// <summary>Maps an entity to the public API response shape.</summary>
    private static IntegrityCheckpointResponse ToResponse(IntegrityCheckpoint c) =>
        new()
        {
            Id                = c.Id,
            CheckpointType    = c.CheckpointType,
            FromRecordedAtUtc = c.FromRecordedAtUtc,
            ToRecordedAtUtc   = c.ToRecordedAtUtc,
            AggregateHash     = c.AggregateHash,
            RecordCount       = c.RecordCount,
            CreatedAtUtc      = c.CreatedAtUtc,
            // IsValid and LastVerifiedAtUtc are verification-run fields — null in v1.
            IsValid           = null,
            LastVerifiedAtUtc = null,
        };
}
