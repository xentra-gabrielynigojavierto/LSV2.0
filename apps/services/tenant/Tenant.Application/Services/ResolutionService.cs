using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Tenant.Application.Configuration;
using Tenant.Application.Metrics;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;
using Tenant.Domain;

namespace Tenant.Application.Services;

public class ResolutionService : IResolutionService
{
    private readonly IDomainRepository   _domains;
    private readonly ITenantRepository   _tenants;
    private readonly IBrandingRepository _brandings;
    private readonly IMemoryCache        _cache;
    private readonly TenantRuntimeMetrics _metrics;
    private readonly TenantFeatures      _features;

    public ResolutionService(
        IDomainRepository        domains,
        ITenantRepository        tenants,
        IBrandingRepository      brandings,
        IMemoryCache             cache,
        TenantRuntimeMetrics     metrics,
        IOptions<TenantFeatures> features)
    {
        _domains   = domains;
        _tenants   = tenants;
        _brandings = brandings;
        _cache     = cache;
        _metrics   = metrics;
        _features  = features.Value;
    }

    // ── by-host ───────────────────────────────────────────────────────────────

    public async Task<TenantResolutionResponse?> ResolveByHostAsync(
        string            host,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(host)) return null;
        _metrics.IncrementResolutionAttempted();

        var normalized = TenantDomain.NormalizeHost(host);
        if (!TenantDomain.IsValidHost(normalized))
        {
            _metrics.IncrementResolutionFailed();
            return null;
        }

        var cacheKey = $"resolution:host:{normalized}";

        if (_features.TenantReadCachingEnabled && _cache.TryGetValue(cacheKey, out TenantResolutionResponse? cached))
        {
            _metrics.IncrementResolutionCacheHit();
            _metrics.IncrementResolutionSucceeded();
            return cached;
        }

        _metrics.IncrementResolutionCacheMiss();

        var domain = await _domains.GetActiveByHostAsync(normalized, ct);
        if (domain is null)
        {
            _metrics.IncrementResolutionFailed();
            return null;
        }

        var tenant = await _tenants.GetByIdAsync(domain.TenantId, ct);
        if (tenant is null)
        {
            _metrics.IncrementResolutionFailed();
            return null;
        }

        var branding = await _brandings.GetByTenantIdAsync(tenant.Id, ct);

        var result = new TenantResolutionResponse(
            tenant.Id,
            tenant.Code,
            tenant.DisplayName,
            tenant.Status.ToString(),
            MatchedBy:      "Host",
            MatchedHost:    domain.Host,
            PrimaryColor:   branding?.PrimaryColor,
            LogoDocumentId: branding?.LogoDocumentId ?? tenant.LogoDocumentId);

        if (_features.TenantReadCachingEnabled)
            _cache.Set(cacheKey, result, CacheTtl());

