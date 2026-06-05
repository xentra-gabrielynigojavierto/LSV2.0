using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;
using Microsoft.Extensions.Logging;

namespace CareConnect.Application.Services;

// CC2-INT-B06 / CC2-INT-B06-01 — provider network management with shared provider registry
public class NetworkService : INetworkService
{
    private readonly INetworkRepository      _networks;
    private readonly ICategoryRepository     _categories;
    private readonly ILogger<NetworkService> _logger;

    public NetworkService(
        INetworkRepository      networks,
        ICategoryRepository     categories,
        ILogger<NetworkService> logger)
    {
        _networks   = networks;
        _categories = categories;
        _logger     = logger;
    }

    // ── Network CRUD ─────────────────────────────────────────────────────────

    public async Task<List<NetworkSummaryResponse>> GetAllAsync(Guid tenantId, CancellationToken ct = default)
    {
        var networks = await _networks.GetAllByTenantAsync(tenantId, ct);

        var tasks = networks.Select(async n =>
        {
            var detail = await _networks.GetWithProvidersAsync(tenantId, n.Id, ct);
            return ToSummary(n, detail?.NetworkProviders.Count ?? 0);
        });

        return (await Task.WhenAll(tasks)).ToList();
    }

    public async Task<NetworkDetailResponse> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var network = await _networks.GetWithProvidersAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Network {id} not found.");

