using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;

namespace CareConnect.Application.Services;

public class AvailabilityTemplateService : IAvailabilityTemplateService
{
    private readonly IAvailabilityTemplateRepository _templates;
    private readonly IProviderRepository _providers;
    private readonly IFacilityRepository _facilities;
    private readonly IServiceOfferingRepository _offerings;

    public AvailabilityTemplateService(
        IAvailabilityTemplateRepository templates,
        IProviderRepository providers,
        IFacilityRepository facilities,
        IServiceOfferingRepository offerings)
    {
        _templates = templates;
        _providers = providers;
        _facilities = facilities;
        _offerings = offerings;
    }

    public async Task<List<AvailabilityTemplateResponse>> GetByProviderAsync(Guid tenantId, Guid providerId, CancellationToken ct = default)
    {
        _ = await _providers.GetByIdAsync(tenantId, providerId, ct)
            ?? throw new NotFoundException($"Provider '{providerId}' was not found.");

        var templates = await _templates.GetByProviderAsync(tenantId, providerId, ct);
        return templates.Select(ToResponse).ToList();
    }

    public async Task<AvailabilityTemplateResponse> CreateAsync(Guid tenantId, Guid providerId, Guid? userId, CreateAvailabilityTemplateRequest request, CancellationToken ct = default)
    {
        _ = await _providers.GetByIdAsync(tenantId, providerId, ct)
            ?? throw new NotFoundException($"Provider '{providerId}' was not found.");

        var facility = await _facilities.GetByIdAsync(tenantId, request.FacilityId, ct)
            ?? throw new NotFoundException($"Facility '{request.FacilityId}' was not found.");

        ServiceOffering? offering = null;
        if (request.ServiceOfferingId.HasValue)
        {
            offering = await _offerings.GetByIdAsync(tenantId, request.ServiceOfferingId.Value, ct)
                ?? throw new NotFoundException($"Service offering '{request.ServiceOfferingId}' was not found.");
        }

        var (start, end) = ParseAndValidate(request.DayOfWeek, request.StartTimeLocal, request.EndTimeLocal, request.SlotDurationMinutes, request.Capacity, request.EffectiveFrom, request.EffectiveTo);

        var template = ProviderAvailabilityTemplate.Create(
            tenantId,
            providerId,
            facility.Id,
            offering?.Id,
            request.DayOfWeek,
            start,
            end,
            request.SlotDurationMinutes,
            request.Capacity,
            request.EffectiveFrom,
            request.EffectiveTo,
            request.IsActive,
            userId);

        await _templates.AddAsync(template, ct);

        var loaded = await _templates.GetByIdAsync(tenantId, template.Id, ct);
        return ToResponse(loaded!);
    }

    public async Task<AvailabilityTemplateResponse> UpdateAsync(Guid tenantId, Guid id, Guid? userId, UpdateAvailabilityTemplateRequest request, CancellationToken ct = default)
    {
        var template = await _templates.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Availability template '{id}' was not found.");

        _ = await _facilities.GetByIdAsync(tenantId, request.FacilityId, ct)
            ?? throw new NotFoundException($"Facility '{request.FacilityId}' was not found.");

        if (request.ServiceOfferingId.HasValue)
        {
            _ = await _offerings.GetByIdAsync(tenantId, request.ServiceOfferingId.Value, ct)
                ?? throw new NotFoundException($"Service offering '{request.ServiceOfferingId}' was not found.");
        }

        var (start, end) = ParseAndValidate(request.DayOfWeek, request.StartTimeLocal, request.EndTimeLocal, request.SlotDurationMinutes, request.Capacity, request.EffectiveFrom, request.EffectiveTo);

        template.Update(
            request.FacilityId,
            request.ServiceOfferingId,
            request.DayOfWeek,
            start,
            end,
            request.SlotDurationMinutes,
            request.Capacity,
            request.EffectiveFrom,
            request.EffectiveTo,
            request.IsActive,
            userId);

        await _templates.UpdateAsync(template, ct);

        var loaded = await _templates.GetByIdAsync(tenantId, template.Id, ct);
        return ToResponse(loaded!);
    }

    private static (TimeSpan start, TimeSpan end) ParseAndValidate(
        int dayOfWeek,
        string startTimeLocal,
        string endTimeLocal,
        int slotDurationMinutes,
        int capacity,
        DateTime? effectiveFrom,
        DateTime? effectiveTo)
    {
        var errors = new Dictionary<string, string[]>();

        if (dayOfWeek < 0 || dayOfWeek > 6)
            errors["dayOfWeek"] = new[] { "DayOfWeek must be between 0 (Sunday) and 6 (Saturday)." };

        if (slotDurationMinutes <= 0)
            errors["slotDurationMinutes"] = new[] { "SlotDurationMinutes must be greater than 0." };

        if (capacity <= 0)
            errors["capacity"] = new[] { "Capacity must be greater than 0." };

        TimeSpan start = default, end = default;

        if (!TimeSpan.TryParse(startTimeLocal, out start))
            errors["startTimeLocal"] = new[] { "StartTimeLocal must be a valid time in HH:mm format." };

        if (!TimeSpan.TryParse(endTimeLocal, out end))
            errors["endTimeLocal"] = new[] { "EndTimeLocal must be a valid time in HH:mm format." };

        if (errors.Count == 0 && end <= start)
            errors["endTimeLocal"] = new[] { "EndTimeLocal must be after StartTimeLocal." };

        if (effectiveFrom.HasValue && effectiveTo.HasValue && effectiveTo.Value < effectiveFrom.Value)
            errors["effectiveTo"] = new[] { "EffectiveTo must be on or after EffectiveFrom." };

        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);

        return (start, end);
    }

    private static AvailabilityTemplateResponse ToResponse(ProviderAvailabilityTemplate t) => new()
    {
        Id = t.Id,
        TenantId = t.TenantId,
        ProviderId = t.ProviderId,
        FacilityId = t.FacilityId,
        FacilityName = t.Facility?.Name ?? string.Empty,
        ServiceOfferingId = t.ServiceOfferingId,
        ServiceOfferingName = t.ServiceOffering?.Name,
        DayOfWeek = t.DayOfWeek,
        StartTimeLocal = t.StartTimeLocal.ToString(@"hh\:mm"),
        EndTimeLocal = t.EndTimeLocal.ToString(@"hh\:mm"),
        SlotDurationMinutes = t.SlotDurationMinutes,
        Capacity = t.Capacity,
        EffectiveFrom = t.EffectiveFrom,
        EffectiveTo = t.EffectiveTo,
        IsActive = t.IsActive
    };
}
