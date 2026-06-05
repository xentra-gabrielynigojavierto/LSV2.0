using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Support.Api.Domain;
using Support.Api.Dtos;
using Support.Api.Services;

namespace Support.Tests;

/// <summary>
/// Validates the Multi-Mode Support configuration (SUP-TNT-04).
///
/// Covers:
///  - Default InternalOnly when no settings row exists
///  - PUT sets mode; GET reflects updated mode
///  - Customer endpoints blocked (403) when InternalOnly (default)
///  - Customer endpoints allowed to proceed to RBAC checks when TenantCustomerSupport + portal enabled
///  - ExternalCustomer cannot call admin settings endpoints
///  - Tenant A settings do not affect Tenant B
///
/// Uses SupportApiProdFactory so real JWT auth runs.
/// </summary>
public class TenantSettingsTests : IClassFixture<SupportApiProdFactory>
{
    private readonly SupportApiProdFactory _factory;

    public TenantSettingsTests(SupportApiProdFactory factory) => _factory = factory;

    // ── Client factories ─────────────────────────────────────────────────────

    private HttpClient ManagerClient(string tenantId) =>
        AuthorizedClient(tenantId, "SupportManager");

    private HttpClient AgentClient(string tenantId) =>
        AuthorizedClient(tenantId, "TenantUser");

