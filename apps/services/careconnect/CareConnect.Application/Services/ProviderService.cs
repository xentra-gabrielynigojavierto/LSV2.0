using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Helpers;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace CareConnect.Application.Services;

public class ProviderService : IProviderService
{
    private readonly IProviderRepository _providers;
    private readonly IAppointmentSlotRepository _slots;
    private readonly ILogger<ProviderService> _logger;

    public ProviderService(
        IProviderRepository providers,
        IAppointmentSlotRepository slots,
        ILogger<ProviderService> logger)
    {
        _providers = providers;
        _slots     = slots;
        _logger    = logger;
    }

    public async Task<PagedResponse<ProviderResponse>> SearchAsync(Guid tenantId, GetProvidersQuery query, CancellationToken ct = default)
    {
        ValidatePaging(query.Page, query.PageSize);
        ValidateSearchGeo(query);

        var (items, totalCount) = await _providers.SearchAsync(tenantId, query, ct);

        return new PagedResponse<ProviderResponse>
        {
            Items      = items.Select(ToResponse).ToList(),
            Page       = query.Page,
            PageSize   = query.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<List<ProviderMarkerResponse>> GetMarkersAsync(Guid tenantId, GetProvidersQuery query, CancellationToken ct = default)
    {
        ValidateSearchGeo(query);

        var items = await _providers.GetMarkersAsync(tenantId, query, ct);
        return items.Select(ToMarker).ToList();
    }

    public async Task<ProviderResponse> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        // Cross-tenant read: referrers from any org may view any provider's detail page.
        var provider = await _providers.GetByIdCrossAsync(id, ct)
            ?? throw new NotFoundException($"Provider '{id}' was not found.");
        return ToResponse(provider);
    }

    public async Task<ProviderResponse> CreateAsync(Guid tenantId, Guid? userId, CreateProviderRequest request, CancellationToken ct = default)
    {
        ValidateFields(request.Name, request.Email, request.Phone, request.AddressLine1, request.City, request.State, request.PostalCode);
        ValidateGeoFields(request.Latitude, request.Longitude, request.GeoPointSource);

        var provider = Provider.Create(
            tenantId,
            request.Name,
            request.OrganizationName,
            request.Email,
            request.Phone,
            request.AddressLine1,
            request.City,
            request.State,
            request.PostalCode,
            request.IsActive,
            request.AcceptingReferrals,
            userId,
            request.Latitude,
            request.Longitude,
            request.GeoPointSource);

        // Phase D / Step 6: link to Identity Organization before persisting so that
        // the OrganizationId is captured in the initial INSERT, eliminating the
        // redundant UPDATE that previously followed AddAsync.
        if (request.OrganizationId.HasValue)
        {
            provider.LinkOrganization(request.OrganizationId.Value);
            _logger.LogDebug(
                "Provider {ProviderId} linking to Identity Organization {OrganizationId}.",
                provider.Id, request.OrganizationId.Value);
        }
        else
        {
            // Phase H: warn when a Provider is created without an Identity org link.
            // An unlinked provider cannot participate in org-scoped authorization or
            // cross-service referral relationship resolution.
            _logger.LogInformation(
                "Provider {ProviderId} created without an Identity Organization link (OrganizationId not supplied). " +
                "Supply OrganizationId on create or update to enable cross-service org-scoped features.",
                provider.Id);
        }

        await _providers.AddAsync(provider, ct);

        if (request.CategoryIds.Count > 0)
            await _providers.SyncCategoriesAsync(provider.Id, request.CategoryIds, ct);

        var loaded = await _providers.GetByIdAsync(tenantId, provider.Id, ct);
        return ToResponse(loaded!);
    }

    public async Task<ProviderResponse> UpdateAsync(Guid tenantId, Guid id, Guid? userId, UpdateProviderRequest request, CancellationToken ct = default)
    {
        var provider = await _providers.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Provider '{id}' was not found.");

        ValidateFields(request.Name, request.Email, request.Phone, request.AddressLine1, request.City, request.State, request.PostalCode);
        ValidateGeoFields(request.Latitude, request.Longitude, request.GeoPointSource);

        provider.Update(
            request.Name,
            request.OrganizationName,
            request.Email,
            request.Phone,
            request.AddressLine1,
            request.City,
            request.State,
            request.PostalCode,
            request.IsActive,
            request.AcceptingReferrals,
            userId,
            request.Latitude,
            request.Longitude,
            request.GeoPointSource);

        // Phase D: link to Identity Organization if supplied.
        if (request.OrganizationId.HasValue)
        {
            provider.LinkOrganization(request.OrganizationId.Value);
            _logger.LogDebug(
                "Provider {ProviderId} org linkage updated to Identity Organization {OrganizationId}.",
                provider.Id, request.OrganizationId.Value);
        }

        await _providers.UpdateAsync(provider, ct);
        await _providers.SyncCategoriesAsync(provider.Id, request.CategoryIds, ct);

        var loaded = await _providers.GetByIdAsync(tenantId, provider.Id, ct);
        return ToResponse(loaded!);
    }

    public async Task<ProviderAvailabilityResponse> GetAvailabilityAsync(
        Guid tenantId,
        Guid providerId,
        DateTime from,
        DateTime to,
        Guid? serviceOfferingId = null,
        Guid? facilityId = null,
        CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        if (from >= to)
            errors["from"] = new[] { "From must be earlier than To." };
        if ((to - from).TotalDays > 90)
            errors["to"] = new[] { "Availability window must not exceed 90 days." };
        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);

        // Cross-tenant read: referrer may view any provider's availability.
        var provider = await _providers.GetByIdCrossAsync(providerId, ct)
            ?? throw new NotFoundException($"Provider '{providerId}' was not found.");

        var allSlots = await _slots.GetOpenByProviderInRangeAsync(provider.TenantId, providerId, from, to, ct);

        // Apply optional filters in memory (the repository returns all open slots in range).
        var filtered = allSlots
            .Where(s => !serviceOfferingId.HasValue || s.ServiceOfferingId == serviceOfferingId)
            .Where(s => !facilityId.HasValue || s.FacilityId == facilityId)
            .OrderBy(s => s.StartAtUtc)
            .ToList();

        // Derive header-level facility/service from first slot when single-dimension filtering.
        var firstSlot = filtered.FirstOrDefault();

        var slots = filtered.Select(s => new AvailableSlotSummary
        {
            Id                  = s.Id,
            StartAtUtc          = s.StartAtUtc,
            EndAtUtc            = s.EndAtUtc,
            AvailableCount      = Math.Max(0, s.Capacity - s.ReservedCount),
            FacilityId          = s.FacilityId,
            FacilityName        = s.Facility?.Name ?? string.Empty,
            ServiceOfferingId   = s.ServiceOfferingId,
            ServiceOfferingName = s.ServiceOffering?.Name,
        }).ToList();

        return new ProviderAvailabilityResponse
        {
            ProviderId          = providerId,
            ProviderName        = provider.Name,
            From                = from,
            To                  = to,
            FacilityId          = facilityId ?? firstSlot?.FacilityId,
            FacilityName        = firstSlot?.Facility?.Name,
            ServiceOfferingId   = serviceOfferingId ?? firstSlot?.ServiceOfferingId,
            ServiceOfferingName = firstSlot?.ServiceOffering?.Name,
            Slots               = slots,
        };
    }

