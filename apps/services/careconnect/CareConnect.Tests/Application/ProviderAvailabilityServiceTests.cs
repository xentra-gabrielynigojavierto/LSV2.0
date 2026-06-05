// LSCC-001: Tests for provider availability API (D — canonical availability endpoint)
using BuildingBlocks.Exceptions;
using CareConnect.Application.Repositories;
using CareConnect.Application.Services;
using CareConnect.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CareConnect.Tests.Application;

/// <summary>
/// LSCC-001 — Verifies ProviderService.GetAvailabilityAsync behavior:
/// slot projection, empty state, date validation, provider not found.
/// </summary>
public class ProviderAvailabilityServiceTests
{
    private readonly Guid _tenantId   = Guid.NewGuid();
    private readonly Guid _providerId = Guid.NewGuid();
    private readonly Guid _facilityId = Guid.NewGuid();

    private readonly Mock<IProviderRepository>         _providerRepo = new();
    private readonly Mock<IAppointmentSlotRepository>  _slotRepo     = new();

    private ProviderService BuildSut() =>
        new ProviderService(_providerRepo.Object, _slotRepo.Object, NullLogger<ProviderService>.Instance);

    private Provider MakeProvider() => Provider.Create(
        _tenantId, "Dr. Test", null, "test@example.com",
        "555-0000", "123 Main St", "Chicago", "IL", "60601",
        true, true, null);

    private AppointmentSlot MakeSlot(DateTime start) => AppointmentSlot.Create(
        _tenantId, _providerId, _facilityId, null, null,
        start, start.AddHours(1), capacity: 3, null);

    // ─── Date range validation ────────────────────────────────────────────────

    [Fact]
    public async Task GetAvailabilityAsync_FromAfterTo_ThrowsValidationException()
    {
        var sut  = BuildSut();
        var from = DateTime.UtcNow.AddDays(5);
        var to   = DateTime.UtcNow;

        await Assert.ThrowsAsync<ValidationException>(() =>
            sut.GetAvailabilityAsync(_tenantId, _providerId, from, to));
    }

    [Fact]
    public async Task GetAvailabilityAsync_RangeExceeds90Days_ThrowsValidationException()
    {
        var sut  = BuildSut();
        var from = DateTime.UtcNow;
        var to   = from.AddDays(91);

        await Assert.ThrowsAsync<ValidationException>(() =>
            sut.GetAvailabilityAsync(_tenantId, _providerId, from, to));
    }

    // ─── Provider not found ───────────────────────────────────────────────────

