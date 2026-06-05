using System.Net;
using BuildingBlocks.Authorization;
using Flow.IntegrationTests.Infrastructure;
using Xunit;

namespace Flow.IntegrationTests.Tests;

/// <summary>
/// LS-FLOW-HARDEN-A1.1 — auth + capability gating against the real
/// production policies.
///
/// Notes:
///   - The Flow.Api host runs with the "Testing" environment, which is
///     NOT Development. The "no permissions at all" dev fallback in the
///     capability policies therefore does not engage — a user must carry
///     the correct permission OR the matching product role to get past
///     the capability check.
///   - Service callers bypass capability policies (the originating product
///     service is the gate); the controller still enforces tenant + parent
///     ownership for them.
/// </summary>
public class AuthTests : IClassFixture<SeedFixture>
{
    private readonly SeedFixture _fx;
    public AuthTests(SeedFixture fx) => _fx = fx;

    [Fact]
    public async Task Anonymous_request_is_401()
    {
        var resp = await _fx.Factory.Anonymous().GetAsync(HttpClientExtensions.Path(
            TestIds.SlugLien, TestIds.LienEntityType,
            TestIds.LienEntityId_Happy_A, TestIds.HappyLienInstance_A));

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task User_with_correct_permission_can_get()
    {
        var resp = await _fx.Factory.AsUser(TestIds.TenantA, permissions: PermissionCodes.LienSell)
            .GetAsync(HttpClientExtensions.Path(
                TestIds.SlugLien, TestIds.LienEntityType,
                TestIds.LienEntityId_Happy_A, TestIds.HappyLienInstance_A));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task User_with_matching_product_role_can_get()
    {
        // Critical: the only permission carried is unrelated to liens
        // (AppointmentCreate). If the capability policy ever stops honouring
        // the product_roles claim, this test will FAIL — proving the branch
        // is what is granting access, not a happenstance permission.
        var resp = await _fx.Factory.AsUser(
                TestIds.TenantA,
                permissions: PermissionCodes.AppointmentCreate,
                productRoles: ProductCodes.SynqLiens + ":lien_sell_clerk")
            .GetAsync(HttpClientExtensions.Path(
                TestIds.SlugLien, TestIds.LienEntityType,
                TestIds.LienEntityId_Happy_A, TestIds.HappyLienInstance_A));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task User_lacking_capability_is_403()
    {
        // Carries some permission so the dev "no permissions at all"
        // fallback never engages, but nothing relevant to lien sale.
        var resp = await _fx.Factory.AsUser(
                TestIds.TenantA,
                permissions: PermissionCodes.AppointmentCreate)
            .GetAsync(HttpClientExtensions.Path(
                TestIds.SlugLien, TestIds.LienEntityType,
                TestIds.LienEntityId_Happy_A, TestIds.HappyLienInstance_A));

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Service_token_caller_bypasses_capability_but_not_tenant()
    {
        var ok = await _fx.Factory.AsService(TestIds.TenantA, "liens-api")
            .GetAsync(HttpClientExtensions.Path(
                TestIds.SlugLien, TestIds.LienEntityType,
                TestIds.LienEntityId_Happy_A, TestIds.HappyLienInstance_A));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        var crossTenant = await _fx.Factory.AsService(TestIds.TenantA, "liens-api")
            .GetAsync(HttpClientExtensions.Path(
                TestIds.SlugLien, TestIds.LienEntityType,
                TestIds.LienEntityId_B, TestIds.CrossTenantLienInstance_B));
        Assert.Equal(HttpStatusCode.NotFound, crossTenant.StatusCode);
    }
}
