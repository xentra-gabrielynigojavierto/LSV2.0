using System.Net;
using System.Text.Json;
using BuildingBlocks.Authorization;
using Flow.IntegrationTests.Infrastructure;
using Xunit;

namespace Flow.IntegrationTests.Tests;

/// <summary>
/// LS-FLOW-HARDEN-A1.1 — positive happy-path execution through the real
/// controller + real <c>WorkflowEngine</c> + real EF (SQLite) for each of
/// SynqLien, CareConnect and SynqFund. GET → POST advance (single
/// outbound transition lands the instance in a terminal stage).
/// </summary>
public class HappyPathExecutionTests : IClassFixture<SeedFixture>
{
    private readonly SeedFixture _fx;
    public HappyPathExecutionTests(SeedFixture fx) => _fx = fx;

    [Fact]
    public async Task SynqLien_happy_path_get_then_advance_to_done()
    {
        var c = _fx.Factory.AsUser(TestIds.TenantA, permissions: PermissionCodes.LienSell);

        await AssertCurrentStepAsync(c, TestIds.SlugLien, TestIds.LienEntityType,
            TestIds.LienEntityId_Happy_A, TestIds.HappyLienInstance_A, "start");

        var advanced = await c.AdvanceAsync(
            TestIds.SlugLien, TestIds.LienEntityType,
            TestIds.LienEntityId_Happy_A, TestIds.HappyLienInstance_A,
            expectedCurrentStepKey: "start");

        Assert.Equal(HttpStatusCode.OK, advanced.StatusCode);
        using var doc = JsonDocument.Parse(await advanced.Content.ReadAsStringAsync());
        Assert.Equal("done", doc.RootElement.GetProperty("currentStepKey").GetString());
        Assert.Equal("Completed", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal(TestIds.KeyLien, doc.RootElement.GetProperty("productKey").GetString());
    }

    [Fact]
    public async Task CareConnect_happy_path_get_then_advance_to_done()
    {
        var c = _fx.Factory.AsUser(TestIds.TenantA, permissions: PermissionCodes.ReferralCreate);

        var advanced = await c.AdvanceAsync(
            TestIds.SlugCareConnect, TestIds.CcEntityType,
            TestIds.CcEntityId_A, TestIds.HappyCcInstance_A,
            expectedCurrentStepKey: "start");

        Assert.Equal(HttpStatusCode.OK, advanced.StatusCode);
        using var doc = JsonDocument.Parse(await advanced.Content.ReadAsStringAsync());
        Assert.Equal("done", doc.RootElement.GetProperty("currentStepKey").GetString());
        Assert.Equal("Completed", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal(TestIds.KeyCareConnect, doc.RootElement.GetProperty("productKey").GetString());
    }

    [Fact]
    public async Task SynqFund_happy_path_get_then_advance_to_done()
    {
        var c = _fx.Factory.AsUser(TestIds.TenantA, permissions: PermissionCodes.ApplicationRefer);

        var advanced = await c.AdvanceAsync(
            TestIds.SlugFund, TestIds.FundEntityType,
            TestIds.FundEntityId_A, TestIds.HappyFundInstance_A,
            expectedCurrentStepKey: "start");

        Assert.Equal(HttpStatusCode.OK, advanced.StatusCode);
        using var doc = JsonDocument.Parse(await advanced.Content.ReadAsStringAsync());
        Assert.Equal("done", doc.RootElement.GetProperty("currentStepKey").GetString());
        Assert.Equal("Completed", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal(TestIds.KeyFund, doc.RootElement.GetProperty("productKey").GetString());
    }

    private static async Task AssertCurrentStepAsync(
        HttpClient client, string slug, string entityType, string entityId,
        Guid instanceId, string expectedStep)
    {
        var get = await client.GetAsync(HttpClientExtensions.Path(slug, entityType, entityId, instanceId));
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        using var doc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.Equal(expectedStep, doc.RootElement.GetProperty("currentStepKey").GetString());
    }
}
