using Microsoft.EntityFrameworkCore;
using PlatformAuditEventService.Data;
using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.Entities;

namespace PlatformAuditEventService.Repositories;

/// <summary>
/// EF Core / MySQL-backed repository for <see cref="IntegrityCheckpoint"/>.
///
/// Checkpoints are append-only. All queries use AsNoTracking() for
/// read performance. The repository never exposes an update or delete method —
/// if a checkpoint is incorrect, a new corrected one is appended instead.
/// </summary>
public sealed class EfIntegrityCheckpointRepository : IIntegrityCheckpointRepository
{
    private readonly IDbContextFactory<AuditEventDbContext> _contextFactory;
    private readonly ILogger<EfIntegrityCheckpointRepository> _logger;

    public EfIntegrityCheckpointRepository(
        IDbContextFactory<AuditEventDbContext> contextFactory,
        ILogger<EfIntegrityCheckpointRepository> logger)
    {
        _contextFactory = contextFactory;
        _logger         = logger;
    }

    public async Task<IntegrityCheckpoint> AppendAsync(
        IntegrityCheckpoint checkpoint,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        db.IntegrityCheckpoints.Add(checkpoint);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "IntegrityCheckpoint created: Id={Id} Type={Type} RecordCount={Count} Window={From:u}-{To:u}",
            checkpoint.Id, checkpoint.CheckpointType,
            checkpoint.RecordCount, checkpoint.FromRecordedAtUtc, checkpoint.ToRecordedAtUtc);

        return checkpoint;
    }

    public async Task<IntegrityCheckpoint?> GetByIdAsync(
        long id,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.IntegrityCheckpoints
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<IntegrityCheckpoint?> GetLatestAsync(
        string checkpointType,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.IntegrityCheckpoints
            .AsNoTracking()
            .Where(c => c.CheckpointType == checkpointType)
            .OrderByDescending(c => c.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<IntegrityCheckpoint>> GetByWindowAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.IntegrityCheckpoints
            .AsNoTracking()
            .Where(c => c.FromRecordedAtUtc >= from && c.FromRecordedAtUtc <= to)
            .OrderBy(c => c.Id)
            .ToListAsync(ct);
    }

    public async Task<PagedResult<IntegrityCheckpoint>> ListByTypeAsync(
        string checkpointType,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var query = db.IntegrityCheckpoints
            .AsNoTracking()
            .Where(c => c.CheckpointType == checkpointType)
            .OrderByDescending(c => c.Id);

        var total = await query.CountAsync(ct);   // int — matches PagedResult<T>.TotalCount
        pageSize  = Math.Max(1, Math.Min(pageSize, 200));
        page      = Math.Max(1, page);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<IntegrityCheckpoint>
        {
            Items      = items,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize,
        };
    }

    public async Task<PagedResult<IntegrityCheckpoint>> ListAsync(
        string?         checkpointType,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int             page,
        int             pageSize,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var query = db.IntegrityCheckpoints.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(checkpointType))
            query = query.Where(c => c.CheckpointType == checkpointType);

        if (from.HasValue)
            query = query.Where(c => c.CreatedAtUtc >= from.Value);

        if (to.HasValue)
            query = query.Where(c => c.CreatedAtUtc <= to.Value);

        query = query.OrderByDescending(c => c.Id);

        var total    = await query.CountAsync(ct);
        pageSize     = Math.Max(1, Math.Min(pageSize, 200));
        page         = Math.Max(1, page);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<IntegrityCheckpoint>
        {
            Items      = items,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize,
        };
    }
}