    private static void ValidateSearchGeo(GetProvidersQuery query)
    {
        var errors = new Dictionary<string, string[]>();

        bool hasRadius   = query.Latitude.HasValue || query.Longitude.HasValue || query.RadiusMiles.HasValue;
        bool hasViewport = query.NorthLat.HasValue  || query.SouthLat.HasValue  || query.EastLng.HasValue || query.WestLng.HasValue;

        ProviderGeoHelper.ValidateNoConflict(hasRadius, hasViewport, errors);

        if (!errors.ContainsKey("search"))
        {
            ProviderGeoHelper.ValidateGeoSearch(query.Latitude, query.Longitude, query.RadiusMiles, errors);
            ProviderGeoHelper.ValidateViewport(query.NorthLat, query.SouthLat, query.EastLng, query.WestLng, errors);
        }

        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);
    }

    private static void ValidatePaging(int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();

        if (page < 1)
            errors["page"] = new[] { "Page must be >= 1." };

        if (pageSize < 1)
            errors["pageSize"] = new[] { "PageSize must be >= 1." };
        else if (pageSize > 100)
            errors["pageSize"] = new[] { "PageSize must not exceed 100." };

        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);
    }

    private static void ValidateGeoFields(double? latitude, double? longitude, string? geoPointSource)
    {
        var errors = new Dictionary<string, string[]>();
        ProviderGeoHelper.ValidateGeoFields(latitude, longitude, geoPointSource, errors);
        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);
    }

    private static void ValidateFields(string name, string email, string phone, string addressLine1, string city, string state, string postalCode)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(name))
            errors["name"] = new[] { "Name is required." };
        else if (name.Trim().Length > 200)
            errors["name"] = new[] { "Name must not exceed 200 characters." };

        if (string.IsNullOrWhiteSpace(email))
            errors["email"] = new[] { "Email is required." };
        else if (email.Trim().Length > 320)
            errors["email"] = new[] { "Email must not exceed 320 characters." };
        else if (!Regex.IsMatch(email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            errors["email"] = new[] { "Email format is invalid." };

        if (string.IsNullOrWhiteSpace(phone))
            errors["phone"] = new[] { "Phone is required." };
        else if (phone.Trim().Length > 50)
            errors["phone"] = new[] { "Phone must not exceed 50 characters." };

        if (string.IsNullOrWhiteSpace(addressLine1))
            errors["addressLine1"] = new[] { "AddressLine1 is required." };

        if (string.IsNullOrWhiteSpace(city))
            errors["city"] = new[] { "City is required." };

        if (string.IsNullOrWhiteSpace(state))
            errors["state"] = new[] { "State is required." };

        if (string.IsNullOrWhiteSpace(postalCode))
            errors["postalCode"] = new[] { "PostalCode is required." };

        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);
    }

    private static ProviderResponse ToResponse(Provider p)
    {
        var categories = p.ProviderCategories
            .Where(pc => pc.Category != null)
            .Select(pc => pc.Category!.Name)
            .OrderBy(n => n)
            .ToList();

        var primary  = categories.FirstOrDefault();
        var label    = p.OrganizationName ?? p.Name;
        var subtitle = BuildSubtitle(p.City, p.State, primary);

        return new ProviderResponse
        {
            Id               = p.Id,
            TenantId         = p.TenantId,
            Name             = p.Name,
            OrganizationName = p.OrganizationName,
            OrganizationId   = p.OrganizationId,
            Email            = p.Email,
            Phone            = p.Phone,
            AddressLine1     = p.AddressLine1,
            City             = p.City,
            State            = p.State,
            PostalCode       = p.PostalCode,
            IsActive         = p.IsActive,
            AcceptingReferrals = p.AcceptingReferrals,
            Categories       = categories,
            Latitude         = p.Latitude,
            Longitude        = p.Longitude,
            GeoPointSource   = p.GeoPointSource,
            GeoUpdatedAtUtc  = p.GeoUpdatedAtUtc,
            HasGeoLocation   = p.Latitude.HasValue && p.Longitude.HasValue,
            PrimaryCategory  = primary,
            DisplayLabel     = label,
            MarkerSubtitle   = subtitle,
            // CC2-INT-B06-02
            AccessStage                = p.AccessStage,
            IdentityUserId             = p.IdentityUserId,
            CommonPortalActivatedAtUtc = p.CommonPortalActivatedAtUtc,
            TenantProvisionedAtUtc     = p.TenantProvisionedAtUtc,
        };
    }

    private static ProviderMarkerResponse ToMarker(Provider p)
    {
        var categories = p.ProviderCategories
            .Where(pc => pc.Category != null)
            .Select(pc => pc.Category!.Name)
            .OrderBy(n => n)
            .ToList();

        var primary  = categories.FirstOrDefault();
        var label    = p.OrganizationName ?? p.Name;
        var subtitle = BuildSubtitle(p.City, p.State, primary);

        return new ProviderMarkerResponse
        {
            Id               = p.Id,
            Name             = p.Name,
            OrganizationName = p.OrganizationName,
            DisplayLabel     = label,
            MarkerSubtitle   = subtitle,
            City             = p.City,
            State            = p.State,
            AddressLine1     = p.AddressLine1,
            PostalCode       = p.PostalCode,
            Email            = p.Email,
            Phone            = p.Phone,
            AcceptingReferrals = p.AcceptingReferrals,
            IsActive         = p.IsActive,
            Latitude         = p.Latitude!.Value,
            Longitude        = p.Longitude!.Value,
            GeoPointSource   = p.GeoPointSource,
            PrimaryCategory  = primary,
            Categories       = categories
        };
    }

    // LSCC-002: Admin org-link backfill — explicit, idempotent, admin-only operation.
    public async Task<ProviderResponse> LinkOrganizationAsync(
        Guid tenantId,
        Guid providerId,
        Guid organizationId,
        CancellationToken ct = default)
    {
        var provider = await _providers.GetByIdAsync(tenantId, providerId, ct)
            ?? throw new NotFoundException($"Provider '{providerId}' was not found.");

        provider.LinkOrganization(organizationId);
        await _providers.UpdateAsync(provider, ct);

        var loaded = await _providers.GetByIdAsync(tenantId, providerId, ct);
        return ToResponse(loaded!);
    }

    // LSCC-01-005-01 (DEF-001): Cross-tenant provider org-link — used by activation approval
    // when the provider TenantId differs from the requesting tenant's TenantId.
    public async Task<ProviderResponse> LinkOrganizationGlobalAsync(
        Guid providerId,
        Guid organizationId,
        CancellationToken ct = default)
    {
        var provider = await _providers.GetByIdCrossAsync(providerId, ct)
            ?? throw new NotFoundException($"Provider '{providerId}' was not found.");

        provider.LinkOrganization(organizationId);
        await _providers.UpdateAsync(provider, ct);

        var loaded = await _providers.GetByIdCrossAsync(providerId, ct);
        return ToResponse(loaded!);
    }

    // LSCC-002-01: List active providers with no Identity org link (backfill candidates).
    public async Task<List<ProviderResponse>> GetUnlinkedProvidersAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var unlinked = await _providers.GetUnlinkedAsync(tenantId, ct);
        return unlinked.Select(ToResponse).ToList();
    }

    // LSCC-002-01: Bulk org-link — processes each item independently; never auto-guesses mappings.
    // Skipped  = provider already has an OrganizationId (already linked).
    // Unresolved = provider not found in this tenant.
    // Updated  = successfully linked in this call.
    public async Task<BulkLinkReport> BulkLinkOrganizationAsync(
        Guid tenantId,
        IReadOnlyList<ProviderOrgLinkItem> items,
        CancellationToken ct = default)
    {
        int updated = 0, skipped = 0, unresolved = 0;

        foreach (var item in items)
        {
            var provider = await _providers.GetByIdAsync(tenantId, item.ProviderId, ct);

            if (provider is null)
            {
                unresolved++;
                continue;
            }

            if (provider.OrganizationId.HasValue)
            {
                // Already linked — idempotent skip.
                skipped++;
                continue;
            }

            provider.LinkOrganization(item.OrganizationId);
            await _providers.UpdateAsync(provider, ct);
            updated++;
        }

        return new BulkLinkReport(
            Total: items.Count,
            Updated: updated,
            Skipped: skipped,
            Unresolved: unresolved);
    }

    // LSCC-01-003: Admin CareConnect receiver provisioning — CC-side idempotent activation.
    public async Task<ProviderActivationResult> ActivateForCareConnectAsync(
        Guid providerId,
        CancellationToken ct = default)
    {
        var provider = await _providers.GetByIdCrossAsync(providerId, ct);
        if (provider is null)
            throw new NotFoundException($"Provider '{providerId}' was not found.");

        bool alreadyActive = provider.IsActive && provider.AcceptingReferrals;

        if (!alreadyActive)
        {
            provider.Activate();
            await _providers.UpdateAsync(provider, ct);
        }

        return new ProviderActivationResult(
            ProviderId:        provider.Id,
            AlreadyActive:     alreadyActive,
            IsActive:          provider.IsActive,
            AcceptingReferrals: provider.AcceptingReferrals);
    }

    private static string BuildSubtitle(string city, string state, string? primaryCategory)
    {
        var location = $"{city}, {state}";
        return primaryCategory is null ? location : $"{location} · {primaryCategory}";
    }
}