        _metrics.IncrementResolutionSucceeded();
        return result;
    }

    // ── by-subdomain ──────────────────────────────────────────────────────────

    public async Task<TenantResolutionResponse?> ResolveBySubdomainAsync(
        string            subdomain,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(subdomain)) return null;
        _metrics.IncrementResolutionAttempted();

        var normalized = subdomain.Trim().ToLowerInvariant();
        var cacheKey   = $"resolution:sub:{normalized}";

        if (_features.TenantReadCachingEnabled && _cache.TryGetValue(cacheKey, out TenantResolutionResponse? cached))
        {
            _metrics.IncrementResolutionCacheHit();
            _metrics.IncrementResolutionSucceeded();
            return cached;
        }

        _metrics.IncrementResolutionCacheMiss();

        // 1. Check TenantDomain — prefer IsPrimary=true, then any active Subdomain.
        var domain = await _domains.GetActiveSubdomainByLabelAsync(normalized, ct);

        if (domain is not null)
        {
            var tenant = await _tenants.GetByIdAsync(domain.TenantId, ct);
            if (tenant is not null)
            {
                var branding = await _brandings.GetByTenantIdAsync(tenant.Id, ct);
                var result   = new TenantResolutionResponse(
                    tenant.Id,
                    tenant.Code,
                    tenant.DisplayName,
                    tenant.Status.ToString(),
                    MatchedBy:      "Subdomain",
                    MatchedHost:    domain.Host,
                    PrimaryColor:   branding?.PrimaryColor,
                    LogoDocumentId: branding?.LogoDocumentId ?? tenant.LogoDocumentId);

                if (_features.TenantReadCachingEnabled)
                    _cache.Set(cacheKey, result, CacheTtl());

                _metrics.IncrementResolutionSucceeded();
                return result;
            }
        }

        // 2. Migration fallback — check Tenant.Subdomain for backward compatibility.
        // COMPATIBILITY-ONLY [TENANT-B08]: This fallback exists for tenants migrated
        // from Identity that have not yet been assigned a TenantDomain record.
        // Retire after Tenant domain resolution is confirmed primary (see retirement plan).
        var fallbackTenant = await _tenants.GetBySubdomainAsync(normalized, ct);
        if (fallbackTenant is null)
        {
            _metrics.IncrementResolutionFailed();
            return null;
        }

        var fallbackBranding = await _brandings.GetByTenantIdAsync(fallbackTenant.Id, ct);
        var fallbackResult   = new TenantResolutionResponse(
            fallbackTenant.Id,
            fallbackTenant.Code,
            fallbackTenant.DisplayName,
            fallbackTenant.Status.ToString(),
            MatchedBy:      "Subdomain",
            MatchedHost:    null,
            PrimaryColor:   fallbackBranding?.PrimaryColor,
            LogoDocumentId: fallbackBranding?.LogoDocumentId ?? fallbackTenant.LogoDocumentId);

        if (_features.TenantReadCachingEnabled)
            _cache.Set(cacheKey, fallbackResult, CacheTtl());

        _metrics.IncrementResolutionSucceeded();
        return fallbackResult;
    }

    // ── by-code ───────────────────────────────────────────────────────────────

    public async Task<TenantResolutionResponse?> ResolveByCodeAsync(
        string            code,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        _metrics.IncrementResolutionAttempted();

        var normalized = code.Trim().ToLowerInvariant();
        var cacheKey   = $"resolution:code:{normalized}";

        if (_features.TenantReadCachingEnabled && _cache.TryGetValue(cacheKey, out TenantResolutionResponse? cached))
        {
            _metrics.IncrementResolutionCacheHit();
            _metrics.IncrementResolutionSucceeded();
            return cached;
        }

        _metrics.IncrementResolutionCacheMiss();

        var tenant = await _tenants.GetByCodeAsync(normalized, ct);
        if (tenant is null)
        {
            _metrics.IncrementResolutionFailed();
            return null;
        }

        var primaryDomain = await _domains.GetActivePrimarySubdomainByTenantAsync(tenant.Id, ct);
        var branding      = await _brandings.GetByTenantIdAsync(tenant.Id, ct);

        var result = new TenantResolutionResponse(
            tenant.Id,
            tenant.Code,
            tenant.DisplayName,
            tenant.Status.ToString(),
            MatchedBy:      "Code",
            MatchedHost:    primaryDomain?.Host,
            PrimaryColor:   branding?.PrimaryColor,
            LogoDocumentId: branding?.LogoDocumentId ?? tenant.LogoDocumentId);

        if (_features.TenantReadCachingEnabled)
            _cache.Set(cacheKey, result, CacheTtl());

        _metrics.IncrementResolutionSucceeded();
        return result;
    }

    // ── by-id ─────────────────────────────────────────────────────────────────

    public async Task<TenantResolutionResponse?> ResolveByIdAsync(
        Guid              id,
        CancellationToken ct = default)
    {
        _metrics.IncrementResolutionAttempted();

        var cacheKey = $"resolution:id:{id}";

        if (_features.TenantReadCachingEnabled && _cache.TryGetValue(cacheKey, out TenantResolutionResponse? cached))
        {
            _metrics.IncrementResolutionCacheHit();
            _metrics.IncrementResolutionSucceeded();
            return cached;
        }

        _metrics.IncrementResolutionCacheMiss();

        var tenant = await _tenants.GetByIdAsync(id, ct);
        if (tenant is null)
        {
            _metrics.IncrementResolutionFailed();
            return null;
        }

        var primaryDomain = await _domains.GetActivePrimarySubdomainByTenantAsync(tenant.Id, ct);
        var branding      = await _brandings.GetByTenantIdAsync(tenant.Id, ct);

        var result = new TenantResolutionResponse(
            tenant.Id,
            tenant.Code,
            tenant.DisplayName,
            tenant.Status.ToString(),
            MatchedBy:      "Id",
            MatchedHost:    primaryDomain?.Host,
            PrimaryColor:   branding?.PrimaryColor,
            LogoDocumentId: branding?.LogoDocumentId ?? tenant.LogoDocumentId);

        if (_features.TenantReadCachingEnabled)
            _cache.Set(cacheKey, result, CacheTtl());

        _metrics.IncrementResolutionSucceeded();
        return result;
    }

    // ── Cache eviction ────────────────────────────────────────────────────────

    /// <summary>
    /// Evicts resolution cache entries for the given code and/or subdomain.
    /// Called after a successful tenant sync.
    /// </summary>
    public void EvictCache(string? code, string? subdomain)
    {
        if (!string.IsNullOrWhiteSpace(code))
            _cache.Remove($"resolution:code:{code.ToLowerInvariant()}");

        if (!string.IsNullOrWhiteSpace(subdomain))
            _cache.Remove($"resolution:sub:{subdomain.ToLowerInvariant()}");
    }

    private TimeSpan CacheTtl() =>
        TimeSpan.FromSeconds(_features.TenantReadCacheTtlSeconds > 0 ? _features.TenantReadCacheTtlSeconds : 60);
}
