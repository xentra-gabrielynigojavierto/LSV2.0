using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Support.Api.Auth;
using Support.Api.Domain;
using Support.Api.Dtos;

namespace Support.Tests;

public class QueueAndAssignmentTests : IClassFixture<SupportApiFactory>
{
    private readonly SupportApiFactory _factory;

    public QueueAndAssignmentTests(SupportApiFactory factory) => _factory = factory;

    private HttpClient ClientFor(string tenantId, string? roles = null)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        if (!string.IsNullOrEmpty(roles))
            c.DefaultRequestHeaders.Add("X-Test-Roles", roles);
        return c;
    }

    private static async Task<TicketResponse> CreateTicket(HttpClient c, string title = "t")
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

    private static async Task<QueueResponse> CreateQueue(HttpClient c, string name)
    {
        var r = await c.PostAsJsonAsync("/support/api/queues", new CreateQueueRequest { Name = name });
        r.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await r.Content.ReadFromJsonAsync<QueueResponse>())!;
    }

    // 1
    [Fact]
    public async Task Create_Queue_Succeeds()
    {
        var c = ClientFor("tenant-Q1");
        var resp = await c.PostAsJsonAsync("/support/api/queues",
            new CreateQueueRequest { Name = "Tier 1", Description = "first line", ProductCode = "docs" });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var q = await resp.Content.ReadFromJsonAsync<QueueResponse>();
        q!.Name.Should().Be("Tier 1");
        q.ProductCode.Should().Be("DOCS"); // normalized uppercase
        q.IsActive.Should().BeTrue();
    }

    // 2
    [Fact]
    public async Task Create_Queue_Duplicate_Name_Returns_409()
    {
        var c = ClientFor("tenant-Q2");
        await CreateQueue(c, "DupName");
        var dup = await c.PostAsJsonAsync("/support/api/queues", new CreateQueueRequest { Name = "DupName" });
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // 3
    [Fact]
    public async Task List_Queues_Tenant_Scoped()
    {
        var a = ClientFor("tenant-Q3A");
        var b = ClientFor("tenant-Q3B");
        await CreateQueue(a, "QueueA");
        await CreateQueue(b, "QueueB");

        var listA = await a.GetFromJsonAsync<List<QueueResponse>>("/support/api/queues");
        listA!.Should().OnlyContain(q => q.TenantId == "tenant-Q3A");
        listA.Should().Contain(q => q.Name == "QueueA");
        listA.Should().NotContain(q => q.Name == "QueueB");
    }

    // 4
    [Fact]
    public async Task Update_Queue_Succeeds()
    {
        var c = ClientFor("tenant-Q4");
        var q = await CreateQueue(c, "Original");
        var resp = await c.PutAsJsonAsync($"/support/api/queues/{q.Id}",
            new UpdateQueueRequest { Name = "Renamed", IsActive = false });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var u = await resp.Content.ReadFromJsonAsync<QueueResponse>();
        u!.Name.Should().Be("Renamed");
        u.IsActive.Should().BeFalse();
    }

    // 5
    [Fact]
    public async Task Add_Queue_Member_Succeeds()
    {
        var c = ClientFor("tenant-Q5");
        var q = await CreateQueue(c, "Members");
        var resp = await c.PostAsJsonAsync($"/support/api/queues/{q.Id}/members",
            new AddQueueMemberRequest { UserId = "user-1", Role = QueueMemberRole.Agent });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var m = await resp.Content.ReadFromJsonAsync<QueueMemberResponse>();
        m!.UserId.Should().Be("user-1");
        m.Role.Should().Be(QueueMemberRole.Agent);
    }

    // 6
    [Fact]
    public async Task Duplicate_Queue_Member_Returns_409()
    {
        var c = ClientFor("tenant-Q6");
        var q = await CreateQueue(c, "DupMembers");
        await c.PostAsJsonAsync($"/support/api/queues/{q.Id}/members",
            new AddQueueMemberRequest { UserId = "user-1", Role = QueueMemberRole.Agent });
        var dup = await c.PostAsJsonAsync($"/support/api/queues/{q.Id}/members",
            new AddQueueMemberRequest { UserId = "user-1", Role = QueueMemberRole.Agent });
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // 7
    [Fact]
    public async Task Remove_Queue_Member_Deactivates()
    {
        var c = ClientFor("tenant-Q7");
        var q = await CreateQueue(c, "Removable");
        var added = await (await c.PostAsJsonAsync($"/support/api/queues/{q.Id}/members",
                new AddQueueMemberRequest { UserId = "user-1", Role = QueueMemberRole.Agent }))
            .Content.ReadFromJsonAsync<QueueMemberResponse>();

        var del = await c.DeleteAsync($"/support/api/queues/{q.Id}/members/{added!.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var members = await c.GetFromJsonAsync<List<QueueMemberResponse>>($"/support/api/queues/{q.Id}/members");
        members!.Single(m => m.Id == added.Id).IsActive.Should().BeFalse();
    }

    // 8
    [Fact]
    public async Task Assign_Ticket_To_Queue_Succeeds()
    {
        var c = ClientFor("tenant-Q8");
        var t = await CreateTicket(c);
        var q = await CreateQueue(c, "AssignTarget");

        var resp = await c.PutAsJsonAsync($"/support/api/tickets/{t.Id}/assignment",
            new AssignTicketRequest { AssignedQueueId = q.Id });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await resp.Content.ReadFromJsonAsync<TicketResponse>();
        updated!.AssignedQueueId.Should().Be(q.Id.ToString());
    }

    // 9
    [Fact]
    public async Task Assign_Ticket_To_Missing_Queue_Returns_404()
    {
        var c = ClientFor("tenant-Q9");
        var t = await CreateTicket(c);
        var resp = await c.PutAsJsonAsync($"/support/api/tickets/{t.Id}/assignment",
            new AssignTicketRequest { AssignedQueueId = Guid.NewGuid() });
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Assign_Ticket_To_Inactive_Queue_Returns_400()
    {
        var c = ClientFor("tenant-Q9b");
        var t = await CreateTicket(c);
        var q = await CreateQueue(c, "InactiveQ");
        await c.PutAsJsonAsync($"/support/api/queues/{q.Id}",
            new UpdateQueueRequest { IsActive = false });

        var resp = await c.PutAsJsonAsync($"/support/api/tickets/{t.Id}/assignment",
            new AssignTicketRequest { AssignedQueueId = q.Id });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // 10
    [Fact]
    public async Task Assign_Ticket_To_User_Succeeds()
    {
        var c = ClientFor("tenant-Q10");
        var t = await CreateTicket(c);
        var resp = await c.PutAsJsonAsync($"/support/api/tickets/{t.Id}/assignment",
            new AssignTicketRequest { AssignedUserId = "user-42" });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await resp.Content.ReadFromJsonAsync<TicketResponse>();
        updated!.AssignedUserId.Should().Be("user-42");
    }

    // 11
    [Fact]
    public async Task Clear_Assignment_Succeeds()
    {
        var c = ClientFor("tenant-Q11");
        var t = await CreateTicket(c);
        await c.PutAsJsonAsync($"/support/api/tickets/{t.Id}/assignment",
            new AssignTicketRequest { AssignedUserId = "user-42" });

        var resp = await c.PutAsJsonAsync($"/support/api/tickets/{t.Id}/assignment",
            new AssignTicketRequest { ClearAssignment = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await resp.Content.ReadFromJsonAsync<TicketResponse>();
        updated!.AssignedUserId.Should().BeNull();
        updated.AssignedQueueId.Should().BeNull();
    }

    // 12
    [Fact]
    public async Task Assignment_Creates_Timeline_Event()
    {
        var c = ClientFor("tenant-Q12");
        var t = await CreateTicket(c);
        var q = await CreateQueue(c, "TimelineQ");

        await c.PutAsJsonAsync($"/support/api/tickets/{t.Id}/assignment",
            new AssignTicketRequest { AssignedQueueId = q.Id, AssignedUserId = "u-77" });

        var timeline = await c.GetFromJsonAsync<List<TimelineItem>>($"/support/api/tickets/{t.Id}/timeline");
        timeline!.Should().Contain(it => it.EventType == "assignment_changed");

        var ev = timeline!.First(it => it.EventType == "assignment_changed");
        ev.MetadataJson.Should().NotBeNullOrEmpty();
        using var doc = JsonDocument.Parse(ev.MetadataJson!);
        doc.RootElement.GetProperty("assigned_queue_id").GetString().Should().Be(q.Id.ToString());
        doc.RootElement.GetProperty("assigned_user_id").GetString().Should().Be("u-77");
    }

    // 13
    [Fact]
    public async Task List_Filters_By_Assigned_Queue_Id()
    {
        var c = ClientFor("tenant-Q13");
        var t1 = await CreateTicket(c, "t1");
        var t2 = await CreateTicket(c, "t2");
        var q = await CreateQueue(c, "FilterQ");
        await c.PutAsJsonAsync($"/support/api/tickets/{t1.Id}/assignment",
            new AssignTicketRequest { AssignedQueueId = q.Id });

        var page = await c.GetFromJsonAsync<PagedResponse<TicketResponse>>(
            $"/support/api/tickets?assignedQueueId={q.Id}");
        page!.Items.Should().ContainSingle(x => x.Id == t1.Id);
        page.Items.Should().NotContain(x => x.Id == t2.Id);
    }

    // 14
    [Fact]
    public async Task List_Filters_Unassigned_True()
    {
        var c = ClientFor("tenant-Q14");
        var t1 = await CreateTicket(c, "u1");
        var t2 = await CreateTicket(c, "u2");
        await c.PutAsJsonAsync($"/support/api/tickets/{t1.Id}/assignment",
            new AssignTicketRequest { AssignedUserId = "u-1" });

        var page = await c.GetFromJsonAsync<PagedResponse<TicketResponse>>(
            "/support/api/tickets?unassigned=true");
        page!.Items.Should().Contain(x => x.Id == t2.Id);
        page.Items.Should().NotContain(x => x.Id == t1.Id);
    }

    // 15
    [Fact]
    public async Task TenantUser_Cannot_Manage_Queues()
    {
        var c = ClientFor("tenant-Q15", roles: SupportRoles.TenantUser);
        var resp = await c.PostAsJsonAsync("/support/api/queues",
            new CreateQueueRequest { Name = "Forbidden" });
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // 16
    [Fact]
    public async Task TenantUser_Cannot_Assign_Ticket()
    {
        // create with default (full roles) tenant
        var owner = ClientFor("tenant-Q16");
        var t = await CreateTicket(owner);

        var user = ClientFor("tenant-Q16", roles: SupportRoles.TenantUser);
        var resp = await user.PutAsJsonAsync($"/support/api/tickets/{t.Id}/assignment",
            new AssignTicketRequest { AssignedUserId = "u-x" });
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SupportAgent_Can_Assign_Ticket()
    {
        var owner = ClientFor("tenant-Q16b");
        var t = await CreateTicket(owner);
        var agent = ClientFor("tenant-Q16b", roles: SupportRoles.SupportAgent);
        var resp = await agent.PutAsJsonAsync($"/support/api/tickets/{t.Id}/assignment",
            new AssignTicketRequest { AssignedUserId = "u-y" });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Cross_Tenant_Queue_Returns_404()
    {
        var a = ClientFor("tenant-Q17a");
        var b = ClientFor("tenant-Q17b");
        var qa = await CreateQueue(a, "OnlyForA");
        var resp = await b.GetAsync($"/support/api/queues/{qa.Id}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Assign_Validation_Requires_At_Least_One_Field()
    {
        var c = ClientFor("tenant-Q18");
        var t = await CreateTicket(c);
        var resp = await c.PutAsJsonAsync($"/support/api/tickets/{t.Id}/assignment",
            new AssignTicketRequest());
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Clear_Combined_With_Assignment_Returns_400()
    {
        var c = ClientFor("tenant-Q19");
        var t = await CreateTicket(c);
        var resp = await c.PutAsJsonAsync($"/support/api/tickets/{t.Id}/assignment",
            new AssignTicketRequest { ClearAssignment = true, AssignedUserId = "u-1" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
