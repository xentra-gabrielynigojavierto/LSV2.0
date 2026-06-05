using Documents.Application.DTOs;
using Documents.Application.Exceptions;
using Documents.Application.Models;
using Documents.Domain.Entities;
using Documents.Domain.Enums;
using Documents.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Documents.Application.Services;

public sealed class DocumentServiceOptions
{
    public bool RequireCleanScanForAccess { get; set; } = false;
    public int  SignedUrlTtlSeconds       { get; set; } = 30;

    /// <summary>
    /// Maximum file size (MB) accepted at the upload API layer.
    /// Requests exceeding this limit are rejected with HTTP 413 before any storage is allocated.
    /// Must be ≤ MaxScannableFileSizeMb.
    /// </summary>
    public int MaxUploadSizeMb { get; set; } = 25;

    /// <summary>
    /// Maximum file size (MB) that may be submitted for antivirus scanning.
    /// Files exceeding this limit are rejected with HTTP 422 before enqueueing.
    /// Operators should align this with ClamAV's configured StreamMaxLength.
    /// </summary>
    public int MaxScannableFileSizeMb { get; set; } = 25;
}

public sealed class DocumentService
{
    private readonly IDocumentRepository        _docs;
    private readonly IDocumentVersionRepository _versions;
    private readonly IStorageProvider           _storage;
    private readonly ScanService                _scan;
    private readonly ScanOrchestrationService   _scanOrchestration;
    private readonly AuditService               _audit;
    private readonly DocumentServiceOptions     _opts;
    private readonly ILogger<DocumentService>   _log;

    private static readonly HashSet<string> AllowedMimeTypes = new()
    {
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "image/jpeg",
        "image/png",
        "image/tiff",
        "text/plain",
        "text/csv",
    };

    /// <summary>
    /// The reserved document type GUID for tenant logo files.
    /// Only tenant/platform admins may assign this type directly.
    /// </summary>
    private static readonly Guid TenantLogoDocTypeId =
        Guid.Parse("20000000-0000-0000-0000-000000000002");

