using Documents.Application.DTOs;
using Documents.Application.Exceptions;
using Documents.Application.Models;
using Documents.Domain.Enums;
using Documents.Domain.Interfaces;
using Documents.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Documents.Application.Services;

public sealed class AccessTokenOptions
{
    public int  TtlSeconds   { get; set; } = 300;
    public int  RedirectTtlSeconds { get; set; } = 30;
    public bool OneTimeUse   { get; set; } = true;
}

public sealed class AccessTokenService
{
    private readonly IAccessTokenStore       _store;
    private readonly IDocumentRepository     _docs;
    private readonly IStorageProvider        _storage;
    private readonly ScanService             _scan;
    private readonly AuditService            _audit;
    private readonly AccessTokenOptions      _opts;
    private readonly DocumentServiceOptions  _docOpts;
    private readonly ILogger<AccessTokenService> _log;

    private static readonly Dictionary<string, string[]> Permissions = new()
    {
        ["DocReader"]     = new[] { "read" },
        ["DocUploader"]   = new[] { "read", "write" },
        ["DocManager"]    = new[] { "read", "write", "delete" },
        ["TenantAdmin"]   = new[] { "read", "write", "delete" },
        ["PlatformAdmin"] = new[] { "read", "write", "delete", "admin" },
    };

    public AccessTokenService(
        IAccessTokenStore       store,
        IDocumentRepository     docs,
        IStorageProvider        storage,
        ScanService             scan,
        AuditService            audit,
        IOptions<AccessTokenOptions>     opts,
        IOptions<DocumentServiceOptions> docOpts,
        ILogger<AccessTokenService>      log)
    {
        _store   = store;
        _docs    = docs;
        _storage = storage;
        _scan    = scan;
        _audit   = audit;
        _opts    = opts.Value;
        _docOpts = docOpts.Value;
        _log     = log;
    }

    public async Task<IssuedTokenResponse> IssueAsync(
        Guid           documentId,
        string         type,
        RequestContext ctx,
        CancellationToken ct = default)
    {
        AssertPermission(ctx.Principal, "read");

        var doc = await _docs.FindByIdAsync(documentId, ctx.EffectiveTenantId, ct)
            ?? throw new NotFoundException("Document", documentId);

        await AssertDocumentTenantScopeAsync(ctx, doc);

        _scan.EnforceCleanScan(doc, _docOpts.RequireCleanScanForAccess);

        var tokenBytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(tokenBytes);
        var tokenString = Convert.ToHexString(tokenBytes).ToLowerInvariant();

        var token = new AccessToken
        {
            Token          = tokenString,
            DocumentId     = documentId,
            TenantId       = ctx.EffectiveTenantId,
            Type           = type,
            IssuedFromIp   = ctx.IpAddress,
            IsUsed         = false,
            ExpiresAt      = DateTime.UtcNow.AddSeconds(_opts.TtlSeconds),
            CreatedAt      = DateTime.UtcNow,
            IssuedToUserId = ctx.Principal.UserId,
        };

        await _store.StoreAsync(token, ct);

        await _audit.LogAsync(
            AuditEvent.AccessTokenIssued,
            ctx,
            documentId,
            detail: new { type, ttlSeconds = _opts.TtlSeconds });

        return new IssuedTokenResponse
        {
            AccessToken      = tokenString,
            RedeemUrl        = $"/access/{tokenString}",
            ExpiresInSeconds = _opts.TtlSeconds,
            Type             = type,
        };
    }

    public async Task<string> RedeemAsync(
        string  tokenString,
        string? ipAddress,
        string? userAgent,
        string? correlationId,
        CancellationToken ct = default)
    {
        var token = await _store.GetAsync(tokenString, ct);

        if (token is null || token.IsExpired)
        {
            _log.LogWarning("Token redemption failed — not found or expired: {Token}", tokenString[..8]);
            throw new TokenExpiredException("Access token has expired");
        }

        if (_opts.OneTimeUse)
        {
            var marked = await _store.MarkUsedAsync(tokenString, ct);
            if (!marked)
            {
                _log.LogWarning("Token already used: {Token}", tokenString[..8]);
                throw new TokenInvalidException("Access token is invalid or has already been used");
            }
        }

        var doc = await _docs.FindByIdAsync(token.DocumentId, token.TenantId, ct)
            ?? throw new NotFoundException("Document", token.DocumentId);

        _scan.EnforceCleanScan(doc);

        var redirectUrl = await _storage.GenerateSignedUrlAsync(
            doc.StorageKey,
            _opts.RedirectTtlSeconds,
            token.Type,
            ct);

        // Minimal context for audit (no JWT — unauthenticated endpoint)
        var fakeCtx = new RequestContext
        {
            Principal     = new Domain.ValueObjects.Principal { UserId = token.IssuedToUserId, TenantId = token.TenantId },
            CorrelationId = correlationId,
            IpAddress     = ipAddress,
            UserAgent     = userAgent,
        };

        await _audit.LogAsync(
            AuditEvent.AccessTokenRedeemed,
            fakeCtx,
            token.DocumentId,
            detail: new { type = token.Type, issuedFromIp = token.IssuedFromIp });

        return redirectUrl;
    }

    private static void AssertPermission(Principal principal, string action)
    {
        var hasPermission = principal.Roles.Any(role =>
            Permissions.TryGetValue(role, out var perms) && perms.Contains(action));

        if (!hasPermission)
            throw new ForbiddenException(
                $"Role(s) [{string.Join(", ", principal.Roles)}] do not have '{action}' permission");
    }

    private async Task AssertDocumentTenantScopeAsync(RequestContext ctx, Domain.Entities.Document doc)
    {
        if (doc.TenantId == ctx.Principal.TenantId) return;

        if (!ctx.Principal.IsPlatformAdmin)
        {
            await _audit.LogAsync(AuditEvent.TenantIsolationViolation, ctx, doc.Id, outcome: "DENIED",
                detail: new { resourceTenantId = doc.TenantId });
            throw new TenantIsolationException();
        }

        await _audit.LogAsync(AuditEvent.AdminCrossTenantAccess, ctx, doc.Id,
            detail: new { actorTenantId = ctx.Principal.TenantId, resourceTenantId = doc.TenantId });
    }
}
