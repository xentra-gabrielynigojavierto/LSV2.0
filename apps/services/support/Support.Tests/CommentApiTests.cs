using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Support.Api.Domain;
using Support.Api.Dtos;

namespace Support.Tests;

public class CommentApiTests : IClassFixture<SupportApiFactory>
{
    private readonly SupportApiFactory _factory;

    public CommentApiTests(SupportApiFactory factory) => _factory = factory;

    private HttpClient ClientForTenant(string tenantId)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        return c;
    }

    private async Task<TicketResponse> CreateTicketAsync(HttpClient c, string title = "T")
    {
        var resp = await c.PostAsJsonAsync("/support/api/tickets", new CreateTicketRequest
        {
            Title = title,
            Priority = TicketPriority.Normal,
            Source = TicketSource.Portal,
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<TicketResponse>())!;
    }

    [Fact]
    public async Task Add_Comment_Succeeds()
    {
        var a = ClientForTenant("tenant-CMT-A");
        var t = await CreateTicketAsync(a);

        var resp = await a.PostAsJsonAsync($"/support/api/tickets/{t.Id}/comments", new CreateCommentRequest
        {
            Body = "Looking into this now.",
            CommentType = CommentType.InternalNote,
            Visibility = CommentVisibility.Internal,
            AuthorName = "Agent A",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await resp.Content.ReadFromJsonAsync<CommentResponse>();
        body!.Body.Should().Be("Looking into this now.");
        body.CommentType.Should().Be(CommentType.InternalNote);
        body.Visibility.Should().Be(CommentVisibility.Internal);
        body.TicketId.Should().Be(t.Id);
    }

    [Fact]
    public async Task Add_Comment_Fails_If_Ticket_Not_Found()
    {
        var a = ClientForTenant("tenant-CMT-B");
        var resp = await a.PostAsJsonAsync($"/support/api/tickets/{Guid.NewGuid()}/comments",
            new CreateCommentRequest { Body = "x" });
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Add_Comment_Fails_For_Wrong_Tenant()
    {
        var a = ClientForTenant("tenant-CMT-C1");
        var b = ClientForTenant("tenant-CMT-C2");
        var t = await CreateTicketAsync(a);

        var resp = await b.PostAsJsonAsync($"/support/api/tickets/{t.Id}/comments",
            new CreateCommentRequest { Body = "from another tenant" });
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Add_Comment_Fails_For_Empty_Body()
    {
        var a = ClientForTenant("tenant-CMT-D");
        var t = await CreateTicketAsync(a);
        var resp = await a.PostAsJsonAsync($"/support/api/tickets/{t.Id}/comments",
            new CreateCommentRequest { Body = "" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_Comments_Ordered_Asc_And_Tenant_Scoped()
    {
        var a = ClientForTenant("tenant-CMT-E");
        var t = await CreateTicketAsync(a);

        await a.PostAsJsonAsync($"/support/api/tickets/{t.Id}/comments", new CreateCommentRequest { Body = "first" });
        await Task.Delay(15);
        await a.PostAsJsonAsync($"/support/api/tickets/{t.Id}/comments", new CreateCommentRequest { Body = "second" });

        var list = await (await a.GetAsync($"/support/api/tickets/{t.Id}/comments"))
            .Content.ReadFromJsonAsync<List<CommentResponse>>();
        list.Should().HaveCount(2);
        list![0].Body.Should().Be("first");
        list[1].Body.Should().Be("second");
    }

    [Fact]
    public async Task Timeline_Returns_Events_And_Comments_In_Order()
    {
        var a = ClientForTenant("tenant-CMT-F");
        var t = await CreateTicketAsync(a);
        await Task.Delay(10);
        await a.PutAsJsonAsync($"/support/api/tickets/{t.Id}",
            new UpdateTicketRequest { Status = TicketStatus.InProgress });
        await Task.Delay(10);
        await a.PostAsJsonAsync($"/support/api/tickets/{t.Id}/comments",
            new CreateCommentRequest { Body = "investigating" });

        var timeline = await (await a.GetAsync($"/support/api/tickets/{t.Id}/timeline"))
            .Content.ReadFromJsonAsync<List<TimelineItem>>();

        timeline.Should().NotBeNull();
        timeline!.Should().BeInAscendingOrder(i => i.CreatedAt);
        timeline.Select(i => i.EventType).Should().Contain("created");
        timeline.Select(i => i.EventType).Should().Contain("status_changed");
        timeline.Select(i => i.EventType).Should().Contain("updated");
        timeline.Select(i => i.EventType).Should().Contain("comment_added");
        timeline.Where(i => i.Type == "comment").Select(i => i.Body).Should().Contain("investigating");
    }

    [Fact]
    public async Task Ticket_Creation_Logs_Created_Event()
    {
        var a = ClientForTenant("tenant-CMT-G");
        var t = await CreateTicketAsync(a);
        var timeline = await (await a.GetAsync($"/support/api/tickets/{t.Id}/timeline"))
            .Content.ReadFromJsonAsync<List<TimelineItem>>();
        timeline!.Single(i => i.Type == "event" && i.EventType == "created").Summary.Should().Be("Ticket created");
    }

    [Fact]
    public async Task Ticket_Update_Logs_Updated_Event()
    {
        var a = ClientForTenant("tenant-CMT-H");
        var t = await CreateTicketAsync(a);
        await a.PutAsJsonAsync($"/support/api/tickets/{t.Id}",
            new UpdateTicketRequest { Title = "renamed" });
        var timeline = await (await a.GetAsync($"/support/api/tickets/{t.Id}/timeline"))
            .Content.ReadFromJsonAsync<List<TimelineItem>>();
        timeline!.Should().Contain(i => i.EventType == "updated");
    }

    [Fact]
    public async Task List_Comments_Cross_Tenant_Returns_404()
    {
        var a = ClientForTenant("tenant-CMT-J1");
        var b = ClientForTenant("tenant-CMT-J2");
        var t = await CreateTicketAsync(a);
        var resp = await b.GetAsync($"/support/api/tickets/{t.Id}/comments");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Timeline_Cross_Tenant_Returns_404()
    {
        var a = ClientForTenant("tenant-CMT-K1");
        var b = ClientForTenant("tenant-CMT-K2");
        var t = await CreateTicketAsync(a);
        var resp = await b.GetAsync($"/support/api/tickets/{t.Id}/timeline");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Timeline_Without_Tenant_Returns_400()
    {
        var a = ClientForTenant("tenant-CMT-L");
        var t = await CreateTicketAsync(a);
        var c = _factory.CreateClient(); // no header
        var resp = await c.GetAsync($"/support/api/tickets/{t.Id}/timeline");
        ((int)resp.StatusCode).Should().Be(400);
    }

    [Fact]
    public async Task Status_Change_Logs_Event_With_Metadata()
    {
        var a = ClientForTenant("tenant-CMT-I");
        var t = await CreateTicketAsync(a);
        await a.PutAsJsonAsync($"/support/api/tickets/{t.Id}",
            new UpdateTicketRequest { Status = TicketStatus.InProgress });
        var timeline = await (await a.GetAsync($"/support/api/tickets/{t.Id}/timeline"))
            .Content.ReadFromJsonAsync<List<TimelineItem>>();
        var ev = timeline!.Single(i => i.EventType == "status_changed");
        ev.Summary.Should().Be("Status changed from Open to InProgress");
        ev.MetadataJson.Should().NotBeNull();
        ev.MetadataJson!.Should().Contain("\"from\":\"Open\"").And.Contain("\"to\":\"InProgress\"");
    }
}
