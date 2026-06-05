using Microsoft.EntityFrameworkCore;
using Monitoring.Application.Scheduling;
using Monitoring.Domain.Monitoring;
using Monitoring.Infrastructure.Persistence;

namespace Monitoring.Infrastructure.Scheduling;

/// <summary>
/// EF Core <see cref="ICheckResultWriter"/>. Persists one
/// <see cref="CheckResultRecord"/> per call by attaching the new entity to
/// the cycle-scoped <see cref="MonitoringDbContext"/> and saving immediately.
///
/// <para><b>Per-result save</b>: we save after each result rather than
/// batching at the end of the cycle so a single broken row does not
/// prevent the cycle's other results from being persisted. The cycle
/// is small (one row per enabled entity) so the chattier I/O is not
/// meaningful in practice.</para>
///
/// <para><b>Insert time</b>: <see cref="DateTime.UtcNow"/> is captured
/// once per call and threaded into the record so every column we own
/// has a consistent value. We deliberately bypass the
/// <c>IAuditableEntity</c>-driven SaveChanges interceptor here because
/// history rows are immutable — the interceptor is for mutable entities.</para>
///
/// <para><b>Failure surfacing</b>: this writer does not catch its own
/// exceptions. The cycle executor wraps each call so one persistence
/// failure isolates to a single row.</para>
/// </summary>
public sealed class EfCoreCheckResultWriter : ICheckResultWriter
{
    private readonly MonitoringDbContext _db;

    public EfCoreCheckResultWriter(MonitoringDbContext db)
    {
        _db = db;
    }

    public async Task WriteAsync(CheckResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);

        var record = new CheckResultRecord(
            id: Guid.NewGuid(),
            monitoredEntityId: result.EntityId,
            entityName: result.EntityName,
            monitoringType: result.MonitoringType,
            target: result.Target,
            succeeded: result.Succeeded,
            outcome: result.Outcome,
            statusCode: result.StatusCode,
            elapsedMs: result.ElapsedMs,
            checkedAtUtc: result.CheckedAtUtc,
            message: result.Message,
            errorType: result.ErrorType,
            createdAtUtc: DateTime.UtcNow);

        await _db.CheckResults.AddAsync(record, cancellationToken).ConfigureAwait(false);
        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // Detach the just-written entity so the per-cycle DbContext
            // does not accumulate tracked entries across many results.
            _db.Entry(record).State = EntityState.Detached;
        }
    }
}
