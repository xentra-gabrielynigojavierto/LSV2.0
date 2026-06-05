using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;

namespace CareConnect.Application.Services;

public class AvailabilityExceptionService : IAvailabilityExceptionService
{
    private readonly IAvailabilityExceptionRepository _exceptions;
    private readonly IProviderRepository _providers;
    private readonly IFacilityRepository _facilities;
    private readonly IAppointmentSlotRepository _slots;

    public AvailabilityExceptionService(
        IAvailabilityExceptionRepository exceptions,
        IProviderRepository providers,
        IFacilityRepository facilities,
        IAppointmentSlotRepository slots)
    {
        _exceptions = exceptions;
        _providers  = providers;
        _facilities = facilities;
        _slots      = slots;
    }

    public async Task<List<AvailabilityExceptionResponse>> GetByProviderAsync(
        Guid tenantId,
        Guid providerId,
        bool? isActive,
        CancellationToken ct = default)
    {
        _ = await _providers.GetByIdAsync(tenantId, providerId, ct)
            ?? throw new NotFoundException($"Provider '{providerId}' was not found.");

        var rows = await _exceptions.GetByProviderAsync(tenantId, providerId, isActive, ct);
        return rows.Select(ToResponse).ToList();
    }

    public async Task<AvailabilityExceptionResponse> CreateAsync(
        Guid tenantId,
        Guid providerId,
        Guid? userId,
        CreateAvailabilityExceptionRequest request,
        CancellationToken ct = default)
    {
        await ValidateRequestAsync(tenantId, providerId, request.FacilityId, request.StartAtUtc, request.EndAtUtc, request.ExceptionType, ct);

        var entity = ProviderAvailabilityException.Create(
            tenantId,
            providerId,
            request.FacilityId,
            request.StartAtUtc,
            request.EndAtUtc,
            request.ExceptionType,
            request.Reason,
            request.IsActive,
            userId);

        await _exceptions.AddAsync(entity, ct);

        var loaded = await _exceptions.GetByIdAsync(tenantId, entity.Id, ct);
        return ToResponse(loaded!);
    }

    public async Task<AvailabilityExceptionResponse> UpdateAsync(
        Guid tenantId,
        Guid id,
        Guid? userId,
        UpdateAvailabilityExceptionRequest request,
        CancellationToken ct = default)
    {
        var entity = await _exceptions.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Availability exception '{id}' was not found.");

        await ValidateRequestAsync(tenantId, entity.ProviderId, request.FacilityId, request.StartAtUtc, request.EndAtUtc, request.ExceptionType, ct);

        entity.Update(
            request.FacilityId,
            request.StartAtUtc,
            request.EndAtUtc,
            request.ExceptionType,
            request.Reason,
            request.IsActive,
            userId);

        await _exceptions.UpdateAsync(entity, ct);

        var loaded = await _exceptions.GetByIdAsync(tenantId, id, ct);
        return ToResponse(loaded!);
    }

    public async Task<ApplyExceptionsResponse> ApplyExceptionsToSlotsAsync(
        Guid tenantId,
        Guid providerId,
        Guid? userId,
        CancellationToken ct = default)
    {
        _ = await _providers.GetByIdAsync(tenantId, providerId, ct)
            ?? throw new NotFoundException($"Provider '{providerId}' was not found.");

        var activeExceptions = await _exceptions.GetByProviderAsync(tenantId, providerId, isActive: true, ct);

        if (activeExceptions.Count == 0)
            return new ApplyExceptionsResponse { ProviderId = providerId, SlotsBlocked = 0, SlotsSkipped = 0 };

        var rangeFrom = activeExceptions.Min(e => e.StartAtUtc);
        var rangeTo   = activeExceptions.Max(e => e.EndAtUtc);

        var openSlots = await _slots.GetOpenByProviderInRangeAsync(tenantId, providerId, rangeFrom, rangeTo, ct);

        var toBlock = new List<AppointmentSlot>();
        int skipped = 0;

        foreach (var slot in openSlots)
        {
            bool overlaps = activeExceptions.Any(ex =>
                ex.OverlapsWith(slot.StartAtUtc, slot.EndAtUtc) &&
                (ex.FacilityId == null || ex.FacilityId == slot.FacilityId));

            if (!overlaps)
                continue;

            if (slot.ReservedCount > 0)
            {
                skipped++;
                continue;
            }

            slot.Block(userId);
            toBlock.Add(slot);
        }

        if (toBlock.Count > 0)
            await _slots.UpdateRangeAsync(toBlock, ct);

        return new ApplyExceptionsResponse
        {
            ProviderId   = providerId,
            SlotsBlocked = toBlock.Count,
            SlotsSkipped = skipped
        };
    }

    private async Task ValidateRequestAsync(
        Guid tenantId,
        Guid providerId,
        Guid? facilityId,
        DateTime startAtUtc,
        DateTime endAtUtc,
        string exceptionType,
        CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();

        _ = await _providers.GetByIdAsync(tenantId, providerId, ct)
            ?? throw new NotFoundException($"Provider '{providerId}' was not found.");

        if (facilityId.HasValue)
        {
            var facility = await _facilities.GetByIdAsync(tenantId, facilityId.Value, ct);
            if (facility == null)
                errors["facilityId"] = new[] { $"Facility '{facilityId}' was not found." };
        }

        if (endAtUtc <= startAtUtc)
            errors["endAtUtc"] = new[] { "EndAtUtc must be after StartAtUtc." };

        if (!ExceptionType.IsValid(exceptionType))
            errors["exceptionType"] = new[] { $"'{exceptionType}' is not a valid exception type. Allowed: Unavailable, Holiday, Vacation, Blocked." };

        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);
    }

    private static AvailabilityExceptionResponse ToResponse(ProviderAvailabilityException e) => new()
    {
        Id            = e.Id,
        TenantId      = e.TenantId,
        ProviderId    = e.ProviderId,
        FacilityId    = e.FacilityId,
        FacilityName  = e.Facility?.Name,
        StartAtUtc    = e.StartAtUtc,
        EndAtUtc      = e.EndAtUtc,
        ExceptionType = e.ExceptionType,
        Reason        = e.Reason,
        IsActive      = e.IsActive,
        CreatedAtUtc  = e.CreatedAtUtc,
        UpdatedAtUtc  = e.UpdatedAtUtc
    };
}
