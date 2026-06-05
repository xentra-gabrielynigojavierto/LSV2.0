using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Support.Api.Domain;
using Support.Api.Dtos;

namespace Support.Tests;

public class AttachmentApiTests : IClassFixture<SupportApiFactory>
{
    private readonly SupportApiFactory _factory;

    public AttachmentApiTests(SupportApiFactory factory) => _factory = factory;

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
    public async Task Add_Attachment_Succeeds()
    {
        var a = ClientForTenant("tenant-ATT-A");
        var t = await CreateTicketAsync(a);

        var resp = await a.PostAsJsonAsync($"/support/api/tickets/{t.Id}/attachments", new CreateTicketAttachmentRequest
        {
            DocumentId = "doc_abc123",
            FileName = "evidence.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 4096,
            UploadedByUserId = "user-1",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await resp.Content.ReadFromJsonAsync<TicketAttachmentResponse>();
        body!.DocumentId.Should().Be("doc_abc123");
        body.FileName.Should().Be("evidence.pdf");
        body.TicketId.Should().Be(t.Id);
        body.FileSizeBytes.Should().Be(4096);
    }

    [Fact]
    public async Task Add_Attachment_Fails_Without_DocumentId()
    {
        var a = ClientForTenant("tenant-ATT-B");
        var t = await CreateTicketAsync(a);
        var resp = await a.PostAsJsonAsync($"/support/api/tickets/{t.Id}/attachments", new CreateTicketAttachmentRequest
        {
            DocumentId = "",
            FileName = "file.pdf",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Add_Attachment_Fails_Without_FileName()
    {
        var a = ClientForTenant("tenant-ATT-C");
        var t = await CreateTicketAsync(a);
        var resp = await a.PostAsJsonAsync($"/support/api/tickets/{t.Id}/attachments", new CreateTicketAttachmentRequest
        {
            DocumentId = "doc_x",
            FileName = "",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_Attachments_Returns_Tenant_Scoped_Records()
    {
        var a = ClientForTenant("tenant-ATT-D");
        var t = await CreateTicketAsync(a);

        await a.PostAsJsonAsync($"/support/api/tickets/{t.Id}/attachments",
            new CreateTicketAttachmentRequest { DocumentId = "d1", FileName = "a.pdf" });
        await Task.Delay(15);
        await a.PostAsJsonAsync($"/support/api/tickets/{t.Id}/attachments",
            new CreateTicketAttachmentRequest { DocumentId = "d2", FileName = "b.pdf" });

        var list = await (await a.GetAsync($"/support/api/tickets/{t.Id}/attachments"))
            .Content.ReadFromJsonAsync<List<TicketAttachmentResponse>>();
        list.Should().HaveCount(2);
        list![0].DocumentId.Should().Be("d1");
        list[1].DocumentId.Should().Be("d2");
    }

    [Fact]
    public async Task Cross_Tenant_Attachment_Add_Returns_404()
    {
        var a = ClientForTenant("tenant-ATT-E1");
        var b = ClientForTenant("tenant-ATT-E2");
        var t = await CreateTicketAsync(a);

        var resp = await b.PostAsJsonAsync($"/support/api/tickets/{t.Id}/attachments",
            new CreateTicketAttachmentRequest { DocumentId = "x", FileName = "x.pdf" });
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var listResp = await b.GetAsync($"/support/api/tickets/{t.Id}/attachments");
        listResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Add_Attachment_Rejects_Duplicate_Document_Link()
    {
        var a = ClientForTenant("tenant-ATT-G");
        var t = await CreateTicketAsync(a);
        var first = await a.PostAsJsonAsync($"/support/api/tickets/{t.Id}/attachments",
            new CreateTicketAttachmentRequest { DocumentId = "doc_dup", FileName = "x.pdf" });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var dup = await a.PostAsJsonAsync($"/support/api/tickets/{t.Id}/attachments",
            new CreateTicketAttachmentRequest { DocumentId = "doc_dup", FileName = "x.pdf" });
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Attachment_Add_Creates_Timeline_Event()
    {
        var a = ClientForTenant("tenant-ATT-F");
        var t = await CreateTicketAsync(a);
        await a.PostAsJsonAsync($"/support/api/tickets/{t.Id}/attachments",
            new CreateTicketAttachmentRequest { DocumentId = "doc_z", FileName = "zoo.pdf" });

        var timeline = await (await a.GetAsync($"/support/api/tickets/{t.Id}/timeline"))
            .Content.ReadFromJsonAsync<List<TimelineItem>>();
        var ev = timeline!.SingleOrDefault(i => i.EventType == "attachment_added");
        ev.Should().NotBeNull();
        ev!.Summary.Should().Be("Attachment added");
        ev.MetadataJson.Should().Contain("\"document_id\":\"doc_z\"")
            .And.Contain("\"file_name\":\"zoo.pdf\"");
    }
}
