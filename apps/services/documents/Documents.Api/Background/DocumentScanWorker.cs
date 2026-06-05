using Documents.Application.Services;
using Documents.Domain.Entities;
using Documents.Domain.Enums;
using Documents.Domain.Events;
using Documents.Domain.Interfaces;
using Documents.Infrastructure.Observability;
using Documents.Infrastructure.Scanner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Documents.Api.Background;

/// <summary>
/// Long-running background service that dequeues scan jobs from IScanJobQueue,
/// downloads the file from storage, invokes the IFileScannerProvider, then
/// updates the document/version scan status in the database.
///
/// Hardening features:
///   - Configurable WorkerCount concurrent scan tasks
///   - Exponential backoff retry up to MaxRetryAttempts
///   - Lease/Ack pattern for at-least-once delivery (Redis Streams-compatible)
///   - Prometheus metrics on all lifecycle events
///   - Graceful shutdown via CancellationToken
/// </summary>
public sealed class DocumentScanWorker : BackgroundService
{
    private readonly IScanJobQueue               _queue;
    private readonly IStorageProvider            _storage;
    private readonly IFileScannerProvider        _scanner;
    private readonly IScanCompletionPublisher    _publisher;
    private readonly IServiceScopeFactory        _scopes;
    private readonly ScanWorkerOptions           _opts;
    private readonly ILogger<DocumentScanWorker> _log;

    public DocumentScanWorker(
        IScanJobQueue                queue,
        IStorageProvider             storage,
        IFileScannerProvider         scanner,
        IScanCompletionPublisher     publisher,
        IServiceScopeFactory         scopes,
        IOptions<ScanWorkerOptions>  opts,
        ILogger<DocumentScanWorker>  log)
    {
        _queue     = queue;
        _storage   = storage;
        _scanner   = scanner;
        _publisher = publisher;
        _scopes    = scopes;
        _opts      = opts.Value;
        _log       = log;
    }

    // ── BackgroundService entry point ─────────────────────────────────────────

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation(
            "DocumentScanWorker started — provider={Provider} workers={Count} maxRetries={MaxRetry}",
            _scanner.ProviderName, _opts.WorkerCount, _opts.MaxRetryAttempts);

        // Spawn WorkerCount concurrent scan loops
        var workerTasks = Enumerable
            .Range(0, Math.Max(1, _opts.WorkerCount))
            .Select(i => RunWorkerLoopAsync($"worker-{i}", stoppingToken))
            .ToArray();

        await Task.WhenAll(workerTasks);

