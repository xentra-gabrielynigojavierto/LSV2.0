using System.Net;
using System.Text.Json;
using BuildingBlocks.Authorization;
using BuildingBlocks.FlowClient;
using Flow.IntegrationTests.Infrastructure;
using Xunit;

namespace Flow.IntegrationTests.Tests;

/// <summary>
/// LS-FLOW-HARDEN-A1.1 — guarantees the error envelope clients (UI,
/// passthrough, smoke scripts) match against — the machine-readable
/// <c>code</c> field — is always present and never leaks information about
/// the existence of rows under a different parent.
/// </summary>
public class ErrorContractTests : IClassFixture<SeedFixture>
{
    private readonly SeedFixture _fx;
    public ErrorContractTests(SeedFixture fx) => _fx = fx;

    [Fact]
    public async Task NotOwned_responses_carry_only_the_canonical_message()
    {
        // Two requests with very different semantics that both collapse to
        // "not owned" should produce IDENTICAL response bodies — proving no
        // information disclosure differentiates the two cases.
        var clientA = _fx.Factory.AsUser(TestIds.TenantA, permissions: PermissionCodes.LienSell);

        var crossTenant = await clientA.GetAsync(HttpClientExtensions.Path(
            TestIds.SlugLien, TestIds.LienEntityType,
            TestIds.LienEntityId_B, TestIds.CrossTenantLienInstance_B));

        var wrongParent = await clientA.GetAsync(HttpClientExtensions.Path(
            TestIds.SlugLien, TestIds.LienEntityType,
            "fictional", TestIds.HappyLienInstance_A));

        var aBody = await crossTenant.Content.ReadAsStringAsync();
        var bBody = await wrongParent.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, crossTenant.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, wrongParent.StatusCode);
        Assert.Equal(aBody, bBody);

        using var doc = JsonDocument.Parse(aBody);
        Assert.Equal(FlowErrorCodes.WorkflowInstanceNotOwned,
            doc.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Conflict_responses_include_machine_readable_code()
    {
        var resp = await _fx.Factory
            .AsUser(TestIds.TenantA, permissions: PermissionCodes.LienSell)
            .AdvanceAsync(TestIds.SlugLien, TestIds.LienEntityType,
                TestIds.LienEntityId_Happy_A, TestIds.HappyLienInstance_A,
                expectedCurrentStepKey: "wrong-step");

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("code", out var code));
        Assert.False(string.IsNullOrWhiteSpace(code.GetString()));
    }
}
