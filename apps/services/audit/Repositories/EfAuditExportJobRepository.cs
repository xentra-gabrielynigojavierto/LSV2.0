using Microsoft.EntityFrameworkCore;
using PlatformAuditEventService.Data;
using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.Entities;
using PlatformAuditEventService.Enums;

namespace PlatformAuditEventService.Repositories;

/// <summary>
/// EF Core / MySQL-backed repository for <see cref="AuditExportJob"/>.
///
/// Export jobs have a mutable lifecycle. Write operations (Create, Update) open
/// short-lived contexts to keep transaction scope minimal and avoid stale-tracking
/// issues between the create and update calls from the export worker.
///
/// UpdateAsync uses Attach + selective property marking to avoid a redundant
/// SELECT before updating lifecycle fields (Status, FilePath, ErrorMessage,
/// CompletedAtUtc).  The caller is responsible for providing the full entity
/// with all immutable fields populated correctly.
/// </summary>
public sealed class EfAuditExportJobRepository : IAuditExportJobRepository
{
    private readonly IDbContextFactory<AuditEventDbContext> _contextFactory;
    private readonly ILogger<EfAuditExportJobRepository> _logger;

    public EfAuditExportJobRepository(
        IDbContextFactory<AuditEventDbContext> contextFactory,
        ILogger<EfAuditExportJobRepository> logger)
    {
        _contextFactory = contextFactory;
        _logger         = logger;
    }

    // ── Write ──────────────────────────────────────────────────────────────────

    public async Task<AuditExportJob> CreateAsync(
        AuditExportJob job,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        db.AuditExportJobs.Add(job);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "AuditExportJob created: ExportId={ExportId} RequestedBy={RequestedBy} Format={Format}",
            job.ExportId, job.RequestedBy, job.Format);

        return job;
    }

    public async Task<AuditExportJob> UpdateAsync(
        AuditExportJob job,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        // Attach the detached entity and mark only mutable lifecycle fields as modified.
        // This avoids a redundant SELECT and prevents overwriting immutable fields.
        db.AuditExportJobs.Attach(job);
        var entry = db.Entry(job);
        entry.Property(j => j.Status).IsModified         = true;
        entry.Property(j => j.FilePath).IsModified       = true;
        entry.Property(j => j.ErrorMessage).IsModified   = true;
        entry.Property(j => j.CompletedAtUtc).IsModified = true;
        entry.Property(j => j.RecordCount).IsModified    = true;

        await db.SaveChangesAsync(ct);

        _logger.LogDebug(
            "AuditExportJob updated: ExportId={ExportId} Status={Status}",
            job.ExportId, job.Status);

        return job;
    }

    // ── Point lookup ───────────────────────────────────────────────────────────

    public async Task<AuditExportJob?> GetByExportIdAsync(
        Guid exportId,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.AuditExportJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.ExportId == exportId, ct);
    }

    // ── List operations ────────────────────────────────────────────────────────

    public async Task<PagedResult<AuditExportJob>> ListByRequesterAsync(
        string requestedBy,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var query = db.AuditExportJobs
            .AsNoTracking()
            .Where(j => j.RequestedBy == requestedBy)
            .OrderByDescending(j => j.Id);

        var total = await query.CountAsync(ct);
        pageSize  = Math.Max(1, Math.Min(pageSize, 200));
        page      = Math.Max(1, page);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<AuditExportJob>
        {
            Items      = items,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize,
        };
    }

    public async Task<IReadOnlyList<AuditExportJob>> ListActiveAsync(
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.AuditExportJobs
            .AsNoTracking()
            .Where(j => j.Status == ExportStatus.Pending || j.Status == ExportStatus.Processing)
            .OrderBy(j => j.Id)
            .ToListAsync(ct);
    }

    public async Task<PagedResult<AuditExportJob>> ListByStatusAsync(
        IReadOnlyList<ExportStatus> statuses,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        IQueryable<AuditExportJob> query = db.AuditExportJobs
            .AsNoTracking();

        // Empty statuses list → all statuses (no filter applied)
        if (statuses is { Count: > 0 })
            query = query.Where(j => statuses.Contains(j.Status));

        query = query.OrderByDescending(j => j.Id);

        var total = await query.CountAsync(ct);
        pageSize  = Math.Max(1, Math.Min(pageSize, 200));
        page      = Math.Max(1, page);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<AuditExportJob>
        {
            Items      = items,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize,
        };
    }
}
