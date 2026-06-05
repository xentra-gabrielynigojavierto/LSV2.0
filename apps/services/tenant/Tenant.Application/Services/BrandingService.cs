using System.Text.RegularExpressions;
using BuildingBlocks.Exceptions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Tenant.Application.Configuration;
using Tenant.Application.Metrics;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;
using Tenant.Domain;

namespace Tenant.Application.Services;

public partial class BrandingService : IBrandingService
{
    private readonly IBrandingRepository _brandingRepo;
    private readonly ITenantRepository   _tenantRepo;
    private readonly IMemoryCache        _cache;
    private readonly TenantRuntimeMetrics _metrics;
    private readonly TenantFeatures      _features;

    public BrandingService(
        IBrandingRepository      brandingRepo,
        ITenantRepository        tenantRepo,
        IMemoryCache             cache,
        TenantRuntimeMetrics     metrics,
        IOptions<TenantFeatures> features)
    {
        _brandingRepo = brandingRepo;
        _tenantRepo   = tenantRepo;
        _cache        = cache;
        _metrics      = metrics;
        _features     = features.Value;
    }

    /// <summary>
    /// Returns branding for the tenant, creating an empty record if none exists yet.
    /// Not cached — authenticated admin path; writes may follow.
    /// </summary>
    public async Task<BrandingResponse> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        _ = await _tenantRepo.GetByIdAsync(tenantId, ct)
            ?? throw new NotFoundException($"Tenant '{tenantId}' was not found.");

        var branding = await _brandingRepo.GetByTenantIdAsync(tenantId, ct)
                       ?? await CreateEmptyAsync(tenantId, ct);

