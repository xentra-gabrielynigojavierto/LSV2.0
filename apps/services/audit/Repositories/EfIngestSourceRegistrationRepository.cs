using Microsoft.EntityFrameworkCore;
using PlatformAuditEventService.Data;
using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.Entities;

namespace PlatformAuditEventService.Repositories;

/// <summary>
/// EF Core / MySQL-backed repository for <see cref="IngestSourceRegistration"/>.
///
/// Upsert semantics on (SourceSystem, SourceService): if the registration already
/// exists, IsActive and Notes are updated; otherwise a new record is inserted.
/// This avoids callers needing to know whether a source is registered before
/// calling UpsertAsync.
/// </summary>
public sealed class EfIngestSourceRegistrationRepository : IIngestSourceRegistrationRepository
{
    private readonly IDbContextFactory<AuditEventDbContext> _contextFactory;
    private readonly ILogger<EfIngestSourceRegistrationRepository> _logger;

    public EfIngestSourceRegistrationRepository(
        IDbContextFactory<AuditEventDbContext> contextFactory,
        ILogger<EfIngestSourceRegistrationRepository> logger)
    {
        _contextFactory = contextFactory;
        _logger         = logger;
    }

    public async Task<IngestSourceRegistration> UpsertAsync(
        IngestSourceRegistration registration,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var existing = await db.IngestSourceRegistrations
            .FirstOrDefaultAsync(r =>
                r.SourceSystem  == registration.SourceSystem &&
                r.SourceService == registration.SourceService, ct);

        if (existing is null)
        {
            db.IngestSourceRegistrations.Add(registration);
            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "IngestSourceRegistration created: SourceSystem={System} SourceService={Service}",
                registration.SourceSystem, registration.SourceService);

            return registration;
        }

        // Update mutable fields on the tracked entity
        existing.IsActive = registration.IsActive;
        existing.Notes    = registration.Notes;

        await db.SaveChangesAsync(ct);

        _logger.LogDebug(
            "IngestSourceRegistration updated: SourceSystem={System} SourceService={Service} IsActive={Active}",
            existing.SourceSystem, existing.SourceService, existing.IsActive);

        return existing;
    }

    public async Task<IngestSourceRegistration?> GetBySourceAsync(
        string sourceSystem,
        string? sourceService,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.IngestSourceRegistrations
            .AsNoTracking()
            .FirstOrDefaultAsync(r =>
                r.SourceSystem  == sourceSystem &&
                r.SourceService == sourceService, ct);
    }

    public async Task<IReadOnlyList<IngestSourceRegistration>> ListActiveAsync(
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.IngestSourceRegistrations
            .AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.SourceSystem)
            .ThenBy(r => r.SourceService)
            .ToListAsync(ct);
    }

    public async Task<PagedResult<IngestSourceRegistration>> ListAllAsync(
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var query = db.IngestSourceRegistrations
            .AsNoTracking()
            .OrderBy(r => r.SourceSystem)
            .ThenBy(r => r.SourceService);

        var total = await query.CountAsync(ct);   // int — matches PagedResult<T>.TotalCount
        pageSize  = Math.Max(1, Math.Min(pageSize, 200));
        page      = Math.Max(1, page);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<IngestSourceRegistration>
        {
            Items      = items,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize,
        };
    }

    public async Task<IngestSourceRegistration?> SetActiveAsync(
        string sourceSystem,
        string? sourceService,
        bool isActive,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var registration = await db.IngestSourceRegistrations
            .FirstOrDefaultAsync(r =>
                r.SourceSystem  == sourceSystem &&
                r.SourceService == sourceService, ct);

        if (registration is null)
            return null;

        registration.IsActive = isActive;
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "IngestSourceRegistration toggled: SourceSystem={System} SourceService={Service} IsActive={Active}",
            registration.SourceSystem, registration.SourceService, registration.IsActive);

        return registration;
    }
}
