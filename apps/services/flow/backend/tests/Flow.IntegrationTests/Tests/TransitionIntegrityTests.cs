using System.Net;
using System.Text.Json;
using BuildingBlocks.Authorization;
using Flow.IntegrationTests.Infrastructure;
using Xunit;

namespace Flow.IntegrationTests.Tests;

/// <summary>
/// LS-FLOW-HARDEN-A1.1 — engine-level integrity surfaced as 409 with the
/// canonical error codes:
///   - <c>stale_current_step</c>   when expectedCurrentStepKey is wrong.
///   - <c>instance_not_active</c>  when the instance is already terminal.
/// </summary>
public class TransitionIntegrityTests : IClassFixture<SeedFixture>
{
    private readonly SeedFixture _fx;
    public TransitionIntegrityTests(SeedFixture fx) => _fx = fx;

    private HttpClient LienUser() =>
        _fx.Factory.AsUser(TestIds.TenantA, permissions: PermissionCodes.LienSell);

    [Fact]
    public async Task Advance_with_stale_expected_step_returns_409_stale_current_step()
    {
        var resp = await LienUser().AdvanceAsync(
            TestIds.SlugLien, TestIds.LienEntityType,
            TestIds.LienEntityId_Happy_A, TestIds.HappyLienInstance_A,
            expectedCurrentStepKey: "totally-not-the-current-step");

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal("stale_current_step", doc.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Advance_on_completed_instance_returns_409_instance_not_active()
    {
        var resp = await LienUser().AdvanceAsync(
            TestIds.SlugLien, TestIds.LienEntityType,
            "decoy-completed", TestIds.CompletedLienInstance_A,
            expectedCurrentStepKey: "done");

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal("instance_not_active", doc.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Complete_on_already_completed_is_idempotent_200()
    {
        // Engine treats "already Completed" as a no-op success.
        var resp = await LienUser().CompleteAsync(
            TestIds.SlugLien, TestIds.LienEntityType,
            "decoy-completed", TestIds.CompletedLienInstance_A);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal("Completed", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Advance_with_blank_expectedCurrentStepKey_returns_400()
    {
        var path = HttpClientExtensions.Path(
            TestIds.SlugLien, TestIds.LienEntityType,
            TestIds.LienEntityId_Happy_A, TestIds.HappyLienInstance_A, "advance");

        var resp = await LienUser().PostAsync(path,
            new StringContent("{\"expectedCurrentStepKey\":\"\"}", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
