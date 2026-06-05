namespace Liens.Application.Interfaces;

/// <summary>
/// TASK-B04 — one-shot admin service that backfills all existing Liens task rows
/// into the canonical Task service. Idempotent: skips tasks whose externalId already
/// exists in the Task service.
/// </summary>
public interface ILienTaskBackfillService
{
    Task<LienTaskBackfillReport> RunAsync(
        Guid              actingAdminUserId,
        int               batchSize = 100,
        CancellationToken ct        = default);
}

public sealed record LienTaskBackfillReport(
    int  Attempted,
    int  Created,
    int  AlreadyExisted,
    int  Failed,
    int  TotalNotes,
    int  TotalLinks,
    TimeSpan Elapsed);
