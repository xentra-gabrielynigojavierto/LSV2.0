using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using FluentAssertions;
using Support.Api.Audit;
using Support.Api.Auth;
using Support.Api.Domain;
using Support.Api.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Support.Tests;

public class AuditTests : IClassFixture<AuditApiFactory>
{
    private readonly AuditApiFactory _factory;

    public AuditTests(AuditApiFactory factory) => _factory = factory;

    private HttpClient ClientFor(string tenantId, string? sub = null, string? roles = null)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        if (!string.IsNullOrEmpty(sub)) c.DefaultRequestHeaders.Add("X-Test-Sub", sub);
        if (!string.IsNullOrEmpty(roles)) c.DefaultRequestHeaders.Add("X-Test-Roles", roles);
        return c;
    }

    private static async Task<TicketResponse> CreateTicket(HttpClient c,
        string title = "audit-t",
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
    public async Task Ticket_Created_Emits_Audit_Event()
    {
        _factory.Recorder.Clear();
        var c = ClientFor("tenant-A1", sub: "actor-1");
        var t = await CreateTicket(c, "audit hello");

        var events = _factory.Recorder.ForResource(t.Id.ToString());
        var created = events.Single(e => e.EventType == SupportAuditEventTypes.TicketCreated);

        created.TenantId.Should().Be("tenant-A1");
        created.ActorUserId.Should().Be("actor-1");
        created.ResourceType.Should().Be(SupportAuditResourceTypes.SupportTicket);
        created.ResourceId.Should().Be(t.Id.ToString());
        created.ResourceNumber.Should().Be(t.TicketNumber);
        created.Action.Should().Be(SupportAuditActions.Create);
        created.Outcome.Should().Be(SupportAuditOutcomes.Success);
        created.Metadata["title"].Should().Be("audit hello");
        created.Metadata["status"].Should().Be(TicketStatus.Open.ToString());
        created.ActorRoles.Should().NotBeEmpty();
    }

    // 2
    [Fact]
    public async Task Ticket_Updated_Emits_Audit_Event()
    {
        _factory.Recorder.Clear();
        var c = ClientFor("tenant-A2");
        var t = await CreateTicket(c, "to update");

        var resp = await c.PutAsJsonAsync($"/support/api/tickets/{t.Id}",
            new UpdateTicketRequest { Title = "renamed", Category = "billing" });
        resp.EnsureSuccessStatusCode();

        var events = _factory.Recorder.ForResource(t.Id.ToString());
        var updated = events.Last(e => e.EventType == SupportAuditEventTypes.TicketUpdated);
        updated.Action.Should().Be(SupportAuditActions.Update);
        updated.Metadata["title_changed"].Should().Be(true);
        updated.Metadata["category_changed"].Should().Be(true);
        updated.Metadata["due_at_changed"].Should().Be(false);
    }

    // 3
    [Fact]
    public async Task Ticket_Status_Change_Emits_Status_And_Update_Audit_Events()
    {
        _factory.Recorder.Clear();
        var c = ClientFor("tenant-A3");
        var t = await CreateTicket(c, "status flip");

        var resp = await c.PutAsJsonAsync($"/support/api/tickets/{t.Id}",
            new UpdateTicketRequest { Status = TicketStatus.InProgress });
        resp.EnsureSuccessStatusCode();

        var events = _factory.Recorder.ForResource(t.Id.ToString());
        var status = events.Single(e => e.EventType == SupportAuditEventTypes.TicketStatusChanged);
        status.Action.Should().Be(SupportAuditActions.StatusChange);
        status.Metadata["previous_status"].Should().Be(TicketStatus.Open.ToString());
        status.Metadata["new_status"].Should().Be(TicketStatus.InProgress.ToString());

        events.Should().Contain(e => e.EventType == SupportAuditEventTypes.TicketUpdated);
    }

    // 4
    [Fact]
    public async Task Ticket_Assignment_Emits_Audit_Event()
    {
        _factory.Recorder.Clear();
        var c = ClientFor("tenant-A4");
        var t = await CreateTicket(c, "assign me");

        var resp = await c.PutAsJsonAsync($"/support/api/tickets/{t.Id}/assignment",
            new AssignTicketRequest { AssignedUserId = "agent-007" });
        resp.EnsureSuccessStatusCode();

        var events = _factory.Recorder.ForResource(t.Id.ToString());
        var a = events.Single(e => e.EventType == SupportAuditEventTypes.TicketAssignmentChanged);
        a.Action.Should().Be(SupportAuditActions.Assign);
        a.Metadata["assigned_user_id"].Should().Be("agent-007");
        a.Metadata["previous_assigned_user_id"].Should().BeNull();
        a.Metadata["cleared"].Should().Be(false);
    }

    // 5
    [Fact]
    public async Task Comment_Added_Emits_Audit_Event_Without_Body()
    {
        _factory.Recorder.Clear();
        var c = ClientFor("tenant-A5");
        var t = await CreateTicket(c, "talk");

        const string secretBody = "PII: ssn 123-45-6789";
        var resp = await c.PostAsJsonAsync($"/support/api/tickets/{t.Id}/comments",
            new CreateCommentRequest
            {
                Body = secretBody,
                CommentType = CommentType.SystemNote,
                Visibility = CommentVisibility.CustomerVisible,
            });
        resp.EnsureSuccessStatusCode();

        var events = _factory.Recorder.ForResource(t.Id.ToString());
        var ca = events.Single(e => e.EventType == SupportAuditEventTypes.TicketCommentAdded);
        ca.Action.Should().Be(SupportAuditActions.CommentAdd);
        ca.Metadata["visibility"].Should().Be(CommentVisibility.CustomerVisible.ToString());
        ca.Metadata["comment_type"].Should().Be(CommentType.SystemNote.ToString());
        ca.Metadata["body_length"].Should().Be(secretBody.Length);
        ca.Metadata.Should().NotContainKey("body");
        // Body content must not appear anywhere in the metadata payload.
        ca.Metadata.Values.OfType<string>().Should().NotContain(v => v.Contains("ssn"));
    }

    // 6
    [Fact]
    public async Task Attachment_Added_Emits_Audit_Event_Without_Contents()
    {
        _factory.Recorder.Clear();
        var c = ClientFor("tenant-A6");
        var t = await CreateTicket(c, "attach");

        var resp = await c.PostAsJsonAsync($"/support/api/tickets/{t.Id}/attachments",
            new CreateTicketAttachmentRequest
            {
                DocumentId = "doc_abc",
                FileName = "contract.pdf",
                ContentType = "application/pdf",
                FileSizeBytes = 12345,
            });
        resp.EnsureSuccessStatusCode();

        var events = _factory.Recorder.ForResource(t.Id.ToString());
        var att = events.Single(e => e.EventType == SupportAuditEventTypes.TicketAttachmentAdded);
        att.Action.Should().Be(SupportAuditActions.AttachmentLink);
        att.Metadata["document_id"].Should().Be("doc_abc");
        att.Metadata["file_name"].Should().Be("contract.pdf");
        att.Metadata["content_type"].Should().Be("application/pdf");
        att.Metadata["file_size_bytes"].Should().Be(12345L);
        att.Metadata.Should().NotContainKey("content");
        att.Metadata.Should().NotContainKey("bytes");
    }

    // 7
    [Fact]
    public async Task Product_Reference_Linked_And_Removed_Emit_Audit_Events()
    {
        _factory.Recorder.Clear();
        var c = ClientFor("tenant-A7");
        var t = await CreateTicket(c, "refs");

        var createReq = new CreateProductReferenceRequest
        {
            ProductCode = "DOCS",
            EntityType = "matter",
            EntityId = "matter-99",
            DisplayLabel = "Matter #99",
        };
        var createResp = await c.PostAsJsonAsync($"/support/api/tickets/{t.Id}/product-refs", createReq);
        createResp.EnsureSuccessStatusCode();
        var created = (await createResp.Content.ReadFromJsonAsync<ProductReferenceResponse>())!;

        var del = await c.DeleteAsync($"/support/api/tickets/{t.Id}/product-refs/{created.Id}");
        del.EnsureSuccessStatusCode();

        var events = _factory.Recorder.ForResource(t.Id.ToString());
        var linked = events.Single(e => e.EventType == SupportAuditEventTypes.TicketProductRefLinked);
        linked.Action.Should().Be(SupportAuditActions.ProductRefLink);
        linked.Metadata["product_code"].Should().Be("DOCS");
        linked.Metadata["entity_type"].Should().Be("matter");
        linked.Metadata["entity_id"].Should().Be("matter-99");
        linked.Metadata["display_label"].Should().Be("Matter #99");

        var removed = events.Single(e => e.EventType == SupportAuditEventTypes.TicketProductRefRemoved);
        removed.Action.Should().Be(SupportAuditActions.ProductRefRemove);
        removed.Metadata["product_code"].Should().Be("DOCS");
        removed.Metadata["entity_id"].Should().Be("matter-99");
    }

    // 8
    [Fact]
    public async Task Queue_Created_Emits_Audit_Event()
    {
        _factory.Recorder.Clear();
        var c = ClientFor("tenant-A8");
        var q = await CreateQueue(c, "Audit Queue 8");

        var events = _factory.Recorder.ForResource(q.Id.ToString());
        var ev = events.Single(e => e.EventType == SupportAuditEventTypes.QueueCreated);
        ev.ResourceType.Should().Be(SupportAuditResourceTypes.SupportQueue);
        ev.Action.Should().Be(SupportAuditActions.Create);
        ev.Metadata["name"].Should().Be("Audit Queue 8");
        ev.Metadata["is_active"].Should().Be(true);
    }

    // 9
    [Fact]
    public async Task Queue_Updated_Emits_Audit_Event()
    {
        _factory.Recorder.Clear();
        var c = ClientFor("tenant-A9");
        var q = await CreateQueue(c, "old name");

        var resp = await c.PutAsJsonAsync($"/support/api/queues/{q.Id}",
            new UpdateQueueRequest { Name = "new name" });
        resp.EnsureSuccessStatusCode();

        var events = _factory.Recorder.ForResource(q.Id.ToString());
        var ev = events.Single(e => e.EventType == SupportAuditEventTypes.QueueUpdated);
        ev.Action.Should().Be(SupportAuditActions.Update);
        ev.Metadata["name"].Should().Be("new name");
    }

    // 10
    [Fact]
    public async Task Queue_Member_Added_And_Removed_Emit_Audit_Events()
    {
        _factory.Recorder.Clear();
        var c = ClientFor("tenant-A10");
        var q = await CreateQueue(c, "members");

        var addResp = await c.PostAsJsonAsync($"/support/api/queues/{q.Id}/members",
            new AddQueueMemberRequest { UserId = "agent-1", Role = QueueMemberRole.Lead });
        addResp.EnsureSuccessStatusCode();
        var member = (await addResp.Content.ReadFromJsonAsync<QueueMemberResponse>())!;

        var del = await c.DeleteAsync($"/support/api/queues/{q.Id}/members/{member.Id}");
        del.EnsureSuccessStatusCode();

        var events = _factory.Recorder.ForResource(member.Id.ToString());
        var added = events.Single(e => e.EventType == SupportAuditEventTypes.QueueMemberAdded);
        added.ResourceType.Should().Be(SupportAuditResourceTypes.SupportQueueMember);
        added.Action.Should().Be(SupportAuditActions.MemberAdd);
        added.Metadata["user_id"].Should().Be("agent-1");
        added.Metadata["role"].Should().Be(QueueMemberRole.Lead.ToString());

        var removed = events.Single(e => e.EventType == SupportAuditEventTypes.QueueMemberRemoved);
        removed.Action.Should().Be(SupportAuditActions.MemberRemove);
        removed.Metadata["user_id"].Should().Be("agent-1");
        removed.Metadata["is_active"].Should().Be(false);
    }

    // 11 — failure isolation: throwing publisher must not break the write
    [Fact]
    public async Task Audit_Failure_Does_Not_Break_Ticket_Create()
    {
        _factory.Recorder.Clear();
        var c = ClientFor("tenant-A11");
        try
        {
            _factory.Recorder.ThrowOnPublish = true;
            var resp = await c.PostAsJsonAsync("/support/api/tickets", new CreateTicketRequest
            {
                Title = "robust",
                Priority = TicketPriority.Normal,
                Source = TicketSource.Portal,
            });
            resp.StatusCode.Should().Be(HttpStatusCode.Created);
            var t = (await resp.Content.ReadFromJsonAsync<TicketResponse>())!;
            t.Title.Should().Be("robust");
        }
        finally
        {
            _factory.Recorder.ThrowOnPublish = false;
        }
    }

    // 12 — failure isolation across all 5 services for comment add
    [Fact]
    public async Task Audit_Failure_Does_Not_Break_Comment_Add()
    {
        _factory.Recorder.Clear();
        var c = ClientFor("tenant-A12");
        var t = await CreateTicket(c, "comment robust");

        try
        {
            _factory.Recorder.ThrowOnPublish = true;
            var resp = await c.PostAsJsonAsync($"/support/api/tickets/{t.Id}/comments",
                new CreateCommentRequest
                {
                    Body = "still works",
                    CommentType = CommentType.SystemNote,
                    Visibility = CommentVisibility.Internal,
                });
            resp.EnsureSuccessStatusCode();
        }
        finally
        {
            _factory.Recorder.ThrowOnPublish = false;
        }
    }

    // 13 — actor + request enrichment from JWT/headers
    [Fact]
    public async Task Audit_Event_Includes_Actor_Roles_And_Correlation_Id()
    {
        _factory.Recorder.Clear();
        var c = ClientFor("tenant-A13", sub: "actor-13", roles: "SupportAgent,SupportAdmin");
        c.DefaultRequestHeaders.Add("X-Correlation-Id", "corr-xyz-123");
        c.DefaultRequestHeaders.Add("User-Agent", "AuditTestAgent/1.0");

        var t = await CreateTicket(c, "enriched");
        var ev = _factory.Recorder.OfType(SupportAuditEventTypes.TicketCreated)
            .Single(e => e.ResourceId == t.Id.ToString());

        ev.ActorUserId.Should().Be("actor-13");
        ev.ActorRoles.Should().Contain("SupportAgent");
        ev.ActorRoles.Should().Contain("SupportAdmin");
        ev.CorrelationId.Should().Be("corr-xyz-123");
        ev.UserAgent.Should().Be("AuditTestAgent/1.0");
        ev.OccurredAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    // 14 — NoOp publisher honors Enabled flag (no throw either way)
    [Fact]
    public async Task NoOp_Publisher_Honors_Disabled_Flag()
    {
        var disabledOpts = new AuditOptions { Enabled = false, Mode = AuditDispatchMode.NoOp };
        var disabledMonitor = new StaticOptionsMonitor<AuditOptions>(disabledOpts);
        var disabled = new NoOpAuditPublisher(NullLogger<NoOpAuditPublisher>.Instance, disabledMonitor);

        var enabledOpts = new AuditOptions { Enabled = true, Mode = AuditDispatchMode.NoOp };
        var enabledMonitor = new StaticOptionsMonitor<AuditOptions>(enabledOpts);
        var enabled = new NoOpAuditPublisher(NullLogger<NoOpAuditPublisher>.Instance, enabledMonitor);

        var evt = new SupportAuditEvent(
            EventType: SupportAuditEventTypes.TicketCreated,
            TenantId: "tenant-A14",
            ActorUserId: "actor-x",
            ActorEmail: null,
            ActorRoles: Array.Empty<string>(),
            ResourceType: SupportAuditResourceTypes.SupportTicket,
            ResourceId: Guid.NewGuid().ToString(),
            ResourceNumber: "SUP-X-1",
            Action: SupportAuditActions.Create,
            Outcome: SupportAuditOutcomes.Success,
            OccurredAt: DateTime.UtcNow,
            CorrelationId: null,
            IpAddress: null,
            UserAgent: null,
            Metadata: new Dictionary<string, object?>());

        // Both must be no-throw, regardless of Enabled flag.
        await disabled.PublishAsync(evt);
        await enabled.PublishAsync(evt);
    }

    // 15 — actor accessor reads JWT `name` and plural `roles` claims
    [Fact]
    public void Actor_Accessor_Reads_Name_And_Plural_Roles_Claims()
    {
        // `roles` arrives as a single claim with comma/space separated values
        // (Auth0-style); `role` arrives as one-per-claim (Microsoft-style).
        // Both must be ingested and de-duplicated.
        var claims = new[]
        {
            new Claim("sub", "user-actor-15"),
            new Claim("name", "Ada Lovelace"),
            new Claim("email", "ada@example.com"),
            new Claim("role", "SupportAgent"),
            new Claim("roles", "SupportAdmin,SupportManager SupportAgent"),
        };
        var identity = new ClaimsIdentity(claims, authenticationType: "test");
        var principal = new ClaimsPrincipal(identity);
        var ctx = new DefaultHttpContext { User = principal };

        var accessor = new HttpContextActorAccessor(
            new FixedHttpContextAccessor(ctx));

        var actor = accessor.Actor;
        actor.UserId.Should().Be("user-actor-15");
        actor.Name.Should().Be("Ada Lovelace");
        actor.Email.Should().Be("ada@example.com");
        actor.Roles.Should().Contain("SupportAgent");
        actor.Roles.Should().Contain("SupportAdmin");
        actor.Roles.Should().Contain("SupportManager");
        // Case-insensitive de-dup keeps the first occurrence of "SupportAgent".
        actor.Roles.Count(r => string.Equals(r, "SupportAgent", StringComparison.OrdinalIgnoreCase))
            .Should().Be(1);
    }

    // 16 — anonymous principal yields empty actor (no exceptions, no leakage)
    [Fact]
    public void Actor_Accessor_Returns_Empty_For_Unauthenticated_Principal()
    {
        var ctx = new DefaultHttpContext(); // anonymous
        var accessor = new HttpContextActorAccessor(
            new FixedHttpContextAccessor(ctx));

        var actor = accessor.Actor;
        actor.UserId.Should().BeNull();
        actor.Name.Should().BeNull();
        actor.Email.Should().BeNull();
        actor.Roles.Should().BeEmpty();
    }

    private sealed class FixedHttpContextAccessor : IHttpContextAccessor
    {
        public FixedHttpContextAccessor(HttpContext ctx) { HttpContext = ctx; }
        public HttpContext? HttpContext { get; set; }
    }

    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public StaticOptionsMonitor(T value) { CurrentValue = value; }
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
