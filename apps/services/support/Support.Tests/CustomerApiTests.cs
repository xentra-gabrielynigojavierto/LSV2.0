using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Support.Api.Domain;
using Support.Api.Dtos;
using Support.Api.Endpoints;

namespace Support.Tests;

/// <summary>
/// Validates the Customer RBAC layer (SUP-TNT-03).
///
/// Uses SupportApiProdFactory so real JWT validation runs and the CustomerAccess
/// policy actually enforces the ExternalCustomer role.
/// </summary>
public class CustomerApiTests : IClassFixture<SupportApiProdFactory>
{
    private readonly SupportApiProdFactory _factory;

    public CustomerApiTests(SupportApiProdFactory factory) => _factory = factory;

    private HttpClient AgentClient(string tenantId)
    {
        var c = _factory.CreateClient();
        var token = TestJwt.Issue(tenantId: tenantId, roles: new[] { "TenantUser" });
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    private HttpClient CustomerClient(string tenantId, Guid externalCustomerId, string email = "customer@example.com")
    {
        var c = _factory.CreateClient();
        var token = TestJwt.IssueCustomer(tenantId, externalCustomerId, email: email);
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    private HttpClient ManagerClient(string tenantId)
    {
        var c = _factory.CreateClient();
        var token = TestJwt.Issue(tenantId: tenantId, roles: new[] { "SupportManager" });
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    /// <summary>
    /// Enables TenantCustomerSupport mode for the given tenant.
    /// Required before any test that expects customer endpoints to succeed
    /// (the default effective mode is InternalOnly, which returns 403).
    /// </summary>
    private async Task EnableCustomerSupportAsync(string tenantId)
    {
        var manager = ManagerClient(tenantId);
        var resp = await manager.PutAsJsonAsync(
            "/support/api/admin/tenant-settings",
            new { supportMode = "TenantCustomerSupport", customerPortalEnabled = true });
        resp.IsSuccessStatusCode.Should().BeTrue(
            $"enabling customer support for tenant {tenantId} failed: {resp.StatusCode}");
    }

    private static CreateTicketRequest CustomerTicketRequest(string email, string title = "Customer Issue") => new()
    {
        Title = title,
        Priority = TicketPriority.Normal,
        Source = TicketSource.Portal,
        ExternalCustomerEmail = email,
    };

    private static CreateTicketRequest InternalTicketRequest(string title = "Internal Issue") => new()
    {
        Title = title,
        Priority = TicketPriority.Normal,
        Source = TicketSource.Portal,
    };

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private async Task<TicketResponse> CreateTicketAs(HttpClient client, CreateTicketRequest req)
    {
        var r = await client.PostAsJsonAsync("/support/api/tickets", req);
        r.IsSuccessStatusCode.Should().BeTrue($"ticket creation failed: {r.StatusCode}");
        return (await r.Content.ReadFromJsonAsync<TicketResponse>())!;
    }

    // ── Test 1: Customer can list their own CustomerVisible tickets ──────────────

    [Fact]
    public async Task Customer_Can_List_Own_CustomerVisible_Tickets()
    {
        const string tenant = "rbac-test-list-01";
        const string email  = "alice@example.com";

        await EnableCustomerSupportAsync(tenant);

        var agent   = AgentClient(tenant);
        var ticket  = await CreateTicketAs(agent, CustomerTicketRequest(email));

        ticket.RequesterType.Should().Be(TicketRequesterType.ExternalCustomer);
        ticket.ExternalCustomerId.Should().NotBeNull();
        ticket.VisibilityScope.Should().Be(TicketVisibilityScope.CustomerVisible);

        var customer = CustomerClient(tenant, ticket.ExternalCustomerId!.Value, email);
        var resp = await customer.GetFromJsonAsync<PagedResponse<TicketResponse>>("/support/api/customer/tickets");

        resp!.Items.Should().ContainSingle(t => t.Id == ticket.Id);
        resp.Items.Should().OnlyContain(t => t.VisibilityScope == TicketVisibilityScope.CustomerVisible);
        resp.Items.Should().OnlyContain(t => t.ExternalCustomerId == ticket.ExternalCustomerId);
    }

    // ── Test 2: Customer cannot see Internal tickets ─────────────────────────────

    [Fact]
    public async Task Customer_Cannot_See_Internal_Visibility_Tickets()
    {
        const string tenant = "rbac-test-visibility-02";
        const string email  = "bob@example.com";

        await EnableCustomerSupportAsync(tenant);

        var agent   = AgentClient(tenant);

        // Create one CustomerVisible ticket and one Internal ticket (linked to same external customer)
        var customerTicket  = await CreateTicketAs(agent, CustomerTicketRequest(email, "Customer ticket"));
        var internalTicket  = await CreateTicketAs(agent, InternalTicketRequest("Internal ticket"));

        internalTicket.VisibilityScope.Should().Be(TicketVisibilityScope.Internal);
        internalTicket.ExternalCustomerId.Should().BeNull();

        var customer = CustomerClient(tenant, customerTicket.ExternalCustomerId!.Value, email);
        var resp = await customer.GetFromJsonAsync<PagedResponse<TicketResponse>>("/support/api/customer/tickets");

        resp!.Items.Should().NotContain(t => t.Id == internalTicket.Id);
        resp.Items.Should().OnlyContain(t => t.VisibilityScope == TicketVisibilityScope.CustomerVisible);
    }

    // ── Test 3: Customer cannot get another customer's ticket ─────────────────────

    [Fact]
    public async Task Customer_Cannot_Get_Another_Customers_Ticket()
    {
        const string tenant  = "rbac-test-isolation-03";
        const string emailA  = "alice-03@example.com";
        const string emailB  = "bob-03@example.com";

        await EnableCustomerSupportAsync(tenant);

        var agent   = AgentClient(tenant);
        var ticketA = await CreateTicketAs(agent, CustomerTicketRequest(emailA, "Alice's ticket"));
        var ticketB = await CreateTicketAs(agent, CustomerTicketRequest(emailB, "Bob's ticket"));

        // Customer B tries to GET Customer A's ticket
        var customerB = CustomerClient(tenant, ticketB.ExternalCustomerId!.Value, emailB);
        var resp = await customerB.GetAsync($"/support/api/customer/tickets/{ticketA.Id}");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Test 4: Customer can get their own CustomerVisible ticket ────────────────

    [Fact]
    public async Task Customer_Can_Get_Own_CustomerVisible_Ticket()
    {
        const string tenant = "rbac-test-get-04";
        const string email  = "carol@example.com";

        await EnableCustomerSupportAsync(tenant);

        var agent   = AgentClient(tenant);
        var ticket  = await CreateTicketAs(agent, CustomerTicketRequest(email));

        var customer = CustomerClient(tenant, ticket.ExternalCustomerId!.Value, email);
        var resp = await customer.GetAsync($"/support/api/customer/tickets/{ticket.Id}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<TicketResponse>();
        body!.Id.Should().Be(ticket.Id);
    }

    // ── Test 5: Customer can add a comment to their own ticket ───────────────────

    [Fact]
    public async Task Customer_Can_Add_Comment_To_Own_Ticket()
    {
        const string tenant = "rbac-test-comment-05";
        const string email  = "dave@example.com";

        await EnableCustomerSupportAsync(tenant);

        var agent   = AgentClient(tenant);
        var ticket  = await CreateTicketAs(agent, CustomerTicketRequest(email));

        var customer = CustomerClient(tenant, ticket.ExternalCustomerId!.Value, email);
        var resp = await customer.PostAsJsonAsync(
            $"/support/api/customer/tickets/{ticket.Id}/comments",
            new CustomerAddCommentRequest { Body = "Hello, any update?" });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var comment = await resp.Content.ReadFromJsonAsync<CommentResponse>();
        comment!.Body.Should().Be("Hello, any update?");
        comment.AuthorEmail.Should().Be(email);
    }

    // ── Test 6: Customer cannot comment on another customer's ticket ─────────────

    [Fact]
    public async Task Customer_Cannot_Comment_On_Another_Customers_Ticket()
    {
        const string tenant  = "rbac-test-comment-isolation-06";
        const string emailA  = "alice-06@example.com";
        const string emailB  = "bob-06@example.com";

        await EnableCustomerSupportAsync(tenant);

        var agent   = AgentClient(tenant);
        var ticketA = await CreateTicketAs(agent, CustomerTicketRequest(emailA, "Alice's ticket"));
        var ticketB = await CreateTicketAs(agent, CustomerTicketRequest(emailB, "Bob's ticket"));

        // Customer B tries to comment on Customer A's ticket
        var customerB = CustomerClient(tenant, ticketB.ExternalCustomerId!.Value, emailB);
        var resp = await customerB.PostAsJsonAsync(
            $"/support/api/customer/tickets/{ticketA.Id}/comments",
            new CustomerAddCommentRequest { Body = "I should not be able to comment here." });

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Test 7: Customer cannot access queue endpoints ───────────────────────────

    [Fact]
    public async Task Customer_Cannot_Access_Queue_Endpoints()
    {
        const string tenant   = "rbac-test-queue-07";
        var customerId = Guid.NewGuid();

        var customer = CustomerClient(tenant, customerId);
        var resp = await customer.GetAsync("/support/api/queues");

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Test 8: Customer cannot access internal ticket list ──────────────────────

    [Fact]
    public async Task Customer_Cannot_Access_Internal_Ticket_List()
    {
        const string tenant   = "rbac-test-internal-08";
        var customerId = Guid.NewGuid();

        var customer = CustomerClient(tenant, customerId);
        var resp = await customer.GetAsync("/support/api/tickets");

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Test 9: Internal agent list still sees all tenant tickets ────────────────

    [Fact]
    public async Task Agent_List_Sees_All_Tenant_Tickets_Including_CustomerVisible()
    {
        const string tenant = "rbac-test-agent-09";
        const string email  = "eve@example.com";

        var agent  = AgentClient(tenant);
        var t1     = await CreateTicketAs(agent, InternalTicketRequest("Internal ticket"));
        var t2     = await CreateTicketAs(agent, CustomerTicketRequest(email, "Customer ticket"));

        var list = await agent.GetFromJsonAsync<PagedResponse<TicketResponse>>("/support/api/tickets");

        list!.Items.Should().Contain(t => t.Id == t1.Id);
        list.Items.Should().Contain(t => t.Id == t2.Id);
    }

    // ── Test 10: No token returns 401 on customer endpoint ───────────────────────

    [Fact]
    public async Task No_Token_Returns_401_On_Customer_Endpoint()
    {
        var c = _factory.CreateClient();
        var resp = await c.GetAsync("/support/api/customer/tickets");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Test 11: Internal-role token returns 403 on customer endpoint ────────────

    [Fact]
    public async Task Internal_Agent_Token_Returns_403_On_Customer_Endpoint()
    {
        const string tenant = "rbac-test-403-11";
        var agent = AgentClient(tenant);
        var resp  = await agent.GetAsync("/support/api/customer/tickets");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
