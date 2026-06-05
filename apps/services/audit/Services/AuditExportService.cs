using System.Text.Json;
using Microsoft.Extensions.Options;
using PlatformAuditEventService.Authorization;
using PlatformAuditEventService.Configuration;
using PlatformAuditEventService.DTOs.Export;
using PlatformAuditEventService.Entities;
using PlatformAuditEventService.Enums;
using PlatformAuditEventService.Repositories;
using PlatformAuditEventService.Services.Export;

using AuditEventQueryRequest = PlatformAuditEventService.DTOs.Query.AuditEventQueryRequest;

namespace PlatformAuditEventService.Services;

/// <summary>
/// Default implementation of <see cref="IAuditExportService"/>.
///
/// Design decisions:
///
/// Synchronous-in-request (v1):
///   The job transitions Pending → Processing → Completed/Failed within the
///   same HTTP request context. This keeps v1 simple and avoids requiring a
///   background worker or message queue. For very large exports, callers should
///   apply narrow time-range filters to stay within HTTP timeout budgets.
///   Future: extract <c>ProcessJobAsync</c> into a BackgroundService or
///   a Quartz.NET job for true async processing.
///
/// Authorization delegation:
///   Scope enforcement is delegated to <see cref="IQueryAuthorizer"/> — the same
///   component used by the query endpoints. The export request is mapped to an
///   <see cref="AuditEventQueryRequest"/> so the authorizer can apply standard
///   constraint rules (TenantId override, ActorId enforcement, VisibilityScope floor).
///
/// Storage abstraction:
///   All file I/O flows through <see cref="IExportStorageProvider"/>. Swapping
///   the backing store (Local → S3 → Azure) requires only a DI registration change.
///
/// Record streaming:
///   Records are streamed via <see cref="IAuditEventRecordRepository.StreamForExportAsync"/>
///   and fed directly to <see cref="AuditExportFormatter.WriteAsync"/> without
///   materializing the full result set in memory.
/// </summary>
public sealed class AuditExportService : IAuditExportService
{
    private readonly IAuditExportJobRepository       _jobRepository;
    private readonly IAuditEventRecordRepository     _recordRepository;
    private readonly IQueryAuthorizer                _authorizer;
    private readonly IExportStorageProvider          _storage;
    private readonly ExportOptions                   _exportOpts;
    private readonly QueryAuthOptions                _queryAuthOpts;
    private readonly ILogger<AuditExportService>     _logger;