        return ToDetail(network);
    }

    public async Task<NetworkSummaryResponse> CreateAsync(
        Guid tenantId, Guid? userId, CreateNetworkRequest request, CancellationToken ct = default)
    {
        ValidateName(request.Name);

        if (await _networks.NameExistsAsync(tenantId, request.Name.Trim(), ct: ct))
            throw new ValidationException("Duplicate network name.",
                new() { ["name"] = [$"A network named '{request.Name.Trim()}' already exists."] });

        var network = ProviderNetwork.Create(tenantId, request.Name, request.Description ?? string.Empty);
        await _networks.AddAsync(network, ct);
        await _networks.SaveChangesAsync(ct);

        _logger.LogInformation("Network {NetworkId} created for tenant {TenantId}.", network.Id, tenantId);

        return ToSummary(network, 0);
    }

    public async Task<NetworkSummaryResponse> UpdateAsync(
        Guid tenantId, Guid id, Guid? userId, UpdateNetworkRequest request, CancellationToken ct = default)
    {
        ValidateName(request.Name);

        var network = await _networks.GetWithProvidersAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Network {id} not found.");

        if (await _networks.NameExistsAsync(tenantId, request.Name.Trim(), excludeId: id, ct: ct))
            throw new ValidationException("Duplicate network name.",
                new() { ["name"] = [$"A network named '{request.Name.Trim()}' already exists."] });

        network.Update(request.Name, request.Description ?? string.Empty);
        await _networks.SaveChangesAsync(ct);

        return ToSummary(network, network.NetworkProviders.Count);
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var network = await _networks.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Network {id} not found.");

        network.Delete();
        await _networks.SaveChangesAsync(ct);

        _logger.LogInformation("Network {NetworkId} soft-deleted for tenant {TenantId}.", id, tenantId);
    }

    // ── CC2-INT-B06-01: Shared provider registry — match-or-create ───────────

    /// <summary>
    /// Search the shared global provider registry (cross-tenant).
    /// Matching priority:
    ///   1. NPI exact match (most specific, globally unique)
    ///   2. Normalized phone + name substring
    ///   3. Name substring + city
    /// </summary>
    public async Task<List<ProviderSearchResult>> SearchProvidersAsync(
        string? name, string? phone, string? npi, string? city, CancellationToken ct = default)
    {
        var providers = await _networks.SearchProvidersGlobalAsync(name, phone, npi, city, limit: 20, ct: ct);
        return providers.Select(ToSearchResult).ToList();
    }

    /// <summary>
    /// Add a provider to a network using the match-or-create flow.
    ///
    /// ExistingProviderId path:
    ///   - Validate the provider exists in the shared registry
    ///   - Associate to the network (idempotent)
    ///
    /// NewProvider path:
    ///   1. If NPI provided → check for existing provider by NPI (dedup)
    ///   2. If match found → associate existing (no duplicate created)
    ///   3. If no match → create new Provider in shared registry → associate
    ///
    /// The Provider.TenantId records the REGISTERING tenant (audit trail),
    /// not ownership. Providers are accessible across all networks/tenants.
    /// </summary>
    public async Task<NetworkProviderItem> AddProviderAsync(
        Guid tenantId, Guid networkId,
        AddProviderToNetworkRequest request, Guid? userId,
        CancellationToken ct = default)
    {
        // Validate the network belongs to this tenant
        var network = await _networks.GetByIdAsync(tenantId, networkId, ct)
            ?? throw new NotFoundException($"Network {networkId} not found.");

        Provider provider;

        if (request.ExistingProviderId.HasValue)
        {
            // ── Path A: Associate an existing shared provider ─────────────────
            provider = await _networks.GetProviderByIdGlobalAsync(request.ExistingProviderId.Value, ct)
                ?? throw new NotFoundException($"Provider {request.ExistingProviderId.Value} not found in the shared registry.");
        }
        else if (request.NewProvider is { } np)
        {
            // ── Path B: Match-or-create ───────────────────────────────────────
            ValidateNewProvider(np);

            // Step 1: NPI dedup — if NPI provided and a record already exists, reuse it
            if (!string.IsNullOrWhiteSpace(np.Npi))
            {
                var byNpi = await _networks.GetProviderByNpiAsync(np.Npi, ct);
                if (byNpi is not null)
                {
                    _logger.LogInformation(
                        "Provider NPI {Npi} already exists (Id={ProviderId}); reusing instead of creating duplicate.",
                        np.Npi, byNpi.Id);
                    provider = byNpi;
                    goto associate;
                }
            }

            // Step 2: No NPI match — create a new Provider in the shared registry
            // TenantId = registering tenant (audit/tracking, not ownership)
            provider = Provider.Create(
                tenantId:          tenantId,
                name:              np.Name,
                organizationName:  np.OrganizationName,
                email:             np.Email,
                phone:             np.Phone,
                addressLine1:      np.AddressLine1,
                city:              np.City,
                state:             np.State,
                postalCode:        np.PostalCode,
                isActive:          np.IsActive,
                acceptingReferrals: np.AcceptingReferrals,
                createdByUserId:   userId,
                npi:               np.Npi);

            await _networks.AddProviderToRegistryAsync(provider, ct);

            // Sync provider categories (types) if provided
            if (np.CategoryCodes is { Count: > 0 } codes)
            {
                var categoryEntities = await _categories.GetByCodesAsync(codes, ct);

                // Build ordered list: primary first, then the rest
                var orderedIds = new List<Guid>();
                if (!string.IsNullOrWhiteSpace(np.PrimaryCategoryCode))
                {
                    var primary = categoryEntities.FirstOrDefault(
                        c => string.Equals(c.Code, np.PrimaryCategoryCode, StringComparison.OrdinalIgnoreCase));
                    if (primary is not null) orderedIds.Add(primary.Id);
                }
                orderedIds.AddRange(categoryEntities
                    .Where(c => !string.Equals(c.Code, np.PrimaryCategoryCode, StringComparison.OrdinalIgnoreCase))
                    .Select(c => c.Id));

                if (orderedIds.Count > 0)
                    await _networks.SyncProviderCategoriesAsync(provider.Id, orderedIds, ct);
            }

            _logger.LogInformation(
                "New provider {ProviderId} ({Name}) registered in shared registry by tenant {TenantId}.",
                provider.Id, provider.Name, tenantId);
        }
        else
        {
            throw new ValidationException("Validation failed.",
                new() { ["request"] = ["Either ExistingProviderId or NewProvider must be provided."] });
        }

        associate:
        // ── Associate provider to network (idempotent) ────────────────────────
        var existing = await _networks.GetMembershipAsync(networkId, provider.Id, ct);
        if (existing is not null)
        {
            _logger.LogDebug("Provider {ProviderId} already in network {NetworkId} — no-op.", provider.Id, networkId);
        }
        else
        {
            var entry = NetworkProvider.Create(tenantId, networkId, provider.Id);
            await _networks.AddProviderAsync(entry, ct);
        }

        await _networks.SaveChangesAsync(ct);

        return ToProviderItem(provider);
    }

    public async Task RemoveProviderAsync(
        Guid tenantId, Guid networkId, Guid providerId, CancellationToken ct = default)
    {
        // Validate the network belongs to this tenant
        _ = await _networks.GetByIdAsync(tenantId, networkId, ct)
            ?? throw new NotFoundException($"Network {networkId} not found.");

        var entry = await _networks.GetMembershipAsync(networkId, providerId, ct)
            ?? throw new NotFoundException($"Provider {providerId} is not a member of network {networkId}.");

        // Remove ONLY the association — the ProviderMaster record remains intact
        await _networks.RemoveProviderAsync(entry, ct);
        await _networks.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Provider {ProviderId} removed from network {NetworkId} (association only; shared record preserved).",
            providerId, networkId);
    }

    public async Task<List<NetworkProviderMarker>> GetMarkersAsync(
        Guid tenantId, Guid networkId, CancellationToken ct = default)
    {
        _ = await _networks.GetByIdAsync(tenantId, networkId, ct)
            ?? throw new NotFoundException($"Network {networkId} not found.");

        var providers = await _networks.GetNetworkProvidersAsync(tenantId, networkId, ct);

        // Include every provider in the network so the frontend can geocode
        // those whose coordinates have not yet been stored (lat/lng = 0 signals
        // "needs geocoding" to the client-side geocoder).
        return providers
            .Select(p => new NetworkProviderMarker(
                p.Id,
                p.Name,
                p.OrganizationName,
                p.City,
                p.State,
                p.AddressLine1,
                p.PostalCode,
                p.Email,
                p.Phone,
                p.AcceptingReferrals,
                p.IsActive,
                p.Latitude ?? 0.0,
                p.Longitude ?? 0.0,
                p.GeoPointSource))
            .ToList();
    }

    // ── Mapping helpers ───────────────────────────────────────────────────────

    private static NetworkSummaryResponse ToSummary(ProviderNetwork n, int providerCount) =>
        new(n.Id, n.Name, n.Description, providerCount, n.CreatedAtUtc, n.UpdatedAtUtc);

    private static NetworkDetailResponse ToDetail(ProviderNetwork n) =>
        new(
            n.Id,
            n.Name,
            n.Description,
            n.NetworkProviders.Select(np => ToProviderItem(np.Provider)).ToList(),
            n.CreatedAtUtc,
            n.UpdatedAtUtc);

    private static NetworkProviderItem ToProviderItem(Provider p) =>
        new(p.Id, p.Name, p.OrganizationName, p.Email, p.Phone, p.City, p.State,
            p.IsActive, p.AcceptingReferrals, p.AccessStage);

    private static ProviderSearchResult ToSearchResult(Provider p) =>
        new(p.Id, p.Name, p.OrganizationName, p.Email, p.Phone, p.City, p.State,
            p.AddressLine1, p.PostalCode, p.Npi, p.IsActive, p.AcceptingReferrals, p.AccessStage);

    // ── Validation ────────────────────────────────────────────────────────────

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Validation failed.",
                new() { ["name"] = ["Network name is required."] });
        if (name.Trim().Length > 200)
            throw new ValidationException("Validation failed.",
                new() { ["name"] = ["Network name must be 200 characters or fewer."] });
    }

    private static void ValidateNewProvider(NewProviderData np)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(np.Name))
            errors["name"] = ["Provider name is required."];
        if (string.IsNullOrWhiteSpace(np.Email))
            errors["email"] = ["Provider email is required."];
        if (string.IsNullOrWhiteSpace(np.Phone))
            errors["phone"] = ["Provider phone is required."];
        if (string.IsNullOrWhiteSpace(np.AddressLine1))
            errors["addressLine1"] = ["Address is required."];
        if (string.IsNullOrWhiteSpace(np.City))
            errors["city"] = ["City is required."];
        if (string.IsNullOrWhiteSpace(np.State))
            errors["state"] = ["State is required."];
        if (string.IsNullOrWhiteSpace(np.PostalCode))
            errors["postalCode"] = ["Postal code is required."];
        if (errors.Count > 0)
            throw new ValidationException("Validation failed.", errors);
    }
}
