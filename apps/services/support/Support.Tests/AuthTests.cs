using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Support.Api.Auth;
using Support.Api.Domain;
using Support.Api.Dtos;

namespace Support.Tests;

public class AuthTests : IClassFixture<SupportApiProdFactory>, IClassFixture<SupportApiFactory>
{
    private readonly SupportApiProdFactory _prod;
    private readonly SupportApiFactory _test;

    public AuthTests(SupportApiProdFactory prod, SupportApiFactory test)
    {
        _prod = prod;
        _test = test;
    }

    private static HttpClient WithAuth(HttpClient c, string? token, string? tenant = null)
    {
        if (!string.IsNullOrEmpty(token))
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (!string.IsNullOrEmpty(tenant))
            c.DefaultRequestHeaders.Add("X-Tenant-Id", tenant);
        return c;
    }

    private static CreateTicketRequest NewTicket(string? tenant = null) => new()
    {
        TenantId = tenant,
        Title = "Title",
        Description = "Description body",
        Priority = TicketPriority.Normal,
        Severity = TicketSeverity.Sev3,
        Source = TicketSource.Portal,
        ProductCode = "PROD"
    };

    // 1 — Protected ticket endpoint returns 401 without token in production-like env
    [Fact]
    public async Task Prod_Tickets_NoToken_Returns_401()
    {
        var c = _prod.CreateClient();
        var r = await c.GetAsync("/support/api/tickets");
        r.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // 2 — Protected comment endpoint returns 401 without token
    [Fact]
    public async Task Prod_Comments_NoToken_Returns_401()
    {
        var c = _prod.CreateClient();
        var r = await c.GetAsync($"/support/api/tickets/{Guid.NewGuid()}/comments");
        r.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // 3 — Protected attachment endpoint returns 401 without token
    [Fact]
    public async Task Prod_Attachments_NoToken_Returns_401()
    {
        var c = _prod.CreateClient();
        var r = await c.GetAsync($"/support/api/tickets/{Guid.NewGuid()}/attachments");
        r.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // 4 — Health endpoint remains accessible without token
    [Fact]
    public async Task Prod_Health_Anonymous_OK()
    {
        var c = _prod.CreateClient();
        var r = await c.GetAsync("/support/api/health");
        r.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // 5 — Authenticated request with tenant_id claim succeeds (prod-like)
    [Fact]
    public async Task Prod_With_Tenant_Claim_Succeeds()
    {
        var token = TestJwt.Issue(tenantId: "tenant-AUTH-1",
            roles: new[] { SupportRoles.SupportAgent });
        var c = WithAuth(_prod.CreateClient(), token);
        var r = await c.GetAsync("/support/api/tickets");
        r.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // 6 — Authenticated request missing tenant claim returns 403
    [Fact]
    public async Task Prod_Missing_Tenant_Claim_Returns_403()
    {
        var token = TestJwt.Issue(tenantId: null, roles: new[] { SupportRoles.SupportAgent });
        var c = WithAuth(_prod.CreateClient(), token);
        var r = await c.GetAsync("/support/api/tickets");
        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // 7 — In production-like env, X-Tenant-Id header alone is rejected
    [Fact]
    public async Task Prod_HeaderTenant_Without_Token_Returns_401()
    {
        var c = _prod.CreateClient();
        c.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-AUTH-2");
        var r = await c.GetAsync("/support/api/tickets");
        r.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // 8 — In dev/testing env, header fallback still works (regression for B01-B03)
    [Fact]
    public async Task Testing_HeaderTenant_Still_Works()
    {
        var c = _test.CreateClient();
        c.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-AUTH-T8");
        var r = await c.GetAsync("/support/api/tickets");
        r.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // 9 — SupportAgent can read ticket
    [Fact]
    public async Task SupportAgent_Can_Read_Tickets()
    {
        var c = _test.CreateClient();
        c.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-AUTH-9");
        c.DefaultRequestHeaders.Add("X-Test-Roles", SupportRoles.SupportAgent);
        var r = await c.GetAsync("/support/api/tickets");
        r.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // 10 — SupportAgent can update ticket
    [Fact]
    public async Task SupportAgent_Can_Update_Ticket()
    {
        // create as full-roles user
        var creator = _test.CreateClient();
        creator.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-AUTH-10");
        var created = await creator.PostAsJsonAsync("/support/api/tickets", NewTicket());
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var t = await created.Content.ReadFromJsonAsync<TicketResponse>();

        // update as agent
        var agent = _test.CreateClient();
        agent.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-AUTH-10");
        agent.DefaultRequestHeaders.Add("X-Test-Roles", SupportRoles.SupportAgent);
        var upd = await agent.PutAsJsonAsync($"/support/api/tickets/{t!.Id}",
            new UpdateTicketRequest { Status = TicketStatus.InProgress });
        upd.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // 11 — TenantUser can create ticket
    [Fact]
    public async Task TenantUser_Can_Create_Ticket()
    {
        var c = _test.CreateClient();
        c.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-AUTH-11");
        c.DefaultRequestHeaders.Add("X-Test-Roles", SupportRoles.TenantUser);
        var r = await c.PostAsJsonAsync("/support/api/tickets", NewTicket());
        r.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // 12 — Unauthorized role returns 403
    [Fact]
    public async Task Unknown_Role_Returns_403()
    {
        var c = _test.CreateClient();
        c.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-AUTH-12");
        c.DefaultRequestHeaders.Add("X-Test-Roles", "RandomOutsideRole");
        var r = await c.GetAsync("/support/api/tickets");
        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // 13 — Product-ref delete requires SupportManage; SupportAgent → 403
    [Fact]
    public async Task ProductRef_Delete_Requires_Manage()
    {
        // create ticket + product-ref as full roles
        var owner = _test.CreateClient();
        owner.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-AUTH-13");
        var created = await owner.PostAsJsonAsync("/support/api/tickets", NewTicket());
        var t = await created.Content.ReadFromJsonAsync<TicketResponse>();
        var refResp = await owner.PostAsJsonAsync(
            $"/support/api/tickets/{t!.Id}/product-refs",
            new CreateProductReferenceRequest
            {
                ProductCode = "DOCS",
                EntityType = "document",
                EntityId = "doc-1"
            });
        refResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var prRef = await refResp.Content.ReadFromJsonAsync<ProductReferenceResponse>();

        // SupportAgent cannot delete
        var agent = _test.CreateClient();
        agent.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-AUTH-13");
        agent.DefaultRequestHeaders.Add("X-Test-Roles", SupportRoles.SupportAgent);
        var deny = await agent.DeleteAsync(
            $"/support/api/tickets/{t.Id}/product-refs/{prRef!.Id}");
        deny.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // SupportManager can delete
        var mgr = _test.CreateClient();
        mgr.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-AUTH-13");
        mgr.DefaultRequestHeaders.Add("X-Test-Roles", SupportRoles.SupportManager);
        var ok = await mgr.DeleteAsync(
            $"/support/api/tickets/{t.Id}/product-refs/{prRef.Id}");
        ok.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