    private static readonly JsonSerializerOptions _filterSerializerOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented        = false,
    };

    public AuditExportService(
        IAuditExportJobRepository       jobRepository,
        IAuditEventRecordRepository     recordRepository,
        IQueryAuthorizer                authorizer,
        IExportStorageProvider          storage,
        IOptions<ExportOptions>         exportOpts,
        IOptions<QueryAuthOptions>      queryAuthOpts,
        ILogger<AuditExportService>     logger)
    {
        _jobRepository    = jobRepository;
        _recordRepository = recordRepository;
        _authorizer       = authorizer;
        _storage          = storage;
        _exportOpts       = exportOpts.Value;
        _queryAuthOpts    = queryAuthOpts.Value;
        _logger           = logger;
    }

    // ── IAuditExportService ───────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<ExportStatusResponse> SubmitAsync(
        ExportRequest        request,
        IQueryCallerContext  caller,
        CancellationToken    ct = default)
    {
        // ── Step 1: Map request → query filter ────────────────────────────────
        var queryFilter = MapToQueryFilter(request);

        // ── Step 2: Authorization (same rules as query endpoints) ─────────────
        var authResult = _authorizer.Authorize(caller, queryFilter);
        if (!authResult.IsAuthorized)
        {
            _logger.LogWarning(
                "Export access denied. Scope={Scope} Reason={Reason}",
                caller.Scope, authResult.DenialReason);
            throw new UnauthorizedAccessException(authResult.DenialReason ?? "Export access denied.");
        }

        // ── Step 3: Determine requester identity ──────────────────────────────
        // Prefer UserId from claims; fall back to TenantId for service accounts.
        var requestedBy = caller.UserId
            ?? caller.TenantId
            ?? "anonymous";

        // ── Step 4: Persist job in Pending state ──────────────────────────────
        var filterJson = JsonSerializer.Serialize(queryFilter, _filterSerializerOpts);
        var job = await _jobRepository.CreateAsync(new AuditExportJob
        {
            ExportId     = Guid.NewGuid(),
            RequestedBy  = requestedBy,
            ScopeType    = request.ScopeType,
            ScopeId      = request.ScopeId,
            FilterJson   = filterJson,
            Format       = request.Format,
            Status       = ExportStatus.Pending,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        }, ct);

        _logger.LogInformation(
            "Export job created: ExportId={ExportId} RequestedBy={RequestedBy} " +
            "Format={Format} Provider={Provider}",
            job.ExportId, requestedBy, request.Format, _storage.ProviderName);

        // ── Step 5: Process immediately (v1: synchronous in-request) ──────────
        var writerOpts = new ExportWriterOptions(
            IncludeHashes:         request.IncludeHashes && _queryAuthOpts.ExposeIntegrityHash,
            IncludeStateSnapshots: request.IncludeStateSnapshots,
            IncludeTags:           request.IncludeTags);

        await ProcessJobAsync(job, queryFilter, writerOpts, ct);

        return MapToResponse(job);
    }

    /// <inheritdoc/>
    public async Task<ExportStatusResponse?> GetStatusAsync(
        Guid                exportId,
        IQueryCallerContext caller,
        CancellationToken   ct = default)
    {
        var job = await _jobRepository.GetByExportIdAsync(exportId, ct);
        if (job is null)
            return null;

        // PlatformAdmin callers may inspect any export job (cross-tenant read).
        // All other callers must satisfy two independent ownership checks:
        //
        //  1. Identity ownership:
        //     job.RequestedBy must match the caller's effective identity
        //     (UserId ?? TenantId), which is the value stored at submission time.
        //
        //  2. Tenant scope guard (defense-in-depth):
        //     When the job is explicitly tenant-scoped (ScopeType == Tenant),
        //     the job's ScopeId must equal the caller's TenantId.
        //     This guards against the edge case where UserId values are not
        //     globally unique across tenants — a matching RequestedBy alone would
        //     not be sufficient without this second, tenant-bound check.
        //     For non-Tenant-scoped jobs (Organization, Global, Platform) the
        //     tenant guard is skipped and identity ownership alone applies.
        if (caller.Scope != Authorization.CallerScope.PlatformAdmin)
        {
            var effectiveCallerId = caller.UserId ?? caller.TenantId;
            var ownershipMatches  = effectiveCallerId is not null
                                    && job.RequestedBy == effectiveCallerId;

            var tenantScopeOk = job.ScopeType != Enums.ScopeType.Tenant
                                || caller.TenantId is null
                                || job.ScopeId == caller.TenantId;

            if (!ownershipMatches || !tenantScopeOk)
            {
                _logger.LogWarning(
                    "GetStatusAsync: ExportId={ExportId} access denied. " +
                    "Scope={Scope} EffectiveCallerId={CallerId} RequestedBy={RequestedBy} " +
                    "JobScopeType={ScopeType} JobScopeId={ScopeId} CallerTenantId={TenantId}",
                    exportId, caller.Scope,
                    effectiveCallerId ?? "(none)", job.RequestedBy,
                    job.ScopeType, job.ScopeId ?? "(none)",
                    caller.TenantId ?? "(none)");
                return null;
            }
        }

        return MapToResponse(job);
    }

    /// <inheritdoc/>
    public async Task ProcessJobAsync(Guid exportId, CancellationToken ct = default)
    {
        var job = await _jobRepository.GetByExportIdAsync(exportId, ct);
        if (job is null)
        {
            _logger.LogWarning(
                "ProcessJobAsync: ExportId={ExportId} not found. Skipping.", exportId);
            return;
        }

        // Skip jobs already in a terminal state
        if (job.Status is ExportStatus.Completed or ExportStatus.Failed
                       or ExportStatus.Cancelled or ExportStatus.Expired)
        {
            _logger.LogDebug(
                "ProcessJobAsync: ExportId={ExportId} already in terminal state {Status}. Skipping.",
                exportId, job.Status);
            return;
        }

        // Deserialise the stored filter
        AuditEventQueryRequest queryFilter;
        try
        {
            if (string.IsNullOrWhiteSpace(job.FilterJson))
                throw new InvalidOperationException("FilterJson is null or empty.");

            queryFilter = System.Text.Json.JsonSerializer.Deserialize<AuditEventQueryRequest>(
                job.FilterJson, _filterSerializerOpts)
                ?? throw new InvalidOperationException("Deserialised filter was null.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ProcessJobAsync: failed to deserialise FilterJson for ExportId={ExportId}.",
                exportId);

            job.Status       = ExportStatus.Failed;
            job.ErrorMessage = $"FilterJson deserialisation failed: {ex.Message}";
            job.CompletedAtUtc = DateTimeOffset.UtcNow;
            await _jobRepository.UpdateAsync(job, ct);
            return;
        }

        // Use safe defaults for background processing (no live HTTP caller context)
        var writerOpts = new ExportWriterOptions(
            IncludeHashes:         _queryAuthOpts.ExposeIntegrityHash,
            IncludeStateSnapshots: true,
            IncludeTags:           true);

        await ProcessJobAsync(job, queryFilter, writerOpts, ct);
    }

    // ── Private: processing ───────────────────────────────────────────────────

    /// <summary>
    /// Drives the Pending → Processing → Completed / Failed state machine.
    /// Streams records through the formatter and into the storage provider.
    /// All exceptions are caught and translated to the Failed terminal state.
    /// </summary>
    private async Task ProcessJobAsync(
        AuditExportJob         job,
        AuditEventQueryRequest queryFilter,
        ExportWriterOptions    writerOpts,
        CancellationToken      ct)
    {
        // Transition: Pending → Processing
        job.Status = ExportStatus.Processing;
        await _jobRepository.UpdateAsync(job, ct);

        try
        {
            long recordCount = 0;

            var records = _recordRepository.StreamForExportAsync(queryFilter, ct);

            var filePath = await _storage.WriteAsync(
                job.ExportId,
                job.Format,
                async stream =>
                {
                    recordCount = await AuditExportFormatter.WriteAsync(
                        stream, records, job.ExportId, job.Format, writerOpts, ct);
                },
                ct);

            // Transition: Processing → Completed
            job.Status         = ExportStatus.Completed;
            job.FilePath       = filePath;
            job.RecordCount    = recordCount;
            job.CompletedAtUtc = DateTimeOffset.UtcNow;

            _logger.LogInformation(
                "Export job completed: ExportId={ExportId} RecordCount={RecordCount} Path={Path}",
                job.ExportId, recordCount, filePath);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Transition: Processing → Failed
            job.Status         = ExportStatus.Failed;
            job.ErrorMessage   = ex.Message;
            job.CompletedAtUtc = DateTimeOffset.UtcNow;

            _logger.LogError(ex,
                "Export job failed: ExportId={ExportId} Error={Error}",
                job.ExportId, ex.Message);
        }

        await _jobRepository.UpdateAsync(job, ct);
    }

    // ── Mapping helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Translate the public <see cref="ExportRequest"/> into the internal query filter
    /// format understood by <see cref="IQueryAuthorizer"/> and
    /// <see cref="IAuditEventRecordRepository.StreamForExportAsync"/>.
    ///
    /// Scope fields (TenantId, OrganizationId) are populated from the request's
    /// ScopeType + ScopeId. The authorizer will then override these with the
    /// caller's own claims to prevent scope escalation.
    /// </summary>
    private static AuditEventQueryRequest MapToQueryFilter(ExportRequest req)
    {
        string? tenantId = null;
        string? orgId    = null;

        switch (req.ScopeType)
        {
            case ScopeType.Tenant:
                tenantId = req.ScopeId;
                break;
            case ScopeType.Organization:
                // OrganizationId is the ScopeId; TenantId will be enforced by the authorizer.
                orgId = req.ScopeId;
                break;
            // Global / Platform → no tenant/org constraint from request; authorizer enforces.
        }

        return new AuditEventQueryRequest
        {
            TenantId       = tenantId,
            OrganizationId = orgId,
            Category       = req.Category,
            MinSeverity    = req.MinSeverity,
            EventTypes     = req.EventTypes?.ToList(),
            ActorId        = req.ActorId,
            EntityType     = req.EntityType,
            EntityId       = req.EntityId,
            From           = req.From,
            To             = req.To,
            CorrelationId  = req.CorrelationId,
            // PageSize / Page are ignored by StreamForExportAsync — include default values
            // so the validator (if called on this object) does not complain.
            Page     = 1,
            PageSize = 50,
        };
    }

    private static ExportStatusResponse MapToResponse(AuditExportJob job) =>
        new()
        {
            ExportId       = job.ExportId,
            ScopeType      = job.ScopeType,
            ScopeId        = job.ScopeId,
            Format         = job.Format,
            Status         = job.Status,
            DownloadUrl    = job.FilePath,
            RecordCount    = job.RecordCount,
            ErrorMessage   = job.ErrorMessage,
            CreatedAtUtc   = job.CreatedAtUtc,
            CompletedAtUtc = job.CompletedAtUtc,
        };
}
