using BuildingBlocks.Exceptions;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;
using Tenant.Domain;

namespace Tenant.Application.Services;

public class DomainService : IDomainService
{
    private readonly IDomainRepository  _domains;
    private readonly ITenantRepository  _tenants;

    public DomainService(IDomainRepository domains, ITenantRepository tenants)
    {
        _domains = domains;
        _tenants = tenants;
    }

    // ── List ──────────────────────────────────────────────────────────────────

    public async Task<List<DomainResponse>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        await RequireTenantAsync(tenantId, ct);
        var records = await _domains.ListByTenantAsync(tenantId, ct);
        return records.Select(ToResponse).ToList();
    }

    // ── Create ────────────────────────────────────────────────────────────────

    public async Task<DomainResponse> CreateAsync(
        Guid                tenantId,
        CreateDomainRequest request,
        CancellationToken   ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Host, nameof(request.Host));

        await RequireTenantAsync(tenantId, ct);

        if (!Enum.TryParse<TenantDomainType>(request.DomainType, ignoreCase: true, out var domainType))
            throw new ValidationException($"Invalid domainType '{request.DomainType}'.",
                new Dictionary<string, string[]> { ["domainType"] = [$"'{request.DomainType}' is not a valid domain type."] });

        TenantDomainStatus status = TenantDomainStatus.Active;
        if (request.Status is not null &&
            !Enum.TryParse<TenantDomainStatus>(request.Status, ignoreCase: true, out status))
            throw new ValidationException($"Invalid status '{request.Status}'.",
                new Dictionary<string, string[]> { ["status"] = [$"'{request.Status}' is not a valid status value."] });

        string normalizedHost;
        try { normalizedHost = TenantDomain.NormalizeHost(request.Host); }
        catch { throw new ValidationException("Invalid host.", new Dictionary<string, string[]> { ["host"] = ["Host cannot be empty."] }); }

        if (!TenantDomain.IsValidHost(normalizedHost))
            throw new ValidationException("Invalid hostname.",
                new Dictionary<string, string[]> { ["host"] = [$"'{request.Host}' is not a valid hostname."] });

        if (await _domains.ActiveHostExistsAsync(normalizedHost, null, ct))
            throw new ConflictException($"The host '{normalizedHost}' is already registered and active for another tenant.");

        // Auto-demote previous primary subdomain if a new one is being added as primary.
        if (request.IsPrimary && domainType == TenantDomainType.Subdomain && status == TenantDomainStatus.Active)
            await DemotePreviousSubdomainPrimaryAsync(tenantId, null, ct);

        var domain = TenantDomain.Create(tenantId, normalizedHost, domainType, request.IsPrimary, status);
        await _domains.AddAsync(domain, ct);
        return ToResponse(domain);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public async Task<DomainResponse> UpdateAsync(
        Guid                tenantId,
        Guid                domainId,
        UpdateDomainRequest request,
        CancellationToken   ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Host, nameof(request.Host));

        await RequireTenantAsync(tenantId, ct);

        var domain = await _domains.GetByIdAsync(domainId, ct)
            ?? throw new NotFoundException($"Domain '{domainId}' was not found.");

        if (domain.TenantId != tenantId)
            throw new NotFoundException($"Domain '{domainId}' was not found.");

        if (!Enum.TryParse<TenantDomainType>(request.DomainType, ignoreCase: true, out var domainType))
            throw new ValidationException($"Invalid domainType '{request.DomainType}'.",
                new Dictionary<string, string[]> { ["domainType"] = [$"'{request.DomainType}' is not a valid domain type."] });

        if (!Enum.TryParse<TenantDomainStatus>(request.Status, ignoreCase: true, out var status))
            throw new ValidationException($"Invalid status '{request.Status}'.",
                new Dictionary<string, string[]> { ["status"] = [$"'{request.Status}' is not a valid status value."] });

        string normalizedHost;
        try { normalizedHost = TenantDomain.NormalizeHost(request.Host); }
        catch { throw new ValidationException("Invalid host.", new Dictionary<string, string[]> { ["host"] = ["Host cannot be empty."] }); }

        if (!TenantDomain.IsValidHost(normalizedHost))
            throw new ValidationException("Invalid hostname.",
                new Dictionary<string, string[]> { ["host"] = [$"'{request.Host}' is not a valid hostname."] });

        if (await _domains.ActiveHostExistsAsync(normalizedHost, domainId, ct))
            throw new ConflictException($"The host '{normalizedHost}' is already registered and active for another tenant.");

        // Auto-demote previous primary subdomain when this record is being promoted.
        if (request.IsPrimary && domainType == TenantDomainType.Subdomain && status == TenantDomainStatus.Active)
            await DemotePreviousSubdomainPrimaryAsync(tenantId, domainId, ct);

        domain.Update(normalizedHost, domainType, request.IsPrimary);
        domain.SetStatus(status);

        await _domains.UpdateAsync(domain, ct);
        return ToResponse(domain);
    }

    // ── Deactivate ────────────────────────────────────────────────────────────

    public async Task DeactivateAsync(Guid tenantId, Guid domainId, CancellationToken ct = default)
    {
        await RequireTenantAsync(tenantId, ct);

        var domain = await _domains.GetByIdAsync(domainId, ct)
            ?? throw new NotFoundException($"Domain '{domainId}' was not found.");

        if (domain.TenantId != tenantId)
            throw new NotFoundException($"Domain '{domainId}' was not found.");

        domain.SetStatus(TenantDomainStatus.Inactive);
        await _domains.UpdateAsync(domain, ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task RequireTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var tenant = await _tenants.GetByIdAsync(tenantId, ct);
        if (tenant is null)
            throw new NotFoundException($"Tenant '{tenantId}' was not found.");
    }

    /// <summary>
    /// Demotes any existing primary Subdomain-type records for the tenant except <paramref name="excludeDomainId"/>.
    /// Called before assigning a new primary to maintain the single-primary invariant.
    /// </summary>
    private async Task DemotePreviousSubdomainPrimaryAsync(
        Guid             tenantId,
        Guid?            excludeDomainId,
        CancellationToken ct)
    {
        var primaries = await _domains.GetActiveSubdomainsForTenantAsync(tenantId, ct);
        var todemote  = primaries
            .Where(d => d.IsPrimary && (excludeDomainId == null || d.Id != excludeDomainId))
            .ToList();

        if (todemote.Count == 0) return;

        foreach (var d in todemote)
            d.Demote();

        await _domains.UpdateRangeAsync(todemote, ct);
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    internal static DomainResponse ToResponse(TenantDomain d) => new(
        d.Id,
        d.TenantId,
        d.Host,
        d.DomainType.ToString(),
        d.Status.ToString(),
        d.IsPrimary,
        d.CreatedAtUtc,
        d.UpdatedAtUtc);
}
