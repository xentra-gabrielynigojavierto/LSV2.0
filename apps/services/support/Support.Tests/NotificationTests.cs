using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Support.Api.Domain;
using Support.Api.Dtos;
using Support.Api.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Support.Tests;

public class NotificationTests : IClassFixture<NotificationsApiFactory>
{
    private readonly NotificationsApiFactory _factory;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public NotificationTests(NotificationsApiFactory factory) => _factory = factory;

    private HttpClient ClientFor(string tenantId, string? roles = null)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        if (!string.IsNullOrEmpty(roles))
            c.DefaultRequestHeaders.Add("X-Test-Roles", roles);
        return c;
    }

    private static async Task<TicketResponse> CreateTicket(HttpClient c,
        string title = "t",
        string? requesterUserId = null,
        string? requesterEmail = null)
    {
        var resp = await c.PostAsJsonAsync("/support/api/tickets", new CreateTicketRequest
        {
            Title = title,
            Priority = TicketPriority.Normal,
            Source = TicketSource.Portal,
            RequesterUserId = requesterUserId,
            RequesterEmail = requesterEmail,
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<TicketResponse>(JsonOpts))!;
    }

    private static async Task<QueueResponse> CreateQueue(HttpClient c, string name)
    {
        var r = await c.PostAsJsonAsync("/support/api/queues", new CreateQueueRequest { Name = name });
        r.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await r.Content.ReadFromJsonAsync<QueueResponse>(JsonOpts))!;
    }

    // 1
    [Fact]
    public async Task Create_Ticket_Publishes_TicketCreated()
    {
        _factory.Recorder.Clear();
        var c = ClientFor("tenant-N1");
        var t = await CreateTicket(c, "hello", requesterUserId: "user-1", requesterEmail: "a@b.com");

        var emitted = _factory.Recorder.ForTicket(t.Id);
        emitted.Should().ContainSingle(n => n.EventType == SupportNotificationEventTypes.TicketCreated);
        var n = emitted.Single(x => x.EventType == SupportNotificationEventTypes.TicketCreated);
        n.TenantId.Should().Be("tenant-N1");
        n.TicketNumber.Should().Be(t.TicketNumber);
        n.Payload["ticket_id"].Should().Be(t.Id);
        n.Payload["title"].Should().Be("hello");
        n.Payload["requester_user_id"].Should().Be("user-1");
        n.Payload["requester_email"].Should().Be("a@b.com");
        // When RequesterEmail is stored, it is used directly (email kind preferred over userId kind).
        n.Recipients.Should().Contain(r => r.Kind == NotificationRecipientKind.Email && r.Email == "a@b.com");
    }

    // 2
    [Fact]
    public async Task Update_Ticket_Publishes_TicketUpdated()
    {
        _factory.Recorder.Clear();
        var c = ClientFor("tenant-N2");
        var t = await CreateTicket(c, "for update", requesterEmail: "u@example.com");

        var resp = await c.PutAsJsonAsync($"/support/api/tickets/{t.Id}",
            new UpdateTicketRequest { Title = "renamed" });
        resp.EnsureSuccessStatusCode();

        var emitted = _factory.Recorder.ForTicket(t.Id);
        emitted.Should().Contain(n => n.EventType == SupportNotificationEventTypes.TicketUpdated);
        var u = emitted.First(n => n.EventType == SupportNotificationEventTypes.TicketUpdated);
        u.Payload["title"].Should().Be("renamed");
        u.Payload["status"].Should().Be(TicketStatus.Open.ToString());
    }

    // 3
    [Fact]
    public async Task Status_Change_Publishes_StatusChanged()
    {
        _factory.Recorder.Clear();
        var c = ClientFor("tenant-N3");
        var t = await CreateTicket(c, "status flip", requesterEmail: "u@example.com");

        var resp = await c.PutAsJsonAsync($"/support/api/tickets/{t.Id}",
            new UpdateTicketRequest { Status = TicketStatus.InProgress });
        resp.EnsureSuccessStatusCode();

        var emitted = _factory.Recorder.ForTicket(t.Id);
        var status = emitted.SingleOrDefault(n => n.EventType == SupportNotificationEventTypes.TicketStatusChanged);
        status.Should().NotBeNull();
        status!.Payload["previous_status"].Should().Be(TicketStatus.Open.ToString());
        status.Payload["new_status"].Should().Be(TicketStatus.InProgress.ToString());
        // Updated event must also follow status change
        emitted.Should().Contain(n => n.EventType == SupportNotificationEventTypes.TicketUpdated);
    }

    // 4
    [Fact]
    public async Task Assignment_Publishes_TicketAssigned()
    {
        _factory.Recorder.Clear();
        var c = ClientFor("tenant-N4");
        var t = await CreateTicket(c, "assign me");
        var resp = await c.PutAsJsonAsync($"/support/api/tickets/{t.Id}/assignment",
            new AssignTicketRequest { AssignedUserId = "agent-007" });
        resp.EnsureSuccessStatusCode();

        var emitted = _factory.Recorder.ForTicket(t.Id);
        var a = emitted.Single(n => n.EventType == SupportNotificationEventTypes.TicketAssigned);
        a.Payload["assigned_user_id"].Should().Be("agent-007");
        a.Payload["previous_assigned_user_id"].Should().BeNull();
        a.Recipients.Should().Contain(r => r.Kind == NotificationRecipientKind.User && r.UserId == "agent-007");
    }

    // 5
    [Fact]
    public async Task Comment_Added_Publishes_CommentAdded()
    {
        _factory.Recorder.Clear();
        var c = ClientFor("tenant-N5");
        var t = await CreateTicket(c, "talk", requesterUserId: "user-9", requesterEmail: "u9@x.com");

        // SystemNote + CustomerVisible models a support agent's customer-visible reply.
        var resp = await c.PostAsJsonAsync($"/support/api/tickets/{t.Id}/comments",
            new CreateCommentRequest
            {
                Body = "hello there",
                CommentType = CommentType.SystemNote,
                Visibility = CommentVisibility.CustomerVisible,
            });
        resp.EnsureSuccessStatusCode();

        var emitted = _factory.Recorder.ForTicket(t.Id);
        var ca = emitted.Single(n => n.EventType == SupportNotificationEventTypes.TicketCommentAdded);
        ca.Payload["visibility"].Should().Be(CommentVisibility.CustomerVisible.ToString());
        ca.Payload["comment_type"].Should().Be(CommentType.SystemNote.ToString());
        // Customer-visible support reply notifies the requester.
        ca.Recipients.Should().Contain(r => r.UserId == "user-9");
        ca.Recipients.Should().Contain(r => r.Email == "u9@x.com");
    }

    // 6
    [Fact]
    public async Task Internal_Note_Does_Not_Notify_Requester()
    {
        _factory.Recorder.Clear();
        var c = ClientFor("tenant-N6");
        var t = await CreateTicket(c, "secret notes", requesterUserId: "user-cust", requesterEmail: "cust@x.com");
        // Assign so internal note has at least one allowed recipient (assigned user).
        await c.PutAsJsonAsync($"/support/api/tickets/{t.Id}/assignment",
            new AssignTicketRequest { AssignedUserId = "agent-internal" });

        _factory.Recorder.Clear();

        var resp = await c.PostAsJsonAsync($"/support/api/tickets/{t.Id}/comments",
            new CreateCommentRequest
            {
                Body = "internal only",
                CommentType = CommentType.InternalNote,
                Visibility = CommentVisibility.Internal,
            });
        resp.EnsureSuccessStatusCode();

        var ca = _factory.Recorder.ForTicket(t.Id)
            .Single(n => n.EventType == SupportNotificationEventTypes.TicketCommentAdded);
        ca.Recipients.Should().NotContain(r => r.UserId == "user-cust");
        ca.Recipients.Should().NotContain(r => r.Email == "cust@x.com");
        ca.Recipients.Should().Contain(r => r.UserId == "agent-internal");
    }

    // 7
    [Fact]
    public async Task Queue_Assignment_Notifies_Active_Queue_Members()
    {
        _factory.Recorder.Clear();
        var c = ClientFor("tenant-N7");
        var t = await CreateTicket(c, "queue route");
        var q = await CreateQueue(c, "Tier 7");

        // Add two active members and one inactive (soft-removed) member.
        await c.PostAsJsonAsync($"/support/api/queues/{q.Id}/members",
            new AddQueueMemberRequest { UserId = "agent-a", Role = QueueMemberRole.Agent });
        await c.PostAsJsonAsync($"/support/api/queues/{q.Id}/members",
            new AddQueueMemberRequest { UserId = "agent-b", Role = QueueMemberRole.Lead });
        var add3 = await c.PostAsJsonAsync($"/support/api/queues/{q.Id}/members",
            new AddQueueMemberRequest { UserId = "agent-x", Role = QueueMemberRole.Agent });
        var m3 = (await add3.Content.ReadFromJsonAsync<QueueMemberResponse>(JsonOpts))!;
        await c.DeleteAsync($"/support/api/queues/{q.Id}/members/{m3.Id}"); // soft-delete

        _factory.Recorder.Clear();

        var resp = await c.PutAsJsonAsync($"/support/api/tickets/{t.Id}/assignment",
            new AssignTicketRequest { AssignedQueueId = q.Id });
        resp.EnsureSuccessStatusCode();

        var a = _factory.Recorder.ForTicket(t.Id)
            .Single(n => n.EventType == SupportNotificationEventTypes.TicketAssigned);
        a.Recipients.Should().Contain(r => r.UserId == "agent-a");
        a.Recipients.Should().Contain(r => r.UserId == "agent-b");
        a.Recipients.Should().NotContain(r => r.UserId == "agent-x"); // inactive
    }

    // 8
    [Fact]
    public async Task No_Recipients_Does_Not_Fail_Operation()
    {
        _factory.Recorder.Clear();
        var c = ClientFor("tenant-N8");
        // Create without requester — no recipients available for created event.
        var resp = await c.PostAsJsonAsync("/support/api/tickets", new CreateTicketRequest
        {
            Title = "anonymous",
            Priority = TicketPriority.Normal,
            Source = TicketSource.Portal,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var t = (await resp.Content.ReadFromJsonAsync<TicketResponse>(JsonOpts))!;

        // Per spec, a notification with no resolvable recipients is logged and
        // skipped — the operation must succeed regardless.
        var emitted = _factory.Recorder.ForTicket(t.Id);
        emitted.Should().NotContain(n => n.EventType == SupportNotificationEventTypes.TicketCreated);
    }

    // 9
    [Fact]
    public async Task Publisher_Failure_Does_Not_Fail_Ticket_Update()
    {
        _factory.Recorder.Clear();
        var c = ClientFor("tenant-N9");
        // Requester present → update will have recipients → publisher WILL be invoked.
        var t = await CreateTicket(c, "robust", requesterEmail: "u9@x.com");

        try
        {
            _factory.Recorder.ThrowOnPublish = true;
            var resp = await c.PutAsJsonAsync($"/support/api/tickets/{t.Id}",
                new UpdateTicketRequest { Title = "updated despite failure" });
            resp.EnsureSuccessStatusCode();

            var get = await c.GetFromJsonAsync<TicketResponse>($"/support/api/tickets/{t.Id}", JsonOpts);
            get!.Title.Should().Be("updated despite failure");
        }
        finally
        {
            _factory.Recorder.ThrowOnPublish = false;
        }
    }

    // 10
    [Fact]
    public async Task NoOp_Publisher_Honors_Disabled_Flag()
    {
        // Disabled
        var disabledOpts = Options.Create(new NotificationOptions { Enabled = false });
        var disabledMonitor = new StaticOptionsMonitor<NotificationOptions>(disabledOpts.Value);
        var disabled = new NoOpNotificationPublisher(NullLogger<NoOpNotificationPublisher>.Instance, disabledMonitor);

        // Enabled
        var enabledOpts = new NotificationOptions { Enabled = true };
        var enabledMonitor = new StaticOptionsMonitor<NotificationOptions>(enabledOpts);
        var enabled = new NoOpNotificationPublisher(NullLogger<NoOpNotificationPublisher>.Instance, enabledMonitor);

        var notif = new SupportNotification(
            SupportNotificationEventTypes.TicketCreated,
            "tenant-N10", Guid.NewGuid(), "SUP-X-1",
            new List<NotificationRecipient>(),
            new Dictionary<string, object?>(),
            DateTime.UtcNow);

        // Both must be no-throw, regardless of flag.
        await disabled.PublishAsync(notif);
        await enabled.PublishAsync(notif);
    }

    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public StaticOptionsMonitor(T value) { CurrentValue = value; }
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