        _log.LogInformation("DocumentScanWorker stopped");
    }

    // ── Worker loop ───────────────────────────────────────────────────────────

    private async Task RunWorkerLoopAsync(string workerId, CancellationToken ct)
    {
        _log.LogDebug("Scan worker loop started: {WorkerId}", workerId);

        while (!ct.IsCancellationRequested)
        {
            ScanJobLease? lease = null;
            try
            {
                lease = await _queue.DequeueAsync(workerId, ct);
                if (lease is null) break;  // queue permanently closed

                await ProcessJobAsync(lease, workerId, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                // Unexpected error outside job processing — log and continue
                _log.LogError(ex, "Unexpected error in scan worker loop {WorkerId}", workerId);

                if (lease is not null)
                {
                    try { await _queue.NackAsync(lease, ct); }
                    catch { /* best-effort */ }
                }

                await SafeDelayAsync(3_000, ct);
            }
        }

        _log.LogDebug("Scan worker loop stopped: {WorkerId}", workerId);
    }

    // ── Job processing ────────────────────────────────────────────────────────

    private async Task ProcessJobAsync(ScanJobLease lease, string workerId, CancellationToken ct)
    {
        var job = lease.Job;

        // Retry limit exceeded → permanent failure
        if (job.AttemptCount >= _opts.MaxRetryAttempts)
        {
            _log.LogError(
                "Scan exceeded max retries ({Max}): Document={DocId} — marking FAILED",
                _opts.MaxRetryAttempts, job.DocumentId);

            await SetScanStatusAsync(job, ScanStatus.Failed, new(), null, null, ct);
            await AuditScanEventAsync(job, AuditEvent.ScanFailed,
                new { reason = "max_retries_exceeded", attempt = job.AttemptCount }, ct);

            ScanMetrics.ScanJobsFailed.Inc();
            await _queue.AcknowledgeAsync(lease, ct);
            await PublishCompletionEventAsync(job, ScanStatus.Failed, attemptCount: job.AttemptCount,
                engineVersion: null, ct);
            return;
        }

        _log.LogInformation(
            "Scan starting [{WorkerId}]: Document={DocId} Version={VersionId} File={File} " +
            "Attempt={Attempt}/{Max} Corr={Corr}",
            workerId, job.DocumentId, job.VersionId, job.FileName,
            job.AttemptCount + 1, _opts.MaxRetryAttempts, job.CorrelationId);

        ScanMetrics.ScanJobsStarted.Inc();

        // 1. Audit: scan started
        await AuditScanEventAsync(job, AuditEvent.ScanStarted,
            new { job.FileName, job.StorageKey, attempt = job.AttemptCount + 1 }, ct);

        // 2. Download from quarantine storage
        Stream fileStream;
        try
        {
            fileStream = await _storage.DownloadAsync(job.StorageKey, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log.LogError(ex, "Storage download failed for Document={DocId} Key={Key} — will retry",
                job.DocumentId, job.StorageKey);

            await RetryOrFailAsync(lease, "storage_download_error", ex.Message, ct);
            return;
        }

        // 3. ClamAV scan
        ScanResult result;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await using (fileStream)
            {
                result = await _scanner.ScanAsync(fileStream, job.FileName, ct);
            }
            sw.Stop();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            sw.Stop();
            _log.LogError(ex,
                "ClamAV scan failed for Document={DocId} after {Ms}ms — will retry",
                job.DocumentId, (int)sw.ElapsedMilliseconds);

            await RetryOrFailAsync(lease, "clamav_scan_error", ex.Message, ct);
            return;
        }

        ScanMetrics.ScanDurationSeconds.Observe(sw.Elapsed.TotalSeconds);
        ScanMetrics.ScanQueueDepth.Set(_queue.Count);

        _log.LogInformation(
            "Scan result [{WorkerId}]: Document={DocId} Status={Status} Threats={Count} " +
            "Duration={Ms}ms Corr={Corr}",
            workerId, job.DocumentId, result.Status, result.Threats.Count,
            (int)sw.ElapsedMilliseconds, job.CorrelationId);

        // 4. Persist scan result
        await SetScanStatusAsync(job, result.Status, result.Threats, result.DurationMs, result.EngineVersion, ct);

        // 5. Post-scan actions
        switch (result.Status)
        {
            case ScanStatus.Clean:
                ScanMetrics.ScanJobsClean.Inc();
                await AuditScanEventAsync(job, AuditEvent.ScanClean,
                    new { result.Threats.Count, result.DurationMs, result.EngineVersion }, ct);
                break;

            case ScanStatus.Infected:
                ScanMetrics.ScanJobsInfected.Inc();
                _log.LogWarning(
                    "INFECTED file: Document={DocId} Version={VersionId} Threats={Threats}",
                    job.DocumentId, job.VersionId, string.Join(", ", result.Threats));

                await AuditScanEventAsync(job, AuditEvent.ScanInfected,
                    new { result.Threats, result.EngineVersion }, ct);

                await PurgeInfectedFileAsync(job, ct);
                break;

            case ScanStatus.Failed:
                ScanMetrics.ScanJobsFailed.Inc();
                await AuditScanEventAsync(job, AuditEvent.ScanFailed,
                    new { result.DurationMs, result.EngineVersion }, ct);
                break;

            default:
                await AuditScanEventAsync(job, AuditEvent.ScanCompleted,
                    new { status = result.Status.ToString() }, ct);
                break;
        }

        await _queue.AcknowledgeAsync(lease, ct);

        // Emit scan completion event for all terminal outcomes (CLEAN / INFECTED / FAILED)
        await PublishCompletionEventAsync(job, result.Status,
            attemptCount:  job.AttemptCount + 1,
            engineVersion: result.EngineVersion,
            ct);
    }

    // ── Retry / fail-fast ─────────────────────────────────────────────────────

    private async Task RetryOrFailAsync(
        ScanJobLease      lease,
        string            reason,
        string            errorMessage,
        CancellationToken ct)
    {
        var job         = lease.Job;
        var nextAttempt = job.AttemptCount + 1;

        if (nextAttempt >= _opts.MaxRetryAttempts)
        {
            // Exceeded — mark as permanently failed
            _log.LogError(
                "Scan permanently failed after {Attempts} attempts: Document={DocId} Reason={Reason}",
                nextAttempt, job.DocumentId, reason);

            await SetScanStatusAsync(job, ScanStatus.Failed, new(), null, null, ct);
            await AuditScanEventAsync(job, AuditEvent.ScanFailed,
                new { reason, errorMessage, attempt = nextAttempt }, ct);

            ScanMetrics.ScanJobsFailed.Inc();
            await _queue.AcknowledgeAsync(lease, ct);
            await PublishCompletionEventAsync(job, ScanStatus.Failed,
                attemptCount:  nextAttempt,
                engineVersion: null,
                ct);
            return;
        }

        // Apply exponential backoff delay before re-enqueuing
        var delayMs = ComputeBackoffMs(job.AttemptCount);
        _log.LogWarning(
            "Scan transient failure (attempt {Attempt}/{Max}) for Document={DocId} — retrying in {Delay}ms",
            nextAttempt, _opts.MaxRetryAttempts, job.DocumentId, delayMs);

        ScanMetrics.ScanJobsRetried.Inc();
        await AuditScanEventAsync(job, AuditEvent.ScanFailed,
            new { reason, errorMessage, attempt = nextAttempt, retrying = true, delayMs }, ct);

        await SafeDelayAsync(delayMs, ct);
        await _queue.NackAsync(lease, ct);
    }

    private int ComputeBackoffMs(int attempt)
    {
        var baseMs = _opts.InitialRetryDelaySeconds * 1000;
        var maxMs  = _opts.MaxRetryDelaySeconds * 1000;
        var jitter  = Random.Shared.Next(0, 1_000);
        var delay   = (int)Math.Min(baseMs * Math.Pow(2, attempt), maxMs) + jitter;
        return delay;
    }

    private static async Task SafeDelayAsync(int ms, CancellationToken ct)
    {
        try { await Task.Delay(ms, ct); }
        catch (OperationCanceledException) { }
    }

    // ── Database update (scoped) ───────────────────────────────────────────────

    private async Task SetScanStatusAsync(
        ScanJob           job,
        ScanStatus        status,
        List<string>      threats,
        int?              durationMs,
        string?           engineVersion,
        CancellationToken ct)
    {
        var update = new ScanStatusUpdate
        {
            ScanStatus        = status,
            ScanCompletedAt   = DateTime.UtcNow,
            ScanDurationMs    = durationMs,
            ScanThreats       = threats,
            ScanEngineVersion = engineVersion,
        };

        await using var scope = _scopes.CreateAsyncScope();

        if (job.VersionId.HasValue)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IDocumentVersionRepository>();
            await repo.UpdateScanStatusAsync(job.VersionId.Value, job.TenantId, update, ct);
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
            await repo.UpdateScanStatusAsync(job.DocumentId, job.TenantId, update, ct);
        }
    }

    // ── Quarantine purge ──────────────────────────────────────────────────────

    private async Task PurgeInfectedFileAsync(ScanJob job, CancellationToken ct)
    {
        try
        {
            await _storage.DeleteAsync(job.StorageKey, ct);
            _log.LogWarning("Purged infected file from quarantine: {Key}", job.StorageKey);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to purge infected file: {Key}", job.StorageKey);
        }
    }

    // ── Scan completion notification ─────────────────────────────────────────

    /// <summary>
    /// Publishes a <see cref="DocumentScanCompletedEvent"/> for a terminal scan outcome.
    /// Non-throwing: any delivery error is logged and measured but never propagated.
    /// Scan state persistence is always the primary concern.
    /// </summary>
    private async Task PublishCompletionEventAsync(
        ScanJob           job,
        ScanStatus        status,
        int               attemptCount,
        string?           engineVersion,
        CancellationToken ct)
    {
        try
        {
            var evt = new DocumentScanCompletedEvent
            {
                DocumentId    = job.DocumentId,
                TenantId      = job.TenantId,
                VersionId     = job.VersionId,
                ScanStatus    = status,
                OccurredAt    = DateTime.UtcNow,
                AttemptCount  = attemptCount,
                EngineVersion = engineVersion,
                FileName      = job.FileName,
                CorrelationId = job.CorrelationId,
            };

            await _publisher.PublishAsync(evt, ct);
        }
        catch (Exception ex)
        {
            // Belt-and-suspenders: publisher implementations should already swallow exceptions
            _log.LogWarning(ex,
                "PublishCompletionEventAsync: unhandled error for Document={DocId} Status={Status}",
                job.DocumentId, status);
        }
    }

    // ── Audit helper ──────────────────────────────────────────────────────────

    private async Task AuditScanEventAsync(
        ScanJob           job,
        string            eventType,
        object            detail,
        CancellationToken ct)
    {
        try
        {
            await using var scope    = _scopes.CreateAsyncScope();
            var auditRepo = scope.ServiceProvider.GetRequiredService<IAuditRepository>();

            var audit = new DocumentAudit
            {
                Id         = Guid.NewGuid(),
                TenantId   = job.TenantId,
                DocumentId = job.DocumentId,
                Event      = eventType,
                ActorId    = null,
                Outcome    = "SUCCESS",
                Detail     = System.Text.Json.JsonSerializer.Serialize(detail),
                OccurredAt = DateTime.UtcNow,
            };

            await auditRepo.InsertAsync(audit, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to write audit event {Event} for Document={DocId}",
                eventType, job.DocumentId);
        }
    }
}
