using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Support.Api.Domain;
using Support.Api.Dtos;

namespace Support.Tests;

public class TicketApiTests : IClassFixture<SupportApiFactory>
{
    private readonly SupportApiFactory _factory;

    public TicketApiTests(SupportApiFactory factory) => _factory = factory;

    private HttpClient ClientForTenant(string tenantId)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        return c;
    }

    [Fact]
    public async Task Create_Ticket_Succeeds()
    {
        var client = ClientForTenant("tenant-A");
        var resp = await client.PostAsJsonAsync("/support/api/tickets", new CreateTicketRequest
        {
            Title = "Login broken",
            Priority = TicketPriority.High,
            Source = TicketSource.Portal,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await resp.Content.ReadFromJsonAsync<TicketResponse>();
        body!.TicketNumber.Should().StartWith("SUP-");
        body.Status.Should().Be(TicketStatus.Open);
    }

    [Fact]
    public async Task Create_Without_Title_Fails()
    {
        var client = ClientForTenant("tenant-A");
        var resp = await client.PostAsJsonAsync("/support/api/tickets", new CreateTicketRequest
        {
            Title = "",
            Priority = TicketPriority.Normal,
            Source = TicketSource.Portal,
        });
        ((int)resp.StatusCode).Should().Be(400);
    }

    [Fact]
    public async Task List_Returns_Only_Active_Tenant_Records()
    {
        var a = ClientForTenant("tenant-A");
        var b = ClientForTenant("tenant-B");
        await a.PostAsJsonAsync("/support/api/tickets",
            new CreateTicketRequest { Title = "A1", Priority = TicketPriority.Normal, Source = TicketSource.Portal });
        await b.PostAsJsonAsync("/support/api/tickets",
            new CreateTicketRequest { Title = "B1", Priority = TicketPriority.Normal, Source = TicketSource.Portal });

        var listA = await a.GetFromJsonAsync<PagedResponse<TicketResponse>>("/support/api/tickets");
        listA!.Items.Should().OnlyContain(t => t.TenantId == "tenant-A");
    }

    [Fact]
    public async Task Detail_Returns_404_For_Wrong_Tenant()
    {
        var a = ClientForTenant("tenant-A");
        var b = ClientForTenant("tenant-B");
        var created = await (await a.PostAsJsonAsync("/support/api/tickets",
                new CreateTicketRequest { Title = "secret", Priority = TicketPriority.Normal, Source = TicketSource.Portal }))
            .Content.ReadFromJsonAsync<TicketResponse>();

        var resp = await b.GetAsync($"/support/api/tickets/{created!.Id}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_Valid_Status_Transition_Succeeds()
    {
        var a = ClientForTenant("tenant-A");
        var created = await (await a.PostAsJsonAsync("/support/api/tickets",
                new CreateTicketRequest { Title = "t", Priority = TicketPriority.Normal, Source = TicketSource.Portal }))
            .Content.ReadFromJsonAsync<TicketResponse>();

        var resp = await a.PutAsJsonAsync($"/support/api/tickets/{created!.Id}",
            new UpdateTicketRequest { Status = TicketStatus.InProgress });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<TicketResponse>();
        body!.Status.Should().Be(TicketStatus.InProgress);
    }

    [Fact]
    public async Task Update_Invalid_Transition_Returns_400()
    {
        var a = ClientForTenant("tenant-A");
        var created = await (await a.PostAsJsonAsync("/support/api/tickets",
                new CreateTicketRequest { Title = "t", Priority = TicketPriority.Normal, Source = TicketSource.Portal }))
            .Content.ReadFromJsonAsync<TicketResponse>();

        // Open -> Resolved is not allowed (must go through InProgress)
        var resp = await a.PutAsJsonAsync($"/support/api/tickets/{created!.Id}",
            new UpdateTicketRequest { Status = TicketStatus.Resolved });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Ticket_Number_Is_Generated()
    {
        var a = ClientForTenant("tenant-NUM");
        var year = DateTime.UtcNow.Year;
        var created = await (await a.PostAsJsonAsync("/support/api/tickets",
                new CreateTicketRequest { Title = "n", Priority = TicketPriority.Normal, Source = TicketSource.Portal }))
            .Content.ReadFromJsonAsync<TicketResponse>();
        created!.TicketNumber.Should().StartWith($"SUP-{year}-");
        created.TicketNumber.Should().MatchRegex(@"^SUP-\d{4}-\d{6}$");
    }

    [Fact]
    public async Task Requester_Email_Validation_Works()
    {
        var a = ClientForTenant("tenant-A");
        var resp = await a.PostAsJsonAsync("/support/api/tickets", new CreateTicketRequest
        {
            Title = "x",
            Priority = TicketPriority.Normal,
            Source = TicketSource.Portal,
            RequesterEmail = "not-an-email",
        });
        ((int)resp.StatusCode).Should().Be(400);
    }

    [Fact]
    public async Task Health_Endpoint_Returns_Healthy()
    {
        var c = _factory.CreateClient();
        var resp = await c.GetAsync("/support/api/health");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task List_Without_Tenant_Returns_400()
    {
        var c = _factory.CreateClient(); // no header
        var resp = await c.GetAsync("/support/api/tickets");
        ((int)resp.StatusCode).Should().Be(400);
    }

    [Fact]
    public async Task Pending_Can_Resolve_Directly()
    {
        var a = ClientForTenant("tenant-TR1");
        var created = await (await a.PostAsJsonAsync("/support/api/tickets",
                new CreateTicketRequest { Title = "p", Priority = TicketPriority.Normal, Source = TicketSource.Portal }))
            .Content.ReadFromJsonAsync<TicketResponse>();

        // Open -> InProgress -> Pending -> Resolved
        (await a.PutAsJsonAsync($"/support/api/tickets/{created!.Id}",
            new UpdateTicketRequest { Status = TicketStatus.InProgress })).EnsureSuccessStatusCode();
        (await a.PutAsJsonAsync($"/support/api/tickets/{created.Id}",
            new UpdateTicketRequest { Status = TicketStatus.Pending })).EnsureSuccessStatusCode();
        var resp = await a.PutAsJsonAsync($"/support/api/tickets/{created.Id}",
            new UpdateTicketRequest { Status = TicketStatus.Resolved });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InProgress_Can_Be_Cancelled()
    {
        var a = ClientForTenant("tenant-TR2");
        var created = await (await a.PostAsJsonAsync("/support/api/tickets",
                new CreateTicketRequest { Title = "p", Priority = TicketPriority.Normal, Source = TicketSource.Portal }))
            .Content.ReadFromJsonAsync<TicketResponse>();

        (await a.PutAsJsonAsync($"/support/api/tickets/{created!.Id}",
            new UpdateTicketRequest { Status = TicketStatus.InProgress })).EnsureSuccessStatusCode();
        var resp = await a.PutAsJsonAsync($"/support/api/tickets/{created.Id}",
            new UpdateTicketRequest { Status = TicketStatus.Cancelled });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetched = await (await a.GetAsync($"/support/api/tickets/{created.Id}"))
            .Content.ReadFromJsonAsync<TicketResponse>();
        fetched!.Status.Should().Be(TicketStatus.Cancelled);
        fetched.ClosedAt.Should().NotBeNull(); // Cancelled stamps ClosedAt
    }

    [Fact]
    public async Task Tenants_Get_Independent_Number_Sequences()
    {
        var a = ClientForTenant("tenant-SEQ-A");
        var b = ClientForTenant("tenant-SEQ-B");
        var year = DateTime.UtcNow.Year;

        var aFirst = await (await a.PostAsJsonAsync("/support/api/tickets",
            new CreateTicketRequest { Title = "x", Priority = TicketPriority.Normal, Source = TicketSource.Portal }))
            .Content.ReadFromJsonAsync<TicketResponse>();
        var aSecond = await (await a.PostAsJsonAsync("/support/api/tickets",
            new CreateTicketRequest { Title = "y", Priority = TicketPriority.Normal, Source = TicketSource.Portal }))
            .Content.ReadFromJsonAsync<TicketResponse>();
        var bFirst = await (await b.PostAsJsonAsync("/support/api/tickets",
            new CreateTicketRequest { Title = "z", Priority = TicketPriority.Normal, Source = TicketSource.Portal }))
            .Content.ReadFromJsonAsync<TicketResponse>();

        aFirst!.TicketNumber.Should().Be($"SUP-{year}-000001");
        aSecond!.TicketNumber.Should().Be($"SUP-{year}-000002");
        bFirst!.TicketNumber.Should().Be($"SUP-{year}-000001"); // independent sequence
    }
}
