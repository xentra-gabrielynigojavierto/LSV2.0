using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;

namespace CareConnect.Application.Services;

public class ServiceOfferingService : IServiceOfferingService
{
    private readonly IServiceOfferingRepository _offerings;

    public ServiceOfferingService(IServiceOfferingRepository offerings)
    {
        _offerings = offerings;
    }

    public async Task<List<ServiceOfferingResponse>> GetAllAsync(Guid tenantId, CancellationToken ct = default)
    {
        var offerings = await _offerings.GetAllByTenantAsync(tenantId, ct);
        return offerings.Select(ToResponse).ToList();
    }

    public async Task<ServiceOfferingResponse> CreateAsync(Guid tenantId, Guid? userId, CreateServiceOfferingRequest request, CancellationToken ct = default)
    {
        Validate(request.Name, request.Code, request.DurationMinutes);

        var existing = await _offerings.GetByCodeAsync(tenantId, request.Code, ct);
        if (existing is not null)
            throw new ValidationException("One or more validation errors occurred.",
                new Dictionary<string, string[]> { ["code"] = new[] { $"A service offering with code '{request.Code.ToUpper()}' already exists." } });

        var offering = ServiceOffering.Create(
            tenantId,
            request.Name,
            request.Code,
            request.Description,
            request.DurationMinutes,
            request.IsActive,
            userId);

        await _offerings.AddAsync(offering, ct);
        return ToResponse(offering);
    }

    public async Task<ServiceOfferingResponse> UpdateAsync(Guid tenantId, Guid id, Guid? userId, UpdateServiceOfferingRequest request, CancellationToken ct = default)
    {
        var offering = await _offerings.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Service offering '{id}' was not found.");

        Validate(request.Name, request.Code, request.DurationMinutes);

        var existing = await _offerings.GetByCodeAsync(tenantId, request.Code, ct);
        if (existing is not null && existing.Id != id)
            throw new ValidationException("One or more validation errors occurred.",
                new Dictionary<string, string[]> { ["code"] = new[] { $"A service offering with code '{request.Code.ToUpper()}' already exists." } });

        offering.Update(request.Name, request.Code, request.Description, request.DurationMinutes, request.IsActive, userId);
        await _offerings.UpdateAsync(offering, ct);
        return ToResponse(offering);
    }

    private static void Validate(string name, string code, int durationMinutes)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(name))
            errors["name"] = new[] { "Name is required." };
        else if (name.Trim().Length > 200)
            errors["name"] = new[] { "Name must not exceed 200 characters." };

        if (string.IsNullOrWhiteSpace(code))
            errors["code"] = new[] { "Code is required." };
        else if (code.Trim().Length > 100)
            errors["code"] = new[] { "Code must not exceed 100 characters." };

        if (durationMinutes <= 0)
            errors["durationMinutes"] = new[] { "DurationMinutes must be greater than 0." };

        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);
    }

    private static ServiceOfferingResponse ToResponse(ServiceOffering s) => new()
    {
        Id = s.Id,
        TenantId = s.TenantId,
        Name = s.Name,
        Code = s.Code,
        Description = s.Description,
        DurationMinutes = s.DurationMinutes,
        IsActive = s.IsActive
    };
}
