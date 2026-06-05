using System.Net;
using System.Text.Json;
using BuildingBlocks.Authorization;
using BuildingBlocks.FlowClient;
using Flow.IntegrationTests.Infrastructure;
using Xunit;

namespace Flow.IntegrationTests.Tests;

/// <summary>
/// LS-FLOW-HARDEN-A1.1 — the slug in the URL must agree with the mapping's
/// <c>ProductKey</c>. Cross-product attempts (SynqLien slug pointed at a
/// CareConnect mapping, etc.) collapse to 404 not-owned.
/// </summary>
public class ProductCorrelationTests : IClassFixture<SeedFixture>
{
    private readonly SeedFixture _fx;
    public ProductCorrelationTests(SeedFixture fx) => _fx = fx;

    [Fact]
    public async Task Lien_slug_pointed_at_careconnect_mapping_returns_404()
    {
        var client = _fx.Factory.AsUser(TestIds.TenantA, permissions: PermissionCodes.LienSell);

        var resp = await client.GetAsync(HttpClientExtensions.Path(
            TestIds.SlugLien,                  // wrong slug
            TestIds.CcEntityType,
            TestIds.CcEntityId_A,
            TestIds.HappyCcInstance_A));

        await AssertNotOwned(resp);
    }

    [Fact]
    public async Task CareConnect_slug_pointed_at_fund_mapping_returns_404()
    {
        var client = _fx.Factory.AsUser(TestIds.TenantA, permissions: PermissionCodes.ReferralCreate);

        var resp = await client.GetAsync(HttpClientExtensions.Path(
            TestIds.SlugCareConnect,           // wrong slug
            TestIds.FundEntityType,
            TestIds.FundEntityId_A,
            TestIds.HappyFundInstance_A));

        await AssertNotOwned(resp);
    }

    [Fact]
    public async Task Fund_slug_pointed_at_lien_mapping_returns_404()
    {
        var client = _fx.Factory.AsUser(TestIds.TenantA, permissions: PermissionCodes.ApplicationRefer);

        var resp = await client.GetAsync(HttpClientExtensions.Path(
            TestIds.SlugFund,                  // wrong slug
            TestIds.LienEntityType,
            TestIds.LienEntityId_Happy_A,
            TestIds.HappyLienInstance_A));

        await AssertNotOwned(resp);
    }

    [Fact]
    public async Task Unknown_product_slug_returns_404_not_owned()
    {
        var client = _fx.Factory.AsUser(TestIds.TenantA, permissions: PermissionCodes.LienSell);

        var resp = await client.GetAsync(HttpClientExtensions.Path(
            "synqbogus",
            TestIds.LienEntityType, TestIds.LienEntityId_Happy_A, TestIds.HappyLienInstance_A));

        await AssertNotOwned(resp);
    }

    private static async Task AssertNotOwned(HttpResponseMessage resp)
    {
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(FlowErrorCodes.WorkflowInstanceNotOwned,
            doc.RootElement.GetProperty("code").GetString());
    }
}