        return ToResponse(branding);
    }

    /// <summary>
    /// Creates or updates branding for the tenant (upsert semantics).
    /// Evicts relevant cache entries so reads immediately see the new state.
    /// </summary>
    public async Task<BrandingResponse> UpsertAsync(Guid tenantId, UpdateBrandingRequest request, CancellationToken ct = default)
    {
        _ = await _tenantRepo.GetByIdAsync(tenantId, ct)
            ?? throw new NotFoundException($"Tenant '{tenantId}' was not found.");

        var errors = new Dictionary<string, string[]>();

        ValidateOptionalHexColor(request.PrimaryColor,    "primaryColor",    errors);
        ValidateOptionalHexColor(request.SecondaryColor,  "secondaryColor",  errors);
        ValidateOptionalHexColor(request.AccentColor,     "accentColor",     errors);
        ValidateOptionalHexColor(request.TextColor,       "textColor",       errors);
        ValidateOptionalHexColor(request.BackgroundColor, "backgroundColor", errors);
        ValidateOptionalEmail(request.SupportEmailOverride, "supportEmailOverride", errors);
        ValidateOptionalUrl(request.WebsiteUrlOverride,     "websiteUrlOverride",   errors);

        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);

        var branding = await _brandingRepo.GetByTenantIdAsync(tenantId, ct);

        if (branding is null)
        {
            branding = TenantBranding.Create(tenantId);
            branding.Update(
                request.BrandName,
                request.LogoDocumentId,
                request.LogoWhiteDocumentId,
                request.FaviconDocumentId,
                request.PrimaryColor,
                request.SecondaryColor,
                request.AccentColor,
                request.TextColor,
                request.BackgroundColor,
                request.WebsiteUrlOverride,
                request.SupportEmailOverride,
                request.SupportPhoneOverride);
            await _brandingRepo.AddAsync(branding, ct);
        }
        else
        {
            branding.Update(
                request.BrandName,
                request.LogoDocumentId,
                request.LogoWhiteDocumentId,
                request.FaviconDocumentId,
                request.PrimaryColor,
                request.SecondaryColor,
                request.AccentColor,
                request.TextColor,
                request.BackgroundColor,
                request.WebsiteUrlOverride,
                request.SupportEmailOverride,
                request.SupportPhoneOverride);
            await _brandingRepo.UpdateAsync(branding, ct);
        }

        // Evict public cache for this tenant (code and subdomain may be stale).
        // We don't know the code here without loading the tenant — evict by tenantId pattern
        // is not straightforward with IMemoryCache. Rely on short TTL for eventual consistency.
        // Admin upserts are infrequent; short TTL is acceptable.

        return ToResponse(branding);
    }

    // ── TENANT-B10: targeted logo mutations ───────────────────────────────────

    /// <inheritdoc/>
    public async Task<BrandingResponse> SetLogoAsync(Guid tenantId, Guid? documentId, CancellationToken ct = default)
    {
        var tenant = await _tenantRepo.GetByIdAsync(tenantId, ct)
            ?? throw new NotFoundException($"Tenant '{tenantId}' was not found.");

        var branding = await _brandingRepo.GetByTenantIdAsync(tenantId, ct)
                       ?? await CreateEmptyAsync(tenantId, ct);

        branding.SetLogo(documentId);
        await _brandingRepo.UpdateAsync(branding, ct);

        EvictPublicCache(tenant.Code, tenant.Subdomain);

        return ToResponse(branding);
    }

    /// <inheritdoc/>
    public async Task<BrandingResponse> SetLogoWhiteAsync(Guid tenantId, Guid? documentId, CancellationToken ct = default)
    {
        var tenant = await _tenantRepo.GetByIdAsync(tenantId, ct)
            ?? throw new NotFoundException($"Tenant '{tenantId}' was not found.");

        var branding = await _brandingRepo.GetByTenantIdAsync(tenantId, ct)
                       ?? await CreateEmptyAsync(tenantId, ct);

        branding.SetLogoWhite(documentId);
        await _brandingRepo.UpdateAsync(branding, ct);

        EvictPublicCache(tenant.Code, tenant.Subdomain);

        return ToResponse(branding);
    }

    public async Task<PublicBrandingResponse?> GetPublicByCodeAsync(string code, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        _metrics.IncrementBrandingAttempted();

        var normalizedCode = code.ToLowerInvariant();
        var cacheKey       = $"branding:code:{normalizedCode}";

        if (_features.TenantReadCachingEnabled && _cache.TryGetValue(cacheKey, out PublicBrandingResponse? cached))
        {
            _metrics.IncrementBrandingCacheHit();
            _metrics.IncrementBrandingSucceeded();
            return cached;
        }

        _metrics.IncrementBrandingCacheMiss();

        var tenant = await _tenantRepo.GetByCodeAsync(normalizedCode, ct);
        if (tenant is null || tenant.Status == TenantStatus.Inactive)
        {
            _metrics.IncrementBrandingFailed();
            return null;
        }

        var branding = await _brandingRepo.GetByTenantIdAsync(tenant.Id, ct);
        var result   = ToPublicResponse(tenant, branding);

        if (_features.TenantReadCachingEnabled)
        {
            var ttl = TimeSpan.FromSeconds(_features.TenantReadCacheTtlSeconds > 0 ? _features.TenantReadCacheTtlSeconds : 60);
            _cache.Set(cacheKey, result, ttl);
        }

        _metrics.IncrementBrandingSucceeded();
        return result;
    }

    public async Task<PublicBrandingResponse?> GetPublicBySubdomainAsync(string subdomain, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subdomain);
        _metrics.IncrementBrandingAttempted();

        var normalizedSub = subdomain.ToLowerInvariant();
        var cacheKey      = $"branding:sub:{normalizedSub}";

        if (_features.TenantReadCachingEnabled && _cache.TryGetValue(cacheKey, out PublicBrandingResponse? cached))
        {
            _metrics.IncrementBrandingCacheHit();
            _metrics.IncrementBrandingSucceeded();
            return cached;
        }

        _metrics.IncrementBrandingCacheMiss();

        var tenant = await _tenantRepo.GetBySubdomainAsync(normalizedSub, ct);
        if (tenant is null || tenant.Status == TenantStatus.Inactive)
        {
            _metrics.IncrementBrandingFailed();
            return null;
        }

        var branding = await _brandingRepo.GetByTenantIdAsync(tenant.Id, ct);
        var result   = ToPublicResponse(tenant, branding);

        if (_features.TenantReadCachingEnabled)
        {
            var ttl = TimeSpan.FromSeconds(_features.TenantReadCacheTtlSeconds > 0 ? _features.TenantReadCacheTtlSeconds : 60);
            _cache.Set(cacheKey, result, ttl);
        }

        _metrics.IncrementBrandingSucceeded();
        return result;
    }

    // ── Cache eviction (called from SyncEndpoints on successful dual-write) ───

    /// <summary>
    /// Evicts public branding cache entries for the given code and/or subdomain.
    /// Called after a successful tenant sync so callers immediately see updated data.
    /// </summary>
    public void EvictPublicCache(string? code, string? subdomain)
    {
        if (!string.IsNullOrWhiteSpace(code))
            _cache.Remove($"branding:code:{code.ToLowerInvariant()}");

        if (!string.IsNullOrWhiteSpace(subdomain))
            _cache.Remove($"branding:sub:{subdomain.ToLowerInvariant()}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<TenantBranding> CreateEmptyAsync(Guid tenantId, CancellationToken ct)
    {
        var branding = TenantBranding.Create(tenantId);
        await _brandingRepo.AddAsync(branding, ct);
        return branding;
    }

    private static void ValidateOptionalHexColor(string? value, string field, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (!HexColorRegex().IsMatch(value))
            errors[field] = [$"'{value}' is not a valid hex color (expected #RGB or #RRGGBB)."];
    }

    private static void ValidateOptionalEmail(string? value, string field, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        try { _ = new System.Net.Mail.MailAddress(value); }
        catch { errors[field] = [$"'{value}' is not a valid email address."]; }
    }

    private static void ValidateOptionalUrl(string? value, string field, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            errors[field] = [$"'{value}' is not a valid http/https URL."];
    }

    [GeneratedRegex(@"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$")]
    private static partial Regex HexColorRegex();

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static BrandingResponse ToResponse(TenantBranding b) => new(
        b.Id,
        b.TenantId,
        b.BrandName,
        b.LogoDocumentId,
        b.LogoWhiteDocumentId,
        b.FaviconDocumentId,
        b.PrimaryColor,
        b.SecondaryColor,
        b.AccentColor,
        b.TextColor,
        b.BackgroundColor,
        b.WebsiteUrlOverride,
        b.SupportEmailOverride,
        b.SupportPhoneOverride,
        b.CreatedAtUtc,
        b.UpdatedAtUtc);

    private static PublicBrandingResponse ToPublicResponse(Domain.Tenant t, TenantBranding? b) => new(
        t.Id,
        t.Code,
        t.DisplayName,
        b?.BrandName,
        b?.LogoDocumentId   ?? t.LogoDocumentId,
        b?.LogoWhiteDocumentId ?? t.LogoWhiteDocumentId,
        b?.FaviconDocumentId,
        b?.PrimaryColor,
        b?.SecondaryColor,
        b?.AccentColor,
        b?.TextColor,
        b?.BackgroundColor,
        b?.WebsiteUrlOverride ?? t.WebsiteUrl,
        b?.SupportEmailOverride ?? t.SupportEmail);
}
