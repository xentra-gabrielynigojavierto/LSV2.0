using System.Data;
using System.Data.Common;
using Flow.Domain.Common;
using Flow.Domain.Entities;
using Flow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Flow.Infrastructure.Outbox;

/// <summary>
/// LS-FLOW-E10.2 — background worker that drains the Flow outbox.
///
/// <para>
/// Lifecycle (per tick):
///   1. Open a fresh DI scope (so <see cref="FlowDbContext"/> is fresh and
///      the request-scoped tenant provider is null — see the entity
///      configuration: the outbox has no tenant query filter for exactly
///      this reason).
///   2. Inside one short-lived MySQL transaction, claim a batch by:
///        SELECT … FOR UPDATE SKIP LOCKED  (MySQL 8+)
///        + UPDATE Status='Processing', AttemptCount += 1
///      The skip-locked select is what makes the worker safe under
///      multiple replicas — competing workers see disjoint row sets.
///   3. Commit the claim transaction so the rows are visible as
///      "Processing" to any operator dashboard, then dispatch each row
///      OUT of the claim transaction so downstream HTTP latency does
///      not hold row locks.
///   4. Per-row outcome:
///        success  → Status='Succeeded',   ProcessedAt=now
///        failure  → if AttemptCount &lt; Max: Status='Pending',
///                   NextAttemptAt=now + backoff, LastError captured
///                   else: Status='DeadLettered', ProcessedAt=now
/// </para>
///
/// <para>
/// Exponential backoff: <c>BaseBackoffSeconds * BackoffMultiplier^(attempt-1)</c>.
/// With defaults (30s base, 2.0×) the schedule is ≈ 30s, 60s, 120s, 240s
/// before the row is dead-lettered on attempt 5.
/// </para>
///
/// <para>
/// Disabled mode (<c>Outbox:Enabled=false</c>): the worker still runs but
/// short-circuits each tick without claiming, so enqueueing keeps working
/// and operators can drain manually later. Documented limitation: this
/// is single-process only in the sense that SKIP LOCKED prevents
/// duplicate concurrent processing of the same row, but does NOT prevent
/// duplicate dispatch to downstream targets if a worker crashes between
/// "downstream call succeeded" and "row marked Succeeded" — which is why
/// every dispatched payload carries the outbox id for downstream dedupe.
/// </para>
/// </summary>
public sealed class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptionsMonitor<OutboxOptions> _options;
    private readonly ILogger<OutboxProcessor> _log;

    public OutboxProcessor(
        IServiceScopeFactory scopes,
        IOptionsMonitor<OutboxOptions> options,
        ILogger<OutboxProcessor> log)
    {
        _scopes = scopes;
        _options = options;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = _options.CurrentValue;
        _log.LogInformation(
            "OutboxProcessor starting. Enabled={Enabled} pollSeconds={Poll} batchSize={Batch} maxAttempts={MaxAttempts} baseBackoffSeconds={Base} backoffMultiplier={Mult}",
            opts.Enabled, opts.PollingIntervalSeconds, opts.BatchSize,
            opts.MaxAttempts, opts.BaseBackoffSeconds, opts.BackoffMultiplier);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "OutboxProcessor tick threw — sleeping then retrying.");
            }

            try
            {
                var delay = TimeSpan.FromSeconds(Math.Max(1, _options.CurrentValue.PollingIntervalSeconds));
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _log.LogInformation("OutboxProcessor stopped.");
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var opts = _options.CurrentValue;
        if (!opts.Enabled) return;

        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();

        // ---------- reaper phase --------------------------------------
        // Recover orphaned 'Processing' rows whose worker crashed or was
        // terminated mid-dispatch. Without this, a hard kill between claim
        // commit and outcome save would strand the row forever (claim only
        // selects 'Pending'). Rows whose AttemptCount has already reached
        // MaxAttempts are dead-lettered directly with a stranded marker.
        var reaped = await ReapStaleProcessingAsync(db, opts, ct);
        if (reaped > 0)
        {
            _log.LogWarning("Outbox reaper recovered {Reaped} stale 'Processing' row(s) past lease window.", reaped);
        }

        // ---------- claim phase ---------------------------------------
        // Use raw ADO.NET (not EF) for the claim transaction so we can:
        //   (a) express FOR UPDATE SKIP LOCKED (MySQL 8+),
        //   (b) keep the SELECT + UPDATE inside the same short row-lock
        //       window without triggering MySqlRetryingExecutionStrategy's
        //       "user-initiated transaction" guard. EF's retry strategy
        //       refuses any explicit BeginTransactionAsync that isn't
        //       wrapped inside its own ExecuteAsync — and even when wrapped,
        //       inner LINQ queries spin up *another* strategy instance and
        //       trip the same check. ADO.NET sidesteps the whole dance.
        var claimedIds = await ClaimBatchAsync(db, opts.BatchSize, ct);

        if (claimedIds.Count == 0) return;

        _log.LogInformation("Outbox claim batchSize={BatchSize}", claimedIds.Count);

        // ---------- dispatch phase (one tx per row outcome) ------------
        foreach (var id in claimedIds)
        {
            if (ct.IsCancellationRequested) break;
            await ProcessOneAsync(scope.ServiceProvider, dispatcher, id, ct);
        }
    }

    /// <summary>
    /// Recovery for orphaned 'Processing' rows whose owning worker died
    /// before persisting an outcome. Runs once per tick before claim.
    ///
    /// Splits into two single-statement updates so neither requires a
    /// user-initiated transaction:
    ///   • rows past lease and below MaxAttempts → back to 'Pending' for
    ///     immediate re-claim (NextAttemptAt = now).
    ///   • rows past lease and at/above MaxAttempts → 'DeadLettered' with
    ///     a stranded marker, so they surface in operator dashboards
    ///     instead of silently piling up.
    /// </summary>
    private static async Task<int> ReapStaleProcessingAsync(FlowDbContext db, OutboxOptions opts, CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        var openedHere = false;
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
            openedHere = true;
        }

        try
        {
            var nowUtc       = DateTime.UtcNow;
            var leaseCutoff  = nowUtc.AddSeconds(-Math.Max(1, opts.ProcessingLeaseSeconds));
            var totalReaped  = 0;

            // 1) Strand → DeadLetter (already exhausted).
            await using (var dl = conn.CreateCommand())
            {
                dl.CommandText =
                    @"UPDATE flow_outbox_messages
                      SET Status='DeadLettered',
                          ProcessedAt=@now,
                          UpdatedAt=@now,
                          LastError = CONCAT('stranded in Processing past lease (', IFNULL(LastError, ''), ')')
                      WHERE Status='Processing'
                        AND UpdatedAt <= @cutoff
                        AND AttemptCount >= @max";
                AddParam(dl, "@now",    nowUtc,      DbType.DateTime);
                AddParam(dl, "@cutoff", leaseCutoff, DbType.DateTime);
                AddParam(dl, "@max",    opts.MaxAttempts, DbType.Int32);
                totalReaped += await dl.ExecuteNonQueryAsync(ct);
            }

            // 2) Strand → Pending (still has attempts left).
            await using (var rq = conn.CreateCommand())
            {
                rq.CommandText =
                    @"UPDATE flow_outbox_messages
                      SET Status='Pending',
                          NextAttemptAt=@now,
                          UpdatedAt=@now
                      WHERE Status='Processing'
                        AND UpdatedAt <= @cutoff
                        AND AttemptCount < @max";
                AddParam(rq, "@now",    nowUtc,      DbType.DateTime);
                AddParam(rq, "@cutoff", leaseCutoff, DbType.DateTime);
                AddParam(rq, "@max",    opts.MaxAttempts, DbType.Int32);
                totalReaped += await rq.ExecuteNonQueryAsync(ct);
            }

            return totalReaped;
        }
        finally
        {
            if (openedHere) await conn.CloseAsync();
        }
    }

    /// <summary>
    /// Atomic claim of up to <paramref name="batchSize"/> due rows using raw
    /// ADO.NET. Returns the IDs that this worker now owns (Status='Processing',
    /// AttemptCount bumped). Bypasses EF's retrying execution strategy so we
    /// can hold one short transaction across the SELECT … FOR UPDATE SKIP
    /// LOCKED + UPDATE without tripping its "user-initiated transaction" guard.
    /// </summary>
    private static async Task<List<Guid>> ClaimBatchAsync(FlowDbContext db, int batchSize, CancellationToken ct)
    {
        var claimed = new List<Guid>();
        var conn = db.Database.GetDbConnection();
        var openedHere = false;
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
            openedHere = true;
        }

        DbTransaction? tx = null;
        try
        {
            tx = await conn.BeginTransactionAsync(ct);
            var nowUtc = DateTime.UtcNow;

            // 1) Lock and read due pending rows.
            await using (var sel = conn.CreateCommand())
            {
                sel.Transaction = tx;
                sel.CommandText =
                    @"SELECT Id FROM flow_outbox_messages
                      WHERE Status = 'Pending' AND NextAttemptAt <= @now
                      ORDER BY NextAttemptAt ASC
                      LIMIT @lim
                      FOR UPDATE SKIP LOCKED";
                AddParam(sel, "@now", nowUtc, DbType.DateTime);
                AddParam(sel, "@lim", batchSize, DbType.Int32);

                await using var reader = await sel.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    // Id stored as char(36) GUID via Pomelo defaults.
                    var raw = reader.GetValue(0);
                    var id = raw is Guid g ? g : Guid.Parse(Convert.ToString(raw)!);
                    claimed.Add(id);
                }
            }

            if (claimed.Count == 0)
            {
                await tx.CommitAsync(ct);
                return claimed;
            }

            // 2) Flip them to Processing in-place (single UPDATE).
            await using (var upd = conn.CreateCommand())
            {
                upd.Transaction = tx;
                // Build IN clause with named params for safety.
                var paramNames = new List<string>(claimed.Count);
                for (var i = 0; i < claimed.Count; i++)
                {
                    var name = $"@id{i}";
                    paramNames.Add(name);
                    AddParam(upd, name, claimed[i].ToString(), DbType.String);
                }
                upd.CommandText =
                    $@"UPDATE flow_outbox_messages
                       SET Status='Processing',
                           AttemptCount = AttemptCount + 1,
                           UpdatedAt = @now
                       WHERE Id IN ({string.Join(",", paramNames)})";
                AddParam(upd, "@now", nowUtc, DbType.DateTime);
                await upd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return claimed;
        }
        catch
        {
            if (tx is not null)
            {
                try { await tx.RollbackAsync(ct); } catch { /* ignore */ }
            }
            throw;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
            if (openedHere) await conn.CloseAsync();
        }
    }

    private static void AddParam(DbCommand cmd, string name, object value, DbType type)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.DbType = type;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    private async Task ProcessOneAsync(
        IServiceProvider sp, OutboxDispatcher dispatcher, Guid id, CancellationToken ct)
    {
        // Fresh inner scope per row so a poisonous DbContext state from
        // one row cannot bleed into the next one.
        await using var inner = sp.CreateAsyncScope();
        var db = inner.ServiceProvider.GetRequiredService<FlowDbContext>();
        var innerDispatcher = inner.ServiceProvider.GetRequiredService<OutboxDispatcher>();

        var row = await db.OutboxMessages.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (row is null)
        {
            _log.LogWarning("Outbox row {OutboxId} disappeared between claim and dispatch.", id);
            return;
        }

        try
        {
            await innerDispatcher.DispatchAsync(row, ct);

            row.Status      = OutboxStatus.Succeeded;
            row.ProcessedAt = DateTime.UtcNow;
            row.LastError   = null;
            row.UpdatedAt   = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            _log.LogInformation(
                "Outbox succeeded id={OutboxId} type={EventType} workflowInstance={WorkflowInstanceId} tenant={TenantId} attempt={Attempt}",
                row.Id, row.EventType, row.WorkflowInstanceId, row.TenantId, row.AttemptCount);
        }
        catch (Exception ex)
        {
            await HandleFailureAsync(db, row, ex, ct);
        }
    }

    private async Task HandleFailureAsync(FlowDbContext db, OutboxMessage row, Exception ex, CancellationToken ct)
    {
        var opts    = _options.CurrentValue;
        row.LastError = Truncate($"{ex.GetType().Name}: {ex.Message}", 2048);
        row.UpdatedAt = DateTime.UtcNow;

        if (row.AttemptCount >= opts.MaxAttempts)
        {
            row.Status      = OutboxStatus.DeadLettered;
            row.ProcessedAt = DateTime.UtcNow;
            // Persist outcome with a non-cancelable token so a shutdown
            // mid-failure doesn't strand the row in 'Processing'. The
            // reaper would eventually rescue it, but bounding the window
            // to a single SaveChanges round-trip is cheap insurance.
            await db.SaveChangesAsync(CancellationToken.None);

            _log.LogError(ex,
                "Outbox dead-lettered id={OutboxId} type={EventType} workflowInstance={WorkflowInstanceId} tenant={TenantId} attempt={Attempt}/{Max} — exceeded max attempts.",
                row.Id, row.EventType, row.WorkflowInstanceId, row.TenantId, row.AttemptCount, opts.MaxAttempts);
        }
        else
        {
            // Exponential backoff. AttemptCount has already been bumped at claim time,
            // so attempt N-th retry uses base * mult^(N-1).
            var seconds = opts.BaseBackoffSeconds * Math.Pow(opts.BackoffMultiplier, row.AttemptCount - 1);
            row.NextAttemptAt = DateTime.UtcNow.AddSeconds(seconds);
            row.Status        = OutboxStatus.Pending;
            await db.SaveChangesAsync(CancellationToken.None);

            _log.LogWarning(ex,
                "Outbox failed id={OutboxId} type={EventType} workflowInstance={WorkflowInstanceId} tenant={TenantId} attempt={Attempt}/{Max} — retrying in ~{Seconds}s.",
                row.Id, row.EventType, row.WorkflowInstanceId, row.TenantId, row.AttemptCount, opts.MaxAttempts, (int)seconds);
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max);
}
