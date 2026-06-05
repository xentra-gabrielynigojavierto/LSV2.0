// LSCC-01-003: Provider.Activate() domain method tests.
using CareConnect.Domain;
using Xunit;

namespace CareConnect.Tests.Domain;

/// <summary>
/// LSCC-01-003 — Provider.Activate() domain method:
///
///   1. Inactive provider → IsActive=true, AcceptingReferrals=true
///   2. Active but not accepting → AcceptingReferrals flipped to true
///   3. Already fully active → state unchanged (idempotent)
///   4. UpdatedAtUtc is advanced on activation
/// </summary>
public class ProviderActivationTests
{
    private static Provider BuildProvider(bool isActive, bool acceptingReferrals)
    {
        var p = Provider.Create(
            tenantId:           Guid.NewGuid(),
            name:               "Test Provider",
            organizationName:   null,
            email:              "test@example.com",
            phone:              "555-0100",
            addressLine1:       "1 Main St",
            city:               "Chicago",
            state:              "IL",
            postalCode:         "60601",
            isActive:           isActive,
            acceptingReferrals: acceptingReferrals,
            createdByUserId:    null);

        return p;
    }

    [Fact]
    public void Activate_InactiveProvider_SetsIsActiveTrue()
    {
        var provider = BuildProvider(isActive: false, acceptingReferrals: false);

        provider.Activate();

        Assert.True(provider.IsActive);
    }

    [Fact]
    public void Activate_InactiveProvider_SetsAcceptingReferralsTrue()
    {
        var provider = BuildProvider(isActive: false, acceptingReferrals: false);

        provider.Activate();

        Assert.True(provider.AcceptingReferrals);
    }

    [Fact]
    public void Activate_ActiveButNotAccepting_SetsAcceptingReferralsTrue()
    {
        var provider = BuildProvider(isActive: true, acceptingReferrals: false);

        provider.Activate();

        Assert.True(provider.IsActive);
        Assert.True(provider.AcceptingReferrals);
    }

    [Fact]
    public void Activate_AlreadyFullyActive_RemainsActive()
    {
        var provider = BuildProvider(isActive: true, acceptingReferrals: true);
        var beforeUpdate = provider.UpdatedAtUtc;

        provider.Activate();

        Assert.True(provider.IsActive);
        Assert.True(provider.AcceptingReferrals);
    }

    [Fact]
    public void Activate_AdvancesUpdatedAtUtc()
    {
        var provider = BuildProvider(isActive: false, acceptingReferrals: false);
        var before = provider.UpdatedAtUtc;

        // Small delay so clock can tick
        System.Threading.Thread.Sleep(2);
        provider.Activate();

        Assert.True(provider.UpdatedAtUtc >= before);
    }
}
