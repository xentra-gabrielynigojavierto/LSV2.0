using Microsoft.EntityFrameworkCore;
using Monitoring.Application.Scheduling;
using Monitoring.Domain.Monitoring;
using Monitoring.Infrastructure.Persistence;

namespace Monitoring.Infrastructure.Scheduling;

/// <summary>
/// EF Core <see cref="IEntityStatusWriter"/>. Upserts one
/// <see cref="EntityCurrentStatus"/> row per call: read-or-create,
/// apply the evaluated state, then save.
///
/// <para><b>Per-result save</b>: matches the history-row writer for
/// per-row failure isolation. The cycle is small (one row per enabled
/// entity) so the chattier I/O is not meaningful in practice.</para>
///
/// <para><b>Audit timestamps</b>: <c>EntityCurrentStatus</c> implements
/// <c>IAuditableEntity</c>, so the existing SaveChanges interceptor
/// stamps <c>CreatedAtUtc</c> on first insert and <c>UpdatedAtUtc</c>
/// on every change. This writer never sets those fields directly.</para>
///
/// <para><b>Failure surfacing</b>: this writer does not catch its own
/// exceptions. The cycle executor wraps each call so one persistence
/// failure isolates to a single entity.</para>
/// </summary>
public sealed class EfCoreEntityStatusWriter : IEntityStatusWriter
{
    private readonly MonitoringDbContext _db;

    public EfCoreEntityStatusWriter(MonitoringDbContext db)
    {
        _db = db;
    }

    public async Task UpsertFromResultAsync(CheckResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);

        var status = StatusEvaluator.EvaluateFromOutcome(result.Outcome);

        var existing = await _db.EntityCurrentStatuses
            .FirstOrDefaultAsync(s => s.MonitoredEntityId == result.EntityId, cancellationToken)
            .ConfigureAwait(false);

        EntityCurrentStatus row;
        if (existing is null)
        {
            row = new EntityCurrentStatus(
                monitoredEntityId: result.EntityId,
                currentStatus: status,
                lastOutcome: result.Outcome,
                lastStatusCode: result.StatusCode,
                lastElapsedMs: result.ElapsedMs,
                lastCheckedAtUtc: result.CheckedAtUtc,
                lastMessage: result.Message,
                lastErrorType: result.ErrorType);
            await _db.EntityCurrentStatuses.AddAsync(row, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            existing.ApplyResult(
                currentStatus: status,
                lastOutcome: result.Outcome,
                lastStatusCode: result.StatusCode,
                lastElapsedMs: result.ElapsedMs,
                lastCheckedAtUtc: result.CheckedAtUtc,
                lastMessage: result.Message,
                lastErrorType: result.ErrorType);
            row = existing;
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // Detach so the per-cycle DbContext does not accumulate
            // tracked entries across many entities.
            _db.Entry(row).State = EntityState.Detached;
        }
    }
}