    [Fact]
    public async Task GetAvailabilityAsync_ProviderNotFound_ThrowsNotFoundException()
    {
        _providerRepo
            .Setup(r => r.GetByIdAsync(_tenantId, _providerId, default))
            .ReturnsAsync((Provider?)null);

        var sut  = BuildSut();
        var from = DateTime.UtcNow;
        var to   = from.AddDays(7);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetAvailabilityAsync(_tenantId, _providerId, from, to));
    }

    // ─── Empty state ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAvailabilityAsync_NoSlots_ReturnsEmptyList()
    {
        // LSCC-001: No slots → empty Slots list (valid response, not an error)
        var provider = MakeProvider();
        var from     = DateTime.UtcNow;
        var to       = from.AddDays(7);

        _providerRepo
            .Setup(r => r.GetByIdAsync(_tenantId, _providerId, default))
            .ReturnsAsync(provider);

        _slotRepo
            .Setup(r => r.GetOpenByProviderInRangeAsync(_tenantId, _providerId, from, to, default))
            .ReturnsAsync(new List<AppointmentSlot>());

        var sut    = BuildSut();
        var result = await sut.GetAvailabilityAsync(_tenantId, _providerId, from, to);

        Assert.NotNull(result);
        Assert.Empty(result.Slots);
        Assert.Equal(_providerId, result.ProviderId);
        Assert.Equal(provider.Name, result.ProviderName);
    }

    // ─── Slots returned ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetAvailabilityAsync_WithSlots_ReturnsProjectedSummary()
    {
        // LSCC-001: Slots present → correct projection with AvailableCount derived from Capacity - ReservedCount
        var provider = MakeProvider();
        var from     = DateTime.UtcNow;
        var to       = from.AddDays(7);
        var slot1    = MakeSlot(from.AddDays(1));
        var slot2    = MakeSlot(from.AddDays(2));

        _providerRepo
            .Setup(r => r.GetByIdAsync(_tenantId, _providerId, default))
            .ReturnsAsync(provider);

        _slotRepo
            .Setup(r => r.GetOpenByProviderInRangeAsync(_tenantId, _providerId, from, to, default))
            .ReturnsAsync(new List<AppointmentSlot> { slot1, slot2 });

        var sut    = BuildSut();
        var result = await sut.GetAvailabilityAsync(_tenantId, _providerId, from, to);

        Assert.Equal(2, result.Slots.Count);
        Assert.All(result.Slots, s => Assert.Equal(3, s.AvailableCount)); // capacity=3, reserved=0
        Assert.Equal(slot1.Id, result.Slots[0].Id);
        Assert.Equal(slot2.Id, result.Slots[1].Id);
    }

    [Fact]
    public async Task GetAvailabilityAsync_SlotsOrderedByStartTime()
    {
        var provider = MakeProvider();
        var from     = DateTime.UtcNow;
        var to       = from.AddDays(7);
        var slotLate = MakeSlot(from.AddDays(3));
        var slotEarly = MakeSlot(from.AddDays(1));

        _providerRepo
            .Setup(r => r.GetByIdAsync(_tenantId, _providerId, default))
            .ReturnsAsync(provider);

        _slotRepo
            .Setup(r => r.GetOpenByProviderInRangeAsync(_tenantId, _providerId, from, to, default))
            .ReturnsAsync(new List<AppointmentSlot> { slotLate, slotEarly }); // unordered input

        var sut    = BuildSut();
        var result = await sut.GetAvailabilityAsync(_tenantId, _providerId, from, to);

        Assert.Equal(slotEarly.Id, result.Slots[0].Id);
        Assert.Equal(slotLate.Id,  result.Slots[1].Id);
    }

    // ─── Facility filter ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetAvailabilityAsync_FacilityFilter_ExcludesNonMatchingSlots()
    {
        var provider        = MakeProvider();
        var targetFacility  = Guid.NewGuid();
        var otherFacility   = Guid.NewGuid();
        var from            = DateTime.UtcNow;
        var to              = from.AddDays(7);

        var matchingSlot    = AppointmentSlot.Create(_tenantId, _providerId, targetFacility, null, null, from.AddDays(1), from.AddDays(1).AddHours(1), 2, null);
        var nonMatchingSlot = AppointmentSlot.Create(_tenantId, _providerId, otherFacility,  null, null, from.AddDays(2), from.AddDays(2).AddHours(1), 2, null);

        _providerRepo
            .Setup(r => r.GetByIdAsync(_tenantId, _providerId, default))
            .ReturnsAsync(provider);

        _slotRepo
            .Setup(r => r.GetOpenByProviderInRangeAsync(_tenantId, _providerId, from, to, default))
            .ReturnsAsync(new List<AppointmentSlot> { matchingSlot, nonMatchingSlot });

        var sut    = BuildSut();
        var result = await sut.GetAvailabilityAsync(_tenantId, _providerId, from, to, facilityId: targetFacility);

        Assert.Single(result.Slots);
        Assert.Equal(matchingSlot.Id, result.Slots[0].Id);
    }

    // ─── Response header fields ───────────────────────────────────────────────

    [Fact]
    public async Task GetAvailabilityAsync_SetsDateRange_InResponse()
    {
        var provider = MakeProvider();
        var from     = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var to       = new DateTime(2026, 4, 14, 0, 0, 0, DateTimeKind.Utc);

        _providerRepo
            .Setup(r => r.GetByIdAsync(_tenantId, _providerId, default))
            .ReturnsAsync(provider);

        _slotRepo
            .Setup(r => r.GetOpenByProviderInRangeAsync(_tenantId, _providerId, from, to, default))
            .ReturnsAsync(new List<AppointmentSlot>());

        var sut    = BuildSut();
        var result = await sut.GetAvailabilityAsync(_tenantId, _providerId, from, to);

        Assert.Equal(from, result.From);
        Assert.Equal(to,   result.To);
    }
}