    /// <summary>
    /// Maps allowed MIME types to their expected file magic-byte signatures.
    /// At least one signature must match the file header for the declared type to be accepted.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, byte[][]> MimeTypeSignatures =
        new Dictionary<string, byte[][]>
        {
            ["application/pdf"] = new[]
            {
                new byte[] { 0x25, 0x50, 0x44, 0x46 },                         // %PDF
            },
            ["application/msword"] = new[]
            {
                new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }, // OLE2
            },
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = new[]
            {
                new byte[] { 0x50, 0x4B, 0x03, 0x04 },                         // ZIP/PK
            },
            ["application/vnd.ms-excel"] = new[]
            {
                new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }, // OLE2
            },
            ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = new[]
            {
                new byte[] { 0x50, 0x4B, 0x03, 0x04 },                         // ZIP/PK
            },
            ["image/jpeg"] = new[]
            {
                new byte[] { 0xFF, 0xD8, 0xFF },                                // JPEG SOI
            },
            ["image/png"] = new[]
            {
                new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, // PNG
            },
            ["image/tiff"] = new[]
            {
                new byte[] { 0x49, 0x49, 0x2A, 0x00 },                         // TIFF little-endian
                new byte[] { 0x4D, 0x4D, 0x00, 0x2A },                         // TIFF big-endian
            },
        };

    /// <summary>
    /// Binary magic-byte prefixes that must NOT appear in text/plain or text/csv files.
    /// </summary>
    private static readonly byte[][] BinaryMagicPrefixes =
    {
        new byte[] { 0xD0, 0xCF, 0x11, 0xE0 },  // OLE2
        new byte[] { 0x25, 0x50, 0x44, 0x46 },  // PDF
        new byte[] { 0x50, 0x4B, 0x03, 0x04 },  // ZIP/PK
        new byte[] { 0xFF, 0xD8, 0xFF },         // JPEG
        new byte[] { 0x89, 0x50, 0x4E, 0x47 },  // PNG
        new byte[] { 0x49, 0x49, 0x2A, 0x00 },  // TIFF LE
        new byte[] { 0x4D, 0x4D, 0x00, 0x2A },  // TIFF BE
    };

    public DocumentService(
        IDocumentRepository        docs,
        IDocumentVersionRepository versions,
        IStorageProvider           storage,
        ScanService                scan,
        ScanOrchestrationService   scanOrchestration,
        AuditService               audit,
        IOptions<DocumentServiceOptions> opts,
        ILogger<DocumentService>   log)
    {
        _docs              = docs;
        _versions          = versions;
        _storage           = storage;
        _scan              = scan;
        _scanOrchestration = scanOrchestration;
        _audit             = audit;
        _opts              = opts.Value;
        _log               = log;
    }

    // ── Create ───────────────────────────────────────────────────────────────

    public async Task<DocumentResponse> CreateAsync(
        CreateDocumentRequest req,
        Stream                fileStream,
        string                fileName,
        string                mimeType,
        long                  fileSizeBytes,
        RequestContext        ctx,
        CancellationToken     ct = default)
    {
        AssertTenantScope(ctx.Principal, req.TenantId);
        AssertPermission(ctx.Principal, "write");

        if (req.DocumentTypeId == TenantLogoDocTypeId && !IsAdminPrincipal(ctx.Principal))
            throw new ForbiddenException("The tenant logo document type is reserved and cannot be assigned directly.");

        ValidateMimeType(mimeType);

        // ── File size enforcement ─────────────────────────────────────────────
        // 413: upload limit (fast-path at API layer; this is the service-layer safety net)
        var uploadLimitBytes  = (long)_opts.MaxUploadSizeMb * 1_048_576L;
        if (fileSizeBytes > uploadLimitBytes)
            throw new FileTooLargeException(fileSizeBytes, _opts.MaxUploadSizeMb);

        // 422: scan-compatibility limit — reject before any storage I/O
        var scanLimitBytes = (long)_opts.MaxScannableFileSizeMb * 1_048_576L;
        if (fileSizeBytes > scanLimitBytes)
        {
            _log.LogWarning(
                "Scan-size policy rejection: File={File} SizeBytes={Size} LimitMb={Limit} CorrelationId={Corr}",
                fileName, fileSizeBytes, _opts.MaxScannableFileSizeMb, ctx.CorrelationId);
            throw new FileSizeExceedsScanLimitException(fileSizeBytes, _opts.MaxScannableFileSizeMb);
        }

        // Store in quarantine prefix — scan status starts as Pending
        var storageKey    = BuildQuarantineKey(req.TenantId, req.DocumentTypeId, fileName);
        var storageBucket = await _storage.UploadAsync(storageKey, fileStream, mimeType, ct);

        var doc = Document.Create(
            req.TenantId,
            req.ProductId,
            req.ReferenceId,
            req.ReferenceType,
            req.DocumentTypeId,
            req.Title,
            req.Description,
            mimeType,
            fileSizeBytes,
            storageKey,
            storageBucket,
            checksum: null,
            ctx.Principal.UserId);

        // ScanStatus.Pending is the default — file access blocked until scan completes
        var created = await _docs.CreateAsync(doc, ct);

        // Enqueue async background scan — does NOT block the upload response
        await _scanOrchestration.EnqueueDocumentScanAsync(created, fileName, mimeType, ctx, ct);

        await _audit.LogAsync(AuditEvent.DocumentCreated, ctx, created.Id,
            detail: new { mimeType, fileSizeBytes, scanStatus = ScanStatus.Pending.ToString() });

        return DocumentResponse.From(created);
    }

    // ── List ─────────────────────────────────────────────────────────────────

    public async Task<DocumentListResponse> ListAsync(
        ListDocumentsRequest req,
        RequestContext       ctx,
        CancellationToken    ct = default)
    {
        AssertPermission(ctx.Principal, "read");

        var filter = new DocumentFilter
        {
            TenantId      = ctx.EffectiveTenantId,
            ProductId     = req.ProductId,
            ReferenceId   = req.ReferenceId,
            ReferenceType = req.ReferenceType,
            Status        = req.Status,
            Limit         = Math.Min(req.Limit, 200),
            Offset        = req.Offset,
        };

        var (items, total) = await _docs.ListAsync(filter, ct);

        return new DocumentListResponse
        {
            Data   = items.Select(DocumentResponse.From).ToList(),
            Total  = total,
            Limit  = filter.Limit,
            Offset = filter.Offset,
        };
    }

    // ── Get by ID ────────────────────────────────────────────────────────────

    public async Task<DocumentResponse> GetByIdAsync(
        Guid              id,
        RequestContext    ctx,
        CancellationToken ct = default)
    {
        AssertPermission(ctx.Principal, "read");

        var doc = await _docs.FindByIdAsync(id, ctx.EffectiveTenantId, ct)
            ?? throw new NotFoundException("Document", id);

        await AssertDocumentTenantScopeAsync(ctx, doc);
        return DocumentResponse.From(doc);
    }

    // ── Update ───────────────────────────────────────────────────────────────

    public async Task<DocumentResponse> UpdateAsync(
        Guid                  id,
        UpdateDocumentRequest req,
        RequestContext        ctx,
        CancellationToken     ct = default)
    {
        AssertPermission(ctx.Principal, "write");

        var doc = await _docs.FindByIdAsync(id, ctx.EffectiveTenantId, ct)
            ?? throw new NotFoundException("Document", id);

        await AssertDocumentTenantScopeAsync(ctx, doc);

        if (req.Title        is not null) doc.Title          = req.Title;
        if (req.Description  is not null) doc.Description    = req.Description;
        if (req.DocumentTypeId.HasValue)
        {
            if (req.DocumentTypeId.Value == TenantLogoDocTypeId && !IsAdminPrincipal(ctx.Principal))
                throw new ForbiddenException("The tenant logo document type is reserved and cannot be assigned directly.");
            doc.DocumentTypeId = req.DocumentTypeId.Value;
        }
        if (req.RetainUntil.HasValue)     doc.RetainUntil    = req.RetainUntil;

        if (req.Status is not null)
        {
            var newStatus = ParseStatus(req.Status);
            if (newStatus == DocumentStatus.LegalHold && !doc.LegalHoldAt.HasValue)
                doc.LegalHoldAt = DateTime.UtcNow;
            doc.Status = newStatus;
        }

        doc.UpdatedAt = DateTime.UtcNow;
        doc.UpdatedBy = ctx.Principal.UserId;

        var updated = await _docs.UpdateAsync(doc, ct);

        await _audit.LogAsync(AuditEvent.DocumentUpdated, ctx, id,
            detail: new { changes = req });

        return DocumentResponse.From(updated);
    }

    // ── Delete ───────────────────────────────────────────────────────────────

    public async Task DeleteAsync(
        Guid              id,
        RequestContext    ctx,
        CancellationToken ct = default)
    {
        AssertPermission(ctx.Principal, "delete");

        var doc = await _docs.FindByIdAsync(id, ctx.EffectiveTenantId, ct)
            ?? throw new NotFoundException("Document", id);

        await AssertDocumentTenantScopeAsync(ctx, doc);

        if (doc.IsOnLegalHold)
            throw new ForbiddenException("Document is on legal hold and cannot be deleted");

        await _docs.SoftDeleteAsync(id, ctx.EffectiveTenantId, ctx.Principal.UserId, ct);

        await _audit.LogAsync(AuditEvent.DocumentDeleted, ctx, id);
    }

    // ── Upload version ───────────────────────────────────────────────────────

    public async Task<DocumentVersionResponse> CreateVersionAsync(
        Guid                       documentId,
        UploadDocumentVersionRequest req,
        Stream                     fileStream,
        string                     fileName,
        string                     mimeType,
        long                       fileSizeBytes,
        RequestContext             ctx,
        CancellationToken          ct = default)
    {
        AssertPermission(ctx.Principal, "write");

        var doc = await _docs.FindByIdAsync(documentId, ctx.EffectiveTenantId, ct)
            ?? throw new NotFoundException("Document", documentId);

        await AssertDocumentTenantScopeAsync(ctx, doc);

        if (doc.DocumentTypeId == TenantLogoDocTypeId && !IsAdminPrincipal(ctx.Principal))
            throw new ForbiddenException("Only tenant or platform administrators may upload new versions of a logo document.");

        ValidateMimeType(mimeType);

        // ── File size enforcement (mirrors CreateAsync) ───────────────────────
        var uploadLimitBytesV = (long)_opts.MaxUploadSizeMb * 1_048_576L;
        if (fileSizeBytes > uploadLimitBytesV)
            throw new FileTooLargeException(fileSizeBytes, _opts.MaxUploadSizeMb);

        var scanLimitBytesV = (long)_opts.MaxScannableFileSizeMb * 1_048_576L;
        if (fileSizeBytes > scanLimitBytesV)
        {
            _log.LogWarning(
                "Scan-size policy rejection (version): File={File} SizeBytes={Size} LimitMb={Limit} CorrelationId={Corr}",
                fileName, fileSizeBytes, _opts.MaxScannableFileSizeMb, ctx.CorrelationId);
            throw new FileSizeExceedsScanLimitException(fileSizeBytes, _opts.MaxScannableFileSizeMb);
        }

        // Store in quarantine prefix — scan status starts as Pending
        var storageKey    = BuildQuarantineKey(doc.TenantId, doc.DocumentTypeId, fileName);
        var storageBucket = await _storage.UploadAsync(storageKey, fileStream, mimeType, ct);

        var version = new DocumentVersion
        {
            Id            = Guid.NewGuid(),
            DocumentId    = documentId,
            TenantId      = doc.TenantId,
            VersionNumber = doc.VersionCount + 1,
            MimeType      = mimeType,
            FileSizeBytes = fileSizeBytes,
            StorageKey    = storageKey,
            StorageBucket = storageBucket,
            ScanStatus    = ScanStatus.Pending,
            Label         = req.Label,
            UploadedAt    = DateTime.UtcNow,
            UploadedBy    = ctx.Principal.UserId,
        };

        var created = await _versions.CreateAsync(version, ct);

        // Update parent document — track current version, inherit Pending scan status
        doc.CurrentVersionId = created.Id;
        doc.VersionCount     = created.VersionNumber;
        doc.ScanStatus       = ScanStatus.Pending;
        doc.ScanCompletedAt  = null;
        doc.UpdatedAt        = DateTime.UtcNow;
        doc.UpdatedBy        = ctx.Principal.UserId;
        await _docs.UpdateAsync(doc, ct);

        // Enqueue async background scan — does NOT block the upload response
        await _scanOrchestration.EnqueueVersionScanAsync(created, doc, fileName, mimeType, ctx, ct);

        await _audit.LogAsync(AuditEvent.VersionUploaded, ctx, documentId,
            detail: new { versionId = created.Id, versionNumber = created.VersionNumber, mimeType,
                          scanStatus = ScanStatus.Pending.ToString() });

        return DocumentVersionResponse.From(created);
    }

    // ── Logo registration ────────────────────────────────────────────────────

    /// <summary>
    /// Registers a document as the published tenant logo. Only admins may call this.
    /// Clears the IsPublishedAsLogo flag on any prior logo document for the same tenant
    /// before setting it on the target document.
    /// </summary>
    public async Task RegisterAsLogoAsync(
        Guid              documentId,
        RequestContext    ctx,
        CancellationToken ct = default)
    {
        if (!IsAdminPrincipal(ctx.Principal))
            throw new ForbiddenException("Only tenant or platform administrators may register a logo document.");

        var doc = await _docs.FindByIdAsync(documentId, ctx.EffectiveTenantId, ct)
            ?? throw new NotFoundException("Document", documentId);

        await AssertDocumentTenantScopeAsync(ctx, doc);

        if (doc.DocumentTypeId != TenantLogoDocTypeId)
            throw new ForbiddenException("Only documents of the tenant logo type may be registered as a logo.");

        // Clear the flag on any existing logo documents for this tenant
        await _docs.ClearPublishedLogoFlagAsync(ctx.EffectiveTenantId, ct);

        doc.IsPublishedAsLogo = true;
        doc.UpdatedAt = DateTime.UtcNow;
        doc.UpdatedBy = ctx.Principal.UserId;
        await _docs.UpdateAsync(doc, ct);

        await _audit.LogAsync(AuditEvent.DocumentUpdated, ctx, documentId,
            detail: new { action = "LOGO_REGISTERED" });
    }

    /// <summary>
    /// Removes the published logo registration for a specific document.
    /// </summary>
    public async Task UnregisterLogoAsync(
        Guid              documentId,
        RequestContext    ctx,
        CancellationToken ct = default)
    {
        if (!IsAdminPrincipal(ctx.Principal))
            throw new ForbiddenException("Only tenant or platform administrators may unregister a logo document.");

        var doc = await _docs.FindByIdAsync(documentId, ctx.EffectiveTenantId, ct)
            ?? throw new NotFoundException("Document", documentId);

        await AssertDocumentTenantScopeAsync(ctx, doc);

        doc.IsPublishedAsLogo = false;
        doc.UpdatedAt = DateTime.UtcNow;
        doc.UpdatedBy = ctx.Principal.UserId;
        await _docs.UpdateAsync(doc, ct);

        await _audit.LogAsync(AuditEvent.DocumentUpdated, ctx, documentId,
            detail: new { action = "LOGO_UNREGISTERED" });
    }

    /// <summary>
    /// Clears the logo registration flag for all documents belonging to the tenant.
    /// Used when the tenant logo is deleted and no replacement is set.
    /// </summary>
    public async Task ClearTenantLogoRegistrationAsync(
        RequestContext    ctx,
        CancellationToken ct = default)
    {
        if (!IsAdminPrincipal(ctx.Principal))
            throw new ForbiddenException("Only tenant or platform administrators may clear the logo registration.");

        await _docs.ClearPublishedLogoFlagAsync(ctx.EffectiveTenantId, ct);

        await _audit.LogAsync(AuditEvent.DocumentUpdated, ctx, null,
            detail: new { action = "TENANT_LOGO_REGISTRATION_CLEARED" });
    }

    // ── List versions ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<DocumentVersionResponse>> ListVersionsAsync(
        Guid              documentId,
        RequestContext    ctx,
        CancellationToken ct = default)
    {
        AssertPermission(ctx.Principal, "read");

        var doc = await _docs.FindByIdAsync(documentId, ctx.EffectiveTenantId, ct)
            ?? throw new NotFoundException("Document", documentId);

        await AssertDocumentTenantScopeAsync(ctx, doc);

        var versions = await _versions.ListByDocumentAsync(documentId, ctx.EffectiveTenantId, ct);
        return versions.Select(DocumentVersionResponse.From).ToList();
    }

    // ── Signed URL (direct — for view/download URL endpoints) ────────────────

    public async Task<string> GetSignedUrlAsync(
        Guid              documentId,
        string            disposition,
        RequestContext    ctx,
        CancellationToken ct = default)
    {
        AssertPermission(ctx.Principal, "read");

        var doc = await _docs.FindByIdAsync(documentId, ctx.EffectiveTenantId, ct)
            ?? throw new NotFoundException("Document", documentId);

        await AssertDocumentTenantScopeAsync(ctx, doc);

        // Enforce scan gate — emit SCAN_ACCESS_DENIED audit if blocked
        try
        {
            _scan.EnforceCleanScan(doc, _opts.RequireCleanScanForAccess);
        }
        catch (ScanBlockedException)
        {
            await _audit.LogAsync(AuditEvent.ScanAccessDenied, ctx, documentId,
                outcome: "DENIED",
                detail: new
                {
                    scanStatus       = doc.ScanStatus.ToString(),
                    requireCleanScan = _opts.RequireCleanScanForAccess,
                });
            throw;
        }

        return await _storage.GenerateSignedUrlAsync(
            doc.StorageKey,
            _opts.SignedUrlTtlSeconds,
            disposition,
            ct);
    }

    // ── Direct content access (authenticated 302) ─────────────────────────────

    public async Task<string> GetContentRedirectAsync(
        Guid              documentId,
        string            disposition,
        RequestContext    ctx,
        CancellationToken ct = default)
    {
        var url = await GetSignedUrlAsync(documentId, disposition, ctx, ct);

        await _audit.LogAsync(AuditEvent.DocumentAccessed, ctx, documentId,
            detail: new { disposition });

        return url;
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static readonly Dictionary<string, string[]> Permissions = new()
    {
        ["DocReader"]    = new[] { "read" },
        ["DocUploader"]  = new[] { "read", "write" },
        ["DocManager"]   = new[] { "read", "write", "delete" },
        ["TenantAdmin"]  = new[] { "read", "write", "delete" },
        ["PlatformAdmin"] = new[] { "read", "write", "delete", "admin" },
    };

    private static void AssertPermission(Domain.ValueObjects.Principal principal, string action)
    {
        var hasPermission = principal.Roles.Any(role =>
            Permissions.TryGetValue(role, out var perms) && perms.Contains(action));

        if (!hasPermission)
            throw new ForbiddenException(
                $"Role(s) [{string.Join(", ", principal.Roles)}] do not have '{action}' permission");
    }

    private static void AssertTenantScope(Domain.ValueObjects.Principal principal, Guid bodyTenantId)
    {
        if (!principal.IsPlatformAdmin && principal.TenantId != bodyTenantId)
            throw new ForbiddenException("Tenant scope mismatch");
    }

    private static bool IsAdminPrincipal(Domain.ValueObjects.Principal principal) =>
        principal.Roles.Any(r => r is "TenantAdmin" or "PlatformAdmin");

    private async Task AssertDocumentTenantScopeAsync(RequestContext ctx, Document doc)
    {
        if (doc.TenantId == ctx.Principal.TenantId) return;  // same tenant — OK

        if (!ctx.Principal.IsPlatformAdmin)
        {
            await _audit.LogAsync(AuditEvent.TenantIsolationViolation, ctx, doc.Id, outcome: "DENIED",
                detail: new { resourceTenantId = doc.TenantId });
            throw new TenantIsolationException();
        }

        // PlatformAdmin cross-tenant — allow + audit
        await _audit.LogAsync(AuditEvent.AdminCrossTenantAccess, ctx, doc.Id,
            detail: new { actorTenantId = ctx.Principal.TenantId, resourceTenantId = doc.TenantId });
    }

    private static void ValidateMimeType(string mimeType)
    {
        if (!AllowedMimeTypes.Contains(mimeType))
            throw new UnsupportedFileTypeException($"File type not permitted: {mimeType}");
    }

    /// <summary>
    /// Validates that the first bytes of <paramref name="stream"/> match the expected magic bytes
    /// for <paramref name="declaredMimeType"/>. Throws <see cref="UnsupportedFileTypeException"/>
    /// if the file content does not match the declared type.
    /// </summary>
    public static async Task ValidateFileSignatureAsync(
        Stream            stream,
        string            declaredMimeType,
        CancellationToken ct = default)
    {
        var header    = new byte[8];
        var totalRead = 0;
        while (totalRead < header.Length)
        {
            var n = await stream.ReadAsync(header, totalRead, header.Length - totalRead, ct);
            if (n == 0) break;
            totalRead += n;
        }

        if (totalRead == 0)
            throw new UnsupportedFileTypeException("File is empty; cannot validate file type.");

        if (MimeTypeSignatures.TryGetValue(declaredMimeType, out var signatures))
        {
            var matched = signatures.Any(sig => HeaderStartsWith(header, totalRead, sig));

            if (!matched)
                throw new UnsupportedFileTypeException(
                    $"File content does not match declared MIME type: {declaredMimeType}");
        }
        else if (declaredMimeType is "text/plain" or "text/csv")
        {
            var isBinary = BinaryMagicPrefixes.Any(sig => HeaderStartsWith(header, totalRead, sig));

            if (isBinary)
                throw new UnsupportedFileTypeException(
                    $"File content appears to be binary data, not text: {declaredMimeType}");
        }
    }

    private static bool HeaderStartsWith(byte[] header, int headerLength, byte[] signature)
    {
        if (headerLength < signature.Length) return false;
        for (var i = 0; i < signature.Length; i++)
            if (header[i] != signature[i]) return false;
        return true;
    }

    /// <summary>
    /// Build a quarantine-prefixed storage key for newly uploaded files.
    /// Files remain under this key until the background scan worker processes them.
    /// Access is gated by application-layer scan status enforcement, not by key obscurity.
    /// </summary>
    private static string BuildQuarantineKey(Guid tenantId, Guid docTypeId, string fileName)
    {
        var ext = Path.GetExtension(fileName).TrimStart('.');
        return $"quarantine/{tenantId}/{docTypeId}/{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.{ext}";
    }

    private static DocumentStatus ParseStatus(string status) => status.ToUpperInvariant() switch
    {
        "DRAFT"      => DocumentStatus.Draft,
        "ACTIVE"     => DocumentStatus.Active,
        "ARCHIVED"   => DocumentStatus.Archived,
        "LEGAL_HOLD" => DocumentStatus.LegalHold,
        _            => throw new ValidationException(new() { ["status"] = new[] { "Invalid status value" } }),
    };
}
