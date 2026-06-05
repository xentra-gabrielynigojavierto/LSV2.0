using System.Net;
using System.Text.Json;
using BuildingBlocks.Authorization;
using BuildingBlocks.FlowClient;
using Flow.IntegrationTests.Infrastructure;
using Xunit;

namespace Flow.IntegrationTests.Tests;

/// <summary>
/// LS-FLOW-HARDEN-A1.1 — tenant isolation under both user and service
/// callers. Tenant-A callers must never observe tenant-B state.
/// </summary>
public class TenantIsolationTests : IClassFixture<SeedFixture>
{
    private readonly SeedFixture _fx;
    public TenantIsolationTests(SeedFixture fx) => _fx = fx;

    [Fact]
    public async Task TenantA_user_cannot_read_tenantB_instance_via_correct_entity_path()
    {
        var client = _fx.Factory.AsUser(TestIds.TenantA, permissions: PermissionCodes.LienSell);

        // The (entityType, entityId, instanceId) below all exist — but on tenant B.
        var resp = await client.GetAsync(HttpClientExtensions.Path(
            TestIds.SlugLien, TestIds.LienEntityType,
            TestIds.LienEntityId_B,
            TestIds.CrossTenantLienInstance_B));

        await AssertNotOwned(resp);
    }

    [Fact]
    public async Task TenantA_user_cannot_advance_tenantB_instance()
    {
        var client = _fx.Factory.AsUser(TestIds.TenantA, permissions: PermissionCodes.LienSell);

        var resp = await client.AdvanceAsync(
            TestIds.SlugLien, TestIds.LienEntityType,
            TestIds.LienEntityId_B,
            TestIds.CrossTenantLienInstance_B,
            expectedCurrentStepKey: "start");

        await AssertNotOwned(resp);
    }

    [Fact]
    public async Task TenantA_service_token_cannot_mutate_tenantB_instance()
    {
        var client = _fx.Factory.AsService(TestIds.TenantA, serviceName: "liens-api");

        var resp = await client.CompleteAsync(
            TestIds.SlugLien, TestIds.LienEntityType,
            TestIds.LienEntityId_B,
            TestIds.CrossTenantLienInstance_B);

        await AssertNotOwned(resp);
    }

    [Fact]
    public async Task Body_tenantId_disagreeing_with_jwt_is_rejected_403_by_middleware()
    {
        var client = _fx.Factory.AsUser(TestIds.TenantA, permissions: PermissionCodes.LienSell);

        var path = HttpClientExtensions.Path(
            TestIds.SlugLien, TestIds.LienEntityType,
            TestIds.LienEntityId_Happy_A, TestIds.HappyLienInstance_A, "advance");

        var content = new StringContent(
            "{ \"expectedCurrentStepKey\": \"start\", \"tenantId\": \"" + TestIds.TenantB + "\" }",
            System.Text.Encoding.UTF8,
            "application/json");

        var resp = await client.PostAsync(path, content);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
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
