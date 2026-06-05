using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;
using Microsoft.Extensions.Logging;

namespace CareConnect.Application.Services;

public class FacilityService : IFacilityService
{
    private readonly IFacilityRepository _facilities;
    private readonly ILogger<FacilityService> _logger;

    public FacilityService(IFacilityRepository facilities, ILogger<FacilityService> logger)
    {
        _facilities = facilities;
        _logger     = logger;
    }

    public async Task<List<FacilityResponse>> GetAllAsync(Guid tenantId, CancellationToken ct = default)
    {
        var facilities = await _facilities.GetAllByTenantAsync(tenantId, ct);
        return facilities.Select(ToResponse).ToList();
    }

    public async Task<FacilityResponse> CreateAsync(Guid tenantId, Guid? userId, CreateFacilityRequest request, CancellationToken ct = default)
    {
        Validate(request.Name, request.AddressLine1, request.City, request.State, request.PostalCode, request.Phone);

        var facility = Facility.Create(
            tenantId,
            request.Name,
            request.AddressLine1,
            request.City,
            request.State,
            request.PostalCode,
            request.Phone,
            request.IsActive,
            userId);

        // Phase 4: link to Identity Organization when provided.
        if (request.OrganizationId.HasValue)
        {
            facility.LinkOrganization(request.OrganizationId.Value);
            _logger.LogDebug(
                "Facility {FacilityId} linked to Identity Organization {OrganizationId} on create.",
                facility.Id, request.OrganizationId.Value);
        }
        else
        {
            // Phase H: warn when a Facility is created without an Identity org link.
            // An unlinked facility cannot participate in org-scoped authorization or
            // cross-service relationship resolution.
            _logger.LogInformation(
                "Facility {FacilityId} created without an Identity Organization link (OrganizationId not supplied). " +
                "Supply OrganizationId on create or update to enable cross-service org-scoped features.",
                facility.Id);
        }

        await _facilities.AddAsync(facility, ct);
        return ToResponse(facility);
    }

    public async Task<FacilityResponse> UpdateAsync(Guid tenantId, Guid id, Guid? userId, UpdateFacilityRequest request, CancellationToken ct = default)
    {
        var facility = await _facilities.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Facility '{id}' was not found.");

        Validate(request.Name, request.AddressLine1, request.City, request.State, request.PostalCode, request.Phone);

        facility.Update(request.Name, request.AddressLine1, request.City, request.State, request.PostalCode, request.Phone, request.IsActive, userId);

        // Phase 4: apply org linkage when provided (supports backfill via update).
        if (request.OrganizationId.HasValue)
        {
            facility.LinkOrganization(request.OrganizationId.Value);
            _logger.LogDebug(
                "Facility {FacilityId} org linkage updated to Identity Organization {OrganizationId}.",
                facility.Id, request.OrganizationId.Value);
        }

        await _facilities.UpdateAsync(facility, ct);
        return ToResponse(facility);
    }

    private static void Validate(string name, string addressLine1, string city, string state, string postalCode, string? phone)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(name))
            errors["name"] = new[] { "Name is required." };
        else if (name.Trim().Length > 200)
            errors["name"] = new[] { "Name must not exceed 200 characters." };

        if (string.IsNullOrWhiteSpace(addressLine1))
            errors["addressLine1"] = new[] { "AddressLine1 is required." };

        if (string.IsNullOrWhiteSpace(city))
            errors["city"] = new[] { "City is required." };

        if (string.IsNullOrWhiteSpace(state))
            errors["state"] = new[] { "State is required." };

        if (string.IsNullOrWhiteSpace(postalCode))
            errors["postalCode"] = new[] { "PostalCode is required." };

        if (phone is not null && phone.Trim().Length > 50)
            errors["phone"] = new[] { "Phone must not exceed 50 characters." };

        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);
    }

    private static FacilityResponse ToResponse(Facility f) => new()
    {
        Id = f.Id,
        TenantId = f.TenantId,
        Name = f.Name,
        AddressLine1 = f.AddressLine1,
        City = f.City,
        State = f.State,
        PostalCode = f.PostalCode,
        Phone = f.Phone,
        IsActive = f.IsActive,
        OrganizationId = f.OrganizationId
    };
}
