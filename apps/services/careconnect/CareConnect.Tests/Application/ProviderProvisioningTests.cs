// LSCC-01-003: ActivateForCareConnectAsync service tests.
using BuildingBlocks.Exceptions;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Application.Services;
using CareConnect.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CareConnect.Tests.Application;

/// <summary>
/// LSCC-01-003 — ProviderService.ActivateForCareConnectAsync:
///
///   1. Provider not found → NotFoundException
///   2. Provider already active → alreadyActive=true, no UpdateAsync called
///   3. Provider inactive → alreadyActive=false, UpdateAsync called once
///   4. Provider active but not accepting referrals → treated as not fully active, UpdateAsync called
///   5. Result carries correct IsActive / AcceptingReferrals values
/// </summary>
public class ProviderProvisioningTests
{
    private static Provider BuildProvider(bool isActive, bool acceptingReferrals)
    {
        return Provider.Create(
            tenantId:           Guid.NewGuid(),
            name:               "Test Provider",
            organizationName:   null,
            email:              "test@example.com",
            phone:              "555-0100",
            addressLine1:       "1 Main St",
            city:               "Austin",
            state:              "TX",
            postalCode:         "78701",
            isActive:           isActive,
            acceptingReferrals: acceptingReferrals,
            createdByUserId:    null);
    }

    private static (ProviderService sut, Mock<IProviderRepository> repoMock)
    BuildSut(Provider? returnedProvider)
    {
        var repoMock  = new Mock<IProviderRepository>();
        var slotsMock = new Mock<IAppointmentSlotRepository>();
        var logger    = NullLogger<ProviderService>.Instance;

        repoMock
            .Setup(r => r.GetByIdCrossAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(returnedProvider);

        repoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Provider>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new ProviderService(repoMock.Object, slotsMock.Object, logger);
        return (sut, repoMock);
    }

    [Fact]
    public async Task ActivateForCareConnectAsync_ProviderNotFound_ThrowsNotFoundException()
    {
        var (sut, _) = BuildSut(returnedProvider: null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sut.ActivateForCareConnectAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ActivateForCareConnectAsync_AlreadyActive_ReturnsAlreadyActiveTrue()
    {
        var provider    = BuildProvider(isActive: true, acceptingReferrals: true);
        var (sut, repo) = BuildSut(provider);

        var result = await sut.ActivateForCareConnectAsync(provider.Id);

        Assert.True(result.AlreadyActive);
        repo.Verify(r => r.UpdateAsync(It.IsAny<Provider>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ActivateForCareConnectAsync_InactiveProvider_CallsUpdate()
    {
        var provider    = BuildProvider(isActive: false, acceptingReferrals: false);
        var (sut, repo) = BuildSut(provider);

        var result = await sut.ActivateForCareConnectAsync(provider.Id);

        Assert.False(result.AlreadyActive);
        repo.Verify(r => r.UpdateAsync(provider, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActivateForCareConnectAsync_InactiveProvider_ResultIsActive()
    {
        var provider = BuildProvider(isActive: false, acceptingReferrals: false);
        var (sut, _) = BuildSut(provider);

        var result = await sut.ActivateForCareConnectAsync(provider.Id);

        Assert.True(result.IsActive);
        Assert.True(result.AcceptingReferrals);
    }

    [Fact]
    public async Task ActivateForCareConnectAsync_ActiveNotAccepting_CallsUpdate()
    {
        var provider    = BuildProvider(isActive: true, acceptingReferrals: false);
        var (sut, repo) = BuildSut(provider);

        var result = await sut.ActivateForCareConnectAsync(provider.Id);

        Assert.False(result.AlreadyActive);
        repo.Verify(r => r.UpdateAsync(provider, It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(result.AcceptingReferrals);
    }
}
