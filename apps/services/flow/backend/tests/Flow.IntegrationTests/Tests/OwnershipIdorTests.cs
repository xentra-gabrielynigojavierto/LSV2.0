using System.Net;
using System.Text.Json;
using BuildingBlocks.Authorization;
using BuildingBlocks.FlowClient;
using Flow.IntegrationTests.Infrastructure;
using Xunit;

namespace Flow.IntegrationTests.Tests;

/// <summary>
/// LS-FLOW-HARDEN-A1.1 — proves the atomic ownership controller refuses
/// every variant of "right tenant, wrong parent" with the standardized
/// 404 + <c>workflow_instance_not_owned</c> error code.
/// </summary>
public class OwnershipIdorTests : IClassFixture<SeedFixture>
{
    private readonly SeedFixture _fx;
    public OwnershipIdorTests(SeedFixture fx) => _fx = fx;

    private HttpClient TenantALienUser() =>
        _fx.Factory.AsUser(TestIds.TenantA, permissions: PermissionCodes.LienSell);

    [Fact]
    public async Task Get_with_wrong_source_entity_id_returns_404_with_not_owned_code()
    {
        var resp = await TenantALienUser().GetAsync(HttpClientExtensions.Path(
            TestIds.SlugLien, TestIds.LienEntityType,
            "some-other-lien-not-mapped",
            TestIds.HappyLienInstance_A));

        await AssertNotOwnedAsync(resp);
    }

    [Fact]
    public async Task Get_with_correct_entity_but_wrong_instance_id_returns_404()
    {
        var resp = await TenantALienUser().GetAsync(HttpClientExtensions.Path(
            TestIds.SlugLien, TestIds.LienEntityType,
            TestIds.LienEntityId_Happy_A,
            Guid.NewGuid()));

        await AssertNotOwnedAsync(resp);
    }

    [Fact]
    public async Task Advance_with_wrong_parent_returns_404_not_409()
    {
        var resp = await TenantALienUser().AdvanceAsync(
            TestIds.SlugLien, TestIds.LienEntityType,
            TestIds.LienEntityId_Other_A, // mapped, but to a *different* (null) instance
            TestIds.HappyLienInstance_A,
            expectedCurrentStepKey: "start");

        await AssertNotOwnedAsync(resp);
    }

    [Fact]
    public async Task Complete_with_unrelated_entity_id_returns_404()
    {
        var resp = await TenantALienUser().CompleteAsync(
            TestIds.SlugLien, TestIds.LienEntityType,
            "totally-fake-lien",
            TestIds.HappyLienInstance_A);

        await AssertNotOwnedAsync(resp);
    }

    private static async Task AssertNotOwnedAsync(HttpResponseMessage resp)
    {
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(FlowErrorCodes.WorkflowInstanceNotOwned,
            doc.RootElement.GetProperty("code").GetString());
    }
}
