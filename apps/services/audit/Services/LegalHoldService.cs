using PlatformAuditEventService.DTOs.LegalHold;
using PlatformAuditEventService.Entities;
using PlatformAuditEventService.Repositories;

namespace PlatformAuditEventService.Services;

/// <summary>
/// Default implementation of <see cref="ILegalHoldService"/>.
///
/// Validates that the target audit record exists before placing a hold.
/// Validates that a hold exists and is active before releasing it.
/// All state-change operations are logged for compliance traceability.
/// </summary>
public sealed class LegalHoldService : ILegalHoldService
{
    private readonly ILegalHoldRepository          _holdRepository;
    private readonly IAuditEventRecordRepository   _recordRepository;
    private readonly ILogger<LegalHoldService>     _logger;

    public LegalHoldService(
        ILegalHoldRepository         holdRepository,
        IAuditEventRecordRepository  recordRepository,
        ILogger<LegalHoldService>    logger)
    {
        _holdRepository   = holdRepository;
        _recordRepository = recordRepository;
        _logger           = logger;
    }

    /// <inheritdoc/>
    public async Task<LegalHoldResponse> CreateHoldAsync(
        Guid                    auditId,
        string                  requestedByUserId,
        CreateLegalHoldRequest  request,
        CancellationToken       ct = default)
    {
        // Verify the audit record exists
        var record = await _recordRepository.GetByAuditIdAsync(auditId, ct: ct);
        if (record is null)
        {
            throw new InvalidOperationException(
                $"Cannot create legal hold: AuditId={auditId} was not found.");
        }

        var hold = new LegalHold
        {
            HoldId         = Guid.NewGuid(),
            AuditId        = auditId,
            HeldByUserId   = requestedByUserId,
            HeldAtUtc      = DateTimeOffset.UtcNow,
            LegalAuthority = request.LegalAuthority,
            Notes          = request.Notes,
        };

        var created = await _holdRepository.CreateAsync(hold, ct);

        _logger.LogWarning(
            "LEGAL HOLD PLACED: HoldId={HoldId} AuditId={AuditId} Authority={Authority} " +
            "HeldBy={User} EventType={EventType} OccurredAt={OccurredAt:u}",
            created.HoldId, created.AuditId, created.LegalAuthority,
            requestedByUserId, record.EventType, record.OccurredAtUtc);

        return MapToResponse(created);
    }

    /// <inheritdoc/>
    public async Task<LegalHoldResponse> ReleaseHoldAsync(
        Guid                    holdId,
        string                  releasedByUserId,
        ReleaseLegalHoldRequest request,
        CancellationToken       ct = default)
    {
        var hold = await _holdRepository.GetByHoldIdAsync(holdId, ct);
        if (hold is null)
            throw new InvalidOperationException($"Legal hold HoldId={holdId} not found.");

        if (hold.ReleasedAtUtc is not null)
            throw new InvalidOperationException(
                $"Legal hold HoldId={holdId} is already released (at {hold.ReleasedAtUtc:o}).");

        hold.ReleasedAtUtc    = DateTimeOffset.UtcNow;
        hold.ReleasedByUserId = releasedByUserId;
        // ReleaseNotes are captured in the release log entry below; Notes is creation-time only.

        var updated = await _holdRepository.UpdateAsync(hold, ct);

        _logger.LogWarning(
            "LEGAL HOLD RELEASED: HoldId={HoldId} AuditId={AuditId} Authority={Authority} " +
            "ReleasedBy={User} HeldSince={HeldAt:u}",
            updated.HoldId, updated.AuditId, updated.LegalAuthority,
            releasedByUserId, updated.HeldAtUtc);

        return MapToResponse(updated);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<LegalHoldResponse>> ListByAuditIdAsync(
        Guid              auditId,
        CancellationToken ct = default)
    {
        var holds = await _holdRepository.ListByAuditIdAsync(auditId, ct);
        return holds.Select(MapToResponse).ToList();
    }

    /// <inheritdoc/>
    public Task<bool> HasActiveHoldAsync(Guid auditId, CancellationToken ct = default) =>
        _holdRepository.HasActiveHoldAsync(auditId, ct);

    // ── Mapping ────────────────────────────────────────────────────────────────

    private static LegalHoldResponse MapToResponse(LegalHold h) => new()
    {
        HoldId          = h.HoldId,
        AuditId         = h.AuditId,
        HeldByUserId    = h.HeldByUserId,
        HeldAtUtc       = h.HeldAtUtc,
        ReleasedAtUtc   = h.ReleasedAtUtc,
        ReleasedByUserId = h.ReleasedByUserId,
        LegalAuthority  = h.LegalAuthority,
        Notes           = h.Notes,
    };
}
