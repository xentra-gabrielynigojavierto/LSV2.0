using Documents.Application.Exceptions;
using Documents.Domain.Entities;
using Documents.Domain.Enums;
using Documents.Domain.Interfaces;
using Documents.Application.Models;
using Microsoft.Extensions.Logging;

namespace Documents.Application.Services;

/// <summary>
/// Application-layer coordinator for the asynchronous scan workflow.
/// Called by DocumentService after upload to enqueue a scan job.
/// The actual scan is performed by DocumentScanWorker (background worker).
///
/// Backpressure: TryEnqueueAsync is non-blocking. If the queue is full,
/// QueueSaturationException is thrown → mapped to HTTP 503 by middleware.
/// </summary>
public sealed class ScanOrchestrationService
{
    private readonly IScanJobQueue                       _queue;
    private readonly AuditService                        _audit;
    private readonly ILogger<ScanOrchestrationService>  _log;

    public ScanOrchestrationService(
        IScanJobQueue                       queue,
        AuditService                        audit,
        ILogger<ScanOrchestrationService>   log)
    {
        _queue = queue;
        _audit = audit;
        _log   = log;
    }

    /// <summary>
    /// Enqueue an antivirus scan for a newly uploaded document (no version).
    /// Throws QueueSaturationException (→ HTTP 503) if the queue is full.
    /// </summary>
    public async Task EnqueueDocumentScanAsync(
        Document       doc,
        string         fileName,
        string         mimeType,
        RequestContext ctx,
        CancellationToken ct = default)
    {
        var job = new ScanJob
        {
            DocumentId    = doc.Id,
            TenantId      = doc.TenantId,
            VersionId     = null,
            StorageKey    = doc.StorageKey,
            FileName      = fileName,
            MimeType      = mimeType,
            CorrelationId = ctx.CorrelationId,
        };

        await EnqueueInternalAsync(job, ctx, doc.Id, fileName, mimeType, ct);
    }

    /// <summary>
    /// Enqueue an antivirus scan for a newly uploaded document version.
    /// Throws QueueSaturationException (→ HTTP 503) if the queue is full.
    /// </summary>
    public async Task EnqueueVersionScanAsync(
        DocumentVersion version,
        Document        parentDoc,
        string          fileName,
        string          mimeType,
        RequestContext  ctx,
        CancellationToken ct = default)
    {
        var job = new ScanJob
        {
            DocumentId    = parentDoc.Id,
            TenantId      = parentDoc.TenantId,
            VersionId     = version.Id,
            StorageKey    = version.StorageKey,
            FileName      = fileName,
            MimeType      = mimeType,
            CorrelationId = ctx.CorrelationId,
        };

        await EnqueueInternalAsync(job, ctx, parentDoc.Id, fileName, mimeType, ct,
            versionId: version.Id);
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    private async Task EnqueueInternalAsync(
        ScanJob           job,
        RequestContext    ctx,
        Guid              documentId,
        string            fileName,
        string            mimeType,
        CancellationToken ct,
        Guid?             versionId = null)
    {
        // Queue depth before enqueue (for audit trail)
        var queueDepthBefore = _queue.Count;

        // Non-blocking fail-fast enqueue — queue tracks its own metrics
        var enqueued = await _queue.TryEnqueueAsync(job, ct);

        if (!enqueued)
        {
            _log.LogError(
                "Scan queue saturated: Document={DocId} Tenant={TenantId} QueueDepth~={Depth} — upload rejected",
                documentId, job.TenantId, queueDepthBefore);

            throw new QueueSaturationException();
        }

        _log.LogInformation(
            "Scan enqueued: Document={DocId} Version={VersionId} Tenant={TenantId} File={File} QueueDepth={Depth}",
            documentId, versionId, job.TenantId, fileName, _queue.Count);

        await _audit.LogAsync(
            AuditEvent.ScanRequested, ctx, documentId,
            detail: new { fileName, mimeType, versionId, queueDepth = _queue.Count });
    }
}
