using Microsoft.EntityFrameworkCore;
using PlatformAuditEventService.Data;
using PlatformAuditEventService.Entities;

namespace PlatformAuditEventService.Repositories;

/// <summary>
/// EF Core / MySQL-backed repository for <see cref="OutboxMessage"/>.
///
/// All write operations use short-lived DbContext instances. MarkProcessedAsync
/// and MarkFailedAsync use ExecuteUpdateAsync (EF bulk update) for efficiency —
/// avoids a read-then-write round trip per message during relay processing.
/// </summary>
public sealed class EfOutboxMessageRepository : IOutboxMessageRepository
{
    private readonly IDbContextFactory<AuditEventDbContext> _contextFactory;
    private readonly ILogger<EfOutboxMessageRepository>     _logger;

    public EfOutboxMessageRepository(
        IDbContextFactory<AuditEventDbContext> contextFactory,
        ILogger<EfOutboxMessageRepository>     logger)
    {
        _contextFactory = contextFactory;
        _logger         = logger;
    }

    public async Task<OutboxMessage> CreateAsync(OutboxMessage message, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(ct);
        return message;
    }

    public async Task<IReadOnlyList<OutboxMessage>> ListPendingAsync(
        int batchSize,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.OutboxMessages
            .AsNoTracking()
            .Where(m => m.ProcessedAtUtc == null && !m.IsPermanentlyFailed)
            .OrderBy(m => m.Id)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task MarkProcessedAsync(
        long id,
        DateTimeOffset processedAtUtc,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        await db.OutboxMessages
            .Where(m => m.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.ProcessedAtUtc, processedAtUtc),
            ct);
    }

    public async Task MarkFailedAsync(
        long id,
        string error,
        int maxRetries,
        CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var message = await db.OutboxMessages.FindAsync([id], ct);
        if (message is null) return;

        message.RetryCount++;
        message.LastError = error;
        message.IsPermanentlyFailed = message.RetryCount >= maxRetries;

        await db.SaveChangesAsync(ct);

        if (message.IsPermanentlyFailed)
        {
            _logger.LogError(
                "OutboxMessage permanently failed after {Retries} attempts: " +
                "Id={Id} EventType={EventType} LastError={Error}",
                message.RetryCount, id, message.EventType, error);
        }
    }
}
