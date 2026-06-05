using Microsoft.EntityFrameworkCore;
using PlatformAuditEventService.Data;
using PlatformAuditEventService.Entities;

namespace PlatformAuditEventService.Repositories;

/// <summary>
/// EF Core / MySQL-backed repository for <see cref="LegalHold"/>.
///
/// Legal holds are created and released by the compliance controller and read
/// by the retention pipeline before deciding whether to archive or delete a record.
/// All reads use AsNoTracking for performance; UpdateAsync uses Attach + property marking.
/// </summary>
public sealed class EfLegalHoldRepository : ILegalHoldRepository
{
    private readonly IDbContextFactory<AuditEventDbContext> _contextFactory;
    private readonly ILogger<EfLegalHoldRepository>         _logger;

    public EfLegalHoldRepository(
        IDbContextFactory<AuditEventDbContext> contextFactory,
        ILogger<EfLegalHoldRepository>         logger)
    {
        _contextFactory = contextFactory;
        _logger         = logger;
    }

    public async Task<LegalHold> CreateAsync(LegalHold hold, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        db.LegalHolds.Add(hold);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "LegalHold created: HoldId={HoldId} AuditId={AuditId} Authority={Authority} HeldBy={User}",
            hold.HoldId, hold.AuditId, hold.LegalAuthority, hold.HeldByUserId);

        return hold;
    }

    public async Task<LegalHold?> GetByHoldIdAsync(Guid holdId, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.LegalHolds
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.HoldId == holdId, ct);
    }

    public async Task<IReadOnlyList<LegalHold>> ListByAuditIdAsync(
        Guid auditId,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.LegalHolds
            .AsNoTracking()
            .Where(h => h.AuditId == auditId)
            .OrderByDescending(h => h.Id)
            .ToListAsync(ct);
    }

    public async Task<bool> HasActiveHoldAsync(Guid auditId, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.LegalHolds
            .AsNoTracking()
            .AnyAsync(h => h.AuditId == auditId && h.ReleasedAtUtc == null, ct);
    }

    public async Task<LegalHold> UpdateAsync(LegalHold hold, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        db.LegalHolds.Attach(hold);
        var entry = db.Entry(hold);
        entry.Property(h => h.ReleasedAtUtc).IsModified    = true;
        entry.Property(h => h.ReleasedByUserId).IsModified = true;

        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "LegalHold released: HoldId={HoldId} AuditId={AuditId} ReleasedBy={User}",
            hold.HoldId, hold.AuditId, hold.ReleasedByUserId);

        return hold;
    }

    public async Task<IReadOnlyList<LegalHold>> ListActiveByAuthorityAsync(
        string legalAuthority,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.LegalHolds
            .AsNoTracking()
            .Where(h => h.LegalAuthority == legalAuthority && h.ReleasedAtUtc == null)
            .OrderBy(h => h.Id)
            .ToListAsync(ct);
    }

    public async Task<HashSet<Guid>> GetActiveHoldAuditIdsAsync(
        IReadOnlyList<Guid> auditIds,
        CancellationToken   ct = default)
    {
        if (auditIds.Count == 0)
            return [];

        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        var held = await db.LegalHolds
            .AsNoTracking()
            .Where(h => auditIds.Contains(h.AuditId) && h.ReleasedAtUtc == null)
            .Select(h => h.AuditId)
            .ToListAsync(ct);

        return [.. held];
    }
}
