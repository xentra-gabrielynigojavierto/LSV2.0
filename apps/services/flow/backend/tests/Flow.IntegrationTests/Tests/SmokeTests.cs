using System.Net;
using System.Text.Json;
using BuildingBlocks.Authorization;
using Flow.IntegrationTests.Infrastructure;
using Xunit;

namespace Flow.IntegrationTests.Tests;

/// <summary>
/// Smoke layer — verifies the host boots and a minimal happy GET succeeds.
/// If this fails, every other test in the suite is unreliable.
/// </summary>
public class SmokeTests : IClassFixture<SeedFixture>
{
    private readonly SeedFixture _fx;

    public SmokeTests(SeedFixture fx) => _fx = fx;

    [Fact]
    public async Task Healthz_is_anonymous_and_returns_200()
    {
        var client = _fx.Factory.Anonymous();
        var resp = await client.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Get_owned_lien_instance_returns_200_and_correct_step()
    {
        var client = _fx.Factory.AsUser(
            TestIds.TenantA,
            permissions: PermissionCodes.LienSell);

        var resp = await client.GetAsync(HttpClientExtensions.Path(
            TestIds.SlugLien, TestIds.LienEntityType, TestIds.LienEntityId_Happy_A,
            TestIds.HappyLienInstance_A));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("start", doc.RootElement.GetProperty("currentStepKey").GetString());
        Assert.Equal("Active", doc.RootElement.GetProperty("status").GetString());
    }
}