    private HttpClient CustomerClient(string tenantId, Guid customerId, string email = "c@example.com")
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwt.IssueCustomer(tenantId, customerId, email: email));
        return c;
    }

    private HttpClient AuthorizedClient(string tenantId, string role)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwt.Issue(tenantId: tenantId, roles: new[] { role }));
        return c;
    }

    // ── Test 1: Default InternalOnly when no row exists ──────────────────────

    [Fact]
    public async Task GetSettings_DefaultsToInternalOnly_WhenNoRowExists()
    {
        var tenant = $"ts-default-{Guid.NewGuid():N}";
        var manager = ManagerClient(tenant);

        var resp = await manager.GetFromJsonAsync<TenantSettingsResponse>(
            "/support/api/admin/tenant-settings");

        resp.Should().NotBeNull();
        resp!.SupportMode.Should().Be("InternalOnly");
        resp.CustomerPortalEnabled.Should().BeFalse();
        resp.EffectiveCustomerSupportEnabled.Should().BeFalse();
        resp.TenantId.Should().Be(tenant);
    }

    // ── Test 2: PUT sets mode; GET reflects it ───────────────────────────────

    [Fact]
    public async Task PutSettings_SetsTenantCustomerSupport_AndGetReflectsIt()
    {
        var tenant = $"ts-set-{Guid.NewGuid():N}";
        var manager = ManagerClient(tenant);

        var putResp = await manager.PutAsJsonAsync(
            "/support/api/admin/tenant-settings",
            new { supportMode = "TenantCustomerSupport", customerPortalEnabled = true });
        putResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var settings = await putResp.Content.ReadFromJsonAsync<TenantSettingsResponse>();
        settings!.SupportMode.Should().Be("TenantCustomerSupport");
        settings.CustomerPortalEnabled.Should().BeTrue();
        settings.EffectiveCustomerSupportEnabled.Should().BeTrue();

        var getResp = await manager.GetFromJsonAsync<TenantSettingsResponse>(
            "/support/api/admin/tenant-settings");
        getResp!.SupportMode.Should().Be("TenantCustomerSupport");
        getResp.CustomerPortalEnabled.Should().BeTrue();
        getResp.EffectiveCustomerSupportEnabled.Should().BeTrue();
    }

    // ── Test 3: Customer endpoints return 403 in default InternalOnly mode ───

    [Fact]
    public async Task CustomerEndpoints_Return403_WhenInternalOnlyDefault()
    {
        var tenant     = $"ts-block-{Guid.NewGuid():N}";
        var customerId = Guid.NewGuid();
        var customer   = CustomerClient(tenant, customerId);

        var listResp = await customer.GetAsync("/support/api/customer/tickets");
        listResp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "customer tickets list must fail closed when no settings row exists (InternalOnly default)");

        var getResp = await customer.GetAsync($"/support/api/customer/tickets/{Guid.NewGuid()}");
        getResp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "customer ticket get must fail closed when no settings row exists");

        var postResp = await customer.PostAsJsonAsync(
            $"/support/api/customer/tickets/{Guid.NewGuid()}/comments",
            new { body = "hello" });
        postResp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "customer comment post must fail closed when no settings row exists");
    }

    // ── Test 4: Customer endpoints proceed when mode enabled ─────────────────

    [Fact]
    public async Task CustomerEndpoints_ProceedToRbacChecks_WhenModeEnabled()
    {
        var tenant = $"ts-allow-{Guid.NewGuid():N}";
        const string email = "bob@example.com";

        var manager = ManagerClient(tenant);
        var agent   = AgentClient(tenant);

        // Enable customer support mode for this tenant.
        var putResp = await manager.PutAsJsonAsync(
            "/support/api/admin/tenant-settings",
            new { supportMode = "TenantCustomerSupport", customerPortalEnabled = true });
        putResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Create a CustomerVisible ticket via internal agent so a customer row exists.
        var createResp = await agent.PostAsJsonAsync("/support/api/tickets", new CreateTicketRequest
        {
            Title = "Customer issue",
            Priority = TicketPriority.Normal,
            Source = TicketSource.Portal,
            ExternalCustomerEmail = email,
        });
        createResp.IsSuccessStatusCode.Should().BeTrue();
        var ticket = await createResp.Content.ReadFromJsonAsync<TicketResponse>();
        ticket.Should().NotBeNull();
        ticket!.ExternalCustomerId.Should().NotBeNull();

        // Customer with correct ID can now list tickets (mode enabled, ownership matches).
        var customer    = CustomerClient(tenant, ticket.ExternalCustomerId!.Value, email);
        var customerList = await customer.GetFromJsonAsync<PagedResponse<TicketResponse>>(
            "/support/api/customer/tickets");
        customerList.Should().NotBeNull();
        customerList!.Items.Should().Contain(t => t.Id == ticket.Id,
            "the customer's own CustomerVisible ticket must be visible");
    }

    // ── Test 5: ExternalCustomer cannot call admin settings endpoints ─────────

    [Fact]
    public async Task AdminSettings_Returns403_ForExternalCustomer()
    {
        var tenant     = $"ts-ecustomer-{Guid.NewGuid():N}";
        var customerId = Guid.NewGuid();
        var customer   = CustomerClient(tenant, customerId);

        var getResp = await customer.GetAsync("/support/api/admin/tenant-settings");
        getResp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "ExternalCustomer must not access admin settings GET");

        var putResp = await customer.PutAsJsonAsync(
            "/support/api/admin/tenant-settings",
            new { supportMode = "TenantCustomerSupport", customerPortalEnabled = true });
        putResp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "ExternalCustomer must not access admin settings PUT");
    }

    // ── Test 6: Tenant A settings do not affect Tenant B ─────────────────────

    [Fact]
    public async Task Settings_AreTenantIsolated_TenantADoesNotAffectTenantB()
    {
        var tenantA    = $"ts-iso-a-{Guid.NewGuid():N}";
        var tenantB    = $"ts-iso-b-{Guid.NewGuid():N}";
        var managerA   = ManagerClient(tenantA);
        var managerB   = ManagerClient(tenantB);

        // Enable customer support for Tenant A only.
        var putA = await managerA.PutAsJsonAsync(
            "/support/api/admin/tenant-settings",
            new { supportMode = "TenantCustomerSupport", customerPortalEnabled = true });
        putA.StatusCode.Should().Be(HttpStatusCode.OK);

        // Tenant B should still be InternalOnly (no row for it).
        var settingsB = await managerB.GetFromJsonAsync<TenantSettingsResponse>(
            "/support/api/admin/tenant-settings");
        settingsB!.SupportMode.Should().Be("InternalOnly");
        settingsB.EffectiveCustomerSupportEnabled.Should().BeFalse();

        // Tenant B customer must be blocked.
        var customerB  = CustomerClient(tenantB, Guid.NewGuid());
        var customerBList = await customerB.GetAsync("/support/api/customer/tickets");
        customerBList.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Tenant B's customer must be blocked when only Tenant A has mode enabled");
    }

    // ── Test 7: EffectiveCustomerSupportEnabled is false when portal disabled ─

    [Fact]
    public async Task EffectiveEnabled_IsFalse_WhenModeSetButPortalDisabled()
    {
        var tenant  = $"ts-noportal-{Guid.NewGuid():N}";
        var manager = ManagerClient(tenant);

        var putResp = await manager.PutAsJsonAsync(
            "/support/api/admin/tenant-settings",
            new { supportMode = "TenantCustomerSupport", customerPortalEnabled = false });
        putResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var settings = await putResp.Content.ReadFromJsonAsync<TenantSettingsResponse>();
        settings!.SupportMode.Should().Be("TenantCustomerSupport");
        settings.CustomerPortalEnabled.Should().BeFalse();
        settings.EffectiveCustomerSupportEnabled.Should().BeFalse(
            "effective is false when portal is disabled even if mode is TenantCustomerSupport");

        // Customer endpoints are still blocked.
        var customer = CustomerClient(tenant, Guid.NewGuid());
        var listResp = await customer.GetAsync("/support/api/customer/tickets");
        listResp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "customer endpoints must be blocked when portal is disabled");
    }

    // ── Test 8: Invalid supportMode value returns 422 ────────────────────────

    [Fact]
    public async Task PutSettings_Returns422_ForInvalidSupportMode()
    {
        var tenant  = $"ts-invalid-{Guid.NewGuid():N}";
        var manager = ManagerClient(tenant);

        var putResp = await manager.PutAsJsonAsync(
            "/support/api/admin/tenant-settings",
            new { supportMode = "FullyOpen", customerPortalEnabled = true });

        putResp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "invalid supportMode value must return 400 validation problem");
    }
}
