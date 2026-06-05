using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;

namespace CareConnect.Application.Services;

public class SlotGenerationService : ISlotGenerationService
{
    private const int MaxGenerationDays = 60;

    private readonly IProviderRepository _providers;
    private readonly IAvailabilityTemplateRepository _templates;
    private readonly IAppointmentSlotRepository _slots;
    private readonly IAvailabilityExceptionRepository _exceptions;

    public SlotGenerationService(
        IProviderRepository providers,
        IAvailabilityTemplateRepository templates,
        IAppointmentSlotRepository slots,
        IAvailabilityExceptionRepository exceptions)
    {
        _providers  = providers;
        _templates  = templates;
        _slots      = slots;
        _exceptions = exceptions;
    }

    public async Task<GenerateSlotsResponse> GenerateSlotsAsync(
        Guid tenantId,
        Guid providerId,
        Guid? userId,
        GenerateSlotsRequest request,
        CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();

        var fromDate = request.FromDateUtc.Date;
        var toDate = request.ToDateUtc.Date;

        if (fromDate.Year < 2000)
            errors["fromDateUtc"] = new[] { "FromDateUtc must be a valid calendar date." };

        if (toDate < fromDate)
            errors["toDateUtc"] = new[] { "ToDateUtc must be on or after FromDateUtc." };

        if ((toDate - fromDate).TotalDays > MaxGenerationDays)
            errors["toDateUtc"] = new[] { $"Date range cannot exceed {MaxGenerationDays} days." };

        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);

        _ = await _providers.GetByIdAsync(tenantId, providerId, ct)
            ?? throw new NotFoundException($"Provider '{providerId}' was not found.");

        var templates = await _templates.GetActiveByProviderAsync(tenantId, providerId, ct);

        if (templates.Count == 0)
            return new GenerateSlotsResponse
            {
                ProviderId = providerId,
                FromDateUtc = fromDate,
                ToDateUtc = toDate,
                SlotsCreated = 0
            };

        var rangeStart = fromDate;
        var rangeEnd = toDate.AddDays(1);

        var existingByTemplate = new Dictionary<Guid, HashSet<DateTime>>();
        foreach (var tmpl in templates)
        {
            existingByTemplate[tmpl.Id] = await _slots.GetExistingStartTimesAsync(
                tenantId, providerId, tmpl.Id, rangeStart, rangeEnd, ct);
        }

        var activeExceptions = await _exceptions.GetActiveInRangeAsync(tenantId, providerId, rangeStart, rangeEnd, ct);

        var newSlots = new List<AppointmentSlot>();

        for (var date = fromDate; date <= toDate; date = date.AddDays(1))
        {
            var dayOfWeek = (int)date.DayOfWeek;

            foreach (var tmpl in templates.Where(t => t.DayOfWeek == dayOfWeek))
            {
                if (tmpl.EffectiveFrom.HasValue && date < tmpl.EffectiveFrom.Value.Date)
                    continue;
                if (tmpl.EffectiveTo.HasValue && date > tmpl.EffectiveTo.Value.Date)
                    continue;

                var existing = existingByTemplate[tmpl.Id];
                var slotStart = tmpl.StartTimeLocal;

                while (slotStart + TimeSpan.FromMinutes(tmpl.SlotDurationMinutes) <= tmpl.EndTimeLocal)
                {
                    var startAtUtc = date + slotStart;
                    var endAtUtc = startAtUtc + TimeSpan.FromMinutes(tmpl.SlotDurationMinutes);

                    var blockedByException = activeExceptions.Any(ex =>
                        ex.OverlapsWith(startAtUtc, endAtUtc) &&
                        (ex.FacilityId == null || ex.FacilityId == tmpl.FacilityId));

                    if (!existing.Contains(startAtUtc) && !blockedByException)
                    {
                        newSlots.Add(AppointmentSlot.Create(
                            tenantId,
                            providerId,
                            tmpl.FacilityId,
                            tmpl.ServiceOfferingId,
                            tmpl.Id,
                            startAtUtc,
                            endAtUtc,
                            tmpl.Capacity,
                            userId));

                        existing.Add(startAtUtc);
                    }

                    slotStart += TimeSpan.FromMinutes(tmpl.SlotDurationMinutes);
                }
            }
        }

        if (newSlots.Count > 0)
            await _slots.AddRangeAsync(newSlots, ct);

        return new GenerateSlotsResponse
        {
            ProviderId = providerId,
            FromDateUtc = fromDate,
            ToDateUtc = toDate,
            SlotsCreated = newSlots.Count
        };
    }
}
